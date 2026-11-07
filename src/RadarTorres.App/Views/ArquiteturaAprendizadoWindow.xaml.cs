using System.Windows;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Janela não-modal "Arquitetura e aprendizado" da tela de Treinamento do Controlador — mesma
/// ViewModel da tela (compartilhada via DataContext), sem lógica própria. Ver
/// <see cref="TreinamentoControladorView.BotaoArquiteturaAprendizado_Click"/> para quem abre.
/// </summary>
public partial class ArquiteturaAprendizadoWindow : Window
{
    public ArquiteturaAprendizadoWindow(TreinamentoControladorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void BotaoFechar_Click(object sender, RoutedEventArgs e) => Close();
}
