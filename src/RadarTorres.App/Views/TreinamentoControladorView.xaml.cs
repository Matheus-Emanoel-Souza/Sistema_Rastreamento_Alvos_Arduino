using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
    private readonly TreinamentoControladorViewModel _viewModel;
    private readonly List<ResultadoGeracao> _historico = new();

    public TreinamentoControladorView(TreinamentoControladorViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.GeracaoConcluida += OnGeracaoConcluida;
        Unloaded += (_, _) => _viewModel.GeracaoConcluida -= OnGeracaoConcluida;
    }

    private void OnGeracaoConcluida(object? sender, ResultadoGeracao resultado)
    {
        _historico.Add(resultado);
        RedesenharGrafico();
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
