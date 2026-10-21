using System.Windows.Controls;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>Code-behind da tela "Histórico de Modos". Sem lógica própria — só recarrega no Loaded.</summary>
public partial class HistoricoModosView : UserControl
{
    private readonly HistoricoModosViewModel _viewModel;

    public HistoricoModosView(HistoricoModosViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        // View é Singleton (ver App.xaml.cs) — Loaded refaz a cada navegação pela barra
        // lateral, mesmo padrão de ObjetosDetectadosView.
        Loaded += (_, _) => _viewModel.Reload();
    }
}
