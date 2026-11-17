using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.App.Views;

/// <summary>
/// Desenha uma série de <see cref="PontoSerieDashboard"/> como linha ou barra, dentro do
/// <see cref="GraficoCanvas"/>, com zoom (roda do mouse, centrado no cursor) e pan (arrastar) no
/// eixo X, destaque do melhor ponto (máximo visível) e uma linha de média móvel sobre a série
/// bruta — pedido do usuário para distinguir tendência de oscilação ponto a ponto. Sem
/// ViewModel/MVVM aqui de propósito — é puramente apresentação (converter números em coordenadas
/// de tela), mesmo princípio já usado no gráfico de fitness de <c>TreinamentoControladorView</c>.
/// </summary>
public partial class GraficoCartesianoView : UserControl
{
    public static readonly DependencyProperty PontosProperty = DependencyProperty.Register(
        nameof(Pontos), typeof(IReadOnlyList<PontoSerieDashboard>), typeof(GraficoCartesianoView),
        new PropertyMetadata(null, (d, _) => ((GraficoCartesianoView)d).AoMudarPontos()));

    public static readonly DependencyProperty TipoGraficoProperty = DependencyProperty.Register(
        nameof(TipoGrafico), typeof(TipoGraficoDashboard), typeof(GraficoCartesianoView),
        new PropertyMetadata(TipoGraficoDashboard.Linha, (d, _) => ((GraficoCartesianoView)d).Redesenhar()));

    public IReadOnlyList<PontoSerieDashboard>? Pontos
    {
        get => (IReadOnlyList<PontoSerieDashboard>?)GetValue(PontosProperty);
        set => SetValue(PontosProperty, value);
    }

    public TipoGraficoDashboard TipoGrafico
    {
        get => (TipoGraficoDashboard)GetValue(TipoGraficoProperty);
        set => SetValue(TipoGraficoProperty, value);
    }

    public GraficoCartesianoView()
    {
        InitializeComponent();
    }

    /// <summary>Uma série nova (fonte/campos trocados, ou "Atualizar todos") reseta o zoom — a janela visível de antes pode não fazer mais sentido para dados diferentes.</summary>
    private void AoMudarPontos()
    {
        ResetarZoomSemRedesenhar();
        Redesenhar();
    }

    private void GraficoCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => Redesenhar();

    private const double LarguraEixoY = 40.0;
    private const int LinhasDeGrade = 3;

    // ---------------------------------------------------------------- Zoom / pan (eixo X)

    /// <summary>1.0 = sem zoom (mostra a faixa inteira de dados). Maior = mais ampliado.</summary>
    private double _zoom = 1.0;
    private const double ZoomMinimo = 1.0;
    private const double ZoomMaximo = 60.0;

    /// <summary>Centro (em unidade de dado do eixo X, não pixel) da janela visível. <c>null</c> = ainda não zoomado, centraliza automaticamente.</summary>
    private double? _centroVisivel;

    private bool _arrastando;
    private Point _pontoArrasteAnterior;

    private void BotaoResetarZoom_Click(object sender, RoutedEventArgs e)
    {
        ResetarZoomSemRedesenhar();
        Redesenhar();
    }

    private void ResetarZoomSemRedesenhar()
    {
        _zoom = ZoomMinimo;
        _centroVisivel = null;
        BotaoResetarZoom.Visibility = Visibility.Collapsed;
    }

    private void GraficoCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        IReadOnlyList<PontoSerieDashboard> pontos = Pontos ?? Array.Empty<PontoSerieDashboard>();
        if (pontos.Count < 2) return;

        e.Handled = true;

        double ordinalMin = pontos.Min(p => p.OrdinalEixoX);
        double ordinalMax = pontos.Max(p => p.OrdinalEixoX);
        double faixaTotal = Math.Max(1e-6, ordinalMax - ordinalMin);

        // Valor de dado sob o cursor ANTES do zoom mudar — é esse ponto que deve continuar sob o
        // cursor depois (zoom "centrado no mouse", não no centro do gráfico).
        double larguraPlotAtual = Math.Max(10.0, GraficoCanvas.ActualWidth - LarguraEixoY);
        double fracaoCursor = Math.Clamp((e.GetPosition(GraficoCanvas).X - LarguraEixoY) / larguraPlotAtual, 0.0, 1.0);

