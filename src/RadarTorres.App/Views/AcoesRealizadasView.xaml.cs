using System.Windows.Controls;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>Code-behind da tela "Ações Realizadas". Sem lógica própria — só recarrega no Loaded.</summary>
public partial class AcoesRealizadasView : UserControl
{
    private readonly AcoesRealizadasViewModel _viewModel;

    public AcoesRealizadasView(AcoesRealizadasViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        // View é Singleton (ver App.xaml.cs) — Loaded refaz a cada navegação pela barra
        // lateral, mesmo padrão de ObjetosDetectadosView.
        Loaded += (_, _) => _viewModel.Reload();
    }
}
