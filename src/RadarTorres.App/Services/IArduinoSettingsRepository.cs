using RadarTorres.App.Configuration;

namespace RadarTorres.App.Services;

/// <summary>Carrega/grava as preferências da aba Configurações do Arduino (ver <see cref="ArduinoCliSettings"/>).</summary>
public interface IArduinoSettingsRepository
{
    ArduinoCliSettings Load();

    void Save(ArduinoCliSettings settings);
}
