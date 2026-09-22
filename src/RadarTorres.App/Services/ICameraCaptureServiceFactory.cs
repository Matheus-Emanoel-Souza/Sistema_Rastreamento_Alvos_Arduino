namespace RadarTorres.App.Services;

/// <summary>
/// Cria instâncias independentes de <see cref="ICameraCaptureService"/> — uma por painel do
/// módulo "Câmeras" (<see cref="ViewModels.CameraSlotViewModel"/>), já que cada painel liga,
/// desliga e trata erros da sua própria câmera sem afetar os demais. Registrado como serviço
/// (em vez de instanciar <c>new CameraCaptureService()</c> diretamente nas ViewModels) para que
/// os testes automatizados possam substituir a captura real por dublês, mesmo racional de
/// <see cref="ICameraCaptureService"/> em si.
/// </summary>
public interface ICameraCaptureServiceFactory
{
    /// <summary>Cria uma nova sessão de captura, independente de qualquer outra já criada.</summary>
    ICameraCaptureService Create();
}
