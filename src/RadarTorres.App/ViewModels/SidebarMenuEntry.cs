using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>Um item da barra lateral já resolvido para exibição (Requisito 2).</summary>
public sealed class SidebarMenuEntry : ViewModelBase
{
    public MenuItem Item { get; }

    /// <summary>Chave de tradução do rótulo (ex.: "Sidebar.PainelPrincipal") — nunca texto fixo.</summary>
    public string LabelKey { get; }

    /// <summary>Chave do recurso de ícone em Resources/Icons.xaml (ex.: "Icon.Monitoramento").</summary>
    public string IconKey { get; }

    /// <summary>Chave de tradução do rótulo do grupo (ex.: "Sidebar.Group.Operacao"), definida
    /// apenas no primeiro item de cada grupo — é o que faz o cabeçalho de grupo aparecer só uma
    /// vez na Sidebar (ver SidebarView.xaml, trigger em cima deste valor não-nulo).</summary>
    public string? GroupLabelKey { get; }

    /// <summary>Rótulo do grupo já traduzido — mesmo motivo/atualização de <see cref="Label"/>.</summary>
    private string? _groupLabel;
    public string? GroupLabel
    {
        get => _groupLabel;
        set => SetProperty(ref _groupLabel, value);
    }

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

    public SidebarMenuEntry(MenuItem item, string labelKey, string iconKey, string? groupLabelKey)
    {
        Item = item;
        LabelKey = labelKey;
        IconKey = iconKey;
        GroupLabelKey = groupLabelKey;
    }
}
