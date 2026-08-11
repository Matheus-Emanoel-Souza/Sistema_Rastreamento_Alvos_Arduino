using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>Um item da barra lateral já resolvido para exibição (Requisito 2).</summary>
public sealed class SidebarMenuEntry : ViewModelBase
{
    public MenuItem Item { get; }

    /// <summary>Chave de tradução do rótulo (ex.: "Sidebar.PainelPrincipal") — nunca texto fixo.</summary>
    public string LabelKey { get; }

    /// <summary>
    /// Texto já traduzido no idioma atual. Atualizado por <see cref="ShellViewModel"/> sempre
    /// que o idioma muda (mais simples de manter do que um MultiBinding/conversor em XAML
    /// para uma chave de tradução dinâmica por item).
    /// </summary>
    private string _label = string.Empty;
    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public SidebarMenuEntry(MenuItem item, string labelKey)
    {
        Item = item;
        LabelKey = labelKey;
    }
}
