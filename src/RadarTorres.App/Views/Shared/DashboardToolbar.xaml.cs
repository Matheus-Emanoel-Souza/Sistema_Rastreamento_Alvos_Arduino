using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using RadarTorres.App.Services;

namespace RadarTorres.App.Views.Shared;

/// <summary>
/// Barra de ações de layout (salvar/restaurar/reabrir), reutilizada pelo painel principal e
/// pelo Monitoramento — ver comentário em DashboardToolbar.xaml. Puramente visual: só expõe
/// eventos, quem trata cada um é o code-behind da View que hospeda o controle (mesmo padrão de
/// DashboardCard).
/// </summary>
public partial class DashboardToolbar : UserControl
{
    public static readonly DependencyProperty StatusIsSuccessProperty = DependencyProperty.Register(
        nameof(StatusIsSuccess), typeof(bool), typeof(DashboardToolbar), new PropertyMetadata(false));

    private readonly List<(string CardId, string Title)> _hiddenCards = new();

    /// <summary>Disparado ao clicar em "Definir layout como padrão" (Requisito 3).</summary>
    public event EventHandler? SaveMyLayoutRequested;

    /// <summary>Disparado ao clicar em "Retornar ao padrão do sistema" (Requisito 3).</summary>
    public event EventHandler? SystemDefaultRequested;

    /// <summary>Disparado ao clicar em "Retornar ao meu layout" (Requisito 3).</summary>
    public event EventHandler? MyLayoutRequested;

    /// <summary>Disparado ao escolher um painel oculto no menu para reabri-lo — o argumento é
    /// o <c>DashboardCard.CardId</c> escolhido (Requisito 2).</summary>
    public event EventHandler<string>? ReopenCardRequested;

    public DashboardToolbar()
    {
        InitializeComponent();
        UpdateHiddenPanelsButton();
    }

    public bool StatusIsSuccess
    {
        get => (bool)GetValue(StatusIsSuccessProperty);
        set => SetValue(StatusIsSuccessProperty, value);
    }

    /// <summary>Mostra uma mensagem de confirmação/erro inline (Requisito 9), no mesmo padrão
    /// visual já usado em ProfileWindow (cor de destaque em sucesso, cor de alerta em falha).</summary>
    public void ShowStatus(string message, bool success)
    {
        StatusIsSuccess = success;
        StatusText.Text = message;
        StatusText.Visibility = Visibility.Visible;
    }

    /// <summary>Atualiza a lista de painéis ocultos exibida no menu (Requisito 2). Chamado pela
    /// View sempre que o layout muda (ocultar, reexibir, restaurar, aplicar).</summary>
    public void SetHiddenCards(IReadOnlyList<(string CardId, string Title)> hidden)
    {
        _hiddenCards.Clear();
        _hiddenCards.AddRange(hidden);
        UpdateHiddenPanelsButton();
    }

    private void UpdateHiddenPanelsButton()
    {
        string label = LocalizationService.Current?["Dashboard.PaineisOcultos"] ?? "Painéis ocultos";
        HiddenPanelsButton.Content = _hiddenCards.Count > 0 ? $"{label} ({_hiddenCards.Count})" : label;
        HiddenPanelsButton.IsEnabled = _hiddenCards.Count > 0;
    }

    private void SaveMyLayoutButton_Click(object sender, RoutedEventArgs e) => SaveMyLayoutRequested?.Invoke(this, EventArgs.Empty);

    private void SystemDefaultButton_Click(object sender, RoutedEventArgs e) => SystemDefaultRequested?.Invoke(this, EventArgs.Empty);

    private void MyLayoutButton_Click(object sender, RoutedEventArgs e) => MyLayoutRequested?.Invoke(this, EventArgs.Empty);

    private void HiddenPanelsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_hiddenCards.Count == 0) return;

        var menu = new ContextMenu();
        foreach ((string cardId, string title) in _hiddenCards)
        {
            var item = new System.Windows.Controls.MenuItem { Header = title, Tag = cardId };
            item.Click += (_, _) => ReopenCardRequested?.Invoke(this, cardId);
            menu.Items.Add(item);
        }

        HiddenPanelsButton.ContextMenu = menu;
        menu.PlacementTarget = HiddenPanelsButton;
        menu.IsOpen = true;
    }
}
