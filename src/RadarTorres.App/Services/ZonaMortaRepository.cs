using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação em JSON de <see cref="IZonaMortaRepository"/>, gravada em
/// <c>%LocalAppData%\RadarTorres\zona-mortas.json</c> — mesmo padrão e mesma pasta de
/// <see cref="ArduinoSettingsRepository"/>/<see cref="DashboardLayoutRepository"/>, mas um
/// arquivo único para toda a instalação (as zonas mortas são uma decisão administrativa, não
/// uma preferência por usuário).
/// </summary>
public sealed class ZonaMortaRepository : IZonaMortaRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly ILoggingService? _logger;

    public ZonaMortaRepository(ILoggingService? logger = null)
        : this(DefaultFilePath(), logger)
    {
    }

    public ZonaMortaRepository(string filePath, ILoggingService? logger = null)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public static string DefaultFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RadarTorres",
        "zona-mortas.json");

    public List<ZonaMorta> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new List<ZonaMorta>();
            }

            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<ZonaMorta>>(json) ?? new List<ZonaMorta>();
        }
        catch (Exception ex)
        {
            // Arquivo corrompido/ilegível não pode impedir a tela de abrir — segue sem
            // nenhuma zona configurada, igual ao tratamento já usado para as demais
            // preferências em JSON do projeto.
            _logger?.Warning($"Não foi possível ler as zonas mortas salvas ({_filePath}): {ex.Message}. Nenhuma zona carregada.");
            return new List<ZonaMorta>();
        }
    }

    public void Save(List<ZonaMorta> zones)
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
