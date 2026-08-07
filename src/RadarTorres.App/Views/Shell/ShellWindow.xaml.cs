using System.Windows;
using RadarTorres.App.Services;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views.Shell;

/// <summary>
/// Janela principal pós-login: barra superior + barra lateral + área de conteúdo navegável
/// (Requisitos 1 e 2). Fica viva durante toda a sessão (Singleton via DI) — logout apenas
/// oculta a janela e devolve o controle para a tela de login, sem recriar o estado do
/// monitoramento (conexão serial, alvos ativos) a cada troca de usuário.
/// </summary>
public partial class ShellWindow : Window
{
    private readonly ShellViewModel _viewModel;

    public ShellWindow(ShellViewModel viewModel, INavigationService navigationService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        navigationService.SetHost(MainContent);
        _viewModel.NavigateToDefault();

        _viewModel.LoggedOut += OnLoggedOut;

        // Fechar a Shell pelo X/Alt+F4 deve encerrar o app inteiro (é a janela principal);
        // ShutdownMode=OnExplicitShutdown (definido em App.xaml.cs) evita que o app suma sem
        // sair de verdade quando ela só é ocultada (Hide) num logout normal.
        Closing += (_, _) =>
        {
            _viewModel.Dispose();
            Application.Current.Shutdown();
        };
    }

    private void OnLoggedOut(object? sender, System.EventArgs e)
    {
        Hide();
        ((App)Application.Current).ReturnToLogin();
    }
}