        double faixaVisivelAtual = faixaTotal / _zoom;
        double centroAtual = _centroVisivel ?? (ordinalMin + faixaTotal / 2);
        double minVisivelAtual = Math.Clamp(centroAtual - faixaVisivelAtual / 2, ordinalMin, ordinalMax - faixaVisivelAtual);
        double ordinalSobCursor = minVisivelAtual + fracaoCursor * faixaVisivelAtual;

        double fatorPasso = e.Delta > 0 ? 1.2 : 1 / 1.2;
        _zoom = Math.Clamp(_zoom * fatorPasso, ZoomMinimo, ZoomMaximo);

        double novaFaixaVisivel = faixaTotal / _zoom;
        double novoMinVisivel = ordinalSobCursor - fracaoCursor * novaFaixaVisivel;
        _centroVisivel = Math.Clamp(novoMinVisivel + novaFaixaVisivel / 2, ordinalMin + novaFaixaVisivel / 2, ordinalMax - novaFaixaVisivel / 2);

        if (ordinalMax - ordinalMin <= novaFaixaVisivel) _centroVisivel = ordinalMin + faixaTotal / 2; // faixa inteira cabe — sem o que centralizar

        BotaoResetarZoom.Visibility = _zoom > ZoomMinimo ? Visibility.Visible : Visibility.Collapsed;
        Redesenhar();
    }

    private void GraficoCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_zoom <= ZoomMinimo) return; // nada para arrastar sem zoom

        _arrastando = true;
        _pontoArrasteAnterior = e.GetPosition(GraficoCanvas);
        GraficoCanvas.CaptureMouse();
    }

    private void GraficoCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_arrastando) return;

        IReadOnlyList<PontoSerieDashboard> pontos = Pontos ?? Array.Empty<PontoSerieDashboard>();
        if (pontos.Count < 2) return;

        Point atual = e.GetPosition(GraficoCanvas);
        double deltaPixelX = atual.X - _pontoArrasteAnterior.X;
        _pontoArrasteAnterior = atual;

        double ordinalMin = pontos.Min(p => p.OrdinalEixoX);
        double ordinalMax = pontos.Max(p => p.OrdinalEixoX);
        double faixaTotal = Math.Max(1e-6, ordinalMax - ordinalMin);
        double faixaVisivel = faixaTotal / _zoom;

        double larguraPlot = Math.Max(10.0, GraficoCanvas.ActualWidth - LarguraEixoY);
        double unidadesPorPixel = faixaVisivel / larguraPlot;

        double centroAtual = _centroVisivel ?? (ordinalMin + faixaTotal / 2);
        // Arrastar para a DIREITA revela dados à ESQUERDA (mesma convenção de mapas/planilhas).
        double novoCentro = centroAtual - deltaPixelX * unidadesPorPixel;
        _centroVisivel = Math.Clamp(novoCentro, ordinalMin + faixaVisivel / 2, ordinalMax - faixaVisivel / 2);

        Redesenhar();
    }

    private void GraficoCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => PararDeArrastar();
    private void GraficoCanvas_MouseLeave(object sender, MouseEventArgs e) => PararDeArrastar();

    private void PararDeArrastar()
    {
        if (!_arrastando) return;
        _arrastando = false;
        GraficoCanvas.ReleaseMouseCapture();
    }

    // ---------------------------------------------------------------- Desenho

    private void Redesenhar()
    {
        GraficoCanvas.Children.Clear();

        IReadOnlyList<PontoSerieDashboard> todosOsPontos = Pontos ?? Array.Empty<PontoSerieDashboard>();
        if (todosOsPontos.Count == 0)
        {
            EixoXTexto.Text = "Sem dados para esta combinação ainda.";
            return;
        }

        double ordinalMinTotal = todosOsPontos.Min(p => p.OrdinalEixoX);
        double ordinalMaxTotal = todosOsPontos.Max(p => p.OrdinalEixoX);
        double faixaTotal = Math.Max(1e-6, ordinalMaxTotal - ordinalMinTotal);
        double faixaVisivel = faixaTotal / _zoom;
        double centro = _centroVisivel ?? (ordinalMinTotal + faixaTotal / 2);
        double visivelMin = Math.Clamp(centro - faixaVisivel / 2, ordinalMinTotal, ordinalMaxTotal - faixaVisivel);
        double visivelMax = visivelMin + faixaVisivel;

        // Dentro da janela visível (zoom aplicado) — se não há zoom, é a série inteira.
        List<PontoSerieDashboard> pontos = _zoom <= ZoomMinimo
            ? todosOsPontos.ToList()
            : todosOsPontos.Where(p => p.OrdinalEixoX >= visivelMin && p.OrdinalEixoX <= visivelMax).ToList();

        if (pontos.Count == 0)
        {
            EixoXTexto.Text = "Nenhum ponto nesta faixa ampliada.";
            return;
        }

        double largura = GraficoCanvas.ActualWidth > 0 ? GraficoCanvas.ActualWidth : 260;
        double altura = GraficoCanvas.ActualHeight > 0 ? GraficoCanvas.ActualHeight : 110;
        double larguraPlot = Math.Max(10.0, largura - LarguraEixoY);

        double maiorY = pontos.Max(p => p.ValorMetrica);
        double menorY = Math.Min(0, pontos.Min(p => p.ValorMetrica));
        double amplitudeY = Math.Max(1e-6, maiorY - menorY);

        double faixaXDesenho = Math.Max(1e-6, pontos[^1].OrdinalEixoX - pontos[0].OrdinalEixoX);
        double EixoX(PontoSerieDashboard p) => pontos.Count == 1
            ? LarguraEixoY + larguraPlot / 2
            : LarguraEixoY + ((p.OrdinalEixoX - pontos[0].OrdinalEixoX) / faixaXDesenho) * larguraPlot;
        double EixoY(double valor) => altura - ((valor - menorY) / amplitudeY * altura);

        DesenharGradeComRotulos(menorY, maiorY, altura, largura);

        Brush corAccent = (Brush)FindResource("Accent.Fill");

        if (TipoGrafico == TipoGraficoDashboard.Barra)
        {
            DesenharBarras(pontos, EixoX, EixoY, menorY, larguraPlot, corAccent);
        }
        else
        {
            DesenharLinha(pontos, EixoX, EixoY, corAccent);
            DesenharMediaMovel(pontos, EixoX, EixoY);
        }

        DestacarMelhorPonto(pontos, EixoX, EixoY);

        string sufixoZoom = _zoom > ZoomMinimo ? $"  ·  zoom {_zoom:0.#}×" : string.Empty;
        EixoXTexto.Text = $"{pontos[0].RotuloEixoX} → {pontos[^1].RotuloEixoX}  ({pontos.Count} pontos){sufixoZoom}";
    }

    private void DesenharBarras(List<PontoSerieDashboard> pontos, Func<PontoSerieDashboard, double> eixoX, Func<double, double> eixoY, double menorY, double larguraPlot, Brush corAccent)
    {
        double larguraBarra = Math.Max(2.0, larguraPlot / pontos.Count * 0.7);
        foreach (PontoSerieDashboard ponto in pontos)
        {
            double x = eixoX(ponto) - larguraBarra / 2;
            double yTopo = eixoY(ponto.ValorMetrica);
            double yBase = eixoY(Math.Max(0, menorY));

            var barra = new Rectangle
            {
                Width = larguraBarra,
                Height = Math.Max(0, yBase - yTopo),
                Fill = corAccent,
                Opacity = 0.85,
                ToolTip = $"{ponto.RotuloEixoX}: {ponto.ValorMetrica:0.###}",
            };
            Canvas.SetLeft(barra, x);
            Canvas.SetTop(barra, Math.Min(yTopo, yBase));
            GraficoCanvas.Children.Add(barra);
        }
    }

    private void DesenharLinha(List<PontoSerieDashboard> pontos, Func<PontoSerieDashboard, double> eixoX, Func<double, double> eixoY, Brush corAccent)
    {
        var linha = new Polyline
        {
            Stroke = corAccent,
            StrokeThickness = 2,
            Points = new PointCollection(pontos.Select(p => new Point(eixoX(p), eixoY(p.ValorMetrica)))),
        };
        GraficoCanvas.Children.Add(linha);

        foreach (PontoSerieDashboard ponto in pontos)
        {
            var marcador = new Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = corAccent,
                ToolTip = $"{ponto.RotuloEixoX}: {ponto.ValorMetrica:0.###}",
            };
            Canvas.SetLeft(marcador, eixoX(ponto) - 2.5);
            Canvas.SetTop(marcador, eixoY(ponto.ValorMetrica) - 2.5);
            GraficoCanvas.Children.Add(marcador);
        }
    }

    /// <summary>
    /// Linha suave por cima da série bruta — mesmo motivo do gráfico de fitness da tela de
    /// Treinamento: separar TENDÊNCIA de oscilação ponto a ponto (pedido do usuário: "ver as
    /// variações"). Janela adaptativa (não um número fixo): séries curtas usam janela pequena,
    /// senão a média móvel não apareceria de jeito nenhum com poucos pontos.
    /// </summary>
    private void DesenharMediaMovel(List<PontoSerieDashboard> pontos, Func<PontoSerieDashboard, double> eixoX, Func<double, double> eixoY)
    {
        int janela = Math.Clamp(pontos.Count / 15, 2, 20);
        if (pontos.Count < janela + 2) return; // série curta demais — a linha bruta já é a melhor leitura

        var suavizados = new PointCollection();
        for (int i = janela - 1; i < pontos.Count; i++)
        {
            double media = pontos.Skip(i - janela + 1).Take(janela).Average(p => p.ValorMetrica);
            suavizados.Add(new Point(eixoX(pontos[i]), eixoY(media)));
        }

        GraficoCanvas.Children.Add(new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0xF4, 0x72, 0xB6)),
            StrokeThickness = 1.5,
            Points = suavizados,
            ToolTip = $"Média móvel (janela de {janela} pontos)",
        });
    }

    /// <summary>Marcador maior + rótulo no ponto de maior valor DENTRO da janela visível atual — pedido do usuário ("pontos ótimos"). Some ao dar zoom numa região sem o pico.</summary>
    private void DestacarMelhorPonto(List<PontoSerieDashboard> pontos, Func<PontoSerieDashboard, double> eixoX, Func<double, double> eixoY)
    {
        PontoSerieDashboard melhor = pontos.MaxBy(p => p.ValorMetrica)!;
        double x = eixoX(melhor);
        double y = eixoY(melhor.ValorMetrica);
        var corMelhor = new SolidColorBrush(Color.FromRgb(0x4A, 0xDE, 0x80)); // mesmo verde usado em "melhor fitness" nas outras telas

        var marcador = new Ellipse
        {
            Width = 11,
            Height = 11,
            Fill = corMelhor,
            Stroke = Brushes.White,
            StrokeThickness = 1.5,
            ToolTip = $"Melhor da faixa visível — {melhor.RotuloEixoX}: {melhor.ValorMetrica:0.###}",
        };
        Canvas.SetLeft(marcador, x - 5.5);
        Canvas.SetTop(marcador, y - 5.5);
        Panel.SetZIndex(marcador, 10);
        GraficoCanvas.Children.Add(marcador);

        var rotulo = new TextBlock
        {
            Text = melhor.ValorMetrica.ToString("0.##"),
            FontSize = 10,
            FontWeight = FontWeights.Bold,
            Foreground = corMelhor,
        };
        Canvas.SetLeft(rotulo, Math.Max(LarguraEixoY, x - 12));
        Canvas.SetTop(rotulo, y < 16 ? y + 8 : y - 20);
        GraficoCanvas.Children.Add(rotulo);
    }

    private void DesenharGradeComRotulos(double menor, double maior, double altura, double largura)
    {
        Brush corGrade = (Brush)FindResource("Border.Divider");
        Brush corTexto = (Brush)FindResource("Text.Muted");

        for (int i = 0; i <= LinhasDeGrade; i++)
        {
            double fracao = i / (double)LinhasDeGrade;
            double y = altura * fracao;
            double valor = maior - ((maior - menor) * fracao);

            GraficoCanvas.Children.Add(new Line
            {
                X1 = LarguraEixoY,
                Y1 = y,
                X2 = largura,
                Y2 = y,
                Stroke = corGrade,
                StrokeThickness = 1,
                StrokeDashArray = [2, 2],
            });

            var rotulo = new TextBlock
            {
                Text = valor.ToString("0.#"),
                FontSize = 9,
                Foreground = corTexto,
            };
            Canvas.SetLeft(rotulo, 0);
            Canvas.SetTop(rotulo, Math.Max(0, y - 6));
            GraficoCanvas.Children.Add(rotulo);
        }
    }
}
