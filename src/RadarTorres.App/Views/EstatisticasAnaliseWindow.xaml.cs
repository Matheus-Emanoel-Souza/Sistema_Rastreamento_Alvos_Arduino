using System.Windows;

namespace RadarTorres.App.Views;

/// <summary>
/// Janela "Estatísticas e Análises" — hospeda a <see cref="EstatisticasTreinamentoView"/> (3
/// abas) recebida já construída pelo contêiner de DI, sem lógica própria de dados: só
/// dimensionamento inicial (~80% x 85% da área de trabalho) e os botões de
/// fechar/maximizar-restaurar pedidos explicitamente.
/// </summary>
public partial class EstatisticasAnaliseWindow : Window
{
    public EstatisticasAnaliseWindow(EstatisticasTreinamentoView conteudo)
    {
        InitializeComponent();

        ConteudoApresentador.Content = conteudo;

        Loaded += (_, _) =>
        {
            Rect area = SystemParameters.WorkArea;
            Width = area.Width * 0.80;
            Height = area.Height * 0.85;
        };
    }

    private void BotaoFechar_Click(object sender, RoutedEventArgs e) => Close();

    private void BotaoMaximizarRestaurar_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
}
