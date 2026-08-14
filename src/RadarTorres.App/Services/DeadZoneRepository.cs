using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação em JSON de <see cref="IDeadZoneRepository"/>, gravada em
/// <c>%LocalAppData%\RadarTorres\dead-zones.json</c> — mesmo padrão e mesma pasta de
/// <see cref="ArduinoSettingsRepository"/>/<see cref="DashboardLayoutRepository"/>, mas um
/// arquivo único para toda a instalação (as zonas mortas são uma decisão administrativa, não
/// uma preferência por usuário).
/// </summary>
public sealed class DeadZoneRepository : IDeadZoneRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly ILoggingService? _logger;

    public DeadZoneRepository(ILoggingService? logger = null)
        : this(DefaultFilePath(), logger)
    {
    }

    public DeadZoneRepository(string filePath, ILoggingService? logger = null)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public static string DefaultFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RadarTorres",
        "dead-zones.json");

    public List<DeadZone> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new List<DeadZone>();
            }

            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<DeadZone>>(json) ?? new List<DeadZone>();
        }
        catch (Exception ex)
        {
            // Arquivo corrompido/ilegível não pode impedir a tela de abrir — segue sem
            // nenhuma zona configurada, igual ao tratamento já usado para as demais
            // preferências em JSON do projeto.
            _logger?.Warning($"Não foi possível ler as zonas mortas salvas ({_filePath}): {ex.Message}. Nenhuma zona carregada.");
            return new List<DeadZone>();
        }
    }

    public void Save(List<DeadZone> zones)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(zones, SerializerOptions));
        }
        catch (Exception ex)
        {
            _logger?.Error($"Não foi possível salvar as zonas mortas: {ex.Message}");
        }
    }
}
