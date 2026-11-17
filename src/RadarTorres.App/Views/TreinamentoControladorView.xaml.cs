using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Shapes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
    private readonly Func<EstatisticasAnaliseWindow> _criarJanelaEstatisticas;
    private EstatisticasAnaliseWindow? _janelaEstatisticasAberta;

    /// <summary>Lê o ângulo do farol decorativo (ver XAML) e mostra em texto, junto da contagem
    /// de alvos — só leitura visual da mesma animação já existente, nenhum estado novo de
    /// domínio. Intervalo igual ao da prévia do treinamento (50 ms) para acompanhar o movimento
    /// sem sobrecarregar a UI com atualizações a cada frame da Storyboard.</summary>
    private readonly DispatcherTimer _timerAnguloSensor = new() { Interval = TimeSpan.FromMilliseconds(50) };

    public TreinamentoControladorView(TreinamentoControladorViewModel viewModel, Func<EstatisticasAnaliseWindow> criarJanelaEstatisticas)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _criarJanelaEstatisticas = criarJanelaEstatisticas;
        DataContext = _viewModel;

        // Fonte dos pontos do gráfico é HistoricoDaSessao (ObservableCollection da ViewModel) —
        // NÃO um histórico local do code-behind: uma lista local era recriada vazia toda vez que
        // esta View era reconstruída (troca de tela, reabertura do app), fazendo o gráfico
        // "esquecer" tudo enquanto os números (ligados direto à ViewModel) continuavam certos.
        //
        // Também não é HistoricoRecente, que vem do banco e traz as últimas 300 gerações de
        // QUALQUER execução: com ela, a curva começava já cheia de dados de treinos anteriores e
        // dava um degrau artificial a cada "Iniciar". A série por sessão mostra a evolução DESTE
        // treinamento; a tabela abaixo continua mostrando o histórico persistido.
        _viewModel.HistoricoDaSessao.CollectionChanged += OnHistoricoAlterado;
        SizeChanged += (_, _) => RedesenharGrafico();
        Unloaded += (_, _) =>
        {
            _viewModel.HistoricoDaSessao.CollectionChanged -= OnHistoricoAlterado;
            _timerAnguloSensor.Stop();
        };

        _timerAnguloSensor.Tick += (_, _) => AnguloSensorTexto.Text = $"{Math.Abs(FarolRotacao.Angle):0.0}°";
        _timerAnguloSensor.Start();

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
    /// Abre o painel "Estatísticas e Análises" (3 abas: Visão geral, Experimentos, Marcos) —
    /// não-modal e sem pausar/reiniciar o treinamento em andamento (ver
    /// EstatisticasAnaliseWindow). Reaproveita a janela já aberta em vez de abrir uma segunda,
    /// só trazendo-a para frente.
    /// </summary>
    private void BotaoEstatisticasEAnalises_Click(object sender, RoutedEventArgs e)
    {
        if (_janelaEstatisticasAberta is not null)
        {
            _janelaEstatisticasAberta.Activate();
            return;
        }

        _janelaEstatisticasAberta = _criarJanelaEstatisticas();
        _janelaEstatisticasAberta.Owner = Window.GetWindow(this);
        _janelaEstatisticasAberta.Closed += (_, _) => _janelaEstatisticasAberta = null;
        _janelaEstatisticasAberta.Show();
    }

    /// <summary>Abre a janela modal "Configurar experimento" (população, teto de complexidade,
    /// mutação estrutural, meta de gerações, alvos por quadrante, eixo Z) — não modal, apenas
    /// não bloqueia o treinamento em segundo plano: os campos continuam ligados à MESMA
    /// ViewModel, então editá-los lá já reflete aqui, sem precisar fechar a janela.</summary>
    private void BotaoConfigurarExperimento_Click(object sender, RoutedEventArgs e)
    {
        var janela = new ConfigurarExperimentoWindow(_viewModel)
        {
            Owner = Window.GetWindow(this)
        };
        janela.Show();
    }

    /// <summary>
    /// Expande a tabela de histórico de gerações para uma altura maior dentro do mesmo card,
    /// alternando com a altura compacta padrão — não há mais linha de Grid dedicada e
    /// redimensionável por arraste (layout atual divide a área inferior em proporções fixas
    /// entre as 3 linhas, para caber sem rolagem dupla nas resoluções-alvo); "expandir" aqui
    /// aumenta a MaxHeight da tabela, que ganha rolagem interna própria quando o conteúdo passa
    /// disso — preserva a função pedida (ver mais registros) sem reintroduzir o layout antigo.
    /// </summary>
    private bool _historicoExpandido;

    private void BotaoMinimizarMaximizarHistorico_Click(object sender, RoutedEventArgs e)
    {
        _historicoExpandido = !_historicoExpandido;
        HistoricoDataGrid.MaxHeight = _historicoExpandido ? 600 : double.PositiveInfinity;
        BotaoMinimizarMaximizarHistorico.Content = _historicoExpandido ? "⤡" : "⤢";
        BotaoMinimizarMaximizarHistorico.ToolTip = _historicoExpandido
            ? "Recolher a tabela de histórico de gerações"
            : "Expandir a tabela de histórico de gerações";
    }

    // ---------------------------------------------------------------- Accordion do painel de controle
    //
    // Mesmo padrão do botão de histórico acima: alterna Visibility do StackPanel de conteúdo e o
    // glifo (▾ aberta / ▸ fechada) no próprio texto do cabeçalho — sem framework de accordion,
    // sem estado novo na ViewModel (é puramente apresentação de qual seção está visível agora).

    private void AlternarSecao(Button cabecalho, FrameworkElement conteudo, string titulo)
    {
        bool abrindo = conteudo.Visibility != Visibility.Visible;
        conteudo.Visibility = abrindo ? Visibility.Visible : Visibility.Collapsed;

        if (cabecalho.Content is DockPanel painel && painel.Children.Count > 0 && painel.Children[0] is TextBlock rotulo)
        {
            rotulo.Text = (abrindo ? "▾ " : "▸ ") + titulo;
        }
    }

    private void BotaoSecaoExecucao_Click(object sender, RoutedEventArgs e) => AlternarSecao((Button)sender, SecaoExecucao, "Execução");
    private void BotaoSecaoCenario_Click(object sender, RoutedEventArgs e) => AlternarSecao((Button)sender, SecaoCenario, "Cenário");
    private void BotaoSecaoExperimento_Click(object sender, RoutedEventArgs e) => AlternarSecao((Button)sender, SecaoExperimento, "Experimento e dados");

    // ---------------------------------------------------------------- Janelas não-modais (ex-seções 4 e 5)
    //
    // "Desempenho do treinamento" e "Arquitetura e aprendizado" eram seções recolhíveis inline
    // (accordion acima) — viraram janelas próprias para liberar espaço na barra lateral, mesmo
    // padrão de BotaoEstatisticasEAnalises_Click: reaproveita a janela já aberta em vez de abrir
    // uma segunda, só trazendo-a para frente.

    private DesempenhoTreinamentoWindow? _janelaDesempenhoAberta;

    private void BotaoDesempenhoTreinamento_Click(object sender, RoutedEventArgs e)
    {
        if (_janelaDesempenhoAberta is not null)
        {
            _janelaDesempenhoAberta.Activate();
            return;
        }

        _janelaDesempenhoAberta = new DesempenhoTreinamentoWindow(_viewModel) { Owner = Window.GetWindow(this) };
        _janelaDesempenhoAberta.Closed += (_, _) => _janelaDesempenhoAberta = null;
        _janelaDesempenhoAberta.Show();
    }

    private ArquiteturaAprendizadoWindow? _janelaArquiteturaAberta;

    private void BotaoArquiteturaAprendizado_Click(object sender, RoutedEventArgs e)
    {
        if (_janelaArquiteturaAberta is not null)
        {
            _janelaArquiteturaAberta.Activate();
            return;
        }

        _janelaArquiteturaAberta = new ArquiteturaAprendizadoWindow(_viewModel) { Owner = Window.GetWindow(this) };
        _janelaArquiteturaAberta.Closed += (_, _) => _janelaArquiteturaAberta = null;
        _janelaArquiteturaAberta.Show();
    }

    /// <summary>Largura reservada à esquerda para os rótulos de valor do eixo Y.</summary>
    private const double LarguraEixoY = 34.0;

    /// <summary>Linhas de grade horizontais desenhadas (além da base) — poucas de propósito: o card é estreito e o excesso virava ruído.</summary>
    private const int LinhasDeGrade = 3;

    /// <summary>
    /// Desenha o gráfico de evolução: grade com rótulos de valor, as três séries (melhor fitness,
    /// média da população e média móvel do melhor) e a faixa de gerações no eixo X.
    /// </summary>
    /// <remarks>
    /// A média móvel é calculada aqui, sobre a mesma série que o gráfico já tem em mãos, com a
    /// mesma janela que a ViewModel usa nos indicadores (<see cref="TreinamentoControladorViewModel.JanelaIndicadoresAprendizado"/>)
    /// — é a terceira linha do gráfico, e serve para separar tendência de ruído de sorteio.
    /// Continua sendo trabalho de apresentação (converter números em coordenadas de tela), que é
    /// o motivo deste gráfico viver no code-behind.
    /// </remarks>
    private void RedesenharGrafico()
    {
        GraficoCanvas.Children.Clear();

        var historico = _viewModel.HistoricoDaSessao;
        if (historico.Count < 2)
        {
            GraficoEixoX.Text = historico.Count == 0
                ? "Sem gerações nesta sessão — inicie o treinamento."
                : "Aguardando a segunda geração para traçar a curva…";
            return;
        }

        double largura = GraficoCanvas.ActualWidth > 0 ? GraficoCanvas.ActualWidth : 260;
        double altura = GraficoCanvas.ActualHeight > 0 ? GraficoCanvas.ActualHeight : 110;
        double larguraPlot = System.Math.Max(10.0, largura - LarguraEixoY);

        double maior = historico.Max(r => r.MelhorFitness);
        double menor = historico.Min(r => System.Math.Min(r.FitnessMedio, r.MelhorFitness));
        double amplitude = System.Math.Max(1.0, maior - menor);

        double EixoX(int indice) => LarguraEixoY + (indice / (double)(historico.Count - 1)) * larguraPlot;
        double EixoY(double valor) => altura - ((valor - menor) / amplitude * altura);

        DesenharGradeComRotulos(menor, maior, altura, largura);

        var corMelhor = (Brush)FindResource("Accent.Fill");
        DesenharSerie(historico.Select((r, i) => new Point(EixoX(i), EixoY(r.MelhorFitness))), corMelhor, 2);
        DesenharSerie(historico.Select((r, i) => new Point(EixoX(i), EixoY(r.FitnessMedio))), new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)), 1);

        PointCollection mediaMovel = CalcularMediaMovel(historico, EixoX, EixoY);
        if (mediaMovel.Count >= 2)
        {
            DesenharSerie(mediaMovel, new SolidColorBrush(Color.FromRgb(0xF4, 0x72, 0xB6)), 1.5);
        }

        GraficoEixoX.Text = $"gerações {historico[0].Geracao:N0} → {historico[^1].Geracao:N0}  ({historico.Count} pontos)";
    }

    /// <summary>Grade horizontal com o valor de fitness de cada linha — sem isso o gráfico mostrava forma, mas nenhuma escala.</summary>
    private void DesenharGradeComRotulos(double menor, double maior, double altura, double largura)
    {
        var corGrade = (Brush)FindResource("Border.Divider");
        var corTexto = (Brush)FindResource("Text.Muted");

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
                StrokeDashArray = new DoubleCollection { 3, 3 },
                Opacity = 0.5
            });

            var rotulo = new TextBlock
            {
                Text = valor.ToString("0"),
                FontSize = 9,
                Foreground = corTexto,
                FontFamily = (System.Windows.Media.FontFamily)FindResource("Font.Mono")
            };

            // Rótulo acima da linha na borda de cima e abaixo na de baixo, para nenhum dos dois
            // ser cortado pelo ClipToBounds do Canvas.
            Canvas.SetLeft(rotulo, 0);
            Canvas.SetTop(rotulo, i == LinhasDeGrade ? y - 11 : y - 1);
            GraficoCanvas.Children.Add(rotulo);
        }
    }

    private void DesenharSerie(IEnumerable<Point> pontos, Brush cor, double espessura) =>
        GraficoCanvas.Children.Add(new Polyline
        {
            Points = pontos is PointCollection colecao ? colecao : new PointCollection(pontos),
            Stroke = cor,
            StrokeThickness = espessura,
            StrokeLineJoin = PenLineJoin.Round
        });

    /// <summary>
    /// Média móvel do melhor fitness. Só produz pontos a partir da janela completa: calcular a
    /// média de 3 gerações e chamá-la de "média de 50" daria um começo de curva que não
    /// corresponde ao que o rótulo promete.
    /// </summary>
    private static PointCollection CalcularMediaMovel(
        IList<RadarTorres.App.Models.HistoricoGeracaoTreinamento> historico,
        Func<int, double> eixoX,
        Func<double, double> eixoY)
    {
        int janela = TreinamentoControladorViewModel.JanelaIndicadoresAprendizado;
        var pontos = new PointCollection();
        if (historico.Count < janela) return pontos;

        double soma = 0;
        for (int i = 0; i < janela; i++) soma += historico[i].MelhorFitness;

        pontos.Add(new Point(eixoX(janela - 1), eixoY(soma / janela)));

        for (int i = janela; i < historico.Count; i++)
        {
            soma += historico[i].MelhorFitness - historico[i - janela].MelhorFitness;
            pontos.Add(new Point(eixoX(i), eixoY(soma / janela)));
        }

        return pontos;
    }
}
