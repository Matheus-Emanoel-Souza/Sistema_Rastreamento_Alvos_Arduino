using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Controls;
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

    public MonitoramentoView(MainViewModel viewModel, IDashboardLayoutRepository layoutRepository, IAuthService authService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _layoutRepository = layoutRepository;
        _authService = authService;
        DataContext = _viewModel;

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

    private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;

        // CardConsoleEventos.CardContent (não FindName) porque o ListBox está dentro do
        // conteúdo de um DashboardCard, que tem seu próprio escopo de nomes — ver comentário
        // equivalente sobre "x:Name" em DashboardCard.xaml.
        if (CardConsoleEventos.CardContent is ListBox listBox && listBox.Items.Count > 0)
        {
            listBox.ScrollIntoView(listBox.Items[^1]);
        }
    }

    private void LoadLayoutForCurrentUser()
    {
        var saved = _layoutRepository.Load(LayoutKey);
        if (saved is { Count: > 0 })
        {
            LayoutCanvas.ApplyLayoutSnapshot(saved);
        }
        else
        {
            LayoutCanvas.ResetToDefaultLayout();
        }

        RefreshHiddenPanelsMenu();
    }

    private void OnSaveMyLayout(object? sender, EventArgs e)
    {
        _layoutRepository.Save(LayoutKey, LayoutCanvas.GetLayoutSnapshot());
        Toolbar.ShowStatus(LocalizationService.Current?["Dashboard.LayoutSalvoComoPadrao"] ?? "Layout salvo.", success: true);
    }

    private void OnSystemDefault(object? sender, EventArgs e)
    {
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
            RefreshHiddenPanelsMenu();
            Toolbar.ShowStatus(LocalizationService.Current?["Dashboard.MeuLayoutRestaurado"] ?? "Seu layout foi restaurado.", success: true);
        }
        else
        {
            Toolbar.ShowStatus(LocalizationService.Current?["Dashboard.SemLayoutPessoalSalvo"] ?? "Você ainda não salvou um layout pessoal.", success: false);
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
        var hidden = LayoutCanvas.GetHiddenCards().Select(c => (c.CardId, c.Title)).ToList();
        Toolbar.SetHiddenCards(hidden);
    }
}
