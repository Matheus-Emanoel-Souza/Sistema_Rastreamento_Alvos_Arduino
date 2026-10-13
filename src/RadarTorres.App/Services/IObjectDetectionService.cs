using System.Collections.Generic;
using System.Drawing;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Executa detecção de objetos num quadro de câmera já decodificado, usando o modelo ONNX
/// local (tiny-YOLOv3 — ver <see cref="OnnxYoloDetectionService"/>). Devolve apenas caixas
/// (posição/tamanho/confiança); não decide o que é ou não o alvo procurado — essa decisão é de
/// <see cref="IAlvoVisualService"/>, que compara cada caixa com o <see cref="PerfilAlvo"/> ativo.
/// Interface separada (em vez de embutir a inferência direto no serviço de visão) para poder
/// trocar o modelo/motor de inferência sem tocar na lógica de negócio, mesmo racional de
/// <see cref="ICameraCaptureService"/> para a câmera em si.
/// </summary>
public interface IObjectDetectionService
{
    /// <summary>
    /// Detecta objetos no quadro informado. <see cref="Bitmap"/> em vez de <c>byte[]</c> cru
    /// porque a classificação de cor/formato por <see cref="IAlvoVisualService"/> também
    /// precisa ler pixels da mesma imagem — evita decodificar o quadro duas vezes.
    /// </summary>
    IReadOnlyList<VisionDetection> Detect(Bitmap frame);
}
