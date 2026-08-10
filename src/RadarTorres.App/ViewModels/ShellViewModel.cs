using System;
using System.Collections.ObjectModel;
using System.Linq;
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

    public ShellViewModel(
        IAuthService authService,
        ILocalizationService localizationService,
        IThemeService themeService,
        INavigationService navigationService,
        IPermissionService permissionService,
        IPreferenciasUsuarioRepository preferenciasRepository)
    {
        _authService = authService;
        _localizationService = localizationService;
        _themeService = themeService;
        _navigationService = navigationService;
        _permissionService = permissionService;
        _preferenciasRepository = preferenciasRepository;

        _authService.SessionChanged += OnSessionChanged;
        _localizationService.PropertyChanged += OnLocalizationChanged;

        SetLanguageCommand = new RelayCommand(codigo => SetLanguage((string)codigo!));
        SetThemeCommand = new RelayCommand(tema => SetTheme((TemaPreferido)tema!));
        ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
        NavigateCommand = new RelayCommand(item => Navigate((MenuItem)item!));
        LogoutCommand = new RelayCommand(Logout);
        RestaurarPadraoCommand = new RelayCommand(RestaurarPadrao);

        MenuItems = new ObservableCollection<SidebarMenuEntry>();
        RebuildMenu();

        // A navegação inicial (Painel principal) só acontece depois que a ShellWindow associa
        // o ContentControl via INavigationService.SetHost — ver ShellWindow construtor.
    }

    // ---------------------------------------------------------------- Sessão / usuário

    public string NomeUsuario => _authService.CurrentUser?.Nome ?? string.Empty;

    public string PerfilUsuario => _authService.CurrentUser?.Perfil.ToString() ?? string.Empty;

    // ---------------------------------------------------------------- Idioma / tema

    public string[] AvailableLanguages => _localizationService.AvailableLanguages;

    public string CurrentLanguage => _localizationService.CurrentLanguage;

    public TemaPreferido CurrentTheme => _themeService.CurrentTheme;

    public RelayCommand SetLanguageCommand { get; }

    public RelayCommand SetThemeCommand { get; }

    public RelayCommand RestaurarPadraoCommand { get; }

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

    private void RebuildMenu()
    {
        MenuItems.Clear();

        Models.PerfilUsuario perfil = _authService.CurrentUser?.Perfil ?? Models.PerfilUsuario.Visualizador;

        (MenuItem Item, string LabelKey)[] todos =
        [
            (MenuItem.PainelPrincipal, "Sidebar.PainelPrincipal"),
            (MenuItem.Monitoramento, "Sidebar.Monitoramento"),
            (MenuItem.ObjetosDetectados, "Sidebar.ObjetosDetectados"),
            (MenuItem.AcoesRealizadas, "Sidebar.AcoesRealizadas"),
            (MenuItem.HistoricoModos, "Sidebar.HistoricoModos"),
            (MenuItem.Usuarios, "Sidebar.Usuarios"),
            (MenuItem.ChamadosAjuda, "Sidebar.ChamadosAjuda"),
            (MenuItem.Configuracoes, "Sidebar.Configuracoes"),
            (MenuItem.ConfiguracoesArduino, "Sidebar.ConfiguracoesArduino"),
        ];

        foreach ((MenuItem item, string labelKey) in todos)
        {
            if (_permissionService.PodeVerMenu(perfil, item))
            {
                MenuItems.Add(new SidebarMenuEntry(item, labelKey) { Label = _localizationService[labelKey] });
            }
        }
    }

    private void OnLocalizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        foreach (SidebarMenuEntry entry in MenuItems)
        {
            entry.Label = _localizationService[entry.LabelKey];
        }
    }

    // ---------------------------------------------------------------- Navegação

    public RelayCommand NavigateCommand { get; }

    /// <summary>Chamado pela ShellWindow logo após registrar o ContentControl host (ver SetHost).</summary>
    public void NavigateToDefault() => Navigate(MenuItem.PainelPrincipal);

    private void Navigate(MenuItem item)
    {
        _navigationService.NavigateTo(item);

        foreach (SidebarMenuEntry entry in MenuItems)
        {
            entry.IsSelected = entry.Item == item;
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
    }
}
