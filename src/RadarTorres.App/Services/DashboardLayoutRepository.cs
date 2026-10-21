using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação em JSON de <see cref="IDashboardLayoutRepository"/>, gravada em
/// <c>%LocalAppData%\RadarTorres\dashboard-layout-{layoutKey}.json</c> — um arquivo por
/// combinação de tela+usuário, mesmo padrão usado por <see cref="ArduinoSettingsRepository"/>
/// para as preferências do Arduino.
/// </summary>
public sealed class DashboardLayoutRepository : IDashboardLayoutRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    private readonly string _folder;
    private readonly ILoggingService? _logger;

    public DashboardLayoutRepository(ILoggingService? logger = null)
        : this(DefaultFolder(), logger)
    {
    }

    public DashboardLayoutRepository(string folder, ILoggingService? logger = null)
    {
        _folder = folder;
        _logger = logger;
    }

    public static string DefaultFolder() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RadarTorres");

    public Dictionary<string, DashboardCardLayout>? Load(string layoutKey)
    {
        string filePath = FilePathFor(layoutKey);

        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            string json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Dictionary<string, DashboardCardLayout>>(json);
        }
        catch (Exception ex)
        {
            // Arquivo corrompido/ilegível não pode impedir a tela de abrir — volta ao layout
            // padrão, igual ao tratamento já usado para arduino-settings.json.
            _logger?.Warning($"Não foi possível ler o layout salvo ({filePath}): {ex.Message}. Usando layout padrão.");
            return null;
        }
    }

    public void Save(string layoutKey, Dictionary<string, DashboardCardLayout> layout)
    {
        string filePath = FilePathFor(layoutKey);

        try
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(filePath, JsonSerializer.Serialize(layout, SerializerOptions));
        }
        catch (Exception ex)
        {
            _logger?.Error($"Não foi possível salvar o layout ({filePath}): {ex.Message}");
        }
    }

    public void Clear(string layoutKey)
    {
        string filePath = FilePathFor(layoutKey);

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            _logger?.Error($"Não foi possível limpar o layout salvo ({filePath}): {ex.Message}");
        }
    }

    /// <summary>Monta o caminho do arquivo a partir da chave (tela+usuário), removendo
    /// qualquer caractere inválido em nome de arquivo por segurança — a chave normalmente já é
    /// segura (ex.: "painel-principal-3"), mas nunca vem de entrada livre do usuário.</summary>
    private string FilePathFor(string layoutKey)
    {
        string safeKey = new(layoutKey.Where(c => !InvalidFileNameChars.Contains(c)).ToArray());
        return Path.Combine(_folder, $"dashboard-layout-{safeKey}.json");
    }
}
