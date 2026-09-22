using System.ComponentModel;
using RadarTorres.App.Services;

namespace RadarTorres.Tests.Fakes;

/// <summary>
/// Dublê de <see cref="ILocalizationService"/> — devolve a própria chave como "tradução",
/// suficiente para os testes de ViewModel verificarem que a chave correta foi escolhida sem
/// depender dos arquivos JSON de <c>Resources/Localization</c> (não copiados para o projeto de
/// testes).
/// </summary>
public sealed class FakeLocalizationService : ILocalizationService
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string[] AvailableLanguages => ["pt-BR", "en-US"];

    public string CurrentLanguage { get; private set; } = "pt-BR";

    public string this[string key] => key;

    public void SetLanguage(string languageCode)
    {
        CurrentLanguage = languageCode;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
