using System.Windows.Controls;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da tela "Perfis de Alvo". Sem lógica própria além de liberar a câmera usada
/// para a foto de referência quando a view sai da árvore visual — mesmo padrão de
/// <see cref="CamerasView"/>.
/// </summary>
public partial class PerfilAlvoView : UserControl
{
    private readonly PerfilAlvoViewModel _viewModel;

    public PerfilAlvoView(PerfilAlvoViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        Unloaded += (_, _) => _viewModel.Dispose();
    }
}
