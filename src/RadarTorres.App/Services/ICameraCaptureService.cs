using System;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>Motivo pelo qual a captura de uma câmera falhou ao iniciar ou parou inesperadamente.</summary>
public enum CameraErrorType
{
    /// <summary>O sistema operacional negou o acesso ao dispositivo (permissão de câmera do Windows, driver, etc.).</summary>
    AccessDenied,

    /// <summary>A câmera já está sendo usada por outro aplicativo.</summary>
    DeviceBusy,

    /// <summary>A câmera foi desconectada (cabo USB removido, dispositivo desligado) enquanto a captura estava ativa.</summary>
    DeviceDisconnected,

    /// <summary>Qualquer outra falha não classificada acima.</summary>
    Unknown
}

/// <summary>
/// Um quadro de vídeo já decodificado em pixels BGR24 (24 bits, sem canal alfa), pronto para
/// ser exibido pela camada de apresentação. Propositalmente não referencia nenhum tipo de
/// GDI+ (<c>System.Drawing</c>) nem de WPF — apenas os bytes crus e as dimensões — para que
/// <see cref="ICameraCaptureService"/> não force a camada de Services a depender de um
/// framework de UI (ver comentário de classe de <see cref="CameraCaptureService"/>).
/// </summary>
public sealed class CameraFrameCapturedEventArgs : EventArgs
{
    public byte[] PixelsBgr24 { get; }
    public int Width { get; }
    public int Height { get; }

    /// <summary>Bytes por linha (pode ser maior que <c>Width * 3</c> por alinhamento de 4 bytes).</summary>
    public int Stride { get; }

    public CameraFrameCapturedEventArgs(byte[] pixelsBgr24, int width, int height, int stride)
    {
        PixelsBgr24 = pixelsBgr24;
        Width = width;
        Height = height;
        Stride = stride;
    }
}

/// <summary>Detalhes de uma falha de captura (acesso negado, câmera ocupada ou desconectada).</summary>
public sealed class CameraErrorEventArgs : EventArgs
{
    public CameraErrorType Type { get; }
    public string Message { get; }

    public CameraErrorEventArgs(CameraErrorType type, string message)
    {
        Type = type;
        Message = message;
    }
}

/// <summary>
/// Contrato da camada de acesso a UMA câmera/webcam. Cada painel do módulo "Câmeras" (até 4
/// simultâneos, ver <see cref="CameraSlotViewModel"/>) possui sua própria instância — obtida via
/// <see cref="ICameraCaptureServiceFactory"/> — de forma que ligar/desligar uma câmera nunca
/// afeta as demais. Definido como interface para permitir substituir a implementação real
/// (<see cref="CameraCaptureService"/>) por um dublê em testes automatizados, sem depender de
/// hardware físico — mesmo racional já usado por <see cref="ISerialCommunicationService"/> para a
/// porta serial. A enumeração de dispositivos (<see cref="ICameraDeviceEnumerator"/>) fica de
/// fora deste contrato porque é uma consulta "global" ao sistema, não algo específico de uma
/// instância/sessão de captura.
/// </summary>
public interface ICameraCaptureService : IDisposable
{
    /// <summary>Se há uma captura em andamento no momento.</summary>
    bool IsRunning { get; }

    /// <summary>Câmera atualmente em uso, ou <c>null</c> se nenhuma captura estiver ativa.</summary>
    CameraDevice? CurrentDevice { get; }

    /// <summary>Disparado (fora da thread de UI) a cada novo quadro capturado.</summary>
    event EventHandler<CameraFrameCapturedEventArgs>? FrameCaptured;

    /// <summary>Disparado quando ocorre uma falha ao iniciar ou durante a captura.</summary>
    event EventHandler<CameraErrorEventArgs>? ErrorOccurred;

    /// <summary>Disparado sempre que a captura para, seja por <see cref="Stop"/> ou por uma falha reportada em <see cref="ErrorOccurred"/>.</summary>
    event EventHandler? Stopped;

    /// <summary>Inicia a captura da câmera informada, encerrando uma captura anterior caso exista.</summary>
    void Start(CameraDevice device);

    /// <summary>Encerra a captura em andamento e libera o dispositivo. Não faz nada se já estiver parado.</summary>
    void Stop();
}
