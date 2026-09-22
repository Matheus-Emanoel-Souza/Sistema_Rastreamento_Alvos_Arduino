using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Consulta as câmeras/webcams atualmente reconhecidas pelo Windows. Separado de
/// <see cref="ICameraCaptureService"/> porque é uma consulta ao sistema como um todo (não a uma
/// captura em andamento) — o módulo "Câmeras" chama isto uma única vez por atualização e reaplica
/// o resultado aos até 4 painéis (<see cref="ViewModels.CameraSlotViewModel"/>) via
/// <see cref="ViewModels.CamerasViewModel"/>, em vez de cada painel consultar o Windows por conta própria.
/// </summary>
public interface ICameraDeviceEnumerator
{
    /// <summary>Lista as câmeras/webcams atualmente reconhecidas pelo Windows.</summary>
    IReadOnlyList<CameraDevice> GetAvailableCameras();
}
