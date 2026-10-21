using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>Persistência das zonas mortas configuradas (independente de usuário — decisão
/// administrativa da instalação, mesmo espírito de <see cref="IArduinoSettingsRepository"/>).</summary>
public interface IZonaMortaRepository
{
    List<ZonaMorta> Load();

    void Save(List<ZonaMorta> zones);
}
