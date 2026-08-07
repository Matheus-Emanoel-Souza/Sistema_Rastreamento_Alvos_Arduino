using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace RadarTorres.App.Views.Shell;

/// <summary>
/// Code-behind da barra superior. Os botões "Perfil" e "Ajuda" abrem janelas próprias
/// resolvidas via DI — mantido no code-behind (e não no ShellViewModel) para não misturar
/// responsabilidade de UI (qual Window abrir) com orquestração de estado da ViewModel.
/// </summary>
public partial class TopBarView : UserControl
{
    public TopBarView()
    {
        InitializeComponent();
    }

    private void ProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var window = App.ServiceProvider.GetRequiredService<ProfileWindow>();
        window.Owner = Window.GetWindow(this);
        window.ShowDialog();
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        var window = App.ServiceProvider.GetRequiredService<HelpDeskFormWindow>();
        window.Owner = Window.GetWindow(this);
        window.ShowDialog();
    }
}
