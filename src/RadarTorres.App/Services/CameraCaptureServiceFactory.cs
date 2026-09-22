namespace RadarTorres.App.Services;

/// <summary>Implementação padrão de <see cref="ICameraCaptureServiceFactory"/>: cada chamada devolve uma <see cref="CameraCaptureService"/> nova, sem nenhum estado compartilhado com as demais.</summary>
public sealed class CameraCaptureServiceFactory : ICameraCaptureServiceFactory
{
    public ICameraCaptureService Create() => new CameraCaptureService();
}
