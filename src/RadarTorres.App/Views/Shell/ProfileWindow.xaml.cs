using System.Windows;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views.Shell;

/// <summary>
/// Code-behind da janela de perfil. Sem lógica própria — os três campos de senha usam
/// <c>Views/Shared/PasswordRevealBox</c>, que já expõe Binding normal (TwoWay).
/// </summary>
public partial class ProfileWindow : Window
{
    public ProfileWindow(ProfileViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
