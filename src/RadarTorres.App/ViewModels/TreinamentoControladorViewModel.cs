using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using RadarTorres.App.Data;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.NeuralEvolution;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da tela "Treinamento do Controlador": comanda o <see cref="AlgoritmoGenetico"/>
/// em segundo plano (gerações o mais rápido possível, sem travar a UI) enquanto anima no Canvas
/// 2D, em tempo real, o melhor genoma encontrado até agora — são dois laços independentes sobre
/// o mesmo <see cref="AmbienteTreinamento"/>: um rápido (fitness) e um visual (prévia).
/// </summary>
public sealed class TreinamentoControladorViewModel : ViewModelBase, IDisposable
{
    private const double JanelaFlashDisparoSegundos = 0.3;

    /// <summary>Abertura angular do campo de visão de cada torre/canhão — fixa, voltada para fora da base (ver DesenharTorresFixas).</summary>
    public const double AnguloCampoDeVisaoGraus = 90.0;

    private readonly ILoggingService _logger;
    private readonly IHistoricoTreinamentoRepository _historicoRepository;
    private readonly DispatcherTimer _timerPreview;
    private readonly Random _randomGenomaInicial = new();

    private AmbienteTreinamento _ambiente;
    private AlgoritmoGenetico? _algoritmoGenetico;
    private CancellationTokenSource? _cts;
    private Task? _tarefaTreinamento;

    private double[] _melhorGenomaAtual = [];
    private EstadoEpisodio? _episodioPreview;

    /// <summary>Quando falso, a prévia roda exatamente um episódio e para — ver <see cref="RodarEpisodioUnico"/>.</summary>
    private bool _previewContinuo = true;

    private const double TamanhoCanvas = 480.0;

    public ObservableCollection<PontoVisualTreinamento> TorresVisuais { get; } = new();
    public ObservableCollection<PontoVisualTreinamento> AlvosVisuais { get; } = new();
    public ObservableCollection<LinhaVisualTreinamento> MirasVisuais { get; } = new();
    public ObservableCollection<SetorVisualTreinamento> CamposDeVisaoVisuais { get; } = new();
    public ObservableCollection<string> RegrasAprendidas { get; } = new();
    public ObservableCollection<HistoricoGeracaoTreinamento> HistoricoRecente { get; } = new();

    public RelayCommand IniciarCommand { get; }
    public RelayCommand PararCommand { get; }
    public RelayCommand RodarEpisodioUnicoCommand { get; }
    public RelayCommand ExportarMelhorGenomaCommand { get; }

    public TreinamentoControladorViewModel(ILoggingService logger, IHistoricoTreinamentoRepository historicoRepository)
    {
        _logger = logger;
        _historicoRepository = historicoRepository;
        _ambiente = new AmbienteTreinamento(ParametrosAtuais);
        DesenharTorresFixas();

        IniciarCommand = new RelayCommand(Iniciar, () => !EmExecucao);
        PararCommand = new RelayCommand(Parar, () => EmExecucao);
        RodarEpisodioUnicoCommand = new RelayCommand(RodarEpisodioUnico, () => !EmExecucao);
        ExportarMelhorGenomaCommand = new RelayCommand(ExportarMelhorGenoma, () => _melhorGenomaAtual.Length > 0);

        _timerPreview = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timerPreview.Tick += (_, _) => AvancarPreview();

        CarregarHistorico();
    }

