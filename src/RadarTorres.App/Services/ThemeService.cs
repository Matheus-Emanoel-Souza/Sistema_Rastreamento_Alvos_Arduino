using System;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>Implementação de <see cref="IThemeService"/> via troca de <c>ResourceDictionary</c> mesclado.</summary>
public sealed class ThemeService : IThemeService
{
    private const string DarkThemeMarker = "Themes/Dark.xaml";
    private const string LightThemeMarker = "Themes/Light.xaml";

    public TemaPreferido CurrentTheme { get; private set; } = TemaPreferido.Escuro;

    public void ApplyTheme(TemaPreferido tema)
    {
        TemaPreferido resolved = tema == TemaPreferido.Sistema ? ResolveSystemTheme() : tema;
        string marker = resolved == TemaPreferido.Claro ? LightThemeMarker : DarkThemeMarker;

        var novoDicionario = new ResourceDictionary { Source = new Uri(marker, UriKind.Relative) };

        ResourceDictionary appResources = Application.Current.Resources;
        ResourceDictionary? existente = appResources.MergedDictionaries.FirstOrDefault(d =>
            d.Source is not null &&
            (d.Source.OriginalString.Contains(DarkThemeMarker, StringComparison.OrdinalIgnoreCase) ||
             d.Source.OriginalString.Contains(LightThemeMarker, StringComparison.OrdinalIgnoreCase)));

        if (existente is not null)
        {
            appResources.MergedDictionaries.Remove(existente);
        }

        appResources.MergedDictionaries.Add(novoDicionario);

        // Guarda a preferência original (podendo ser "Sistema"), não a resolvida — assim a
        // UI de configurações consegue mostrar corretamente o que o usuário escolheu.
        CurrentTheme = tema;
    }

    /// <summary>Lê a preferência de tema claro/escuro do Windows via registro do usuário atual.</summary>
    private static TemaPreferido ResolveSystemTheme()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            if (key?.GetValue("AppsUseLightTheme") is int appsUseLightTheme)
            {
                return appsUseLightTheme == 0 ? TemaPreferido.Escuro : TemaPreferido.Claro;
            }
        }
        catch (Exception)
        {
            // Chave inexistente, sem permissão ou versão do Windows sem esse valor —
            // segue com o padrão do aplicativo (escuro) em vez de propagar o erro.
        }

        return TemaPreferido.Escuro;
    }
}
