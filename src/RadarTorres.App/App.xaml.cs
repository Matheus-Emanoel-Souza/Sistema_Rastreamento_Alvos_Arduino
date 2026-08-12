using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RadarTorres.App.Configuration;
using RadarTorres.App.Data;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;
using RadarTorres.App.ViewModels;
using RadarTorres.App.Views;
using RadarTorres.App.Views.Shell;

namespace RadarTorres.App;

/// <summary>
/// Ponto de entrada da aplicação. Responsável por:
/// 1) carregar <c>appsettings.json</c> (config de sensores/torres, inalterado desde antes
///    desta funcionalidade);
/// 2) montar o contêiner de injeção de dependência — composition root que substitui o
///    wiring manual que existia em <c>MainWindow.xaml.cs</c> (ver comentário original em
///    <see cref="Configuration.AppConfig"/>, que já previa esta migração);
/// 3) garantir que exista um usuário Administrador padrão no primeiro uso;
/// 4) controlar o fluxo login -&gt; shell (sem <c>StartupUri</c>, porque depende de autenticação).
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Acesso estático ao contêiner de DI. Usado apenas nos pontos de composição (este
    /// arquivo); ViewModels e Views recebem suas dependências por construtor, nunca lendo
    /// este campo diretamente (evita o anti-padrão de Service Locator espalhado pelo código).
    /// </summary>
    public static ServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Captura qualquer exceção não tratada (UI thread, threads de fundo e tasks) e exibe
        // uma mensagem clara em vez de deixar o aplicativo fechar sem explicação. Requisito 10.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        LoadAppSettings();

        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        // Bridge estático para o LocExtension (extensão de marcação XAML), resolvido pelo
        // parser XAML antes de qualquer window existir — ver comentário em LocalizationService.
        LocalizationService.Current = ServiceProvider.GetRequiredService<ILocalizationService>();

        AppDataPaths.EnsureDataFolderExists();
        DataSeeder.EnsureDefaultAdmin(
            ServiceProvider.GetRequiredService<IUsuarioRepository>(),
            ServiceProvider.GetRequiredService<IPasswordHasher>());

        // Tema padrão antes de qualquer janela aparecer (evita "flash" sem estilo na tela de
        // login); a preferência real do usuário é aplicada depois de autenticar.
        ServiceProvider.GetRequiredService<IThemeService>().ApplyTheme(TemaPreferido.Escuro);

        // Fecha/ocultar uma janela (ex.: logout) não deve encerrar o app sozinho — controlamos
        // o encerramento explicitamente (login cancelado) via Shutdown().
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        ShowLogin();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // --- Serviços de domínio já existentes (antes instanciados manualmente em
        //     MainWindow.xaml.cs) — comportamento inalterado, só o lugar onde são criados muda.
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<ISerialCommunicationService, SerialCommunicationService>();
        services.AddSingleton<ITargetTrackingService, TargetTrackingService>();
        services.AddSingleton<ITowerSelectionService, TowerSelectionService>();
        services.AddSingleton<IFireControlService, FireControlService>();
        services.AddSingleton<ISimulationService, SimulationService>();

        // --- Aba "Configurações do Arduino" (ambiente/compilação/monitor serial). O compilador
        //     e o localizador do CLI não guardam estado entre chamadas (Transient); as
        //     preferências persistidas usam o mesmo arquivo em disco independentemente da
        //     instância, então Singleton só evita I/O redundante. ISerialCommunicationService
        //     continua Singleton (registrado acima) e é reaproveitado por esta aba — não há
        //     uma segunda implementação de comunicação serial.
        services.AddSingleton<IArduinoCliLocatorService, ArduinoCliLocatorService>();
        services.AddTransient<IArduinoCompilerService, ArduinoCompilerService>();
        services.AddSingleton<IArduinoSettingsRepository, ArduinoSettingsRepository>();

        // --- Dados (CSV hoje — ver TODO(SQL) em AppDataPaths)
        services.AddSingleton<IUsuarioRepository, CsvUsuarioRepository>();
        services.AddSingleton<IObjetoDetectadoRepository, CsvObjetoDetectadoRepository>();
        services.AddSingleton<IAcaoRealizadaRepository, CsvAcaoRealizadaRepository>();
        services.AddSingleton<IAlteracaoModoRepository, CsvAlteracaoModoRepository>();
        services.AddSingleton<IPreferenciasUsuarioRepository, CsvPreferenciasUsuarioRepository>();
        services.AddSingleton<IChamadoAjudaRepository, CsvChamadoAjudaRepository>();

        // --- Layout do painel principal (posição/tamanho dos cards definidos pelo usuário)
        services.AddSingleton<IDashboardLayoutRepository, DashboardLayoutRepository>();

        // --- Infraestrutura nova (autenticação, permissões, idioma, tema, navegação)
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IPermissionService, PermissionService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<INavigationService, NavigationService>();

        // --- Telas / ViewModels
        // MainViewModel + MonitoramentoView são Singleton: representam a sessão de
        // monitoramento em si (conexão serial, alvos ativos) e devem manter estado entre
        // navegações pela barra lateral, exatamente como antes (janela única).
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MonitoramentoView>();

        services.AddTransient<PainelPrincipalViewModel>();
        services.AddSingleton<PainelPrincipalView>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<LoginWindow>();

        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<ShellWindow>();

        services.AddTransient<HelpDeskFormViewModel>();
        services.AddTransient<HelpDeskFormWindow>();

        services.AddTransient<ProfileViewModel>();
        services.AddTransient<ProfileWindow>();

        // Singleton igual a MainViewModel/MonitoramentoView: mantém a conexão serial e o
        // estado da compilação entre navegações pela barra lateral.
        services.AddSingleton<ArduinoSettingsViewModel>();
        services.AddSingleton<ArduinoSettingsView>();
    }

    /// <summary>
    /// Mostra a tela de login (não-modal — <see cref="LoginWindow"/> é Transient, uma nova
    /// instância a cada chamada). Ao fechar, decide entre mostrar a Shell (login OK) ou
    /// encerrar o aplicativo (login cancelado/janela fechada sem autenticar).
    /// </summary>
    private void ShowLogin()
    {
        var loginWindow = ServiceProvider.GetRequiredService<LoginWindow>();
        loginWindow.Closed += (_, _) =>
        {
            var authService = ServiceProvider.GetRequiredService<IAuthService>();
            if (authService.CurrentUser is null)
            {
                Shutdown();
                return;
            }

            AplicarPreferenciasDoUsuario(authService.CurrentUser.Id);
            ShowShell();
        };

        MainWindow = loginWindow;
        loginWindow.Show();
    }

    private void ShowShell()
    {
        var shellWindow = ServiceProvider.GetRequiredService<ShellWindow>();
        MainWindow = shellWindow;
        shellWindow.Show();
    }

    /// <summary>Chamado pela ShellWindow ao fazer logout — volta para a tela de login sem reiniciar o processo.</summary>
    public void ReturnToLogin() => ShowLogin();

    /// <summary>Carrega tema/idioma salvos do usuário (Requisito 8) ou os padrões de fábrica no primeiro login.</summary>
    private void AplicarPreferenciasDoUsuario(int usuarioId)
    {
        var preferenciasRepo = ServiceProvider.GetRequiredService<IPreferenciasUsuarioRepository>();
        var themeService = ServiceProvider.GetRequiredService<IThemeService>();
        var localizationService = ServiceProvider.GetRequiredService<ILocalizationService>();

        PreferenciasUsuario preferencias = preferenciasRepo.GetByUsuarioId(usuarioId) ?? PreferenciasUsuario.PadraoPara(usuarioId);

        themeService.ApplyTheme(preferencias.Tema);
        localizationService.SetLanguage(preferencias.Idioma);
    }

    private void LoadAppSettings()
    {
        try
        {
            var baseDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
            var configPath = Path.Combine(baseDirectory, "appsettings.json");

            if (!File.Exists(configPath))
            {
                // appsettings.json ausente (instalação corrompida/incompleta) — segue com
                // valores padrão em vez de derrubar o aplicativo, e avisa o usuário.
                AppConfig.Current = new AppSettings();
                MessageBox.Show(
                    "O arquivo de configuração 'appsettings.json' não foi encontrado na pasta de instalação.\n" +
                    "O aplicativo será iniciado com valores padrão.\n\n" +
                    "Se o problema persistir, reinstale o RadarTorres.",
                    "RadarTorres - Configuração não encontrada",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var configuration = new ConfigurationBuilder()
                .SetBasePath(baseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            var settings = new AppSettings();
            configuration.Bind(settings);
            AppConfig.Current = settings;
        }
        catch (Exception ex)
        {
            // Falha ao ler/interpretar o appsettings.json (ex.: JSON inválido). Mostra uma
            // mensagem compreensível e segue com configuração padrão em vez de crashar.
            AppConfig.Current = new AppSettings();
            MessageBox.Show(
                $"Não foi possível carregar o arquivo de configuração 'appsettings.json'.\n\n" +
                $"Detalhes: {ex.Message}\n\n" +
                "O aplicativo será iniciado com valores padrão.",
                "RadarTorres - Erro ao iniciar",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    /// <summary>Exceções não tratadas geradas na thread de UI (bindings, comandos, eventos).</summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"Ocorreu um erro inesperado e o RadarTorres pode não continuar funcionando corretamente.\n\n" +
            $"Detalhes: {e.Exception.Message}",
            "RadarTorres - Erro inesperado",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // Marca como tratada para evitar o encerramento abrupto sempre que for seguro
        // continuar (ex.: falha pontual ao processar uma leitura do sensor).
        e.Handled = true;
    }

    /// <summary>Exceções não tratadas geradas em threads de fundo (ex.: leitura serial, timers).</summary>
    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        MessageBox.Show(
            $"Ocorreu um erro crítico no RadarTorres e o aplicativo será encerrado.\n\n" +
            $"Detalhes: {exception?.Message ?? "Erro desconhecido."}",
            "RadarTorres - Erro crítico",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Libera todos os serviços IDisposable registrados no contêiner (MainViewModel,
        // ShellViewModel, ISerialCommunicationService, ...) — sem isso a porta serial e
        // outros recursos poderiam ficar presos até o processo realmente terminar.
        ServiceProvider?.Dispose();
        base.OnExit(e);
    }
}
