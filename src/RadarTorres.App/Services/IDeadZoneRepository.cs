using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>Persistência das zonas mortas configuradas (independente de usuário — decisão
/// administrativa da instalação, mesmo espírito de <see cref="IArduinoSettingsRepository"/>).</summary>
public interface IDeadZoneRepository
{
    List<DeadZone> Load();

    void Save(List<DeadZone> zones);
}
