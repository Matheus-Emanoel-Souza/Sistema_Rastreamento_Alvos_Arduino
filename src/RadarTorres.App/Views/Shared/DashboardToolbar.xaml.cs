using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>Um item do modal "+ Adicionar painel" (Requisito: imagens dos tipos de
    /// monitoramento disponíveis para o usuário selecionar e importar).</summary>
    private sealed record HiddenPanelEntry(string CardId, string Title, string IconKey);

    /// <summary>Ícone representativo de cada tipo de painel, por CardId — cobre os painéis do
    /// Painel Principal e do Monitoramento (Resources/Icons.xaml). CardId sem entrada aqui cai
    /// no ícone genérico de Configurações.</summary>
    private static readonly Dictionary<string, string> CardIconKeys = new()
    {
        ["Radar"] = "Icon.Monitoramento",
        ["RadarAoVivo"] = "Icon.Monitoramento",
        ["ModoOperacao"] = "Icon.HistoricoModos",
        ["ModoAtual"] = "Icon.HistoricoModos",
        ["DeteccaoCamera"] = "Icon.Cameras",
        ["Monitoramento"] = "Icon.Desempenho",
        ["ConsoleEventos"] = "Icon.ConsoleEventos",
        ["ZonasMortas"] = "Icon.ZonasMortas",
        ["ObjetosDetectados"] = "Icon.ObjetosDetectados",
        ["AcoesRealizadas"] = "Icon.AcoesRealizadas",
        ["EstadoComunicacao"] = "Icon.EstadoComunicacao",
        ["UltimaModoAtualTorre"] = "Icon.HistoricoModos",
        ["UltimaAtualizacao"] = "Icon.HistoricoModos",
    };

    private const string DefaultIconKey = "Icon.Configuracoes";

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

        HiddenPanelsList.ItemsSource = _hiddenCards
            .Select(c => new HiddenPanelEntry(c.CardId, c.Title, CardIconKeys.GetValueOrDefault(c.CardId, DefaultIconKey)))
            .ToList();
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
        HiddenPanelsPopup.IsOpen = true;
    }

    private void HiddenPanelItem_Click(object sender, RoutedEventArgs e)
    {
        var cardId = (string)((Button)sender).Tag;
        HiddenPanelsPopup.IsOpen = false;
        ReopenCardRequested?.Invoke(this, cardId);
    }
}
