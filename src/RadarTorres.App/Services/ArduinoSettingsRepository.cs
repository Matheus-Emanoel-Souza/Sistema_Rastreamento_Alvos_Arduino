using System;
using System.IO;
using System.Text.Json;
using RadarTorres.App.Configuration;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação em JSON de <see cref="IArduinoSettingsRepository"/>, gravada em
/// <c>%LocalAppData%\RadarTorres\arduino-settings.json</c> (pasta gravável do usuário comum,
/// nunca dentro da pasta de instalação). O construtor aceita um caminho alternativo apenas
/// para permitir testes automatizados isolados do disco real do usuário.
/// </summary>
public sealed class ArduinoSettingsRepository : IArduinoSettingsRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly ILoggingService? _logger;

    public ArduinoSettingsRepository(ILoggingService? logger = null)
        : this(DefaultFilePath(), logger)
    {
    }

    public ArduinoSettingsRepository(string filePath, ILoggingService? logger = null)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public static string DefaultFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RadarTorres",
        "arduino-settings.json");

    public ArduinoCliSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new ArduinoCliSettings();
            }

            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<ArduinoCliSettings>(json) ?? new ArduinoCliSettings();
        }
        catch (Exception ex)
        {
            // Configuração corrompida/ilegível não pode derrubar a aba — segue com os padrões
            // de fábrica, igual ao tratamento já usado para appsettings.json em App.xaml.cs.
            _logger?.Warning($"Não foi possível ler as preferências do Arduino ({_filePath}): {ex.Message}. Usando valores padrão.");
            return new ArduinoCliSettings();
        }
    }

    public void Save(ArduinoCliSettings settings)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, SerializerOptions));
        }
        catch (Exception ex)
        {
            _logger?.Error($"Não foi possível salvar as preferências do Arduino: {ex.Message}");
        }
    }
}
