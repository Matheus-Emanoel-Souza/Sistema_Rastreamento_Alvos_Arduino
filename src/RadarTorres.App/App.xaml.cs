using System;
using System.Collections.Generic;
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
/// 4) controlar o fluxo login -&gt; shell, agora com suporte a múltiplas "abas" (janelas)
///    simultâneas, cada uma com seu próprio login/sessão — ver <see cref="OpenNewSession"/>.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Acesso estático ao contêiner de DI. Usado apenas nos pontos de composição (este
    /// arquivo); ViewModels e Views recebem suas dependências por construtor, nunca lendo
    /// este campo diretamente (evita o anti-padrão de Service Locator espalhado pelo código).
    /// </summary>
    public static ServiceProvider ServiceProvider { get; private set; } = null!;

    /// <summary>
    /// Uma "aba" = um <see cref="IServiceScope"/> próprio, com sua própria instância de
    /// <c>IAuthService</c>/<c>ShellViewModel</c>/<c>MainViewModel</c>/telas de navegação —
    /// permite logins diferentes em abas diferentes ao mesmo tempo, todas dentro da mesma
    /// janela do Windows (<see cref="_mainWindow"/>, Requisito "abas como em navegador"; antes
    /// cada sessão abria em uma janela própria). Serviços de hardware (conexão serial,
    /// rastreamento de alvos, seleção de torre, acionamento, simulação, zonas mortas) continuam
    /// Singleton no <see cref="ServiceProvider"/> raiz, fora de qualquer escopo — é o mesmo
    /// sistema físico, compartilhado por todas as abas. O app encerra quando a última sessão
    /// fecha (nenhuma aba sozinha derruba as demais).
    /// </summary>
    private readonly List<IServiceScope> _openSessions = new();

    private MainWindow _mainWindow = null!;

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

        // Reaplica no banco qualquer escrita que ficou pendente de uma sessão anterior sem
        // conexão (ver PendingWriteQueue) — antes de qualquer outra leitura/escrita, inclusive
        // o seed do admin padrão abaixo.
        PendingWriteQueue.TryReplayPendingWrites();

        DataSeeder.EnsureDefaultAdmin(
            ServiceProvider.GetRequiredService<IUsuarioRepository>(),
            ServiceProvider.GetRequiredService<IPasswordHasher>());

        // Tema padrão antes de qualquer janela aparecer (evita "flash" sem estilo na tela de
        // login); a preferência real do usuário é aplicada depois de autenticar. Tema/idioma
        // são compartilhados por todas as janelas (recursos globais da Application no WPF).
        ServiceProvider.GetRequiredService<IThemeService>().ApplyTheme(TemaPreferido.Escuro);

        // Cada sessão (aba) fecha independentemente — o app só encerra quando a última fecha
        // (ver CloseSession). Controlamos isso manualmente em vez de deixar o WPF decidir
        // sozinho pelo fechamento da única janela (_mainWindow).
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _mainWindow = new MainWindow();
        _mainWindow.NovaAbaSolicitada += (_, _) => OpenNewSession();
        _mainWindow.AbaFechada += (_, tab) => FecharAba(tab);
        _mainWindow.Show();

        OpenNewSession();
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

        // --- Módulo "Câmeras" (visualização em tempo real de até 4 webcams simultâneas).
        //     ICameraDeviceEnumerator é Singleton só por consistência com o resto do arquivo (é
        //     sem estado). ICameraCaptureServiceFactory é Singleton (a fábrica em si não guarda
        //     estado), mas cada painel usa a fábrica para criar sua PRÓPRIA ICameraCaptureService
        //     (por isso ela não é registrada diretamente no container) — ver CamerasViewModel.
        services.AddSingleton<ICameraDeviceEnumerator, CameraDeviceEnumerator>();
        services.AddSingleton<ICameraCaptureServiceFactory, CameraCaptureServiceFactory>();

        // --- Zonas mortas (quadrante/faixa de distância onde nenhuma torre é selecionada nem
        //     acionamento é autorizado). Repositório e serviço únicos para toda a instalação,
        //     consultados por TowerSelectionService/FireControlService acima.
        services.AddSingleton<IZonaMortaRepository, ZonaMortaRepository>();
        services.AddSingleton<IZonaMortaService, ZonaMortaService>();

        // --- Aba "Configurações do Arduino" (ambiente/compilação/monitor serial). O compilador
        //     e o localizador do CLI não guardam estado entre chamadas (Transient); as
        //     preferências persistidas usam o mesmo arquivo em disco independentemente da
        //     instância, então Singleton só evita I/O redundante. ISerialCommunicationService
        //     continua Singleton (registrado acima) e é reaproveitado por esta aba — não há
        //     uma segunda implementação de comunicação serial.
        services.AddSingleton<IArduinoCliLocatorService, ArduinoCliLocatorService>();
        services.AddTransient<IArduinoCompilerService, ArduinoCompilerService>();
        services.AddSingleton<IArduinoSettingsRepository, ArduinoSettingsRepository>();

        // --- Dados: todo registro do sistema grava direto em SQLite (radartorres.db) — sem
        //     fallback para CSV (ver Data/SqliteConnectionFactory.cs). O único CSV que a
        //     aplicação ainda produz é o export manual de "Objetos Detectados" (ação explícita
        //     do usuário, ver IObjetoDetectadoExportService), que não é persistência.
        services.AddSingleton<IUsuarioRepository, SqliteUsuarioRepository>();
        services.AddSingleton<IObjetoDetectadoRepository, SqliteObjetoDetectadoRepository>();
        services.AddTransient<IObjetoDetectadoExportService, ObjetoDetectadoExportService>();
        services.AddSingleton<IAcaoRealizadaRepository, SqliteAcaoRealizadaRepository>();
        services.AddSingleton<IModoAtualTorreRepository, SqliteModoAtualTorreRepository>();
        services.AddSingleton<IPreferenciasUsuarioRepository, SqlitePreferenciasUsuarioRepository>();

        // --- Layout do painel principal (posição/tamanho dos cards definidos pelo usuário)
        services.AddSingleton<IDashboardLayoutRepository, DashboardLayoutRepository>();

        // --- Infraestrutura nova (autenticação, permissões, idioma, tema, navegação)
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        // Scoped (uma instância por janela/sessão — ver OpenNewSession): é o que torna possível
        // logins diferentes em janelas diferentes ao mesmo tempo. IPermissionService é stateless,
        // Singleton só por não precisar ser recriado; Localização/Tema continuam Singleton de
        // propósito — são recursos globais da Application no WPF, compartilhados por todas as
        // janelas (não daria pra ter um idioma/tema por janela sem reescrever os DynamicResource).
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton<IPermissionService, PermissionService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddScoped<INavigationService, NavigationService>();

        // --- Telas / ViewModels
        // Scoped (uma instância por janela/sessão, não mais uma só pro app inteiro): cada
        // aba/janela precisa da sua própria View (um UserControl não pode estar em duas árvores
        // visuais/janelas ao mesmo tempo) e do seu próprio ViewModel quando ele depende de
        // IAuthService (permissões variam por janela). MainViewModel/MonitoramentoView mantêm
        // estado entre navegações pela barra lateral DENTRO da mesma janela, exatamente como
        // antes — só deixam de ser compartilhados ENTRE janelas. Os serviços de hardware que
        // MainViewModel usa (serial, rastreamento, seleção de torre, acionamento, simulação)
        // continuam Singleton acima — é a mesma conexão física vista por todas as janelas.
        services.AddScoped<MainViewModel>();
        services.AddScoped<MonitoramentoView>();

        services.AddTransient<PainelPrincipalViewModel>();
        services.AddScoped<PainelPrincipalView>();

        services.AddTransient<ObjetosDetectadosViewModel>();
        services.AddScoped<ObjetosDetectadosView>();

        // Tela "Usuários" (exclusiva do Administrador): mesmo padrão de recarregamento no
        // Loaded que ObjetosDetectadosView.
        services.AddTransient<UsuariosViewModel>();
        services.AddScoped<UsuariosView>();

        // Tela "Ações Realizadas": mesmo padrão de recarregamento no Loaded.
        services.AddTransient<AcoesRealizadasViewModel>();
        services.AddScoped<AcoesRealizadasView>();

        // Tela "Histórico de Modos": mesmo padrão de recarregamento no Loaded.
        services.AddTransient<HistoricoModosViewModel>();
        services.AddScoped<HistoricoModosView>();

        services.AddTransient<LoginViewModel>();
        services.AddTransient<LoginView>();

        services.AddScoped<ShellViewModel>();
        services.AddScoped<ShellView>();

        services.AddTransient<ProfileViewModel>();
        services.AddTransient<ProfileWindow>();

        services.AddScoped<ArduinoSettingsViewModel>();
        services.AddScoped<ArduinoSettingsView>();

        services.AddScoped<CamerasViewModel>();
        services.AddScoped<CamerasView>();
    }

    /// <summary>
    /// Abre uma nova aba numa sessão própria (<see cref="IServiceScope"/>) independente de
    /// qualquer outra já aberta — é o que permite logar com usuários diferentes ao mesmo tempo
    /// em abas diferentes, todas na mesma janela (<see cref="_mainWindow"/>). Chamado no início
    /// do app, ao clicar em "+"/"Nova página" (ver <c>MainWindow.NovaAbaSolicitada</c> e
    /// <c>ShellViewModel.NovaJanelaCommand</c>) e — indiretamente, via <see cref="StartLoginFlow"/>
    /// reaproveitando a MESMA aba — depois de um logout.
    /// </summary>
    public void OpenNewSession()
    {
        IServiceScope session = ServiceProvider.CreateScope();
        _openSessions.Add(session);

        var tab = new SessionTabViewModel(session);
        _mainWindow.Tabs.Add(tab);
        _mainWindow.SelectedTab = tab;

        StartLoginFlow(session, tab);
    }

    /// <summary>Mostra a tela de login como conteúdo da aba informada; ao autenticar com
    /// sucesso, troca o conteúdo da mesma aba para o shell pós-login (ver
    /// <see cref="ShowShellInTab"/>). Reaproveitado tanto para abrir uma aba nova quanto para
    /// voltar uma aba existente à tela de login depois de um logout.</summary>
    private void StartLoginFlow(IServiceScope session, SessionTabViewModel tab)
    {
        var loginView = session.ServiceProvider.GetRequiredService<LoginView>();
        tab.Header = session.ServiceProvider.GetRequiredService<ILocalizationService>()["Login.TabHeader"];
        tab.Content = loginView;

        loginView.ViewModel.LoginSucceeded += (_, _) =>
        {
            var authService = session.ServiceProvider.GetRequiredService<IAuthService>();
            AplicarPreferenciasDoUsuario(session, authService.CurrentUser!.Id);
            ShowShellInTab(session, tab);
        };
    }

    private void ShowShellInTab(IServiceScope session, SessionTabViewModel tab)
    {
        var shellView = session.ServiceProvider.GetRequiredService<ShellView>();
        tab.Content = shellView;
        tab.Header = shellView.ViewModel.NomeUsuario;

        // Logout: a mesma aba volta para a tela de login (com uma sessão/escopo novos) em vez de
        // fechar — igual a um navegador voltando uma aba para uma página anterior, não fechando
        // a aba. A sessão antiga é encerrada normalmente (ver CloseSession).
        shellView.ViewModel.LoggedOut += (_, _) =>
        {
            CloseSession(session);

            IServiceScope novaSessao = ServiceProvider.CreateScope();
            _openSessions.Add(novaSessao);
            tab.Session = novaSessao;
            StartLoginFlow(novaSessao, tab);
        };

        shellView.ViewModel.NovaJanelaSolicitada += (_, _) => OpenNewSession();
    }

    /// <summary>Fecha uma aba pelo "✕" (ver <c>MainWindow.AbaFechada</c>): diferente de um
    /// logout, aqui a aba em si desaparece — encerra a sessão correspondente e, se essa era a
    /// aba selecionada, seleciona outra (ou nenhuma, se não sobrar mais nenhuma).</summary>
    private void FecharAba(SessionTabViewModel tab)
    {
        _mainWindow.Tabs.Remove(tab);
        if (ReferenceEquals(_mainWindow.SelectedTab, tab))
        {
            _mainWindow.SelectedTab = _mainWindow.Tabs.Count > 0 ? _mainWindow.Tabs[^1] : null;
        }

        CloseSession(tab.Session);
    }

    /// <summary>
    /// Encerra uma sessão: libera tudo que foi resolvido dentro do escopo (AuthService,
    /// ShellViewModel/MainViewModel dessa aba etc. — ver <see cref="IServiceScope.Dispose"/>) e
    /// só derruba o aplicativo quando essa era a última sessão aberta; as demais abas continuam
    /// funcionando normalmente.
    /// </summary>
    private void CloseSession(IServiceScope session)
    {
        _openSessions.Remove(session);
        session.Dispose();

        if (_openSessions.Count == 0)
        {
            Shutdown();
        }
    }

    /// <summary>Carrega tema/idioma salvos do usuário (Requisito 8) ou os padrões de fábrica no primeiro login.</summary>
    private void AplicarPreferenciasDoUsuario(IServiceScope session, int usuarioId)
    {
        var preferenciasRepo = session.ServiceProvider.GetRequiredService<IPreferenciasUsuarioRepository>();
        var themeService = session.ServiceProvider.GetRequiredService<IThemeService>();
        var localizationService = session.ServiceProvider.GetRequiredService<ILocalizationService>();

        PreferenciasUsuario preferencias = preferenciasRepo.GetByUsuarioId(usuarioId) ?? PreferenciasUsuario.PadraoPara(usuarioId);

        themeService.ApplyTheme(preferencias.Tema);
        localizationService.SetLanguage(preferencias.Idioma);
    }

    private void LoadAppSettings()
    {
        try
        {
            // AppContext.BaseDirectory funciona tanto em publish normal quanto em single-file
            // (Assembly.Location retorna "" em single-file, o que fazia Path.GetDirectoryName
            // devolver null e Path.Combine lançar ArgumentNullException).
            var baseDirectory = AppContext.BaseDirectory;
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
