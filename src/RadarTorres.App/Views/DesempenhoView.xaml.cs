using System.Windows.Controls;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da tela "Desempenho". View é Singleton (ver App.xaml.cs) — a amostragem só roda
/// enquanto a tela está visível: <c>Loaded</c>/<c>Unloaded</c> ligam/desligam o timer da
/// ViewModel a cada navegação, mesmo padrão de custo controlado já usado no <c>RadarControl</c>.
/// </summary>
public partial class DesempenhoView : UserControl
{
    private readonly DesempenhoViewModel _viewModel;

    public DesempenhoView(DesempenhoViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += (_, _) => _viewModel.StartMonitoring();
        Unloaded += (_, _) => _viewModel.StopMonitoring();
    }
}
