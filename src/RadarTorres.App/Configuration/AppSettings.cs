using System.Collections.Generic;

namespace RadarTorres.App.Configuration;

/// <summary>
/// Modelo raiz de configuração, associado por binding ao arquivo <c>appsettings.json</c>
/// através de <c>Microsoft.Extensions.Configuration</c> (ver App.xaml.cs). Centraliza tudo
/// que o enunciado pede para ser "facilmente alterável": baud rate, porta padrão, distâncias
/// mínima/máxima, timeout de alvos, frequência do radar e a lista de torres.
/// </summary>
public sealed class AppSettings
{
    public SerialSettings SerialSettings { get; set; } = new();
    public RadarSettings RadarSettings { get; set; } = new();
    public List<TowerDefinition> Towers { get; set; } = new();
    public SimulationSettings SimulationSettings { get; set; } = new();
}

public sealed class SerialSettings
{
    public string DefaultPort { get; set; } = "COM4";
    public int DefaultBaudRate { get; set; } = 9600;
    public int ReadTimeoutMs { get; set; } = 2000;
    public int WriteTimeoutMs { get; set; } = 1000;
    public int ReconnectAttempts { get; set; } = 3;

    /// <summary>
    /// Tempo máximo (ms) sem receber nenhum byte antes de o watchdog de conexão
    /// considerar a comunicação perdida e disparar o evento de desconexão.
    /// </summary>
    public int ConnectionWatchdogMs { get; set; } = 3000;

    /// <summary>Lista de baud rates comuns oferecida no combo da UI.</summary>
    public static readonly int[] CommonBaudRates = { 9600, 19200, 38400, 57600, 115200 };
}

public sealed class RadarSettings
{
    public double MaxDetectionDistanceMeters { get; set; } = 6.0;
    public double MinSafetyDistanceMeters { get; set; } = 1.5;
    public int TargetTimeoutMs { get; set; } = 5000;
    public int RefreshRateMs { get; set; } = 150;
    public int DistanceRingCount { get; set; } = 4;
}

/// <summary>Configuração declarativa de uma torre, lida do JSON e usada para instanciar <see cref="Models.Tower"/>.</summary>
public sealed class TowerDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class SimulationSettings
{
    public int DefaultTargetCount { get; set; } = 3;
    public int GenerationIntervalMs { get; set; } = 800;
    public int MaxSimultaneousTargets { get; set; } = 8;
}
