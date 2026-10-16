using System.Windows.Controls;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da tela "Configurações". Só liga a View ao seu ViewModel — toda a lógica
/// (edição em cópia, validação de limites, gravação em disco) vive em
/// <see cref="ConfiguracoesViewModel"/>, seguindo o mesmo padrão das demais telas.
/// </summary>
public partial class ConfiguracoesView : UserControl
{
    public ConfiguracoesView(ConfiguracoesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
