using System.Windows;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views.Shell;

/// <summary>Code-behind do formulário de ajuda — sem lógica própria, tudo em <see cref="HelpDeskFormViewModel"/>.</summary>
public partial class HelpDeskFormWindow : Window
{
    public HelpDeskFormWindow(HelpDeskFormViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
