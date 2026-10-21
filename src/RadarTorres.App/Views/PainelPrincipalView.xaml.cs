using System;
using System.Linq;
using System.Windows.Controls;
using RadarTorres.App.Services;
using RadarTorres.App.ViewModels;
using RadarTorres.App.Views.Shared;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind do painel principal. A única responsabilidade fora do MVVM é o layout dos
/// cards (posição/tamanho/visibilidade definidos livremente pelo usuário, por usuário) — é um
/// estado puramente visual do <see cref="DashboardCanvas"/>, então é carregado/salvo aqui em
/// vez de na ViewModel, seguindo o mesmo princípio já usado em <see cref="ArduinoSettingsView"/>
/// para diálogos de arquivo (a ViewModel só expõe dados/eventos; quem mexe em elementos visuais
/// é a View).
/// </summary>
public partial class PainelPrincipalView : UserControl
{
    private const string Screen = "painel-principal";

    private readonly PainelPrincipalViewModel _viewModel;
    private readonly IDashboardLayoutRepository _layoutRepository;
    private readonly IAuthService _authService;

    public PainelPrincipalView(PainelPrincipalViewModel viewModel, IDashboardLayoutRepository layoutRepository, IAuthService authService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _layoutRepository = layoutRepository;
        _authService = authService;
        DataContext = _viewModel;

        Toolbar.SaveMyLayoutRequested += OnSaveMyLayout;
        Toolbar.SystemDefaultRequested += OnSystemDefault;
        Toolbar.MyLayoutRequested += OnMyLayout;
        Toolbar.ReopenCardRequested += OnReopenCard;
        LayoutCanvas.LayoutChanged += (_, _) => RefreshHiddenPanelsMenu();

        _viewModel.ScreenActivated += (_, _) => LoadLayoutForCurrentUser();

        // Primeira exibição: OnNavigatedTo/ScreenActivated só dispara em navegações
        // subsequentes pela barra lateral, então o carregamento inicial precisa do Loaded
        // (primeiro momento em que o LayoutCanvas tem um tamanho real para converter frações
        // em pixels).
        Loaded += (_, _) => LoadLayoutForCurrentUser();
    }

    /// <summary>Chave de persistência do layout — por tela e por usuário logado (Requisito
    /// "salvo individualmente por usuário").</summary>
    private string LayoutKey => $"{Screen}-{_authService.CurrentUser?.Id.ToString() ?? "anonimo"}";

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
