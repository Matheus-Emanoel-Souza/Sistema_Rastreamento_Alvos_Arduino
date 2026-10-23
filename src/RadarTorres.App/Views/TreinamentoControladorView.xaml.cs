using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using RadarTorres.App.NeuralEvolution;
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
    private readonly List<ResultadoGeracao> _historico = new();

    private bool _historicoMinimizado;
    private double _ultimaAlturaExpandidaHistorico = AlturaPadraoExpandidaHistorico;

    public TreinamentoControladorView(TreinamentoControladorViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.GeracaoConcluida += OnGeracaoConcluida;
        _viewModel.Resetado += OnResetado;
        Unloaded += (_, _) =>
        {
            _viewModel.GeracaoConcluida -= OnGeracaoConcluida;
            _viewModel.Resetado -= OnResetado;
        };
    }

    private void OnGeracaoConcluida(object? sender, ResultadoGeracao resultado)
    {
        _historico.Add(resultado);
        RedesenharGrafico();
    }

    /// <summary>Reinício pedido pelo botão "Resetar treinamento" — o gráfico de evolução acumula pontos num histórico próprio (fora da ViewModel), então precisa ser zerado aqui também.</summary>
    private void OnResetado(object? sender, EventArgs e)
    {
        _historico.Clear();
        LinhaMelhorFitness.Points.Clear();
        LinhaFitnessMedio.Points.Clear();
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
        if (_historico.Count < 2) return;

        double largura = GraficoCanvas.ActualWidth > 0 ? GraficoCanvas.ActualWidth : 220;
        double altura = GraficoCanvas.ActualHeight > 0 ? GraficoCanvas.ActualHeight : 120;

        double maiorFitness = _historico.Max(r => r.MelhorFitness);
        double menorFitness = _historico.Min(r => r.FitnessMedio);
        double amplitude = System.Math.Max(1.0, maiorFitness - menorFitness);

        double EixoX(int indice) => (indice / (double)(_historico.Count - 1)) * largura;
        double EixoY(double valor) => altura - ((valor - menorFitness) / amplitude * altura);

        LinhaMelhorFitness.Points = new PointCollection(_historico
            .Select((r, i) => new Point(EixoX(i), EixoY(r.MelhorFitness))));

        LinhaFitnessMedio.Points = new PointCollection(_historico
            .Select((r, i) => new Point(EixoX(i), EixoY(r.FitnessMedio))));
    }
}
