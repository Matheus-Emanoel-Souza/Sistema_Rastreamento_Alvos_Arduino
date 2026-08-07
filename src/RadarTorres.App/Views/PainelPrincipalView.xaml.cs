using System.Windows.Controls;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>Code-behind do painel principal — sem lógica própria, tudo em <see cref="PainelPrincipalViewModel"/>.</summary>
public partial class PainelPrincipalView : UserControl
{
    public PainelPrincipalView(PainelPrincipalViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
