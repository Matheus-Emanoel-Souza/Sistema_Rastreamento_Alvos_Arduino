using System.Windows;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views.Shell;

/// <summary>Code-behind da janela de perfil — só liga os três PasswordBox ao ViewModel.</summary>
public partial class ProfileWindow : Window
{
    private readonly ProfileViewModel _viewModel;

    public ProfileWindow(ProfileViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void SenhaAtualBox_PasswordChanged(object sender, RoutedEventArgs e) => _viewModel.SenhaAtual = SenhaAtualBox.Password;

    private void NovaSenhaBox_PasswordChanged(object sender, RoutedEventArgs e) => _viewModel.NovaSenha = NovaSenhaBox.Password;

    private void ConfirmarSenhaBox_PasswordChanged(object sender, RoutedEventArgs e) => _viewModel.ConfirmarNovaSenha = ConfirmarSenhaBox.Password;
}
