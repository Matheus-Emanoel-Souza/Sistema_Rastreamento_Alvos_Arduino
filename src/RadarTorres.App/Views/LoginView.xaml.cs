using System.Windows.Controls;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Conteúdo da tela de login — antes uma <c>Window</c> própria (<c>LoginWindow</c>), agora um
/// <see cref="UserControl"/> hospedado como conteúdo de uma aba do <c>MainWindow</c> (Requisito
/// "abas como em navegador"): abrir uma aba nova mostra este login ali mesmo, sem abrir outra
/// janela do Windows por cima. Só ligações de interface aqui — nenhuma regra de autenticação
/// (isso é <see cref="LoginViewModel"/> + <c>IAuthService</c>). O campo de senha usa
/// <c>Views/Shared/PasswordRevealBox</c>, que já expõe Binding normal.
/// </summary>
public partial class LoginView : UserControl
{
    public LoginViewModel ViewModel { get; }

    public LoginView(LoginViewModel viewModel)
    {
        InitializeComponent();

        ViewModel = viewModel;
        DataContext = ViewModel;

        Loaded += (_, _) => LoginTextBox.Focus();
    }
}
