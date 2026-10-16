using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using RadarTorres.App.Configuration;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="IObjectDetectionService"/> sobre o modelo
/// <c>tiny-yolov3-11.onnx</c> (ONNX Model Zoo), rodado localmente via
/// <c>Microsoft.ML.OnnxRuntime</c> — sem nenhum serviço/processo externo, mesmo racional do
/// módulo "Câmeras" (ver comentário em RadarTorres.App.csproj).
/// </summary>
/// <remarks>
/// Contrato do modelo (confirmado carregando o .onnx real com <see cref="InferenceSession"/>,
/// não documentação): entrada <c>input_1</c> [1,3,416,416] float32, pixels RGB normalizados
/// 0–1 (não BGR — o quadro da câmera chega em BGR24, os canais são trocados no pré-
/// processamento); entrada <c>image_shape</c> [1,2] float32 = (altura, largura) da imagem
/// original, usada pelo próprio modelo para devolver as caixas já na escala original. Saídas:
/// <c>yolonms_layer_1</c> = caixas (y1,x1,y2,x2) em pixels da imagem original,
/// <c>yolonms_layer_1:1</c> = score por classe COCO (80 classes) por caixa candidata,
/// <c>yolonms_layer_1:2</c> = índices (lote, classe, caixa) já filtrados por NMS — o modelo
/// decide a supressão de caixas sobrepostas internamente, nenhum NMS é feito aqui.
/// A classe COCO escolhida pelo NMS é ignorada pelo restante do pipeline (ver
/// <see cref="VisionDetection"/>) — o modelo serve só para propor regiões candidatas.
/// </remarks>
public sealed class OnnxYoloDetectionService : IObjectDetectionService, IDisposable
{
    private const int InputSize = 416;

    private readonly InferenceSession _session;
    private readonly ILoggingService _logger;

    public OnnxYoloDetectionService(ILoggingService logger)
    {
        _logger = logger;

        string modelPath = Path.Combine(AppContext.BaseDirectory, "ComputerVision", "Models", "YoloModel", "tiny-yolov3-11.onnx");
        _session = new InferenceSession(modelPath);
    }

    public IReadOnlyList<VisionDetection> Detect(Bitmap frame)
    {
        try
        {
            DenseTensor<float> inputTensor = ToInputTensor(frame);
            var imageShapeTensor = new DenseTensor<float>(new float[] { frame.Height, frame.Width }, new[] { 1, 2 });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_1", inputTensor),
                NamedOnnxValue.CreateFromTensor("image_shape", imageShapeTensor)
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);

            Tensor<float> boxes = results.First(r => r.Name == "yolonms_layer_1").AsTensor<float>();
            Tensor<float> scores = results.First(r => r.Name == "yolonms_layer_1:1").AsTensor<float>();
            Tensor<int> indices = results.First(r => r.Name == "yolonms_layer_1:2").AsTensor<int>();

            double confiancaMinima = AppConfig.Current.VisionSettings.ConfiancaMinima;
            var deteccoes = new List<VisionDetection>();

            int totalSelecionadas = indices.Dimensions[1];
            for (int i = 0; i < totalSelecionadas; i++)
            {
                int classId = indices[0, i, 1];
                int boxIndex = indices[0, i, 2];

                double score = scores[0, classId, boxIndex];
                if (score < confiancaMinima) continue;

                float y1 = boxes[0, boxIndex, 0];
                float x1 = boxes[0, boxIndex, 1];
                float y2 = boxes[0, boxIndex, 2];
                float x2 = boxes[0, boxIndex, 3];

                int x = Math.Max(0, (int)Math.Round(x1));
                int y = Math.Max(0, (int)Math.Round(y1));
                int largura = Math.Min(frame.Width - x, (int)Math.Round(x2 - x1));
                int altura = Math.Min(frame.Height - y, (int)Math.Round(y2 - y1));

                if (largura <= 0 || altura <= 0) continue;

                deteccoes.Add(new VisionDetection(x, y, largura, altura, score));
            }

            return deteccoes;
        }
        catch (Exception ex)
        {
            // Uma inferência isolada não deve derrubar o card de detecção — igual ao
            // tratamento já usado para quadros corrompidos em CameraCaptureService.
            _logger.Warning($"Falha ao rodar a detecção por visão computacional neste quadro: {ex.Message}");
            return Array.Empty<VisionDetection>();
        }
    }

    /// <summary>
    /// Converte o quadro para o tensor de entrada [1,3,416,416] em RGB normalizado 0–1.
    /// </summary>
    /// <remarks>
    /// Escreve direto no buffer linear do tensor (<see cref="DenseTensor{T}.Buffer"/>) em vez de
    /// usar o indexador <c>tensor[0, c, y, x]</c>. Não é micro-otimização especulativa: medido
    /// nesta máquina, a versão com indexador levava ~229 ms por quadro contra ~53 ms escrevendo
    /// no span — metade do tempo de preparo da entrada era só overhead do indexador
    /// multidimensional, chamado 519 mil vezes por quadro e recalculando o deslocamento a partir
    /// das dimensões em cada uma. O layout é NCHW: os três canais são planos completos e
    /// consecutivos, por isso o deslocamento de um canal é só
    /// <c>canal * plano + (y * largura + x)</c>.
    /// </remarks>
    private static DenseTensor<float> ToInputTensor(Bitmap frame)
    {
        using Bitmap resized = new(InputSize, InputSize, PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(resized))
        {
            // Bilinear simples (não HighQuality): a imagem já está sendo reduzida para 416px e o
            // modelo não distingue a diferença, mas a reamostragem de alta qualidade cobra caro.
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.DrawImage(frame, 0, 0, InputSize, InputSize);
        }

        var tensor = new DenseTensor<float>(new[] { 1, 3, InputSize, InputSize });
        Span<float> destino = tensor.Buffer.Span;

        const int plano = InputSize * InputSize;

        BitmapData data = resized.LockBits(
            new Rectangle(0, 0, InputSize, InputSize), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        try
        {
            const int bytesPerPixel = 3;
            byte[] linha = new byte[data.Stride];

            for (int y = 0; y < InputSize; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0 + (y * data.Stride), linha, 0, data.Stride);

                int baseLinha = y * InputSize;

                for (int x = 0; x < InputSize; x++)
                {
                    int offset = x * bytesPerPixel;
                    byte b = linha[offset];
                    byte g = linha[offset + 1];
                    byte r = linha[offset + 2];

                    // Format24bppRgb = BGR na memória; o modelo espera RGB normalizado 0–1.
                    int indice = baseLinha + x;
                    destino[indice] = r / 255f;
                    destino[plano + indice] = g / 255f;
                    destino[(2 * plano) + indice] = b / 255f;
                }
            }
        }
        finally
        {
            resized.UnlockBits(data);
        }

        return tensor;
    }

    public void Dispose() => _session.Dispose();
}
