using System.Windows.Controls;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind de um painel individual da grade de câmeras. Sem lógica própria — o
/// <c>DataContext</c> (um <c>CameraSlotViewModel</c>) chega automaticamente via
/// <c>DataTemplate</c> quando o <c>ItemsControl</c> de <see cref="CamerasView"/> gera esta view
/// para cada item de <c>CamerasViewModel.Slots</c>.
/// </summary>
public partial class CameraPanelView : UserControl
{
    public CameraPanelView()
    {
        InitializeComponent();
    }
}
