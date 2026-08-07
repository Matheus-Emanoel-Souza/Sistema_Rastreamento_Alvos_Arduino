using System.Windows;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da tela de login. Só contém ligações de interface (fechar a janela quando o
/// login é bem-sucedido) — nenhuma regra de autenticação mora aqui, isso é
/// <see cref="LoginViewModel"/> + <c>IAuthService</c>. O campo de senha usa
/// <c>Views/Shared/PasswordRevealBox</c>, que já expõe Binding normal.
/// </summary>
public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.LoginSucceeded += (_, _) => Close();

        Loaded += (_, _) => LoginTextBox.Focus();
    }
}
