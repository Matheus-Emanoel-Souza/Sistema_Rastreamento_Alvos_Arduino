using System.Windows;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da tela de login. Só contém ligações de interface (PasswordBox não suporta
/// binding direto de senha por segurança do WPF, e fechar a janela em caso de sucesso) —
/// nenhuma regra de autenticação mora aqui, isso é <see cref="LoginViewModel"/> + <c>IAuthService</c>.
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

    private void PasswordBoxControl_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Senha = PasswordBoxControl.Password;
    }
}
