using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using RadarTorres.App.Configuration;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.App.Helpers;

/// <summary>Resultado de uma rodada de detecção, com o tamanho do quadro que a originou.</summary>
public sealed class AvaliacaoQuadroEventArgs : EventArgs
{
    public AvaliacaoQuadroEventArgs(AlvoVisualAvaliacao avaliacao, int largura, int altura)
    {
        Avaliacao = avaliacao;
        Largura = largura;
        Altura = altura;
    }

    public AlvoVisualAvaliacao Avaliacao { get; }

    public int Largura { get; }

    public int Altura { get; }
}

/// <summary>
/// Liga uma fonte de quadros (<see cref="ICameraCaptureService"/>) à detecção por visão,
/// cuidando do que não tem a ver com nenhuma tela em particular: limitar a frequência das
/// rodadas, nunca rodar duas ao mesmo tempo, copiar o quadro para fora do buffer da câmera e
/// transformá-lo em <see cref="Bitmap"/>.
/// </summary>
/// <remarks>
/// Extraído de <see cref="ViewModels.DeteccaoCameraViewModel"/>, onde essa mecânica já existia,
/// quando a detecção passou a rodar também nos painéis da tela "Câmeras" (um por quadrante): são
/// várias câmeras analisando em paralelo, e cada uma precisa da MESMA disciplina de execução.
/// Duplicar esse trecho por tela seria duplicar justamente as partes delicadas — a marshalização
/// do buffer e o controle de rodada única por vez.
/// </remarks>
public sealed class AnalisadorQuadros : IDisposable
{
    private readonly IAlvoVisualService _alvoVisualService;
    private readonly ILoggingService _logger;

    private DateTime _ultimoProcessamento = DateTime.MinValue;
    private int _processando;
    private bool _descartado;

    public AnalisadorQuadros(IAlvoVisualService alvoVisualService, ILoggingService logger)
    {
        _alvoVisualService = alvoVisualService;
        _logger = logger;
    }

    /// <summary>
    /// Disparado numa thread de trabalho (NÃO na de interface) ao fim de cada rodada — quem
    /// consome precisa despachar para a interface antes de tocar em propriedades ligadas à tela.
    /// </summary>
    public event EventHandler<AvaliacaoQuadroEventArgs>? QuadroAvaliado;

    /// <summary>Zera o controle de frequência — usado ao (re)iniciar uma câmera, para a primeira rodada sair sem espera.</summary>
    public void Reiniciar() => _ultimoProcessamento = DateTime.MinValue;

    /// <summary>
    /// Entrega um quadro recém-capturado. Retorna imediatamente: a rodada é descartada se a
    /// anterior ainda está em andamento ou se o intervalo mínimo não passou — a inferência é bem
    /// mais lenta que a taxa de quadros da câmera.
    /// </summary>
    public void Submeter(CameraFrameCapturedEventArgs e)
    {
        if (_descartado) return;

        DateTime agora = DateTime.Now;
        int intervaloMs = AppConfig.Current.VisionSettings.IntervaloProcessamentoMs;
        if ((agora - _ultimoProcessamento).TotalMilliseconds < intervaloMs) return;

        if (Interlocked.CompareExchange(ref _processando, 1, 0) != 0) return;
        _ultimoProcessamento = agora;

        byte[] pixelsCopia = (byte[])e.PixelsBgr24.Clone();
        int width = e.Width, height = e.Height, stride = e.Stride;

        Task.Run(() => Processar(pixelsCopia, width, height, stride))
            .ContinueWith(_ => Interlocked.Exchange(ref _processando, 0));
    }

    private void Processar(byte[] pixels, int width, int height, int stride)
    {
        // O vetor já é uma CÓPIA exclusiva do quadro (feita em Submeter), então é fixado e
        // entregue direto ao Bitmap. Antes havia um AllocHGlobal + Marshal.Copy aqui: uma
        // segunda cópia do quadro inteiro, a cada rodada e por câmera, sem necessidade.
        GCHandle fixado = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            using var bmp = new Bitmap(width, height, stride, System.Drawing.Imaging.PixelFormat.Format24bppRgb, fixado.AddrOfPinnedObject());

            AlvoVisualAvaliacao avaliacao = _alvoVisualService.AvaliarFrame(bmp);

            if (!_descartado) QuadroAvaliado?.Invoke(this, new AvaliacaoQuadroEventArgs(avaliacao, width, height));
        }
        catch (Exception ex)
        {
            _logger.Warning($"Falha ao processar quadro para detecção por visão: {ex.Message}");
        }
        finally
        {
            fixado.Free();
        }
    }

    public void Dispose() => _descartado = true;
}
