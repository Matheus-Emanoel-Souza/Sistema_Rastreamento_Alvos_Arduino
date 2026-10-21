using System;
using System.Windows.Data;
using System.Windows.Markup;
using RadarTorres.App.Services;

namespace RadarTorres.App.Localization;

/// <summary>
/// Extensão de marcação XAML para textos traduzidos: <c>Text="{loc:Loc TopBar.Help}"</c>.
/// Gera um <see cref="Binding"/> de indexador para <see cref="LocalizationService.Current"/>,
/// então qualquer troca de idioma em runtime atualiza o texto automaticamente (ver comentário
/// em <see cref="LocalizationService"/>). Mantém 100% dos textos em
/// <c>Resources/Localization/*.json</c> — nenhuma string de UI hardcoded no XAML (Requisito 1).
/// </summary>
public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; }

    public LocExtension()
    {
        Key = string.Empty;
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationService.Current,
            Mode = BindingMode.OneWay,
            FallbackValue = Key
        };

        return binding.ProvideValue(serviceProvider);
    }
}
