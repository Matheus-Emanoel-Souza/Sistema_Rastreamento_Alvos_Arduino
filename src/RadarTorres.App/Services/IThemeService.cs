using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Troca o tema visual do aplicativo em runtime (Requisito 1). Como todo o XAML existente já
/// usa <c>DynamicResource</c> para cores, aplicar um tema é apenas trocar qual
/// <c>ResourceDictionary</c> está mesclado em <c>Application.Current.Resources</c> — nenhuma
/// tela precisa ser alterada.
/// </summary>
public interface IThemeService
{
    /// <summary>Preferência de tema atual (inclui "Sistema" — não resolvida para Claro/Escuro).</summary>
    TemaPreferido CurrentTheme { get; }

    /// <summary>Aplica o tema; se <see cref="TemaPreferido.Sistema"/>, resolve para Claro/Escuro conforme o Windows.</summary>
    void ApplyTheme(TemaPreferido tema);
}
