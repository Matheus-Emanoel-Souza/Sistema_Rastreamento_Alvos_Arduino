using System;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da tela "Estatísticas de Treinamento". Mesmo princípio do
/// <see cref="TreinamentoControladorView"/>: desenhar um <see cref="Polyline"/> a partir de
/// pontos é conversão pura para coordenadas de tela, então fica aqui em vez de na ViewModel.
/// </summary>
public partial class EstatisticasTreinamentoView : UserControl
{
    private readonly EstatisticasTreinamentoViewModel _viewModel;

    public EstatisticasTreinamentoView(EstatisticasTreinamentoViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.Marcos.CollectionChanged += OnMarcosAlterados;
        _viewModel.HistoricoRecente.CollectionChanged += OnHistoricoRecenteAlterado;
        SizeChanged += (_, _) => { Redesenhar(); RedesenharGeracaoAGeracao(); };
        Unloaded += (_, _) =>
        {
            _viewModel.Marcos.CollectionChanged -= OnMarcosAlterados;
            _viewModel.HistoricoRecente.CollectionChanged -= OnHistoricoRecenteAlterado;
        };
        Loaded += (_, _) => { Redesenhar(); RedesenharGeracaoAGeracao(); };
    }

    private void OnMarcosAlterados(object? sender, NotifyCollectionChangedEventArgs e) => Redesenhar();

    private void OnHistoricoRecenteAlterado(object? sender, NotifyCollectionChangedEventArgs e) => RedesenharGeracaoAGeracao();

    private void RedesenharGeracaoAGeracao()
    {
        var historico = _viewModel.HistoricoRecente.ToList();
        if (historico.Count < 2)
        {
            LinhaMelhorFitnessGeracao.Points.Clear();
            LinhaFitnessMedioGeracao.Points.Clear();
            return;
        }

        double largura = GraficoGeracaoCanvas.ActualWidth > 0 ? GraficoGeracaoCanvas.ActualWidth : 600;
        double altura = GraficoGeracaoCanvas.ActualHeight > 0 ? GraficoGeracaoCanvas.ActualHeight : 160;

        double maior = historico.Max(r => r.MelhorFitness);
        double menor = historico.Min(r => r.FitnessMedio);
        double amplitude = Math.Max(1.0, maior - menor);

        double EixoX(int indice) => indice / (double)(historico.Count - 1) * largura;
        double EixoY(double valor) => altura - (valor - menor) / amplitude * altura;

        LinhaMelhorFitnessGeracao.Points = new PointCollection(
            historico.Select((r, i) => new Point(EixoX(i), EixoY(r.MelhorFitness))));

        LinhaFitnessMedioGeracao.Points = new PointCollection(
            historico.Select((r, i) => new Point(EixoX(i), EixoY(r.FitnessMedio))));
    }

    private void Redesenhar()
    {
        var marcos = _viewModel.Marcos.ToList();
        if (marcos.Count < 2)
        {
            LinhaFitnessMedio.Points.Clear();
            LinhaMelhorFitness.Points.Clear();
            return;
        }

        double largura = GraficoCanvas.ActualWidth > 0 ? GraficoCanvas.ActualWidth : 600;
        double altura = GraficoCanvas.ActualHeight > 0 ? GraficoCanvas.ActualHeight : 180;

        double maior = marcos.Max(m => m.MelhorFitnessDoBloco);
        double menor = marcos.Min(m => m.FitnessMedioDoBloco);
        double amplitude = Math.Max(1.0, maior - menor);

        double EixoX(int indice) => indice / (double)(marcos.Count - 1) * largura;
        double EixoY(double valor) => altura - (valor - menor) / amplitude * altura;

        LinhaFitnessMedio.Points = new PointCollection(
            marcos.Select((m, i) => new Point(EixoX(i), EixoY(m.FitnessMedioDoBloco))));

        LinhaMelhorFitness.Points = new PointCollection(
            marcos.Select((m, i) => new Point(EixoX(i), EixoY(m.MelhorFitnessDoBloco))));
    }
}
