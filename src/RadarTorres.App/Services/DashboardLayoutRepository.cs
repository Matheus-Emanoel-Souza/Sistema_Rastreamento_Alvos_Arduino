using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação em JSON de <see cref="IDashboardLayoutRepository"/>, gravada em
/// <c>%LocalAppData%\RadarTorres\dashboard-layout.json</c> — mesmo padrão usado por
/// <see cref="ArduinoSettingsRepository"/> para as preferências do Arduino.
/// </summary>
public sealed class DashboardLayoutRepository : IDashboardLayoutRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly ILoggingService? _logger;

    public DashboardLayoutRepository(ILoggingService? logger = null)
        : this(DefaultFilePath(), logger)
    {
    }

    public DashboardLayoutRepository(string filePath, ILoggingService? logger = null)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public static string DefaultFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RadarTorres",
        "dashboard-layout.json");

    public Dictionary<string, DashboardCardLayout>? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<Dictionary<string, DashboardCardLayout>>(json);
        }
        catch (Exception ex)
        {
            // Arquivo corrompido/ilegível não pode impedir a tela de abrir — volta ao layout
            // padrão, igual ao tratamento já usado para arduino-settings.json.
            _logger?.Warning($"Não foi possível ler o layout do painel ({_filePath}): {ex.Message}. Usando layout padrão.");
            return null;
        }
    }

    public void Save(Dictionary<string, DashboardCardLayout> layout)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(layout, SerializerOptions));
        }
        catch (Exception ex)
        {
            _logger?.Error($"Não foi possível salvar o layout do painel: {ex.Message}");
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"Não foi possível limpar o layout salvo do painel: {ex.Message}");
        }
    }
}
