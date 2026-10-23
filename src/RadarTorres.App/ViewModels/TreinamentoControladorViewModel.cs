using System;
using System.Collections.Generic;
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

    /// <summary>Tamanho do bloco de gerações para cada marco registrado em <see cref="IMarcoFitnessTreinamentoRepository"/> (Requisito "fitness médio a cada 500 gerações").</summary>
    private const int TamanhoBlocoMarco = 500;

    private readonly ILoggingService _logger;
    private readonly IHistoricoTreinamentoRepository _historicoRepository;
    private readonly IMarcoFitnessTreinamentoRepository _marcoFitnessRepository;
    private readonly DispatcherTimer _timerPreview;
    private readonly Random _randomGenomaInicial = new();

    // Acumulador do bloco de 500 gerações em andamento — zerado a cada marco registrado (ver
    // RegistrarMarcoFitnessSeCompletouBloco) e também em Iniciar/Resetar, para não misturar
    // gerações de sessões diferentes no mesmo marco.
    private double _somaFitnessMedioBloco;
    private double _melhorFitnessBloco;
    private int _contagemGeracoesNoBloco;

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
    public ObservableCollection<NeuronioVisualTreinamento> NeuroniosVisuais { get; } = new();
    public ObservableCollection<ConexaoVisualTreinamento> ConexoesVisuais { get; } = new();

    /// <summary>Mesma ordem de iteração de <see cref="RedeNeural"/> (camada → entrada → saída) — alinha cada <see cref="ConexaoVisualTreinamento"/> com o peso correspondente na hora de atualizar.</summary>
    private readonly List<(int Camada, int Entrada, int Saida)> _chavesDasConexoes = new();

    public RelayCommand IniciarCommand { get; }
    public RelayCommand PararCommand { get; }
    public RelayCommand RodarEpisodioUnicoCommand { get; }
    public RelayCommand ExportarMelhorGenomaCommand { get; }
    public RelayCommand ResetarCommand { get; }

    public TreinamentoControladorViewModel(
        ILoggingService logger,
        IHistoricoTreinamentoRepository historicoRepository,
        IMarcoFitnessTreinamentoRepository marcoFitnessRepository)
    {
        _logger = logger;
        _historicoRepository = historicoRepository;
        _marcoFitnessRepository = marcoFitnessRepository;
        _ambiente = new AmbienteTreinamento(ParametrosAtuais);
        DesenharTorresFixas();
        ConstruirDiagramaRedeNeural();

        IniciarCommand = new RelayCommand(Iniciar, () => !EmExecucao);
        PararCommand = new RelayCommand(Parar, () => EmExecucao);
        RodarEpisodioUnicoCommand = new RelayCommand(RodarEpisodioUnico, () => !EmExecucao);
        ExportarMelhorGenomaCommand = new RelayCommand(ExportarMelhorGenoma, () => _melhorGenomaAtual.Length > 0);
        ResetarCommand = new RelayCommand(Resetar);

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

    /// <summary>Disparado por <see cref="Resetar"/> — a View usa para limpar o histórico bruto do gráfico de evolução (ver code-behind).</summary>
    public event EventHandler? Resetado;

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
        _somaFitnessMedioBloco = 0;
        _melhorFitnessBloco = 0;
        _contagemGeracoesNoBloco = 0;
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
    /// Para o treinamento (se estiver rodando) e volta tudo ao estado inicial: geração, fitness,
    /// genoma, diagrama da rede, visualizações no Canvas e mensagem de status. NÃO apaga o
    /// histórico já gravado em <see cref="IHistoricoTreinamentoRepository"/> — é um reinício da
    /// sessão atual na tela, não uma limpeza de dados persistidos.
    /// </summary>
    private void Resetar()
    {
        _cts?.Cancel();
        _timerPreview.Stop();
        EmExecucao = false;

        _ambiente = new AmbienteTreinamento(ParametrosAtuais);
        _algoritmoGenetico = null;
        _melhorGenomaAtual = [];
        _episodioPreview = null;
        _previewContinuo = true;

        GeracaoAtual = 0;
        MelhorFitness = 0;
        FitnessMedio = 0;
        _somaFitnessMedioBloco = 0;
        _melhorFitnessBloco = 0;
        _contagemGeracoesNoBloco = 0;

        DesenharTorresFixas();
        ConstruirDiagramaRedeNeural();
        AlvosVisuais.Clear();
        MirasVisuais.Clear();
        RegrasAprendidas.Clear();

        MostrarStatus("Treinamento reiniciado — geração, fitness e genoma voltaram ao zero.", sucesso: true);
        Resetado?.Invoke(this, EventArgs.Empty);
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
            AtualizarDiagramaRedeNeural(_melhorGenomaAtual);
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
            RegistrarMarcoFitnessSeCompletouBloco(resultado);
            AtualizarRegras(resultado.MelhorGenoma);
            AtualizarDiagramaRedeNeural(resultado.MelhorGenoma);

            RunOnUi(() =>
            {
                GeracaoAtual = resultado.Geracao;
                MelhorFitness = resultado.MelhorFitness;
                FitnessMedio = resultado.FitnessMedio;
                GeracaoConcluida?.Invoke(this, resultado);
            });
        }
    }

    /// <summary>
    /// Acumula o fitness médio desta geração no bloco de 500 em andamento e, ao completar o
    /// bloco (geração múltipla de <see cref="TamanhoBlocoMarco"/>), grava um marco em
    /// <see cref="IMarcoFitnessTreinamentoRepository"/> com a média do bloco — não da geração
    /// mais recente só, para o painel de longo prazo mostrar a tendência, não ruído geração a
    /// geração. Acumulador em memória (zera a cada marco e também em Iniciar/Resetar): assim
    /// como <see cref="AlgoritmoGenetico.GeracaoAtual"/>, não sobrevive a um reinício do
    /// aplicativo no meio de um bloco — mesma limitação já aceita pelo histórico de 300 linhas.
    /// </summary>
    private void RegistrarMarcoFitnessSeCompletouBloco(ResultadoGeracao resultado)
    {
        _somaFitnessMedioBloco += resultado.FitnessMedio;
        _melhorFitnessBloco = Math.Max(_melhorFitnessBloco, resultado.MelhorFitness);
        _contagemGeracoesNoBloco++;

        if (resultado.Geracao % TamanhoBlocoMarco != 0) return;

        var marco = new MarcoFitnessTreinamento
        {
            GeracaoMarco = resultado.Geracao,
            FitnessMedioDoBloco = _somaFitnessMedioBloco / Math.Max(1, _contagemGeracoesNoBloco),
            MelhorFitnessDoBloco = _melhorFitnessBloco,
            DataHora = DateTime.Now
        };

        try
        {
            _marcoFitnessRepository.Registrar(marco);
            _logger.Success($"Marco de treinamento registrado: geração {marco.GeracaoMarco}, fitness médio do bloco {marco.FitnessMedioDoBloco:0.0}.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível registrar o marco de fitness da geração {marco.GeracaoMarco}: {ex.Message}");
        }

        _somaFitnessMedioBloco = 0;
        _melhorFitnessBloco = 0;
        _contagemGeracoesNoBloco = 0;
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

    // ---------------------------------------------------------------- Diagrama da rede neural

    private const double DiagramaLargura = 620.0;
    private const double DiagramaAltura = 280.0;
    private const double DiagramaMargemHorizontal = 60.0;

    /// <summary>
    /// Cenário de referência usado para "ligar" a rede e mostrar as ativações no diagrama —
    /// mesmo ponto típico (torre do próprio quadrante, alvo a meia distância, velocidade média)
    /// usado por <see cref="ExtratorDeRegras"/>. É fixo de propósito: assim o que muda no
    /// diagrama geração a geração são os PESOS evoluindo, não ruído de um cenário diferente a
    /// cada atualização.
    /// </summary>
    private static readonly double[] EntradaDeReferenciaParaDiagrama = [0.5, 1.0, 0.0, 0.5, 0.5, 1.0];

    /// <summary>Monta os nós e conexões do diagrama uma única vez — a ARQUITETURA da rede (quantidade de neurônios por camada) nunca muda, só os pesos.</summary>
    private void ConstruirDiagramaRedeNeural()
    {
        NeuroniosVisuais.Clear();
        ConexoesVisuais.Clear();
        _chavesDasConexoes.Clear();

        int[] camadas = ControladorHibrido.ArquiteturaPadrao;
        var posicoes = new Dictionary<(int Camada, int Indice), (double X, double Y)>();

        for (int c = 0; c < camadas.Length; c++)
        {
            double x = camadas.Length == 1
                ? DiagramaLargura / 2.0
                : DiagramaMargemHorizontal + (DiagramaLargura - 2 * DiagramaMargemHorizontal) * c / (camadas.Length - 1);

            int quantidade = camadas[c];
            for (int n = 0; n < quantidade; n++)
            {
                double y = DiagramaAltura * (n + 1) / (quantidade + 1);
                posicoes[(c, n)] = (x, y);

                string rotulo = c == 0 ? ControladorHibrido.RotulosEntradas[n]
                    : c == camadas.Length - 1 ? ControladorHibrido.RotulosSaidas[n]
                    : $"Neurônio oculto #{n + 1}";

                string corBase = c == 0 ? "#86EFAC" : c == camadas.Length - 1 ? "#FDE68A" : "#93C5FD";

                // Rótulo curto fica sempre visível só em entrada/saída — têm significado fixo.
                // Oculto não tem (é matriz de pesos), então continua só no tooltip ao passar o mouse.
                bool temSignificadoFixo = c == 0 || c == camadas.Length - 1;

                NeuroniosVisuais.Add(new NeuronioVisualTreinamento
                {
                    Camada = c,
                    Indice = n,
                    CanvasX = x,
                    CanvasY = y,
                    Raio = c == 0 || c == camadas.Length - 1 ? 16 : 13,
                    Cor = corBase,
                    Rotulo = rotulo,
                    RotuloCurto = temSignificadoFixo ? rotulo : string.Empty
                });
            }
        }

        for (int c = 0; c < camadas.Length - 1; c++)
        {
            for (int e = 0; e < camadas[c]; e++)
            {
                for (int s = 0; s < camadas[c + 1]; s++)
                {
                    (double x1, double y1) = posicoes[(c, e)];
                    (double x2, double y2) = posicoes[(c + 1, s)];

                    ConexoesVisuais.Add(new ConexaoVisualTreinamento { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2 });
                    _chavesDasConexoes.Add((c, e, s));
                }
            }
        }
    }

    /// <summary>
    /// Recalcula cor/espessura das conexões (a partir dos pesos atuais) e ativação/tooltip dos
    /// neurônios (propagando <see cref="EntradaDeReferenciaParaDiagrama"/>) — chamado a cada
    /// geração concluída e sempre que um novo genoma é escolhido para a prévia.
    /// </summary>
    private void AtualizarDiagramaRedeNeural(double[] genoma)
    {
        if (genoma.Length == 0) return;

        var rede = new RedeNeural(ControladorHibrido.ArquiteturaPadrao, genoma);
        double[][] ativacoesPorCamada = rede.AtivarComCamadas(EntradaDeReferenciaParaDiagrama);

        RunOnUi(() =>
        {
            for (int i = 0; i < ConexoesVisuais.Count; i++)
            {
                (int camada, int entrada, int saida) = _chavesDasConexoes[i];
                double peso = rede.Peso(camada, entrada, saida);

                ConexaoVisualTreinamento conexao = ConexoesVisuais[i];
                conexao.Cor = peso >= 0 ? "#3B82F6" : "#EF4444";
                conexao.Espessura = Math.Clamp(0.5 + Math.Abs(peso) * 1.5, 0.5, 4.0);
            }

            foreach (NeuronioVisualTreinamento neuronio in NeuroniosVisuais)
            {
                double ativacao = ativacoesPorCamada[neuronio.Camada][neuronio.Indice];
                neuronio.Rotulo = ConstruirTooltipDoNeuronio(neuronio, ativacao, rede);
                neuronio.Cor = CorComIntensidade(CorBaseDaCamada(neuronio.Camada), Math.Abs(ativacao));
            }
        });
    }

    private string CorBaseDaCamada(int camada) =>
        camada == 0 ? "86EFAC" : camada == ControladorHibrido.ArquiteturaPadrao.Length - 1 ? "FDE68A" : "93C5FD";

    /// <summary>Usa o canal alfa para mostrar a intensidade da ativação (0 = quase transparente/neutro, 1 = cor cheia) sem precisar de uma segunda escala de cor.</summary>
    private static string CorComIntensidade(string corBaseRgbHex, double intensidade01)
    {
        int alfa = 60 + (int)Math.Round(Math.Clamp(intensidade01, 0, 1) * 195);
        return $"#{alfa:X2}{corBaseRgbHex}";
    }

    private string ConstruirTooltipDoNeuronio(NeuronioVisualTreinamento neuronio, double ativacao, RedeNeural rede)
    {
        int ultimaCamada = ControladorHibrido.ArquiteturaPadrao.Length - 1;

        if (neuronio.Camada == 0)
        {
            return $"ENTRADA — {ControladorHibrido.RotulosEntradas[neuronio.Indice]}\nValor no cenário de referência: {ativacao:0.00}";
        }

        if (neuronio.Camada == ultimaCamada)
        {
            string descricao = neuronio.Indice == 0
                ? "Soma à pontuação heurística para decidir qual torre persegue o alvo."
                : "Decide o instante do disparo dentro do alcance de engajamento — nunca aciona nada por conta própria no sistema real.";
            return $"SAÍDA — {ControladorHibrido.RotulosSaidas[neuronio.Indice]}\nAtivação atual: {ativacao:0.00}\n{descricao}";
        }

        // Neurônio oculto: sem significado individual fixo (é uma matriz de pesos, não uma
        // regra) — mostramos a ativação no cenário de referência e, por análise de sensibilidade
        // dos pesos de entrada, quais entradas mais pesam nele, como aproximação honesta de
        // "responsável por quê" em vez de inventar um rótulo fixo.
        var influencias = new List<(string Entrada, double Peso)>();
        for (int i = 0; i < ControladorHibrido.QuantidadeEntradas; i++)
        {
            influencias.Add((ControladorHibrido.RotulosEntradas[i], rede.Peso(0, i, neuronio.Indice)));
        }

        var maisInfluentes = influencias.OrderByDescending(x => Math.Abs(x.Peso)).Take(2).ToList();

        return $"NEURÔNIO OCULTO #{neuronio.Indice + 1}\nAtivação atual: {ativacao:0.00} (cenário de referência)\n" +
               $"Mais influenciado por: {maisInfluentes[0].Entrada} (peso {maisInfluentes[0].Peso:+0.00;-0.00}), " +
               $"{maisInfluentes[1].Entrada} (peso {maisInfluentes[1].Peso:+0.00;-0.00})";
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

        var pontos = new PointCollection { new Point(ParaCanvasX(torre.X), ParaCanvasY(torre.Y)) };

        for (int i = 0; i <= segmentosDoArco; i++)
        {
            double anguloGraus = anguloCentralGraus - (AnguloCampoDeVisaoGraus / 2.0) + (AnguloCampoDeVisaoGraus * i / segmentosDoArco);
            double anguloRad = anguloGraus * Math.PI / 180.0;

            double x = torre.X + raioMetros * Math.Sin(anguloRad);
            double y = torre.Y + raioMetros * Math.Cos(anguloRad);

            pontos.Add(new Point(ParaCanvasX(x), ParaCanvasY(y)));
        }

        // Substitui a coleção inteira (não só o conteúdo) — ver nota em SetorVisualTreinamento
        // sobre por que mutar no lugar não é garantia de redesenho via binding.
        setor.Pontos = pontos;
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
