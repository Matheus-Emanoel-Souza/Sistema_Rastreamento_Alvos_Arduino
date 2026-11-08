using System.Windows.Controls;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da tela "Dashboards" — sem lógica própria, só liga a ViewModel injetada ao
/// DataContext (mesmo padrão das demais telas de nível superior do menu).
/// </summary>
public partial class DashboardsView : UserControl
{
    public DashboardsView(DashboardsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
