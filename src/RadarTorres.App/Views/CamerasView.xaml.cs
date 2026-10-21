using System.Windows.Controls;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind do módulo "Câmeras". Não contém nenhuma lógica de acesso a câmera — apenas
/// libera o dispositivo quando a view sai da árvore visual (troca de tela pela barra lateral),
/// já que <see cref="INavigationService"/> não avisa a view anterior de que foi substituída
/// (só chama <c>OnNavigatedTo</c> na nova). A view é Singleton via DI (mesmo padrão de
/// <see cref="ArduinoSettingsView"/>/<see cref="MonitoramentoView"/>), então o WPF dispara
/// <see cref="UserControl.Unloaded"/> a cada navegação para fora desta tela e
/// <see cref="UserControl.Loaded"/> a cada volta — sem precisar recriar a view.
/// </summary>
public partial class CamerasView : UserControl
{
    private readonly CamerasViewModel _viewModel;

    public CamerasView(CamerasViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        Unloaded += (_, _) => _viewModel.ReleaseCamera();
    }
}
