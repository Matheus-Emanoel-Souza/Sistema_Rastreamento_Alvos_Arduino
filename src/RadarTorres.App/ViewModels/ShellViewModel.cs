using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da <c>ShellWindow</c>: barra superior (Requisito 1) + barra lateral
/// (Requisito 2) + personalização de layout (Requisito 8). Orquestra idioma, tema,
/// navegação e sessão — nenhuma tela concreta (Monitoramento, Painel principal, ...) é
/// referenciada diretamente, isso é responsabilidade de <see cref="INavigationService"/>.
/// </summary>
public sealed class ShellViewModel : ViewModelBase, IDisposable
{
    private readonly IAuthService _authService;
    private readonly ILocalizationService _localizationService;
    private readonly IThemeService _themeService;
    private readonly INavigationService _navigationService;
    private readonly IPermissionService _permissionService;
    private readonly IPreferenciasUsuarioRepository _preferenciasRepository;
    private readonly ISerialCommunicationService _serialService;
    private readonly IFocusModeService _focusModeService;
    private readonly Dispatcher _dispatcher;

    public ShellViewModel(
        IAuthService authService,
        ILocalizationService localizationService,
        IThemeService themeService,
        INavigationService navigationService,
        IPermissionService permissionService,
        IPreferenciasUsuarioRepository preferenciasRepository,
        ISerialCommunicationService serialService,
        IFocusModeService focusModeService)
    {
        _authService = authService;
        _localizationService = localizationService;
        _themeService = themeService;
        _navigationService = navigationService;
        _permissionService = permissionService;
        _preferenciasRepository = preferenciasRepository;
        _serialService = serialService;
        _focusModeService = focusModeService;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _authService.SessionChanged += OnSessionChanged;
        _localizationService.PropertyChanged += OnLocalizationChanged;
        _serialService.ConnectionStateChanged += OnConnectionStateChanged;
        _navigationService.Navigated += OnNavigated;

        SetLanguageCommand = new RelayCommand(codigo => SetLanguage((string)codigo!));
        SetThemeCommand = new RelayCommand(tema => SetTheme((TemaPreferido)tema!));
        ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
        NavigateCommand = new RelayCommand(item => Navigate((MenuItem)item!));
        LogoutCommand = new RelayCommand(Logout);
        RestaurarPadraoCommand = new RelayCommand(RestaurarPadrao);
        NovaJanelaCommand = new RelayCommand(() => NovaJanelaSolicitada?.Invoke(this, EventArgs.Empty));
        ToggleFocusModeCommand = new RelayCommand(async () => await ToggleFocusModeAsync(), () => !_isTogglingFocusMode);

        MenuItems = new ObservableCollection<SidebarMenuEntry>();
        RebuildMenu();

        // A navegação inicial (Painel principal) só acontece depois que a ShellWindow associa
        // o ContentControl via INavigationService.SetHost — ver ShellWindow construtor.
    }

    // ---------------------------------------------------------------- Sessão / usuário

    public string NomeUsuario => _authService.CurrentUser?.Nome ?? string.Empty;

    public string PerfilUsuario => _authService.CurrentUser?.Perfil.ToString() ?? string.Empty;

    /// <summary>Inicial do nome do usuário logado, para o avatar circular do cabeçalho.</summary>
    public string IniciaisUsuario => string.IsNullOrEmpty(NomeUsuario) ? string.Empty : NomeUsuario[..1].ToUpperInvariant();

    // ---------------------------------------------------------------- Comunicação (chip do cabeçalho)

    public ConnectionState EstadoComunicacao => _serialService.State;

    public string EstadoComunicacaoTexto => _localizationService[$"ConnectionState.{EstadoComunicacao}"];

    private void OnConnectionStateChanged(object? sender, ConnectionState e) => RunOnUi(() =>
    {
        OnPropertyChanged(nameof(EstadoComunicacao));
        OnPropertyChanged(nameof(EstadoComunicacaoTexto));
    });

    private void RunOnUi(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.BeginInvoke(action);
    }

    // ---------------------------------------------------------------- Idioma / tema

    public string[] AvailableLanguages => _localizationService.AvailableLanguages;

    public string CurrentLanguage => _localizationService.CurrentLanguage;

    public TemaPreferido CurrentTheme => _themeService.CurrentTheme;

    public RelayCommand SetLanguageCommand { get; }

    public RelayCommand SetThemeCommand { get; }

    public RelayCommand RestaurarPadraoCommand { get; }

    /// <summary>Abre uma nova janela/aba com sua própria tela de login — permite logar com um
    /// usuário diferente sem afetar a sessão atual. A abertura em si (novo IServiceScope) é
    /// responsabilidade da ShellWindow/App (fora do escopo desta ViewModel), que ouve este evento.</summary>
    public event EventHandler? NovaJanelaSolicitada;

    public RelayCommand NovaJanelaCommand { get; }

