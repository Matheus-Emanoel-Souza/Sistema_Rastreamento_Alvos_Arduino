using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="ILocalizationService"/>: carrega o dicionário de traduções de
/// <c>Resources/Localization/{idioma}.json</c> (copiado para a pasta de saída do build, mesmo
/// esquema já usado para <c>appsettings.json</c>) e notifica a UI quando o idioma muda.
/// </summary>
/// <remarks>
/// A troca de idioma em tempo real (sem reiniciar o app) funciona com o truque padrão de
/// binding a indexador do WPF: <see cref="Localization.LocExtension"/> gera um <c>Binding</c>
/// para <c>this[chave]</c>; ao trocar de idioma disparamos
/// <c>PropertyChanged("Item[]")</c>, que é o nome especial que o WPF reconhece para invalidar
/// <b>todos</b> os bindings de indexador de uma vez.
/// </remarks>
public sealed class LocalizationService : ILocalizationService
{
    private static readonly string[] SupportedLanguages = ["pt-BR", "en-US"];

    private readonly string _resourcesFolder;
    private Dictionary<string, string> _translations = new();
    private string _currentLanguage = "pt-BR";

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Acesso estático para uso por <see cref="Localization.LocExtension"/>, que é resolvido
    /// pelo parser XAML antes de qualquer injeção de dependência estar disponível — mesmo
    /// padrão já adotado por <see cref="Configuration.AppConfig.Current"/> neste projeto.
    /// </summary>
    public static ILocalizationService? Current { get; set; }

    public string[] AvailableLanguages => SupportedLanguages;

    public string CurrentLanguage => _currentLanguage;

    public LocalizationService()
    {
        // AppContext.BaseDirectory funciona tanto em publish normal quanto em single-file
        // (Assembly.Location retorna "" em single-file).
        _resourcesFolder = Path.Combine(AppContext.BaseDirectory, "Resources", "Localization");

        Load(_currentLanguage);
    }

    public string this[string key] => _translations.TryGetValue(key, out string? value) ? value : key;

    public void SetLanguage(string languageCode)
    {
        if (string.Equals(languageCode, _currentLanguage, StringComparison.OrdinalIgnoreCase)) return;
        if (!SupportedLanguages.Contains(languageCode, StringComparer.OrdinalIgnoreCase)) return;

        Load(languageCode);
        _currentLanguage = languageCode;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
    }

    private void Load(string languageCode)
    {
        string path = Path.Combine(_resourcesFolder, $"{languageCode}.json");
        if (!File.Exists(path))
        {
            _translations = new Dictionary<string, string>();
            return;
        }

        string json = File.ReadAllText(path, Encoding.UTF8);
        _translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
    }
}
