using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da tela "Treinamento do Controlador". A única responsabilidade fora do MVVM é o
/// gráfico de evolução do fitness: desenhar um <see cref="Polyline"/> a partir de um histórico
/// bruto de pontos é puramente visual (conversão para coordenadas de tela), então fica aqui em
/// vez de na ViewModel — mesmo princípio já usado no <c>PainelPrincipalView</c> para o layout
/// do dashboard.
/// </summary>
public partial class TreinamentoControladorView : UserControl
{
    private const double AlturaMinimizadaHistorico = 36.0;
    private const double AlturaMinimaExpandidaHistorico = 120.0;
    private const double AlturaPadraoExpandidaHistorico = 220.0;

    private readonly TreinamentoControladorViewModel _viewModel;

    private bool _historicoMinimizado;
    private double _ultimaAlturaExpandidaHistorico = AlturaPadraoExpandidaHistorico;

    public TreinamentoControladorView(TreinamentoControladorViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        // Fonte dos pontos do gráfico é HistoricoRecente (ObservableCollection da ViewModel,
        // carregada do banco e persistente entre trocas de tela) — NÃO um histórico local do
        // code-behind: uma lista local era recriada vazia toda vez que esta View era
        // reconstruída (troca de tela, reabertura do app), fazendo o gráfico "esquecer" tudo
        // enquanto os números (ligados direto à ViewModel) continuavam certos — esse descompasso
        // era o motivo do gráfico aparecer vazio mesmo com o treinamento rodando normalmente.
        _viewModel.HistoricoRecente.CollectionChanged += OnHistoricoAlterado;
        Unloaded += (_, _) => _viewModel.HistoricoRecente.CollectionChanged -= OnHistoricoAlterado;

        RedesenharGrafico();
    }

    private void OnHistoricoAlterado(object? sender, NotifyCollectionChangedEventArgs e) => RedesenharGrafico();

    /// <summary>Abre a janela modal de cadastro do cenário personalizado, centralizada sobre a
    /// janela atual (Requisito: substituir o card da barra lateral por uma janela modal).</summary>
    private void BotaoCenarioPersonalizado_Click(object sender, RoutedEventArgs e)
    {
        var janela = new CenarioPersonalizadoWindow(_viewModel)
        {
            Owner = Window.GetWindow(this)
        };
        janela.ShowDialog();
    }

    /// <summary>
    /// Alterna o painel de histórico de gerações entre minimizado (só o título + botão visíveis)
    /// e expandido (tabela visível, do tamanho que estava antes de minimizar). A divisória
    /// (<see cref="DivisoriaHistorico"/>) fica desativada enquanto minimizado — não há o que
    /// arrastar com a tabela escondida.
    /// </summary>
    private void BotaoMinimizarMaximizarHistorico_Click(object sender, RoutedEventArgs e)
    {
        if (_historicoMinimizado)
        {
            LinhaHistorico.MinHeight = AlturaMinimaExpandidaHistorico;
            LinhaHistorico.Height = new GridLength(_ultimaAlturaExpandidaHistorico);
            HistoricoDataGrid.Visibility = Visibility.Visible;
            DivisoriaHistorico.IsEnabled = true;
            BotaoMinimizarMaximizarHistorico.Content = "▾ Minimizar";
        }
        else
        {
            _ultimaAlturaExpandidaHistorico = LinhaHistorico.ActualHeight > 0 ? LinhaHistorico.ActualHeight : AlturaPadraoExpandidaHistorico;
            LinhaHistorico.MinHeight = AlturaMinimizadaHistorico;
            LinhaHistorico.Height = new GridLength(AlturaMinimizadaHistorico);
            HistoricoDataGrid.Visibility = Visibility.Collapsed;
            DivisoriaHistorico.IsEnabled = false;
            BotaoMinimizarMaximizarHistorico.Content = "▸ Maximizar";
        }

        _historicoMinimizado = !_historicoMinimizado;
    }

    // ---------------------------------------------------------------- Zoom independente por visualização
    //
    // Mesma convenção já usada no RadarControl (Ctrl+roda = zoom; roda sozinha continua rolando a
    // tela pelo ScrollViewer que envolve a coluna). Cada visualização (rede neural e simulação
    // 2D) tem seu próprio ScaleTransform/estado — ampliar uma nunca afeta a outra.

    private const double ZoomMinimo = 0.5;
    private const double ZoomMaximo = 2.5;
    private const double PassoZoom = 0.1;

    private void RedeNeuralViewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        AplicarZoomComRoda(e, ZoomRedeNeuralTransform, ZoomRedeNeuralLabel);

    private void MundoViewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e) =>
        AplicarZoomComRoda(e, ZoomMundoTransform, ZoomMundoLabel);

    private static void AplicarZoomComRoda(MouseWheelEventArgs e, ScaleTransform transform, TextBlock rotulo)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        double novoZoom = Math.Clamp(transform.ScaleX + (e.Delta > 0 ? PassoZoom : -PassoZoom), ZoomMinimo, ZoomMaximo);
        transform.ScaleX = novoZoom;
        transform.ScaleY = novoZoom;
        rotulo.Text = $"{novoZoom:0%}";
        e.Handled = true;
    }

    private void RedesenharGrafico()
    {
        var historico = _viewModel.HistoricoRecente;
        if (historico.Count < 2)
        {
            LinhaMelhorFitness.Points.Clear();
            LinhaFitnessMedio.Points.Clear();
            return;
        }

        double largura = GraficoCanvas.ActualWidth > 0 ? GraficoCanvas.ActualWidth : 220;
        double altura = GraficoCanvas.ActualHeight > 0 ? GraficoCanvas.ActualHeight : 120;

        double maiorFitness = historico.Max(r => r.MelhorFitness);
        double menorFitness = historico.Min(r => r.FitnessMedio);
        double amplitude = System.Math.Max(1.0, maiorFitness - menorFitness);

        double EixoX(int indice) => (indice / (double)(historico.Count - 1)) * largura;
        double EixoY(double valor) => altura - ((valor - menorFitness) / amplitude * altura);

        LinhaMelhorFitness.Points = new PointCollection(historico
            .Select((r, i) => new Point(EixoX(i), EixoY(r.MelhorFitness))));

        LinhaFitnessMedio.Points = new PointCollection(historico
            .Select((r, i) => new Point(EixoX(i), EixoY(r.FitnessMedio))));
    }
}
