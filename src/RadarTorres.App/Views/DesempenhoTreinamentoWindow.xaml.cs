using System.Windows;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Janela não-modal "Desempenho do treinamento" da tela de Treinamento do Controlador — mesma
/// ViewModel da tela (compartilhada via DataContext), sem lógica própria. Ver
/// <see cref="TreinamentoControladorView.BotaoDesempenhoTreinamento_Click"/> para quem abre.
/// </summary>
public partial class DesempenhoTreinamentoWindow : Window
{
    public DesempenhoTreinamentoWindow(TreinamentoControladorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void BotaoFechar_Click(object sender, RoutedEventArgs e) => Close();
}
