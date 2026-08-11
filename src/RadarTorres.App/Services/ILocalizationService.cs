using System.ComponentModel;

namespace RadarTorres.App.Services;

/// <summary>
/// Serviço de internacionalização (Requisito 1). Os textos ficam em arquivos de tradução
/// JSON (<c>Resources/Localization/*.json</c>), nunca hardcoded no código/XAML — telas usam
/// a chave (ex.: <c>"TopBar.Help"</c>) via <see cref="Localization.LocExtension"/> no XAML ou
/// pelo indexador <see cref="this[string]"/> no code-behind/ViewModel.
/// </summary>
/// <remarks>
/// Implementa <see cref="INotifyPropertyChanged"/> para permitir troca de idioma em tempo
/// real sem reiniciar o aplicativo: ao trocar de idioma, todo texto ligado via
/// <see cref="Localization.LocExtension"/> é atualizado automaticamente (ver comentário em
/// <see cref="LocalizationService"/>).
/// </remarks>
public interface ILocalizationService : INotifyPropertyChanged
{
    /// <summary>Códigos de idioma suportados (ex.: "pt-BR", "en-US").</summary>
    string[] AvailableLanguages { get; }

    string CurrentLanguage { get; }

    /// <summary>Texto traduzido para a chave informada no idioma atual; a própria chave se não houver tradução.</summary>
    string this[string key] { get; }

    void SetLanguage(string languageCode);
}
