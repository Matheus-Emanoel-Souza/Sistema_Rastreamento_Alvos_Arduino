using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using AForge.Video;
using AForge.Video.DirectShow;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="ICameraCaptureService"/> via DirectShow (biblioteca
/// AForge.Video.DirectShow) — a mesma tecnologia usada por <see cref="CameraDeviceEnumerator"/>
/// para listar os dispositivos, sem exigir nenhum SDK adicional além do já presente em qualquer
/// Windows com uma webcam instalada. Escolhida em vez de alternativas como OpenCvSharp/Emgu.CV
/// por ser uma dependência muito menor (não traz um runtime nativo de visão computacional inteiro
/// só para exibir uma prévia ao vivo) e por já cobrir exatamente o que este módulo precisa:
/// entregar quadros decodificados de um dispositivo por vez. Cada painel do módulo "Câmeras"
/// (até 4 simultâneos) tem sua própria instância — criada por <see cref="CameraCaptureServiceFactory"/>
/// — então nunca compartilha o <see cref="VideoCaptureDevice"/> de outro painel.
/// </summary>
/// <remarks>
/// Cada quadro chega em segundo plano (thread própria do AForge, não a thread de UI) como um
/// <see cref="Bitmap"/> reaproveitado internamente pela biblioteca; por isso é clonado antes de
/// qualquer uso. Os bytes já são extraídos e o <see cref="Bitmap"/> é descartado dentro deste
/// serviço — nada de GDI+ nem de WPF atravessa a fronteira de <see cref="ICameraCaptureService"/>
/// (ver comentário de classe de <see cref="CameraFrameCapturedEventArgs"/>).
/// </remarks>
public sealed class CameraCaptureService : ICameraCaptureService
{
    private VideoCaptureDevice? _videoSource;
    private bool _stoppedExplicitly;

    public bool IsRunning => _videoSource?.IsRunning ?? false;

    public CameraDevice? CurrentDevice { get; private set; }

    public event EventHandler<CameraFrameCapturedEventArgs>? FrameCaptured;
    public event EventHandler<CameraErrorEventArgs>? ErrorOccurred;
    public event EventHandler? Stopped;

    public void Start(CameraDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        Stop();
        _stoppedExplicitly = false;

        try
        {
            _videoSource = new VideoCaptureDevice(device.Id);
            _videoSource.NewFrame += OnNewFrame;
            _videoSource.PlayingFinished += OnPlayingFinished;
            _videoSource.Start();
            CurrentDevice = device;
        }
        catch (Exception ex)
        {
            CleanupVideoSource();
            CurrentDevice = null;
            ErrorOccurred?.Invoke(this, new CameraErrorEventArgs(ClassifyError(ex), DescribeStartError(ex)));
        }
    }

    public void Stop()
    {
        if (_videoSource is null) return;

        _stoppedExplicitly = true;
        CleanupVideoSource();
        CurrentDevice = null;
        Stopped?.Invoke(this, EventArgs.Empty);
    }

    private void CleanupVideoSource()
    {
        if (_videoSource is null) return;

        _videoSource.NewFrame -= OnNewFrame;
        _videoSource.PlayingFinished -= OnPlayingFinished;

        try
        {
            if (_videoSource.IsRunning)
            {
                _videoSource.SignalToStop();
                _videoSource.WaitForStop();
            }
        }
        catch
        {
            // Melhor esforço — o dispositivo pode já ter sido removido fisicamente.
        }

        _videoSource = null;
    }

    private void OnNewFrame(object sender, NewFrameEventArgs eventArgs)
    {
        try
        {
            using Bitmap frame = (Bitmap)eventArgs.Frame.Clone();
            FrameCaptured?.Invoke(this, ToFrameArgs(frame));
        }
        catch (Exception)
        {
            // Um quadro isolado corrompido não deve derrubar a captura inteira — apenas ignora.
        }
    }

    private static CameraFrameCapturedEventArgs ToFrameArgs(Bitmap frame)
    {
        Bitmap source = frame;
        bool convertido = false;
        if (frame.PixelFormat != PixelFormat.Format24bppRgb)
        {
            source = frame.Clone(new Rectangle(0, 0, frame.Width, frame.Height), PixelFormat.Format24bppRgb);
            convertido = true;
        }

        try
        {
            BitmapData data = source.LockBits(
                new Rectangle(0, 0, source.Width, source.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                byte[] pixels = new byte[data.Stride * data.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                return new CameraFrameCapturedEventArgs(pixels, source.Width, source.Height, data.Stride);
            }
            finally
            {
                source.UnlockBits(data);
            }
        }
        finally
        {
            if (convertido) source.Dispose();
        }
    }

    private void OnPlayingFinished(object sender, ReasonToFinishPlaying reason)
    {
        if (_stoppedExplicitly) return; // parada pedida pelo próprio usuário — não é uma falha

        CameraErrorType type = reason == ReasonToFinishPlaying.DeviceLost
            ? CameraErrorType.DeviceDisconnected
            : CameraErrorType.Unknown;

        string message = reason switch
        {
            ReasonToFinishPlaying.DeviceLost => "A câmera foi desconectada.",
            ReasonToFinishPlaying.VideoSourceError => "A câmera está em uso por outro aplicativo ou não pôde ser acessada.",
            _ => "A captura da câmera foi interrompida inesperadamente."
        };

        CurrentDevice = null;
        ErrorOccurred?.Invoke(this, new CameraErrorEventArgs(type, message));
        Stopped?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Classificação best-effort a partir da mensagem da exceção lançada por
    /// <c>VideoCaptureDevice.Start()</c> — o DirectShow/AForge não expõe um código de erro
    /// tipado para distinguir "acesso negado" de "dispositivo ocupado" neste ponto.
    /// </summary>
    private static CameraErrorType ClassifyError(Exception ex)
    {
        string msg = ex.Message.ToLowerInvariant();
        if (msg.Contains("access") || msg.Contains("denied")) return CameraErrorType.AccessDenied;
        if (msg.Contains("busy") || msg.Contains("in use")) return CameraErrorType.DeviceBusy;
        return CameraErrorType.Unknown;
    }

    private static string DescribeStartError(Exception ex) =>
        $"Não foi possível iniciar a câmera selecionada. Detalhes: {ex.Message}";

    public void Dispose() => Stop();
}