    private void CarregarHistorico()
    {
        try
        {
            HistoricoRecente.Clear();
            foreach (HistoricoGeracaoTreinamento registro in _historicoRepository.ObterUltimos())
            {
                HistoricoRecente.Add(registro);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível carregar o histórico de treinamento: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- Parâmetros (editáveis antes de iniciar)

    private int _tamanhoPopulacao = 60;
    public int TamanhoPopulacao
    {
        get => _tamanhoPopulacao;
        set => SetProperty(ref _tamanhoPopulacao, value);
    }

    private double _taxaMutacao = 0.08;
    public double TaxaMutacao
    {
        get => _taxaMutacao;
        set => SetProperty(ref _taxaMutacao, value);
    }

    private int _maxAlvosSimultaneos = 5;
    public int MaxAlvosSimultaneos
    {
        get => _maxAlvosSimultaneos;
        set => SetProperty(ref _maxAlvosSimultaneos, value);
    }

    private ParametrosTreinamento ParametrosAtuais => new()
    {
        TamanhoPopulacao = Math.Max(4, _tamanhoPopulacao),
        TaxaMutacao = Math.Clamp(_taxaMutacao, 0.0, 1.0),
        MaxAlvosSimultaneos = Math.Max(1, _maxAlvosSimultaneos)
    };

    // ---------------------------------------------------------------- Status (mensagem transitória)

    private string? _statusMensagem;
    public string? StatusMensagem { get => _statusMensagem; private set => SetProperty(ref _statusMensagem, value); }

    private bool _statusSucesso;
    public bool StatusSucesso { get => _statusSucesso; private set => SetProperty(ref _statusSucesso, value); }

    private void MostrarStatus(string mensagem, bool sucesso)
    {
        StatusSucesso = sucesso;
        StatusMensagem = mensagem;
    }

    // ---------------------------------------------------------------- Estado / estatísticas

    private bool _emExecucao;
    public bool EmExecucao
    {
        get => _emExecucao;
        private set
        {
            if (SetProperty(ref _emExecucao, value))
            {
                RelayCommand.RaiseCanExecuteChangedForAll();
            }
        }
    }

    private int _geracaoAtual;
    public int GeracaoAtual { get => _geracaoAtual; private set => SetProperty(ref _geracaoAtual, value); }

    private double _melhorFitness;
    public double MelhorFitness { get => _melhorFitness; private set => SetProperty(ref _melhorFitness, value); }

    private double _fitnessMedio;
    public double FitnessMedio { get => _fitnessMedio; private set => SetProperty(ref _fitnessMedio, value); }

    /// <summary>Disparado a cada geração concluída — a View usa para atualizar o gráfico de evolução (ver code-behind).</summary>
    public event EventHandler<ResultadoGeracao>? GeracaoConcluida;

    // ---------------------------------------------------------------- Comandos

    private void Iniciar()
    {
        if (EmExecucao) return;

        _ambiente = new AmbienteTreinamento(ParametrosAtuais);
        DesenharTorresFixas();
        _algoritmoGenetico = new AlgoritmoGenetico(
            tamanhoGenoma: RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao),
            tamanhoPopulacao: ParametrosAtuais.TamanhoPopulacao,
            taxaMutacao: ParametrosAtuais.TaxaMutacao);

        GeracaoAtual = 0;
        MelhorFitness = 0;
        FitnessMedio = 0;
        _melhorGenomaAtual = [];
        _episodioPreview = null;
        AlvosVisuais.Clear();
        MirasVisuais.Clear();
        RegrasAprendidas.Clear();

        _previewContinuo = true;
        _cts = new CancellationTokenSource();
        _tarefaTreinamento = Task.Run(() => LacoDeTreinamento(_cts.Token));
        _timerPreview.Start();
        EmExecucao = true;
    }

    private void Parar()
    {
        if (!EmExecucao) return;

        _cts?.Cancel();
        _timerPreview.Stop();
        EmExecucao = false;
    }

    /// <summary>
    /// Roda exatamente um episódio do melhor genoma atual (ou, se o treinamento nunca rodou,
    /// de um genoma aleatório) de forma visível e para — sem reiniciar outro episódio
    /// automaticamente. Útil para inspecionar um comportamento específico sem o treinamento em
    /// segundo plano competindo por atenção.
    /// </summary>
    private void RodarEpisodioUnico()
    {
        if (EmExecucao) return;

        if (_melhorGenomaAtual.Length == 0)
        {
            _melhorGenomaAtual = GerarGenomaAleatorio();
            AtualizarRegras(_melhorGenomaAtual);
        }

        _previewContinuo = false;
        _episodioPreview = _ambiente.NovoEpisodio();
        AlvosVisuais.Clear();
        MirasVisuais.Clear();
        _timerPreview.Start();
    }

    private double[] GerarGenomaAleatorio()
    {
        int tamanho = RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao);
        var genoma = new double[tamanho];
        for (int i = 0; i < tamanho; i++) genoma[i] = (_randomGenomaInicial.NextDouble() * 2.0) - 1.0;
        return genoma;
    }

    private void ExportarMelhorGenoma()
    {
        if (_melhorGenomaAtual.Length == 0) return;

        try
        {
            AppDataPaths.EnsureTreinamentoFolderExists();
            string caminho = Path.Combine(AppDataPaths.TreinamentoFolder, $"genoma-geracao-{GeracaoAtual}.json");

            var payload = new
            {
                Arquitetura = ControladorHibrido.ArquiteturaPadrao,
                Genoma = _melhorGenomaAtual,
                Geracao = GeracaoAtual,
                MelhorFitness,
                ExportadoEm = DateTime.Now
            };

            File.WriteAllText(caminho, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            _logger.Success($"Genoma exportado: {caminho}");
            MostrarStatus($"Genoma exportado com sucesso! ({Path.GetFileName(caminho)})", sucesso: true);
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível exportar o genoma: {ex.Message}");
            MostrarStatus($"Não foi possível exportar o genoma: {ex.Message}", sucesso: false);
        }
    }

    // ---------------------------------------------------------------- Laço de treinamento (thread de fundo)

    private void LacoDeTreinamento(CancellationToken token)
    {
        AlgoritmoGenetico algoritmo = _algoritmoGenetico!;

        while (!token.IsCancellationRequested)
        {
            ResultadoGeracao resultado = algoritmo.EvoluirGeracao(
                genoma => _ambiente.ExecutarEpisodioRapido(genoma).FitnessTotal);

            _melhorGenomaAtual = resultado.MelhorGenoma;

            // Reavalia o melhor genoma uma vez mais só para capturar como ele se comportou
            // (abatidos/escapados) — o fitness em si já saiu de dentro do algoritmo genético,
            // mas esses dois números não são devolvidos por ele.
            EpisodioResultado comportamento = _ambiente.ExecutarEpisodioRapido(resultado.MelhorGenoma, seed: resultado.Geracao);
            RegistrarHistorico(resultado, comportamento);
            AtualizarRegras(resultado.MelhorGenoma);

            RunOnUi(() =>
            {
                GeracaoAtual = resultado.Geracao;
                MelhorFitness = resultado.MelhorFitness;
                FitnessMedio = resultado.FitnessMedio;
                GeracaoConcluida?.Invoke(this, resultado);
            });
        }
    }

    private void RegistrarHistorico(ResultadoGeracao resultado, EpisodioResultado comportamento)
    {
        var registro = new HistoricoGeracaoTreinamento
        {
            Geracao = resultado.Geracao,
            MelhorFitness = resultado.MelhorFitness,
            FitnessMedio = resultado.FitnessMedio,
            AlvosAbatidos = comportamento.AlvosAbatidos,
            AlvosEscapados = comportamento.AlvosEscapados,
            DataHora = DateTime.Now
        };

        try
        {
            _historicoRepository.Registrar(registro);
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível registrar o histórico de treinamento: {ex.Message}");
            return;
        }

        RunOnUi(() =>
        {
            HistoricoRecente.Add(registro);
            while (HistoricoRecente.Count > 300) HistoricoRecente.RemoveAt(0);
        });
    }

    private void AtualizarRegras(double[] genoma)
    {
        var rede = new RedeNeural(ControladorHibrido.ArquiteturaPadrao, genoma);
        RegrasExtraidas regras = ExtratorDeRegras.Extrair(rede, _ambiente.Parametros.RaioMundoMetros, _ambiente.Parametros.VelocidadeMaximaAlvoMetrosPorSegundo);

        RunOnUi(() =>
        {
            RegrasAprendidas.Clear();
            foreach (string frase in regras.Frases) RegrasAprendidas.Add(frase);
        });
    }

    // ---------------------------------------------------------------- Prévia visual (thread de UI)

    private void AvancarPreview()
    {
        if (_melhorGenomaAtual.Length == 0) return;

        if (_episodioPreview is null)
        {
            _episodioPreview = _ambiente.NovoEpisodio();
        }
        else if (_episodioPreview.Finalizado)
        {
            if (!_previewContinuo)
            {
                _timerPreview.Stop();
                return;
            }
            _episodioPreview = _ambiente.NovoEpisodio();
        }

        var rede = new RedeNeural(ControladorHibrido.ArquiteturaPadrao, _melhorGenomaAtual);
        var controlador = new ControladorHibrido(rede, _ambiente.Parametros.RaioMundoMetros, _ambiente.Parametros.VelocidadeMaximaAlvoMetrosPorSegundo);

        _ambiente.Avancar(_episodioPreview, controlador, dt: 0.05);
        RedesenharAlvos(_episodioPreview);
        AtualizarMiras(_episodioPreview);
        AtualizarCamposDeVisao(_episodioPreview);

        if (_episodioPreview.Finalizado && !_previewContinuo)
        {
            _timerPreview.Stop();
        }
    }

    /// <summary>
    /// Desenha, para cada torre, a linha de mira (torre -> alvo comprometido, enquanto ainda não
    /// disparou) ou de disparo (destacada, por uma janela curta depois do tiro — a posição do
    /// alvo nesse instante é guardada em <see cref="TorreTreinamento.UltimoAlvoDisparoX"/> porque
    /// o alvo abatido já saiu de <see cref="EstadoEpisodio.Alvos"/> quando chegamos aqui).
    /// </summary>
    private void AtualizarMiras(EstadoEpisodio estado)
    {
        var idsComLinha = new HashSet<int>();

        foreach (TorreTreinamento torre in estado.Torres)
        {
            bool disparandoAgora = torre.DisparadoEmSegundos is { } instanteDisparo &&
                (estado.TempoDecorridoSegundos - instanteDisparo) < JanelaFlashDisparoSegundos;

            double alvoX, alvoY, espessura;
            string cor;
            if (disparandoAgora)
            {
                alvoX = torre.UltimoAlvoDisparoX;
                alvoY = torre.UltimoAlvoDisparoY;
                cor = "#EF4444";
                espessura = 3;
            }
            else if (torre.AlvoComprometidoId is { } alvoId &&
                     estado.Alvos.FirstOrDefault(a => a.Id == alvoId) is { } alvoMirado)
            {
                alvoX = alvoMirado.X;
                alvoY = alvoMirado.Y;
                cor = "#FBBF24";
                espessura = 1.5;
            }
            else
            {
                continue; // torre livre e sem disparo recente: nada para desenhar
            }

            idsComLinha.Add(torre.Id);
            LinhaVisualTreinamento linha = MirasVisuais.FirstOrDefault(l => l.Id == torre.Id)
                ?? Adicionar(new LinhaVisualTreinamento { Id = torre.Id });

            linha.X1 = ParaCanvasX(torre.X);
            linha.Y1 = ParaCanvasY(torre.Y);
            linha.X2 = ParaCanvasX(alvoX);
            linha.Y2 = ParaCanvasY(alvoY);
            linha.Cor = cor;
            linha.Espessura = espessura;

            PontoVisualTreinamento? torreVisual = TorresVisuais.FirstOrDefault(t => t.Id == torre.Id);
            if (torreVisual is not null) torreVisual.Cor = cor;
        }

        foreach (LinhaVisualTreinamento removida in MirasVisuais.Where(l => !idsComLinha.Contains(l.Id)).ToList())
        {
            MirasVisuais.Remove(removida);
        }

        foreach (PontoVisualTreinamento torreVisual in TorresVisuais.Where(t => !idsComLinha.Contains(t.Id)))
        {
            torreVisual.Cor = "#334155"; // livre, sem alvo comprometido nem disparo recente
        }
    }

    private LinhaVisualTreinamento Adicionar(LinhaVisualTreinamento linha)
    {
        MirasVisuais.Add(linha);
        return linha;
    }

    // ---------------------------------------------------------------- Desenho (mundo em metros -> pixels do Canvas)

    private double PixelsPorMetro => (TamanhoCanvas / 2.0) / _ambiente.Parametros.RaioMundoMetros;

    private double ParaCanvasX(double x) => (TamanhoCanvas / 2.0) + x * PixelsPorMetro;

    private double ParaCanvasY(double y) => (TamanhoCanvas / 2.0) - y * PixelsPorMetro;

    private void DesenharTorresFixas()
    {
        TorresVisuais.Clear();
        CamposDeVisaoVisuais.Clear();

        foreach (TorreTreinamento torre in _ambiente.TorresBase)
        {
            TorresVisuais.Add(new PontoVisualTreinamento
            {
                Id = torre.Id,
                CanvasX = ParaCanvasX(torre.X),
                CanvasY = ParaCanvasY(torre.Y),
                Tamanho = 16,
                Cor = "#334155"
            });

            CamposDeVisaoVisuais.Add(ConstruirSetorDeVisao(torre));
        }
    }

    /// <summary>Ângulo (bearing, a partir da origem) que uma torre olha quando não está mirando nenhum alvo — voltada para fora da base.</summary>
    private static double AnguloPadraoDaTorre(TorreTreinamento torre) =>
        (Math.Atan2(torre.X, torre.Y) * 180.0 / Math.PI + 360.0) % 360.0;

    /// <summary>Cria o setor de visão de uma torre na direção padrão (voltada para fora) — chamado uma vez ao montar o episódio.</summary>
    private SetorVisualTreinamento ConstruirSetorDeVisao(TorreTreinamento torre)
    {
        var setor = new SetorVisualTreinamento { Id = torre.Id, Cor = "#2563EB" };
        PreencherPontosDoSetor(setor, torre, AnguloPadraoDaTorre(torre));
        return setor;
    }

    /// <summary>
    /// Recalcula os pontos do leque (cone) de uma torre para apontar em
    /// <paramref name="anguloCentralGraus"/> — raio fixo (alcance de engajamento), mutando
    /// <see cref="SetorVisualTreinamento.Pontos"/> no lugar para o Polygon redesenhar sem
    /// precisar de troca de binding.
    /// </summary>
    private void PreencherPontosDoSetor(SetorVisualTreinamento setor, TorreTreinamento torre, double anguloCentralGraus)
    {
        const int segmentosDoArco = 16;
        double raioMetros = _ambiente.Parametros.DistanciaEngajamentoMetros;

        setor.Pontos.Clear();
        setor.Pontos.Add(new Point(ParaCanvasX(torre.X), ParaCanvasY(torre.Y)));

        for (int i = 0; i <= segmentosDoArco; i++)
        {
            double anguloGraus = anguloCentralGraus - (AnguloCampoDeVisaoGraus / 2.0) + (AnguloCampoDeVisaoGraus * i / segmentosDoArco);
            double anguloRad = anguloGraus * Math.PI / 180.0;

            double x = torre.X + raioMetros * Math.Sin(anguloRad);
            double y = torre.Y + raioMetros * Math.Cos(anguloRad);

            setor.Pontos.Add(new Point(ParaCanvasX(x), ParaCanvasY(y)));
        }
    }

    /// <summary>
    /// A cada quadro: torre comprometida com um alvo gira o cone para apontar para ele (segue o
    /// movimento, já que o alvo se desloca a cada passo); torre livre volta para a direção
    /// padrão. Chamado junto de <see cref="AtualizarMiras"/> no mesmo laço da prévia.
    /// </summary>
    private void AtualizarCamposDeVisao(EstadoEpisodio estado)
    {
        foreach (TorreTreinamento torre in estado.Torres)
        {
            SetorVisualTreinamento? setor = CamposDeVisaoVisuais.FirstOrDefault(s => s.Id == torre.Id);
            if (setor is null) continue;

            double anguloCentralGraus = torre.AlvoComprometidoId is { } alvoId &&
                estado.Alvos.FirstOrDefault(a => a.Id == alvoId) is { } alvoMirado
                ? (Math.Atan2(alvoMirado.X - torre.X, alvoMirado.Y - torre.Y) * 180.0 / Math.PI + 360.0) % 360.0
                : AnguloPadraoDaTorre(torre);

            PreencherPontosDoSetor(setor, torre, anguloCentralGraus);
        }
    }

    private void RedesenharAlvos(EstadoEpisodio estado)
    {
        var idsAtuais = estado.Alvos.Select(a => a.Id).ToHashSet();

        foreach (PontoVisualTreinamento removido in AlvosVisuais.Where(v => !idsAtuais.Contains(v.Id)).ToList())
        {
            AlvosVisuais.Remove(removido);
        }

        foreach (AlvoTreinamento alvo in estado.Alvos)
        {
            PontoVisualTreinamento? visual = AlvosVisuais.FirstOrDefault(v => v.Id == alvo.Id);
            if (visual is null)
            {
                visual = new PontoVisualTreinamento { Id = alvo.Id, Tamanho = 10 };
                AlvosVisuais.Add(visual);
            }

            visual.CanvasX = ParaCanvasX(alvo.X);
            visual.CanvasY = ParaCanvasY(alvo.Y);
            visual.Cor = alvo.TorreSelecionadaId is null ? "#F87171" : "#FBBF24";
        }
    }

    private static void RunOnUi(Action action)
    {
        Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _timerPreview.Stop();
    }
}
