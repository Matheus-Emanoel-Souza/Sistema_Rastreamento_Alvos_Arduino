using System.Windows;
using RadarTorres.App.Services;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views.Shell;

/// <summary>
/// Janela principal pós-login: barra superior + barra lateral + área de conteúdo navegável
/// (Requisitos 1 e 2). Uma instância por sessão/"aba" (Scoped via DI — ver
/// <c>App.OpenNewSession</c>), não mais uma só para o app inteiro: cada janela tem seu próprio
/// login e pode ser fechada independentemente das demais (quem decide se isso encerra o app é
/// <c>App.CloseSession</c>, ligado ao evento <see cref="Window.Closed"/>).
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
        _viewModel.NovaJanelaSolicitada += OnNovaJanelaSolicitada;

        Closing += (_, _) => _viewModel.Dispose();
    }

    /// <summary>Logout nesta janela: abre uma sessão nova (login em branco) antes de fechar esta
    /// — garante que sempre sobra pelo menos uma janela aberta enquanto o usuário decide se
    /// entra com outra conta, mesmo se esta fosse a única aberta no momento.</summary>
    private void OnLoggedOut(object? sender, System.EventArgs e)
    {
        ((App)Application.Current).OpenNewSession();
        Close();
    }

    private void OnNovaJanelaSolicitada(object? sender, System.EventArgs e) =>
        ((App)Application.Current).OpenNewSession();
}
