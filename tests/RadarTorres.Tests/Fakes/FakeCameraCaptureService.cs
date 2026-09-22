using System;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.Tests.Fakes;

/// <summary>
/// Dublê de <see cref="ICameraCaptureService"/> — não toca hardware nenhum; permite disparar os
/// eventos publicamente para simular quadros capturados, erros e paradas, exatamente como
/// <see cref="FakeSerialCommunicationService"/> já faz para a porta serial. Cada painel de câmera
/// nos testes usa a sua própria instância (ver <see cref="FakeCameraCaptureServiceFactory"/>),
/// assim como o app real usa uma instância por painel.
/// </summary>
public sealed class FakeCameraCaptureService : ICameraCaptureService
{
    public int StartCallCount { get; private set; }
    public int StopCallCount { get; private set; }

    /// <summary>Quando definido, <see cref="Start"/> dispara <see cref="ErrorOccurred"/> com este erro em vez de "iniciar" a câmera.</summary>
    public CameraErrorEventArgs? ErrorToRaiseOnStart { get; set; }

    public bool IsRunning { get; private set; }

    public CameraDevice? CurrentDevice { get; private set; }

    public event EventHandler<CameraFrameCapturedEventArgs>? FrameCaptured;
    public event EventHandler<CameraErrorEventArgs>? ErrorOccurred;
    public event EventHandler? Stopped;

    public void Start(CameraDevice device)
    {
        StartCallCount++;

        if (ErrorToRaiseOnStart is not null)
        {
            ErrorOccurred?.Invoke(this, ErrorToRaiseOnStart);
            return;
        }

        CurrentDevice = device;
        IsRunning = true;
    }

    public void Stop()
    {
        if (!IsRunning) return;

        StopCallCount++;
        IsRunning = false;
        CurrentDevice = null;
        Stopped?.Invoke(this, EventArgs.Empty);
    }

    public void RaiseFrameCaptured(int width, int height)
    {
        int stride = width * 3;
        RaiseFrameCaptured(new CameraFrameCapturedEventArgs(new byte[stride * height], width, height, stride));
    }

    public void RaiseFrameCaptured(CameraFrameCapturedEventArgs args) => FrameCaptured?.Invoke(this, args);

    public void RaiseErrorOccurred(CameraErrorEventArgs args) => ErrorOccurred?.Invoke(this, args);

    public void RaiseStopped() => Stopped?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
    }
}
