using System.Collections.Generic;
using RadarTorres.App.Services;

namespace RadarTorres.Tests.Fakes;

/// <summary>
/// Dublê de <see cref="ICameraCaptureServiceFactory"/> — cada chamada a <see cref="Create"/> gera
/// uma nova <see cref="FakeCameraCaptureService"/> (mesmo comportamento da fábrica real, uma
/// instância independente por painel) e a expõe em <see cref="CreatedServices"/> na ordem de
/// criação, para o teste conseguir disparar eventos no painel certo (Slots[0] usa
/// CreatedServices[0], e assim por diante).
/// </summary>
public sealed class FakeCameraCaptureServiceFactory : ICameraCaptureServiceFactory
{
    public List<FakeCameraCaptureService> CreatedServices { get; } = [];

    public ICameraCaptureService Create()
    {
        var service = new FakeCameraCaptureService();
        CreatedServices.Add(service);
        return service;
    }
}