    // ---------------------------------------------------------------- Modo foco

    public RelayCommand ToggleFocusModeCommand { get; }

    private bool _isTogglingFocusMode;

    public bool IsFocusModeActive => _focusModeService.IsActive;

    public string FocusModeButtonLabel => _localizationService[IsFocusModeActive
        ? "Shell.FocusMode.Desativar"
        : "Shell.FocusMode.Ativar"];

    private async Task ToggleFocusModeAsync()
    {
        if (_isTogglingFocusMode) return;

        bool ativando = !_focusModeService.IsActive;

        string pergunta = ativando
            ? _localizationService["Shell.FocusMode.ConfirmarAtivar"]
            : _localizationService["Shell.FocusMode.ConfirmarDesativar"];

        bool confirmado = MessageBox.Show(pergunta, _localizationService["Shell.FocusMode.Titulo"],
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
        if (!confirmado) return;

        _isTogglingFocusMode = true;
        RelayCommand.RaiseCanExecuteChangedForAll();

        try
        {
            if (ativando)
            {
                FocusModeResultado resultado = await _focusModeService.EnterFocusModeAsync();
                string resumo = string.Format(_localizationService["Shell.FocusMode.Resumo"],
                    resultado.ProcessosFechados, resultado.ProcessosComFalha,
                    resultado.RedeDesativada
                        ? _localizationService["Shell.FocusMode.RedeOk"]
                        : _localizationService["Shell.FocusMode.RedeFalhou"]);
                MessageBox.Show(resumo, _localizationService["Shell.FocusMode.Titulo"], MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                await _focusModeService.ExitFocusModeAsync();
            }
        }
        finally
        {
            _isTogglingFocusMode = false;
            OnPropertyChanged(nameof(IsFocusModeActive));
            OnPropertyChanged(nameof(FocusModeButtonLabel));
            RelayCommand.RaiseCanExecuteChangedForAll();
        }
    }

    private void SetLanguage(string languageCode)
    {
        _localizationService.SetLanguage(languageCode);
        OnPropertyChanged(nameof(CurrentLanguage));
        SalvarPreferencias();
    }

    private void SetTheme(TemaPreferido tema)
    {
        _themeService.ApplyTheme(tema);
        OnPropertyChanged(nameof(CurrentTheme));
        SalvarPreferencias();
    }

    private void RestaurarPadrao()
    {
        if (_authService.CurrentUser is null) return;

        PreferenciasUsuario padrao = PreferenciasUsuario.PadraoPara(_authService.CurrentUser.Id);
        _preferenciasRepository.Salvar(padrao);

        SetLanguage(padrao.Idioma);
        SetTheme(padrao.Tema);
        IsSidebarCollapsed = padrao.SidebarRecolhida;
    }

    // ---------------------------------------------------------------- Sidebar

    private bool _isSidebarCollapsed;
    public bool IsSidebarCollapsed
    {
        get => _isSidebarCollapsed;
        set
        {
            if (SetProperty(ref _isSidebarCollapsed, value))
            {
                SalvarPreferencias();
            }
        }
    }

    public RelayCommand ToggleSidebarCommand { get; }

    private void ToggleSidebar(object? _) => IsSidebarCollapsed = !IsSidebarCollapsed;

    public ObservableCollection<SidebarMenuEntry> MenuItems { get; }

    /// <summary>Grupos da Sidebar (Requisito "agrupar os itens com rótulos") — a chave de grupo
    /// só é anexada ao PRIMEIRO item de cada grupo; é esse valor não-nulo que faz o cabeçalho de
    /// grupo renderizar uma única vez (ver SidebarView.xaml).</summary>
    private static readonly (MenuItem Item, string LabelKey, string IconKey, string? GroupLabelKey)[] TodosOsItens =
    [
        (MenuItem.PainelPrincipal, "Sidebar.PainelPrincipal", "Icon.PainelPrincipal", "Sidebar.Group.Operacao"),
        (MenuItem.Monitoramento, "Sidebar.Monitoramento", "Icon.Monitoramento", null),
        (MenuItem.Desempenho, "Sidebar.Desempenho", "Icon.Desempenho", null),
        (MenuItem.ObjetosDetectados, "Sidebar.ObjetosDetectados", "Icon.ObjetosDetectados", null),
        (MenuItem.AcoesRealizadas, "Sidebar.AcoesRealizadas", "Icon.AcoesRealizadas", null),
        (MenuItem.HistoricoModos, "Sidebar.HistoricoModos", "Icon.HistoricoModos", null),
        (MenuItem.Cameras, "Sidebar.Cameras", "Icon.Cameras", "Sidebar.Group.Equipamento"),
        (MenuItem.PerfilAlvo, "Sidebar.PerfilAlvo", "Icon.PerfisAlvo", null),
        (MenuItem.ConfiguracoesArduino, "Sidebar.ConfiguracoesArduino", "Icon.ConfiguracoesArduino", null),
        (MenuItem.Usuarios, "Sidebar.Usuarios", "Icon.Usuarios", "Sidebar.Group.Administracao"),
        (MenuItem.TreinamentoControlador, "Sidebar.TreinamentoControlador", "Icon.TreinamentoControlador", null),
        (MenuItem.Configuracoes, "Sidebar.Configuracoes", "Icon.Configuracoes", null),
    ];

    private void RebuildMenu()
    {
        MenuItems.Clear();

        Models.PerfilUsuario perfil = _authService.CurrentUser?.Perfil ?? Models.PerfilUsuario.Visualizador;

        foreach ((MenuItem item, string labelKey, string iconKey, string? groupLabelKey) in TodosOsItens)
        {
            if (_permissionService.PodeVerMenu(perfil, item))
            {
                var entry = new SidebarMenuEntry(item, labelKey, iconKey, groupLabelKey)
                {
                    Label = _localizationService[labelKey]
                };
                if (groupLabelKey is not null) entry.GroupLabel = _localizationService[groupLabelKey];
                MenuItems.Add(entry);
            }
        }
    }

    private void OnLocalizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        foreach (SidebarMenuEntry entry in MenuItems)
        {
            entry.Label = _localizationService[entry.LabelKey];
            if (entry.GroupLabelKey is not null) entry.GroupLabel = _localizationService[entry.GroupLabelKey];
            if (entry.IsSelected) CurrentPageTitle = entry.Label;
        }

        OnPropertyChanged(nameof(EstadoComunicacaoTexto));
        OnPropertyChanged(nameof(FocusModeButtonLabel));
    }

    // ---------------------------------------------------------------- Navegação

    public RelayCommand NavigateCommand { get; }

    /// <summary>Título da página atual (Requisito "título da página" no cabeçalho) — reaproveita
    /// o rótulo já traduzido do item de menu correspondente, em vez de duplicar as chaves de
    /// tradução em outro lugar.</summary>
    private string _currentPageTitle = string.Empty;
    public string CurrentPageTitle
    {
        get => _currentPageTitle;
        private set => SetProperty(ref _currentPageTitle, value);
    }

    /// <summary>Chamado pela ShellWindow logo após registrar o ContentControl host (ver SetHost).</summary>
    public void NavigateToDefault() => Navigate(MenuItem.PainelPrincipal);

    private void Navigate(MenuItem item) => _navigationService.NavigateTo(item);

    /// <summary>Sincroniza seleção da Sidebar e título do cabeçalho com a navegação real —
    /// assina <see cref="INavigationService.Navigated"/> em vez de só reagir ao próprio
    /// <see cref="Navigate"/>, para também cobrir navegação disparada por outra tela (ex.: o
    /// link "Conectar à porta serial" do Painel Principal, que chama NavigateTo diretamente).</summary>
    private void OnNavigated(object? sender, MenuItem item)
    {
        foreach (SidebarMenuEntry entry in MenuItems)
        {
            entry.IsSelected = entry.Item == item;
            if (entry.IsSelected) CurrentPageTitle = entry.Label;
        }
    }

    // ---------------------------------------------------------------- Logout

    public RelayCommand LogoutCommand { get; }

    /// <summary>Disparado após logout efetivo — a ShellWindow ouve este evento para se fechar e voltar ao login.</summary>
    public event EventHandler? LoggedOut;

    private void Logout(object? _)
    {
        _authService.Logout();
        LoggedOut?.Invoke(this, EventArgs.Empty);
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(NomeUsuario));
        OnPropertyChanged(nameof(PerfilUsuario));
        OnPropertyChanged(nameof(IniciaisUsuario));
        RebuildMenu();

        // Um novo usuário logou (a ShellWindow é reaproveitada entre logout/login) — volta
        // para a tela inicial em vez de manter o que o usuário anterior estava vendo.
        if (_authService.CurrentUser is not null)
        {
            Navigate(MenuItem.PainelPrincipal);
        }
    }

    private void SalvarPreferencias()
    {
        if (_authService.CurrentUser is null) return;

        _preferenciasRepository.Salvar(new PreferenciasUsuario
        {
            UsuarioId = _authService.CurrentUser.Id,
            Idioma = _localizationService.CurrentLanguage,
            Tema = _themeService.CurrentTheme,
            SidebarRecolhida = IsSidebarCollapsed,
            TelaInicial = _navigationService.CurrentItem?.ToString(),
            RegistrosPorPagina = _preferenciasRepository.GetByUsuarioId(_authService.CurrentUser.Id)?.RegistrosPorPagina ?? 25
        });
    }

    public void Dispose()
    {
        _authService.SessionChanged -= OnSessionChanged;
        _localizationService.PropertyChanged -= OnLocalizationChanged;
        _serialService.ConnectionStateChanged -= OnConnectionStateChanged;
        _navigationService.Navigated -= OnNavigated;
    }
}
