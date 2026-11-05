using System;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using RadarTorres.App.Models;
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

    /// <summary>Exporta os marcos registrados (os mesmos da tabela "MARCOS") para um arquivo CSV — dados brutos, sem formatação de apresentação.</summary>
    private void BotaoExportarMarcosCsv_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new SaveFileDialog
        {
            Title = "Exportar marcos de treinamento",
            FileName = $"marcos-treinamento-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            DefaultExt = ".csv",
            Filter = "CSV (*.csv)|*.csv",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialogo.ShowDialog(Window.GetWindow(this)) != true) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Geracao;FitnessMedioDoBloco;MelhorFitnessDoBloco;DataHora");
            foreach (MarcoFitnessTreinamento marco in _viewModel.Marcos)
            {
                sb.AppendLine(string.Join(';',
                    marco.GeracaoMarco.ToString(CultureInfo.InvariantCulture),
                    marco.FitnessMedioDoBloco.ToString("0.0", CultureInfo.InvariantCulture),
                    marco.MelhorFitnessDoBloco.ToString("0.0", CultureInfo.InvariantCulture),
                    marco.DataHora.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)));
            }

            File.WriteAllText(dialogo.FileName, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            MessageBox.Show($"Exportado: {dialogo.FileName}", "Exportar marcos", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível exportar: {ex.Message}", "Exportar marcos", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Exporta o gráfico "Evolução do fitness por bloco de 500 gerações" como PNG — captura o mesmo Canvas já desenhado em tela (<see cref="Redesenhar"/>), não reconstrói nada.</summary>
    private void BotaoExportarGraficoPng_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new SaveFileDialog
        {
            Title = "Exportar gráfico de evolução do fitness",
            FileName = $"grafico-fitness-{DateTime.Now:yyyyMMdd-HHmmss}.png",
            DefaultExt = ".png",
            Filter = "PNG (*.png)|*.png",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialogo.ShowDialog(Window.GetWindow(this)) != true) return;

        try
        {
            double largura = GraficoCanvas.ActualWidth > 0 ? GraficoCanvas.ActualWidth : 600;
            double altura = GraficoCanvas.ActualHeight > 0 ? GraficoCanvas.ActualHeight : 180;

            var bitmap = new RenderTargetBitmap((int)largura, (int)altura, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(GraficoCanvas);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using FileStream arquivo = File.Create(dialogo.FileName);
            encoder.Save(arquivo);

            MessageBox.Show($"Exportado: {dialogo.FileName}", "Exportar gráfico", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível exportar: {ex.Message}", "Exportar gráfico", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
