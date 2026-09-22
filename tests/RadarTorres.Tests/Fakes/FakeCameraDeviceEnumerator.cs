using System;
using System.Collections.Generic;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.Tests.Fakes;

/// <summary>Dublê de <see cref="ICameraDeviceEnumerator"/> — devolve uma lista fixa configurada pelo teste, sem consultar o Windows/DirectShow.</summary>
public sealed class FakeCameraDeviceEnumerator : ICameraDeviceEnumerator
{
    public IReadOnlyList<CameraDevice> CamerasToReturn { get; set; } = Array.Empty<CameraDevice>();

    public IReadOnlyList<CameraDevice> GetAvailableCameras() => CamerasToReturn;
}
