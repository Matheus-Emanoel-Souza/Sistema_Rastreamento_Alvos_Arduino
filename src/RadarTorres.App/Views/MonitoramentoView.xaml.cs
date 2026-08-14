using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RadarTorres.App.Models;
using RadarTorres.App.Services;
using RadarTorres.App.ViewModels;
using RadarTorres.App.Views.Shared;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da tela de monitoramento (radar, conexão serial, controle do sistema —
/// conteúdo original de MainWindow, hoje hospedado pela ShellWindow via barra lateral).
/// A <see cref="MainViewModel"/> é injetada via DI (Singleton — ver App.xaml.cs) em vez de
/// ser montada manualmente aqui, mas nenhum comportamento de negócio mudou: continua sem
/// lógica de negócio, só ligações de interface (clique no radar, auto-scroll do console de
/// eventos) mais o layout dos cards (posição/tamanho/visibilidade por usuário), no mesmo
/// padrão já usado em <see cref="PainelPrincipalView"/>.
/// </summary>
public partial class MonitoramentoView : UserControl
{
    private const string Screen = "monitoramento";

    private readonly MainViewModel _viewModel;
    private readonly IDashboardLayoutRepository _layoutRepository;
    private readonly IAuthService _authService;

    /// <summary>
    /// Referência estável ao ListBox do console de eventos, capturada uma única vez na
    /// construção. Necessária porque, ao fixar (<see cref="SetLogPinned"/>), esse mesmo
    /// controle é realocado de <c>CardConsoleEventos.CardContent</c> para
    /// <c>PinnedLogHost.Content</c> — sem essa referência própria, <c>CardConsoleEventos.CardContent
    /// as ListBox</c> pararia de encontrá-lo assim que ele saísse do card (é exatamente esse o
    /// padrão que este campo substitui, usado antes só em <see cref="LogEntries_CollectionChanged"/>).
    /// </summary>
    private ListBox? _logListBox;

    /// <summary>Se o console de eventos está atualmente na faixa lateral (<see cref="PinnedLogPanel"/>)
    /// em vez de dentro do <see cref="LayoutCanvas"/> — usado só para excluí-lo do menu
    /// "Painéis ocultos" (fixado não é a mesma coisa que oculto).</summary>
    private bool _isLogPinned;

    public MonitoramentoView(MainViewModel viewModel, IDashboardLayoutRepository layoutRepository, IAuthService authService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _layoutRepository = layoutRepository;
        _authService = authService;
        DataContext = _viewModel;

        _logListBox = CardConsoleEventos.CardContent as ListBox;

        _viewModel.LogEntries.CollectionChanged += LogEntries_CollectionChanged;

        Toolbar.SaveMyLayoutRequested += OnSaveMyLayout;
        Toolbar.SystemDefaultRequested += OnSystemDefault;
        Toolbar.MyLayoutRequested += OnMyLayout;
        Toolbar.ReopenCardRequested += OnReopenCard;
        LayoutCanvas.LayoutChanged += (_, _) => RefreshHiddenPanelsMenu();

        _viewModel.ScreenActivated += (_, _) => LoadLayoutForCurrentUser();

        // Primeira exibição: ScreenActivated só dispara em navegações subsequentes pela barra
        // lateral, então o carregamento inicial precisa do Loaded (primeiro momento em que o
        // LayoutCanvas tem um tamanho real para converter frações em pixels).
        Loaded += (_, _) => LoadLayoutForCurrentUser();
    }

    /// <summary>Chave de persistência do layout — por tela e por usuário logado (Requisito
    /// "salvo individualmente por usuário").</summary>
    private string LayoutKey => $"{Screen}-{_authService.CurrentUser?.Id.ToString() ?? "anonimo"}";

    private void Radar_TargetClicked(object? sender, int targetId) => _viewModel.SelectTargetById(targetId);

    private void Radar_DeadZoneQuadrantSelected(object? sender, Quadrant quadrant) => _viewModel.OnRadarQuadrantSelected(quadrant);

    private void Radar_DeadZoneRangeSelected(object? sender, (double MinDistance, double MaxDistance) range) =>
        _viewModel.OnRadarRangeSelected(range.MinDistance, range.MaxDistance);

    private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;

        // _logListBox (não CardConsoleEventos.CardContent) porque o ListBox pode estar tanto
        // dentro do card quanto na faixa lateral fixada — ver comentário no campo.
        if (_logListBox is { Items.Count: > 0 } listBox)
        {
            listBox.ScrollIntoView(listBox.Items[^1]);
        }
    }

    // ---------------------------------------------------------------- Fixar console na lateral

    private void PinLogToggle_Checked(object sender, RoutedEventArgs e) => SetLogPinned(true);

    private void PinLogToggle_Unchecked(object sender, RoutedEventArgs e) => SetLogPinned(false);

    /// <summary>
    /// Move o ListBox do console de eventos entre <c>CardConsoleEventos</c> (card arrastável
    /// normal, dentro do <see cref="LayoutCanvas"/>) e <see cref="PinnedLogPanel"/> (faixa fixa
    /// na borda direita, fora do canvas) — sempre o mesmo controle realocado, nunca uma cópia,
    /// então nenhum binding/ItemTemplate precisa existir duas vezes.
    /// </summary>
    private void SetLogPinned(bool pinned)
    {
        if (_logListBox is null || pinned == _isLogPinned) return;

        if (pinned)
        {
            CardConsoleEventos.CardContent = null;
            LayoutCanvas.HideCard(CardConsoleEventos); // some do canvas — não é "oculto", é "mudou de lugar"
            PinnedLogHost.Content = _logListBox;
            PinnedLogColumn.Width = new GridLength(340);
            PinnedLogPanel.Visibility = Visibility.Visible;
        }
        else
        {
            PinnedLogHost.Content = null;
            PinnedLogPanel.Visibility = Visibility.Collapsed;
            PinnedLogColumn.Width = new GridLength(0);
            CardConsoleEventos.CardContent = _logListBox;
            LayoutCanvas.ShowCard(CardConsoleEventos); // volta à última posição/tamanho conhecidos
        }

        _isLogPinned = pinned;
        RefreshHiddenPanelsMenu(); // fixado não deve aparecer/desaparecer do menu "Painéis ocultos"
    }

    private void LoadLayoutForCurrentUser()
    {
        var saved = _layoutRepository.Load(LayoutKey);
        if (saved is { Count: > 0 })
        {
            LayoutCanvas.ApplyLayoutSnapshot(saved);
            ApplyLogPinFromSnapshot(saved);
        }
        else
        {
            PinLogToggle.IsChecked = false;
            LayoutCanvas.ResetToDefaultLayout();
        }

        RefreshHiddenPanelsMenu();
    }

    private void OnSaveMyLayout(object? sender, EventArgs e)
    {
        var snapshot = LayoutCanvas.GetLayoutSnapshot();
        ApplyLogPinToSnapshot(snapshot);
        _layoutRepository.Save(LayoutKey, snapshot);
        Toolbar.ShowStatus(LocalizationService.Current?["Dashboard.LayoutSalvoComoPadrao"] ?? "Layout salvo.", success: true);
    }

    private void OnSystemDefault(object? sender, EventArgs e)
    {
        // "Padrão do sistema" nunca inclui o console fixado — solta antes de rearranjar, senão
        // ArrangeDefaultFlow reexibiria o card vazio (o ListBox continuaria em PinnedLogHost).
        PinLogToggle.IsChecked = false;

        // Só recalcula o arranjo em memória/tela — nunca toca no arquivo salvo (Requisito 6).
        LayoutCanvas.ResetToDefaultLayout();
        RefreshHiddenPanelsMenu();
        Toolbar.ShowStatus(LocalizationService.Current?["Dashboard.LayoutSistemaRestaurado"] ?? "Layout do sistema restaurado.", success: true);
    }

    private void OnMyLayout(object? sender, EventArgs e)
    {
        var saved = _layoutRepository.Load(LayoutKey);
        if (saved is { Count: > 0 })
        {
            LayoutCanvas.ApplyLayoutSnapshot(saved);
            ApplyLogPinFromSnapshot(saved);
            RefreshHiddenPanelsMenu();
            Toolbar.ShowStatus(LocalizationService.Current?["Dashboard.MeuLayoutRestaurado"] ?? "Seu layout foi restaurado.", success: true);
        }
        else
        {
            Toolbar.ShowStatus(LocalizationService.Current?["Dashboard.SemLayoutPessoalSalvo"] ?? "Você ainda não salvou um layout pessoal.", success: false);
        }
    }

    /// <summary>Lê <c>IsPinnedRight</c> do card do console (se houver entrada salva) e aplica via
    /// o toggle — atribuir <c>PinLogToggle.IsChecked</c> dispara Checked/Unchecked, que já chama
    /// <see cref="SetLogPinned"/>; nenhuma outra lógica precisa ser duplicada aqui.</summary>
    private void ApplyLogPinFromSnapshot(Dictionary<string, DashboardCardLayout> snapshot)
    {
        bool pinned = snapshot.TryGetValue(CardConsoleEventos.CardId, out DashboardCardLayout? layout) && layout.IsPinnedRight;
        PinLogToggle.IsChecked = pinned;
    }

    private void ApplyLogPinToSnapshot(Dictionary<string, DashboardCardLayout> snapshot)
    {
        if (snapshot.TryGetValue(CardConsoleEventos.CardId, out DashboardCardLayout? layout))
        {
            layout.IsPinnedRight = _isLogPinned;
        }
    }

    private void OnReopenCard(object? sender, string cardId)
    {
        var card = LayoutCanvas.Children.OfType<DashboardCard>().FirstOrDefault(c => c.CardId == cardId);
        if (card is null) return;

        LayoutCanvas.ShowCard(card);
        RefreshHiddenPanelsMenu();
    }

    private void RefreshHiddenPanelsMenu()
    {
        // O card do console de eventos some do canvas tanto quando ocultado (Requisito 1) quanto
        // quando fixado na lateral (SetLogPinned) — só o primeiro caso deve aparecer aqui, senão
        // "reabrir" um card fixado via este menu entraria em conflito com PinnedLogHost.
        var hidden = LayoutCanvas.GetHiddenCards()
            .Where(c => !(_isLogPinned && c == CardConsoleEventos))
            .Select(c => (c.CardId, c.Title))
            .ToList();
        Toolbar.SetHiddenCards(hidden);
    }
}
