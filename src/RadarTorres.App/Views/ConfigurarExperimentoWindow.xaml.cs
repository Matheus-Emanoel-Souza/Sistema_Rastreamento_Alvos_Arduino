using System.Windows;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Janela modal "Configurar experimento" da tela de Treinamento do Controlador — mesma
/// ViewModel da tela (compartilhada via DataContext), sem lógica própria. Ver
/// <see cref="TreinamentoControladorView.BotaoConfigurarExperimento_Click"/> para quem abre.
/// </summary>
public partial class ConfigurarExperimentoWindow : Window
{
    public ConfigurarExperimentoWindow(TreinamentoControladorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void BotaoFechar_Click(object sender, RoutedEventArgs e) => Close();
}
