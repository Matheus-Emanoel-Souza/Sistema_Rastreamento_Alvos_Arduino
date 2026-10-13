using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação em JSON de <see cref="IPerfilAlvoRepository"/>, gravada em
/// <c>%LocalAppData%\RadarTorres\perfis-alvo.json</c> — mesmo padrão e mesma pasta de
/// <see cref="ZonaMortaRepository"/>, um arquivo único para toda a instalação (os perfis de
/// alvo definem o que a detecção vai travar, não é uma preferência por usuário).
/// </summary>
public sealed class PerfilAlvoRepository : IPerfilAlvoRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly ILoggingService? _logger;

    public PerfilAlvoRepository(ILoggingService? logger = null)
        : this(DefaultFilePath(), logger)
    {
    }

    public PerfilAlvoRepository(string filePath, ILoggingService? logger = null)
    {
        _filePath = filePath;
        _logger = logger;
    }

    public static string DefaultFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RadarTorres",
        "perfis-alvo.json");

    public List<PerfilAlvo> Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new List<PerfilAlvo>();
            }

            string json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<PerfilAlvo>>(json) ?? new List<PerfilAlvo>();
        }
        catch (Exception ex)
        {
            // Arquivo corrompido/ilegível não pode impedir a tela de abrir — segue sem
            // nenhum perfil configurado, igual ao tratamento já usado para as demais
            // preferências em JSON do projeto.
            _logger?.Warning($"Não foi possível ler os perfis de alvo salvos ({_filePath}): {ex.Message}. Nenhum perfil carregado.");
            return new List<PerfilAlvo>();
        }
    }

    public void Save(List<PerfilAlvo> perfis)
    {
        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, JsonSerializer.Serialize(perfis, SerializerOptions));
        }
        catch (Exception ex)
        {
            _logger?.Error($"Não foi possível salvar os perfis de alvo: {ex.Message}");
        }
    }
}
