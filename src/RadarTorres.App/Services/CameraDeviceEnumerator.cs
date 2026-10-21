using System;
using System.Collections.Generic;
using System.Linq;
using AForge.Video.DirectShow;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="ICameraDeviceEnumerator"/> via DirectShow (biblioteca
/// AForge.Video.DirectShow) — mesma tecnologia/biblioteca usada por <see cref="CameraCaptureService"/>
/// para efetivamente capturar os quadros (ver comentário de classe lá para a justificativa de
/// escolha da biblioteca).
/// </summary>
public sealed class CameraDeviceEnumerator : ICameraDeviceEnumerator
{
    public IReadOnlyList<CameraDevice> GetAvailableCameras()
    {
        try
        {
            var devices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            return devices
                .Cast<FilterInfo>()
                .Select(d => new CameraDevice(d.MonikerString, d.Name))
                .ToList();
        }
        catch (Exception)
        {
            // Falha ao consultar o subsistema DirectShow (raro) — trata como "nenhuma câmera
            // encontrada" em vez de propagar, mesma postura defensiva de GetAvailablePorts.
            return Array.Empty<CameraDevice>();
        }
    }
}
