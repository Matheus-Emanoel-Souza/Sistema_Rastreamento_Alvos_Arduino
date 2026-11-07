using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
using SharpNeat.IO.Models;
using SharpNeat.Neat.Genome;
using SharpNeat.Neat.Genome.IO;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da tela "Treinamento do Controlador": comanda o <see cref="TreinadorNeat"/>
/// em segundo plano (gerações o mais rápido possível, sem travar a UI) enquanto anima no Canvas
/// 2D, em tempo real, o melhor genoma encontrado até agora — são dois laços independentes sobre
/// o mesmo <see cref="AmbienteTreinamento"/>: um rápido (fitness) e um visual (prévia).
/// </summary>
public sealed class TreinamentoControladorViewModel : ViewModelBase, IDisposable
{
    private const double JanelaFlashDisparoSegundos = 0.3;

    /// <summary>
    /// Passo da prévia visual, em segundos — vale ao mesmo tempo como intervalo do relógio e como
    /// <c>dt</c> entregue à simulação. Serem o mesmo número é o que faz a prévia correr em TEMPO
    /// NORMAL (um segundo simulado por segundo real), condição do experimento de integração com o
    /// sistema físico: nenhuma aceleração artificial entre o que se vê e o que seria enviado ao
    /// hardware.
    /// </summary>
    private const double IntervaloPreviaSegundos = 0.05;

    /// <summary>Abertura angular do campo de visão de cada torre/canhão — fixa, voltada para fora da base (ver DesenharTorresFixas).</summary>
    public const double AnguloCampoDeVisaoGraus = 90.0;

    /// <summary>Tamanho do bloco de gerações para cada marco registrado em <see cref="IMarcoFitnessTreinamentoRepository"/> (Requisito "fitness médio a cada 500 gerações").</summary>
    private const int TamanhoBlocoMarco = 500;

    /// <summary>
    /// A cada quantas gerações o comportamento completo do melhor genoma é fotografado e
    /// persistido (ver <see cref="IInstantaneoComportamentoRepository"/>) — distinto do marco de
    /// fitness agregado acima: aqui é um retrato detalhado (decisões/acertos/erros/genoma
    /// inteiro) de uma geração específica, não uma média de bloco, por isso o intervalo maior.
    /// </summary>
    private const int TamanhoBlocoInstantaneo = 1000;

    private readonly ILoggingService _logger;
    private readonly IHistoricoTreinamentoRepository _historicoRepository;
    private readonly IMarcoFitnessTreinamentoRepository _marcoFitnessRepository;
    private readonly IInstantaneoComportamentoRepository _instantaneoRepository;
    private readonly ITelemetriaSimulacaoService _telemetria;
    private readonly IEventoTreinamentoRepository _eventoRepository;
    private readonly IConfiguracoesAplicativoRepository _configuracoes;
    private readonly DispatcherTimer _timerPreview;

    /// <summary>
    /// Atualiza só os indicadores que mudam com a passagem do tempo (tempo decorrido, gerações
    /// por minuto). Separado de <see cref="_timerPreview"/> porque a prévia pode estar parada
    /// (episódio único já terminado) enquanto o treinamento continua rodando atrás — nesse caso o
    /// relógio da tela não pode congelar junto.
    /// </summary>
    private readonly DispatcherTimer _timerEstado;

    /// <summary>Mede o tempo de parede da sessão de treinamento, base de "tempo decorrido" e "gerações/min".</summary>
    private readonly Stopwatch _cronometroSessao = new();

    /// <summary>
    /// Intervalo mínimo entre duas remontagens do diagrama/regras. As gerações rodam o mais rápido
    /// possível e cada remontagem converte o genoma, propaga a rede, extrai regras e troca dezenas
    /// de itens em coleções ligadas à tela: fazer isso a cada geração inunda o dispatcher e trava
    /// a interface. O campeão exibido passa a ser o da última geração dentro da janela — nenhum
    /// número é alterado, só a frequência do redesenho.
    /// </summary>
    private static readonly TimeSpan IntervaloMinimoRemontagemDiagrama = TimeSpan.FromMilliseconds(700);

    private readonly Stopwatch _cronometroDiagrama = Stopwatch.StartNew();

    /// <summary>
    /// Ordem já atribuída a cada neurônio oculto, por id de nó. Mantida entre gerações para que um
    /// oculto que continua existindo não mude de lugar no diagrama só porque outro nasceu ou
    /// morreu — sem isso os nós "saltavam" a cada remontagem e ficava impossível acompanhar
    /// visualmente o crescimento da rede.
    /// </summary>
    /// <remarks>
    /// Guarda a ORDEM, e não a coordenada: os ocultos passaram a ser distribuídos em várias
    /// colunas conforme a rede cresce (ver <see cref="CalcularLayoutDoDiagrama"/>), e nesse layout
    /// a posição vertical sozinha não identifica mais o lugar de um nó.
    /// </remarks>
    private readonly Dictionary<int, int> _ordemDoOcultoPorId = new();

    /// <summary>Ocultos e conexões do diagrama anterior — o que não está aqui é NOVO e recebe destaque na geração em que surge.</summary>
    private readonly HashSet<int> _ocultosDaRemontagemAnterior = new();
    private readonly HashSet<(int Origem, int Destino)> _conexoesDaRemontagemAnterior = new();

    // Acumulador do bloco de 500 gerações em andamento — zerado a cada marco registrado (ver
    // RegistrarMarcoFitnessSeCompletouBloco) e também em Iniciar/Resetar, para não misturar
    // gerações de sessões diferentes no mesmo marco.
    private double _somaFitnessMedioBloco;
    private double _melhorFitnessBloco;
    private int _contagemGeracoesNoBloco;

    // Acumuladores da EXECUÇÃO INTEIRA (desde o "Iniciar"/"Resetar" até agora) — diferentes de
    // HistoricoDaSessao, que é só uma JANELA das últimas LimiteSerieDaSessao gerações (para o
    // gráfico não crescer sem limite numa execução de horas). Os indicadores do topo da tela
    // (Melhor fitness, Fitness médio, Taxa de abate) precisam somar TODA a execução, não só o que
    // ainda está na janela — por isso vivem em contadores próprios, atualizados a cada geração em
    // AplicarResultadoNaTela e zerados só em ZerarIndicadoresDaSessao (Iniciar/Resetar).
    private double _somaFitnessMedioPonderadaExecucao;
    private long _somaIndividuosAvaliadosExecucao;
    private long _totalAlvosAbatidosExecucao;
    private long _totalAlvosEscapadosExecucao;

    /// <summary>
    /// Liberado (sinalizado) = treino corre normalmente; limpo por <see cref="Pausar"/> = o laço
    /// (<see cref="Evoluir"/>) bloqueia antes da próxima geração até <see cref="Continuar"/>
    /// sinalizar de novo, ou até o <see cref="CancellationToken"/> de "Parar" ser cancelado.
    /// </summary>
    private readonly ManualResetEventSlim _pausaLiberada = new(initialState: true);

    private AmbienteTreinamento _ambiente;
    private TreinadorNeat? _treinador;

    /// <summary>
    /// Identifica esta sessão de treinamento para os marcos/instantâneos gravados por ela (ver
    /// <see cref="RegistrarMarcoFitnessSeCompletouBloco"/>/<see cref="RegistrarInstantaneoDeComportamentoSeCompletouBloco"/>)
    /// — gerado de novo toda vez que um <see cref="TreinadorNeat"/> novo é criado (<see cref="Iniciar"/>
    /// e o <c>??=</c> equivalente). Necessário porque a numeração de gerações do NEAT reinicia em
    /// 0 a cada sessão: sem isto, duas sessões que passassem pela mesma geração de marco (500,
    /// 1000...) colidiam no índice único da tabela e o INSERT falhava ("UNIQUE constraint failed").
    /// </summary>
    private string _execucaoId = Guid.NewGuid().ToString("N");
    private CancellationTokenSource? _cts;
    private Task? _tarefaTreinamento;

    /// <summary>Genoma campeão da última geração — um GRAFO do SharpNEAT, não mais um vetor de pesos.</summary>
    private NeatGenome<double>? _melhorGenomaAtual;

    /// <summary>A rede já decodificada a partir de <see cref="_melhorGenomaAtual"/> — é o que a prévia anima e o que o diagrama/extrator de regras analisam.</summary>
    private RedeNeat? _melhorRedeAtual;
    private EstadoEpisodio? _episodioPreview;

    /// <summary>Quando falso, a prévia roda exatamente um episódio e para — ver <see cref="RodarEpisodioUnico"/>.</summary>
    private bool _previewContinuo = true;

    private const double TamanhoCanvas = 480.0;

    public ObservableCollection<PontoVisualTreinamento> TorresVisuais { get; } = new();
    public ObservableCollection<PontoVisualTreinamento> AlvosVisuais { get; } = new();
    public ObservableCollection<LinhaVisualTreinamento> MirasVisuais { get; } = new();
    public ObservableCollection<SetorVisualTreinamento> CamposDeVisaoVisuais { get; } = new();
    public ObservableCollection<HistoricoGeracaoTreinamento> HistoricoRecente { get; } = new();
    public ObservableCollection<NeuronioVisualTreinamento> NeuroniosVisuais { get; } = new();
    public ObservableCollection<ConexaoVisualTreinamento> ConexoesVisuais { get; } = new();

    /// <summary>
    /// Série de gerações desta SESSÃO de treinamento — é o que o gráfico de evolução desenha.
    /// Distinta de <see cref="HistoricoRecente"/>, que vem do banco e traz as últimas 300 gerações
    /// de qualquer execução: alimentar o gráfico com ela fazia a curva começar com dados de treinos
    /// anteriores e dar um degrau artificial a cada "Iniciar".
    /// </summary>
    public ObservableCollection<HistoricoGeracaoTreinamento> HistoricoDaSessao { get; } = new();

    /// <summary>Espécies da geração mais recente (ver <see cref="LinhaEspecieTreinamento"/>).</summary>
    public ObservableCollection<LinhaEspecieTreinamento> Especies { get; } = new();

    /// <summary>Acontecimentos da execução, do mais recente para o mais antigo (ver <see cref="EventoTreinamento"/>).</summary>
    public ObservableCollection<EventoTreinamento> EventosExecucao { get; } = new();

    /// <summary>Configuração efetivamente em uso na execução em andamento (ver <see cref="AtualizarParametrosEmUso"/>).</summary>
    public ObservableCollection<ParametroEmUso> ParametrosEmUso { get; } = new();

    public RelayCommand IniciarCommand { get; }
    public RelayCommand PararCommand { get; }
    public RelayCommand PausarCommand { get; }
    public RelayCommand ContinuarCommand { get; }
    public RelayCommand RodarEpisodioUnicoCommand { get; }
    public RelayCommand SimularCenarioCommand { get; }
    public RelayCommand ExportarMelhorGenomaCommand { get; }
    public RelayCommand ResetarCommand { get; }
    public RelayCommand AdicionarSpawnPersonalizadoCommand { get; }
    public RelayCommand RemoverSpawnPersonalizadoCommand { get; }
    public RelayCommand SimularCenarioPersonalizadoCommand { get; }

    /// <summary>Linhas do cenário desenhado à mão pelo usuário (ver <see cref="SimularCenarioPersonalizado"/>) — cada uma é um alvo com posição e velocidade fixadas, sem sorteio nenhum.</summary>
    public ObservableCollection<SpawnPersonalizadoEditavel> SpawnsPersonalizados { get; } = new();

    /// <summary>Disparado quando o cenário personalizado começa a rodar de fato (ver <see cref="SimularCenarioPersonalizado"/>) — a janela modal de configuração usa isso para se fechar sozinha após confirmar.</summary>
    public event EventHandler? CenarioPersonalizadoIniciado;

    /// <summary>Item do seletor de cenário na tela — nome/descrição vêm de <see cref="CatalogoCenarios"/>, lido só na construção (o catálogo é estático).</summary>
    public sealed record CenarioOpcao(TipoCenarioTreinamento Tipo, string Nome, string Descricao);

    /// <summary>Todos os casos táticos mapeados (ver <see cref="CatalogoCenarios"/>), na ordem de exibição do ComboBox da tela.</summary>
    public IReadOnlyList<CenarioOpcao> CenariosDisponiveis { get; } = CatalogoCenarios.Todos
        .Select(t => new CenarioOpcao(t, CatalogoCenarios.NomeExibicao(t), CatalogoCenarios.Descricao(t)))
        .ToList();

    private CenarioOpcao _cenarioOpcaoSelecionada;
    public CenarioOpcao CenarioOpcaoSelecionada
    {
        get => _cenarioOpcaoSelecionada;
        set => SetProperty(ref _cenarioOpcaoSelecionada, value);
    }

    public TreinamentoControladorViewModel(
        ILoggingService logger,
        IHistoricoTreinamentoRepository historicoRepository,
        IMarcoFitnessTreinamentoRepository marcoFitnessRepository,
        IInstantaneoComportamentoRepository instantaneoRepository,
        ITelemetriaSimulacaoService telemetria,
        IEventoTreinamentoRepository eventoRepository,
        IConfiguracoesAplicativoRepository configuracoes)
    {
        _logger = logger;
        _historicoRepository = historicoRepository;
        _marcoFitnessRepository = marcoFitnessRepository;
        _instantaneoRepository = instantaneoRepository;
        _telemetria = telemetria;
        _eventoRepository = eventoRepository;
        _configuracoes = configuracoes;
        _ambiente = new AmbienteTreinamento(ParametrosAtuais);
        DesenharTorresFixas();
        LimparDiagramaRedeNeural();
        _cenarioOpcaoSelecionada = CenariosDisponiveis[0];

        IniciarCommand = new RelayCommand(Iniciar, () => !EmExecucao);
        PararCommand = new RelayCommand(Parar, () => EmExecucao);
        PausarCommand = new RelayCommand(Pausar, () => EmExecucao && !EmPausa);
        ContinuarCommand = new RelayCommand(Continuar, () => EmExecucao && EmPausa);
        RodarEpisodioUnicoCommand = new RelayCommand(RodarEpisodioUnico, () => !EmExecucao);
        SimularCenarioCommand = new RelayCommand(SimularCenarioSelecionado, () => !EmExecucao);
        ExportarMelhorGenomaCommand = new RelayCommand(ExportarMelhorGenoma, () => _melhorGenomaAtual is not null);
        ResetarCommand = new RelayCommand(Resetar);
        AdicionarSpawnPersonalizadoCommand = new RelayCommand(() => SpawnsPersonalizados.Add(
            new SpawnPersonalizadoEditavel { Nome = $"Alvo {SpawnsPersonalizados.Count + 1}" }));
        RemoverSpawnPersonalizadoCommand = new RelayCommand(RemoverSpawnPersonalizado);
        SimularCenarioPersonalizadoCommand = new RelayCommand(SimularCenarioPersonalizado, () => !EmExecucao);

        _timerPreview = new DispatcherTimer { Interval = TimeSpan.FromSeconds(IntervaloPreviaSegundos) };
        _timerPreview.Tick += (_, _) => AvancarPreview();

        _timerEstado = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timerEstado.Tick += (_, _) => TempoDecorrido = _cronometroSessao.Elapsed;

        AtualizarParametrosEmUso(ParametrosAtuais, treinador: null);
        CarregarHistorico();
        CarregarEventos();
    }

    /// <summary>
    /// Traz do banco o histórico de execução das sessões anteriores — é o que permite abrir a tela
    /// no dia seguinte e descobrir por que o treinamento da madrugada parou.
    /// </summary>
    private void CarregarEventos()
    {
        try
        {
            EventosExecucao.Clear();
            foreach (EventoTreinamento evento in _eventoRepository.ObterUltimos(LimiteEventosExecucao))
            {
                // _execucaoId já está definido aqui (inicializado no field initializer — ver
                // declaração do campo), então compara com o Guid da sessão que esta tela abriu
                // agora, mesmo antes de qualquer "Iniciar" ser clicado nesta sessão.
                evento.EhSessaoAtual = evento.ExecucaoId == _execucaoId;
                EventosExecucao.Add(evento);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível carregar o histórico de execução do treinamento: {ex.Message}");
        }
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

    /// <summary>
    /// Probabilidade de mutação ESTRUTURAL (acrescentar neurônio) — ver
    /// <see cref="ParametrosTreinamento.ProbabilidadeAdicionarNo"/>.
    /// </summary>
    /// <remarks>
    /// O padrão é o mesmo baseline do plano experimental da tela "Estatísticas de Treinamento"
    /// (<see cref="Services.ConfiguracaoExperimento.ProbabilidadeAdicionarNoBaseline"/>), para um
    /// treinamento manual aqui nascer comparável com os resultados registrados lá — mesma razão de
    /// <see cref="NeuroniosOcultosDisponiveis"/>. Herdava 0,08 da antiga taxa de mutação POR GENE
    /// do algoritmo genético, que é outra grandeza: 8% de chance de ganhar um neurônio a cada
    /// descendente infla a topologia muito acima do que o plano experimental mede.
    /// </remarks>
    private double _taxaMutacao = Services.ConfiguracaoExperimento.ProbabilidadeAdicionarNoBaseline;
    public double TaxaMutacao
    {
        get => _taxaMutacao;
        set => SetProperty(ref _taxaMutacao, value);
    }

    private int _maxAlvosPorQuadrante = 5;
    public int MaxAlvosPorQuadrante
    {
        get => _maxAlvosPorQuadrante;
        set => SetProperty(ref _maxAlvosPorQuadrante, value);
    }

    private bool _eixoZHabilitado;
    /// <summary>
    /// Liga a terceira dimensão na simulação — ver <see cref="ParametrosTreinamento.EixoZHabilitado"/>.
    /// Como os demais parâmetros do ambiente, vale a partir do próximo "Iniciar"/"Resetar" ou da
    /// próxima simulação avulsa: um episódio já em curso não muda de dimensão no meio.
    /// </summary>
    public bool EixoZHabilitado
    {
        get => _eixoZHabilitado;
        set
        {
            if (SetProperty(ref _eixoZHabilitado, value)) OnPropertyChanged(nameof(DescricaoEixoZ));
        }
    }

    /// <summary>Explica, em uma linha, o que o estado atual do controle significa para a simulação.</summary>
    public string DescricaoEixoZ => EixoZHabilitado
        ? $"Alvos nascem com altura entre 0 e {AlturaMaximaAlvoMetros:0.#} m; acima de {AlturaVisivelPorTodasAsTorresMetros:0.#} m todas as torres passam a enxergá-los."
        : "Simulação bidimensional: todos os alvos no solo (Z = 0) e cobertura apenas por quadrante.";

    /// <summary>Altura máxima sorteada para um alvo com o eixo Z ligado — exposta para a tela e para a descrição acima.</summary>
    public double AlturaMaximaAlvoMetros => _ambiente.Parametros.AlturaMaximaAlvoMetros;

    /// <summary>Altura a partir da qual todas as torres enxergam o alvo.</summary>
    public double AlturaVisivelPorTodasAsTorresMetros => _ambiente.Parametros.AlturaVisivelPorTodasAsTorresMetros;

    /// <summary>
    /// Tetos de complexidade (em número de conexões) disponíveis na tela — os mesmos três
    /// valores do plano experimental da tela "Estatísticas de Treinamento" (ver
    /// ConfiguracaoExperimento.TetosComplexidadeDaSerie), para um treinamento manual aqui ser
    /// comparável com os resultados registrados lá.
    /// </summary>
    /// <remarks>
    /// Substitui a antiga lista de "neurônios na camada oculta": com NEAT não se escolhe quantos
    /// neurônios a rede tem — escolhe-se até onde ela pode crescer.
    /// </remarks>
    public IReadOnlyList<int> NeuroniosOcultosDisponiveis { get; } = [30, 60, 120];

    private int _neuroniosOcultosSelecionado = 60;
    /// <summary>Teto de complexidade usado pelo PRÓXIMO "Iniciar"/"Resetar" — não afeta um treinamento já em andamento (mesmo princípio de <see cref="TamanhoPopulacao"/>).</summary>
    public int NeuroniosOcultosSelecionado
    {
        get => _neuroniosOcultosSelecionado;
        set => SetProperty(ref _neuroniosOcultosSelecionado, value);
    }

    /// <summary>
    /// Lidos de <see cref="ConfiguracoesAplicativo"/> (tela "Configurações") a cada chamada —
    /// não copiados/cacheados aqui — para ajustar "Radar e mira"/"Disparo" na tela de
    /// Configurações refletir no próximo treinamento iniciado (ou resetado) sem precisar reabrir
    /// a tela de Treinamento do Controlador. É a MESMA regra usada pelo sistema real
    /// (<see cref="Services.TowerSelectionService"/>/<see cref="Services.FireControlService"/>):
    /// o alcance de mira configurado vira <see cref="ParametrosTreinamento.DistanciaEngajamentoMetros"/>
    /// e a certeza mínima de disparo vira <see cref="ParametrosTreinamento.LimiarConfiancaDisparoFracao"/>,
    /// nunca abaixo do piso absoluto (<see cref="ConfiguracoesAplicativo.ConfiancaMinimaDisparoPercentual"/>).
    /// A velocidade considerada "rápida" (aciona ajuda de torre adjacente) segue a mesma regra.
    /// </summary>
    private ParametrosTreinamento ParametrosAtuais => new()
    {
        TamanhoPopulacao = Math.Max(4, _tamanhoPopulacao),
        ProbabilidadeAdicionarNo = Math.Clamp(_taxaMutacao, 0.0, 1.0),
        TetoComplexidade = Math.Max(4, _neuroniosOcultosSelecionado),
        MaxAlvosPorQuadrante = Math.Max(1, _maxAlvosPorQuadrante),
        EixoZHabilitado = _eixoZHabilitado,
        DistanciaEngajamentoMetros = _configuracoes.Atual.AlcanceMiraMetros,
        LimiarConfiancaDisparoFracao = _configuracoes.Atual.LimiarConfiancaDisparoPercentual / 100.0,
        VelocidadeRapidaMetrosPorSegundo = _configuracoes.Atual.VelocidadeRapidaMetrosPorSegundo
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
                OnPropertyChanged(nameof(EstadoTexto));
                OnPropertyChanged(nameof(EstadoCor));
            }
        }
    }

    /// <summary>
    /// Ligado quando o laço de treinamento morre por exceção. Antes disso não existir, uma falha
    /// na thread de fundo deixava <see cref="EmExecucao"/> em <c>true</c> para sempre: a tela
    /// seguia anunciando "rodando" com os números congelados e sem nenhuma mensagem de erro.
    /// </summary>
    private bool _falhou;
    public bool Falhou
    {
        get => _falhou;
        private set
        {
            if (SetProperty(ref _falhou, value))
            {
                OnPropertyChanged(nameof(EstadoTexto));
                OnPropertyChanged(nameof(EstadoCor));
            }
        }
    }

    /// <summary>
    /// Verdadeiro entre "Pausar" e "Continuar" — o laço de treinamento (<see cref="Evoluir"/>)
    /// bloqueia em <see cref="_pausaLiberada"/> ANTES de começar a próxima geração, nunca no meio
    /// de uma: pausar não corta uma avaliação de população pela metade, só segura o início da
    /// seguinte. Não é cancelamento — <see cref="_treinador"/> e toda a população continuam
    /// intactos na memória, "Continuar" retoma exatamente da geração seguinte.
    /// </summary>
    private bool _emPausa;
    public bool EmPausa
    {
        get => _emPausa;
        private set
        {
            if (SetProperty(ref _emPausa, value))
            {
                RelayCommand.RaiseCanExecuteChangedForAll();
                OnPropertyChanged(nameof(EstadoTexto));
                OnPropertyChanged(nameof(EstadoCor));
            }
        }
    }

    /// <summary>Estado em uma palavra, para o selo no topo da tela.</summary>
    public string EstadoTexto => Falhou ? "FALHOU" : EmPausa ? "PAUSADO" : EmExecucao ? "TREINANDO" : GeracaoAtual > 0 ? "PARADO" : "PRONTO";

    public string EstadoCor => Falhou ? "#F87171" : EmPausa ? "#FBBF24" : EmExecucao ? "#4ADE80" : "#94A3B8";

    private int _geracaoAtual;
    public int GeracaoAtual
    {
        get => _geracaoAtual;
        private set
        {
            if (SetProperty(ref _geracaoAtual, value))
            {
                OnPropertyChanged(nameof(EstadoTexto));
                OnPropertyChanged(nameof(ProgressoFracao));
                OnPropertyChanged(nameof(ProgressoTexto));
                OnPropertyChanged(nameof(ProgressoNoBlocoFracao));
                OnPropertyChanged(nameof(ProgressoNoBlocoTexto));
            }
        }
    }

    private double _melhorFitness;
    public double MelhorFitness { get => _melhorFitness; private set => SetProperty(ref _melhorFitness, value); }

    private double _fitnessMedio;
    public double FitnessMedio { get => _fitnessMedio; private set => SetProperty(ref _fitnessMedio, value); }

    // ------------------------------------------------- Melhor resultado acumulado da sessão

    private double _melhorFitnessDaSessao;
    /// <summary>Pico de fitness alcançado desde o início desta sessão — distinto de <see cref="MelhorFitness"/>, que é o campeão da geração mais recente e pode regredir quando as seeds mudam.</summary>
    public double MelhorFitnessDaSessao { get => _melhorFitnessDaSessao; private set => SetProperty(ref _melhorFitnessDaSessao, value); }

    private int _geracaoDoMelhorFitness;
    public int GeracaoDoMelhorFitness { get => _geracaoDoMelhorFitness; private set => SetProperty(ref _geracaoDoMelhorFitness, value); }

    private double _fitnessMedioExecucao;
    /// <summary>
    /// Média ponderada do fitness médio de TODAS as gerações desde o início da execução — ponderada
    /// pela quantidade de indivíduos avaliados em cada geração (<see cref="ParametrosTreinamento.TamanhoPopulacao"/>
    /// da execução em uso), não uma média simples das médias. Diferente de <see cref="FitnessMedio"/>
    /// (só da geração mais recente) e não derivada de <see cref="HistoricoDaSessao"/>, que é só uma
    /// JANELA das últimas <see cref="LimiteSerieDaSessao"/> gerações — soma à parte em
    /// <see cref="_somaFitnessMedioPonderadaExecucao"/>/<see cref="_somaIndividuosAvaliadosExecucao"/>,
    /// atualizada a cada geração e zerada só em <see cref="ZerarIndicadoresDaSessao"/>.
    /// </summary>
    public double FitnessMedioExecucao { get => _fitnessMedioExecucao; private set => SetProperty(ref _fitnessMedioExecucao, value); }

    private double? _taxaDeAbateExecucao;
    /// <summary>
    /// Total de alvos abatidos dividido pelo total de alvos finalizados (abatidos + escapados) em
    /// TODA a execução — soma de cada episódio de verificação do campeão, geração a geração, não
    /// só o último (ver <see cref="TaxaDeCobertura"/>, que é só do episódio mais recente). <c>null</c>
    /// enquanto nenhum alvo tiver sido finalizado ainda nesta execução.
    /// </summary>
    public double? TaxaDeAbateExecucao { get => _taxaDeAbateExecucao; private set => SetProperty(ref _taxaDeAbateExecucao, value); }

    /// <summary>Quantas gerações entram na média móvel e na comparação "antes × agora" do fitness.</summary>
    public const int JanelaIndicadoresAprendizado = 50;

    private double _fitnessMediaMovel;
    /// <summary>Média do melhor fitness nas últimas <see cref="JanelaIndicadoresAprendizado"/> gerações — alisa o ruído de seed para mostrar a tendência, não o sobe-e-desce geração a geração.</summary>
    public double FitnessMediaMovel { get => _fitnessMediaMovel; private set => SetProperty(ref _fitnessMediaMovel, value); }

    private double? _variacaoNaJanela;
    /// <summary>
    /// Diferença entre a média móvel atual e a de <see cref="JanelaIndicadoresAprendizado"/>
    /// gerações atrás. Positiva = aprendendo; perto de zero = estabilizou; negativa = regredindo.
    /// <c>null</c> enquanto não houver gerações suficientes para a comparação existir.
    /// </summary>
    public double? VariacaoNaJanela
    {
        get => _variacaoNaJanela;
        private set
        {
            if (SetProperty(ref _variacaoNaJanela, value)) OnPropertyChanged(nameof(DiagnosticoAprendizado));
        }
    }

    private int _geracoesSemCampeaoNovo;
    /// <summary>
    /// Gerações desde que o campeão atual nasceu (<c>BirthGeneration</c> do genoma). É estagnação
    /// MEDIDA: nenhum indivíduo novo conseguiu superá-lo nesse intervalo.
    /// </summary>
    public int GeracoesSemCampeaoNovo
    {
        get => _geracoesSemCampeaoNovo;
        private set
        {
            if (SetProperty(ref _geracoesSemCampeaoNovo, value)) OnPropertyChanged(nameof(DiagnosticoAprendizado));
        }
    }

    /// <summary>
    /// Leitura em uma frase dos dois indicadores acima — serve para quem olha a tela não precisar
    /// interpretar os números sozinho. Deriva só de valores medidos; não há nenhum limiar de
    /// "qualidade" embutido além do tempo de estagnação.
    /// </summary>
    public string DiagnosticoAprendizado
    {
        get
        {
            if (VariacaoNaJanela is not { } variacao) return $"Coletando as primeiras {JanelaIndicadoresAprendizado} gerações…";

            string tendencia = variacao switch
            {
                > 1.0 => $"Evoluindo (+{variacao:0.0} em {JanelaIndicadoresAprendizado} gerações)",
                < -1.0 => $"Regredindo ({variacao:0.0} em {JanelaIndicadoresAprendizado} gerações)",
                _ => $"Estável ({variacao:+0.0;-0.0;0.0} em {JanelaIndicadoresAprendizado} gerações)"
            };

            return GeracoesSemCampeaoNovo >= JanelaIndicadoresAprendizado
                ? $"{tendencia} · campeão sem ser superado há {GeracoesSemCampeaoNovo} gerações"
                : tendencia;
        }
    }

    // ------------------------------------------------- Complexidade e fase da evolução

    private double _complexidadeMelhor;
    /// <summary>Conexões do campeão. Com NEAT isso cresce ao longo da evolução — no método anterior a topologia era fixa e esse número nunca mudava.</summary>
    public double ComplexidadeMelhor { get => _complexidadeMelhor; private set => SetProperty(ref _complexidadeMelhor, value); }

    private double _complexidadeMedia;
    public double ComplexidadeMedia { get => _complexidadeMedia; private set => SetProperty(ref _complexidadeMedia, value); }

    private double _complexidadeMaxima;
    public double ComplexidadeMaxima { get => _complexidadeMaxima; private set => SetProperty(ref _complexidadeMaxima, value); }

    private int _tetoComplexidadeEmUso;
    /// <summary>Teto em vigor na execução atual — é a referência que dá sentido às três medidas acima.</summary>
    public int TetoComplexidadeEmUso { get => _tetoComplexidadeEmUso; private set => SetProperty(ref _tetoComplexidadeEmUso, value); }

    private bool _simplificando;
    /// <summary>Fase da regulação de complexidade do NEAT — ver <see cref="FaseTexto"/>.</summary>
    public bool Simplificando
    {
        get => _simplificando;
        private set
        {
            if (SetProperty(ref _simplificando, value))
            {
                OnPropertyChanged(nameof(FaseTexto));
                OnPropertyChanged(nameof(FaseCor));
            }
        }
    }

    /// <summary>
    /// A "etapa" em que o treinamento está, no único sentido que o NEAT tem: enquanto a
    /// complexidade média está abaixo do teto a evolução COMPLEXIFICA (pode acrescentar neurônios
    /// e conexões); ao passar do teto ela SIMPLIFICA, enxugando a rede até voltar a caber.
    /// </summary>
    public string FaseTexto => Simplificando ? "SIMPLIFICANDO" : "COMPLEXIFICANDO";

    public string FaseCor => Simplificando ? "#FBBF24" : "#60A5FA";

    private int _quantidadeEspecies;
    public int QuantidadeEspecies { get => _quantidadeEspecies; private set => SetProperty(ref _quantidadeEspecies, value); }

    // ------------------------------------------------- Vazão e progresso

    private long _totalAvaliacoes;
    /// <summary>Indivíduos avaliados desde o início da execução. Cada avaliação roda <see cref="ParametrosTreinamento.EpisodiosPorAvaliacao"/> episódios completos, então este número cresce bem mais rápido que a geração.</summary>
    public long TotalAvaliacoes { get => _totalAvaliacoes; private set => SetProperty(ref _totalAvaliacoes, value); }

    private double _avaliacoesPorSegundo;
    public double AvaliacoesPorSegundo { get => _avaliacoesPorSegundo; private set => SetProperty(ref _avaliacoesPorSegundo, value); }

    private TimeSpan _tempoDecorrido;
    public TimeSpan TempoDecorrido
    {
        get => _tempoDecorrido;
        private set
        {
            if (SetProperty(ref _tempoDecorrido, value))
            {
                OnPropertyChanged(nameof(TempoDecorridoTexto));
                OnPropertyChanged(nameof(GeracoesPorMinuto));
            }
        }
    }

    public string TempoDecorridoTexto => TempoDecorrido.ToString(TempoDecorrido.TotalHours >= 1 ? @"h\:mm\:ss" : @"m\:ss");

    /// <summary>Gerações por minuto medidas no relógio de parede — complementa <see cref="AvaliacoesPorSegundo"/>, que é a vazão interna do framework.</summary>
    public double GeracoesPorMinuto =>
        TempoDecorrido.TotalMinutes > 0 ? _geracoesNestaSessao / TempoDecorrido.TotalMinutes : 0.0;

    private int _geracoesNestaSessao;

    private int _metaGeracoes;
    /// <summary>
    /// Meta de gerações desta execução, ou 0 para "sem meta". Existe porque o NEAT aqui não tem
    /// condição de parada própria (ver <see cref="EsquemaAvaliacaoAmbiente.TestForStopCondition"/>):
    /// sem um alvo declarado não existe "percentual concluído" honesto. Com meta definida, o
    /// treinamento para sozinho ao alcançá-la.
    /// </summary>
    public int MetaGeracoes
    {
        get => _metaGeracoes;
        set
        {
            if (SetProperty(ref _metaGeracoes, value))
            {
                OnPropertyChanged(nameof(TemMeta));
                OnPropertyChanged(nameof(ProgressoFracao));
                OnPropertyChanged(nameof(ProgressoTexto));
            }
        }
    }

    public bool TemMeta => _metaGeracoes > 0;

    /// <summary>Progresso até <see cref="MetaGeracoes"/>, de 0 a 1 — 0 quando não há meta.</summary>
    public double ProgressoFracao => TemMeta ? Math.Clamp(GeracaoAtual / (double)_metaGeracoes, 0.0, 1.0) : 0.0;

    public string ProgressoTexto => TemMeta
        ? $"{ProgressoFracao:P0} — geração {GeracaoAtual:N0} de {_metaGeracoes:N0}"
        : $"Sem meta definida — geração {GeracaoAtual:N0}";

    /// <summary>Avanço dentro do bloco de <see cref="TamanhoBlocoMarco"/> gerações que será gravado como marco — sempre disponível, mesmo sem meta.</summary>
    public double ProgressoNoBlocoFracao => (GeracaoAtual % TamanhoBlocoMarco) / (double)TamanhoBlocoMarco;

    public string ProgressoNoBlocoTexto =>
        $"{GeracaoAtual % TamanhoBlocoMarco} de {TamanhoBlocoMarco} gerações até o próximo marco gravado";

    // ------------------------------------------------- Arquitetura do campeão (resultado da evolução, não configuração)

    private int _ocultosDoCampeao;
    /// <summary>Neurônios ocultos do campeão. Com NEAT é RESULTADO da evolução — era fixo em 8 no algoritmo anterior.</summary>
    public int OcultosDoCampeao
    {
        get => _ocultosDoCampeao;
        private set
        {
            if (SetProperty(ref _ocultosDoCampeao, value)) OnPropertyChanged(nameof(NosDoCampeao));
        }
    }

    /// <summary>Total de nós do campeão (entradas + bias + ocultos + saídas) — exibido no resumo "Topologia (campeão)" da tela.</summary>
    public int NosDoCampeao => EntradasDaRede + 1 + OcultosDoCampeao + SaidasDaRede;

    private int _conexoesDiretasEntradaSaida;
    /// <summary>Conexões que pulam direto de uma entrada para uma saída, sem passar por oculto nenhum — impossíveis na rede em camadas anterior, comuns no NEAT.</summary>
    public int ConexoesDiretasEntradaSaida { get => _conexoesDiretasEntradaSaida; private set => SetProperty(ref _conexoesDiretasEntradaSaida, value); }

    private int _idDoCampeao;
    public int IdDoCampeao { get => _idDoCampeao; private set => SetProperty(ref _idDoCampeao, value); }

    private int _geracaoDeNascimentoDoCampeao;
    public int GeracaoDeNascimentoDoCampeao { get => _geracaoDeNascimentoDoCampeao; private set => SetProperty(ref _geracaoDeNascimentoDoCampeao, value); }

    /// <summary>Entradas reais da rede (sem o nó de bias) — ver <see cref="ControladorHibrido.QuantidadeEntradas"/>.</summary>
    public int EntradasDaRede => ControladorHibrido.QuantidadeEntradas;

    public int SaidasDaRede => ControladorHibrido.QuantidadeSaidas;

    public string FuncaoAtivacaoTexto => TreinadorNeat.FuncaoAtivacao.ToString();

    public string TopologiaTexto => TreinadorNeat.Aciclica ? "Acíclica (sem memória entre ativações)" : "Cíclica";

    private bool _ativacoesOcultasDisponiveis;
    /// <summary>
    /// Se o diagrama está conseguindo mostrar a ativação real dos neurônios ocultos (ver
    /// <see cref="AtivacoesRedeNeat"/>). Quando falso, os ocultos aparecem em cor neutra e a
    /// legenda avisa o motivo — em vez de exibir uma cor que não corresponde a valor nenhum.
    /// </summary>
    public bool AtivacoesOcultasDisponiveis { get => _ativacoesOcultasDisponiveis; private set => SetProperty(ref _ativacoesOcultasDisponiveis, value); }

    // ------------------------------------------------- Comportamento do campeão (episódio de verificação de cada geração)

    private int _alvosAbatidos;
    public int AlvosAbatidos { get => _alvosAbatidos; private set => SetProperty(ref _alvosAbatidos, value); }

    private int _alvosEscapados;
    public int AlvosEscapados { get => _alvosEscapados; private set => SetProperty(ref _alvosEscapados, value); }

    private int _alvosNascidos;
    public int AlvosNascidos { get => _alvosNascidos; private set => SetProperty(ref _alvosNascidos, value); }

    private int _tirosErrados;
    public int TirosErrados { get => _tirosErrados; private set => SetProperty(ref _tirosErrados, value); }

    private double? _taxaDeAcerto;
    /// <summary>Abates sobre tentativas de disparo — <c>null</c> quando o campeão não disparou nenhuma vez no episódio.</summary>
    public double? TaxaDeAcerto { get => _taxaDeAcerto; private set => SetProperty(ref _taxaDeAcerto, value); }

    private double? _taxaDeCobertura;
    /// <summary>Abates sobre alvos que apareceram — mede quanto da ameaça o controlador deu conta, não só a precisão do tiro.</summary>
    public double? TaxaDeCobertura { get => _taxaDeCobertura; private set => SetProperty(ref _taxaDeCobertura, value); }

    private int _oportunidadesDeDisparo;
    public int OportunidadesDeDisparo { get => _oportunidadesDeDisparo; private set => SetProperty(ref _oportunidadesDeDisparo, value); }

    private int _disparosRecusadosPelaRede;
    public int DisparosRecusadosPelaRede { get => _disparosRecusadosPelaRede; private set => SetProperty(ref _disparosRecusadosPelaRede, value); }

    private double? _taxaDeRecusa;
    /// <summary>
    /// Fração das oportunidades legítimas que a rede recusou — a heurística pura recusaria zero.
    /// É a medida direta da hesitação que motivou <see cref="ParametrosTreinamento.PenalidadeRecusaDisparo"/>,
    /// e até agora só aparecia no instantâneo gravado a cada 1.000 gerações.
    /// </summary>
    public double? TaxaDeRecusa { get => _taxaDeRecusa; private set => SetProperty(ref _taxaDeRecusa, value); }

    // ---------------------------------------------------------------- Comandos

    private void Iniciar()
    {
        if (EmExecucao) return;

        ParametrosTreinamento parametros = ParametrosAtuais;
        _ambiente = new AmbienteTreinamento(parametros);
        DesenharTorresFixas();
        LimparDiagramaRedeNeural();
        // Os eventos já na tela (desta sessão de app, de "Iniciar"s anteriores ou carregados do
        // banco ao abrir a tela) passam a pertencer a uma execução PASSADA a partir daqui — sem
        // isto, EhSessaoAtual ficaria com o valor computado na hora em que cada evento entrou na
        // coleção, e não seria reavaliado quando _execucaoId muda de novo.
        foreach (EventoTreinamento eventoAnterior in EventosExecucao) eventoAnterior.EhSessaoAtual = false;
        _execucaoId = Guid.NewGuid().ToString("N");
        _treinador = new TreinadorNeat(
            _ambiente,
            tamanhoPopulacao: parametros.TamanhoPopulacao,
            quantidadeEspecies: parametros.QuantidadeEspecies,
            tetoComplexidade: parametros.TetoComplexidade,
            probabilidadeAdicionarNo: parametros.ProbabilidadeAdicionarNo);

        ZerarIndicadoresDaSessao();
        _melhorGenomaAtual = null;
        _melhorRedeAtual = null;
        _episodioPreview = null;
        AlvosVisuais.Clear();
        MirasVisuais.Clear();

        TetoComplexidadeEmUso = parametros.TetoComplexidade;
        AtualizarParametrosEmUso(parametros, _treinador);

        RegistrarEvento(
            MetaGeracoes > 0
                ? $"Treinamento iniciado — população {parametros.TamanhoPopulacao}, {parametros.QuantidadeEspecies} espécies, teto de {parametros.TetoComplexidade} conexões, meta de {MetaGeracoes:N0} gerações."
                : $"Treinamento iniciado — população {parametros.TamanhoPopulacao}, {parametros.QuantidadeEspecies} espécies, teto de {parametros.TetoComplexidade} conexões, sem meta de gerações.",
            geracao: null,
            TipoEventoTreinamento.Informacao);

        _previewContinuo = true;
        _cts = new CancellationTokenSource();
        _pausaLiberada.Set();
        EmPausa = false;
        _cronometroSessao.Restart();
        _tarefaTreinamento = Task.Run(() => LacoDeTreinamento(_cts.Token));
        _timerPreview.Start();
        _timerEstado.Start();
        EmExecucao = true;
    }

    /// <summary>
    /// Segura o laço de treinamento ANTES da próxima geração (ver <see cref="_pausaLiberada"/>) —
    /// a prévia visual e o cronômetro continuam rodando normalmente, só a evolução em si pausa.
    /// </summary>
    private void Pausar()
    {
        if (!EmExecucao || EmPausa) return;

        _pausaLiberada.Reset();
        EmPausa = true;
        RegistrarEvento("Treinamento pausado pelo usuário.", GeracaoAtual, TipoEventoTreinamento.Informacao);
    }

    /// <summary>Libera o laço de treinamento para retomar a partir da próxima geração.</summary>
    private void Continuar()
    {
        if (!EmExecucao || !EmPausa) return;

        EmPausa = false;
        _pausaLiberada.Set();
        RegistrarEvento("Treinamento retomado pelo usuário.", GeracaoAtual, TipoEventoTreinamento.Informacao);
    }

    private void Parar()
    {
        if (!EmExecucao) return;

        _pausaLiberada.Set();
        EmPausa = false;
        _cts?.Cancel();
        _timerPreview.Stop();
        _timerEstado.Stop();
        _cronometroSessao.Stop();
        TempoDecorrido = _cronometroSessao.Elapsed;
        EmExecucao = false;

        RegistrarEvento(
            $"Treinamento parado pelo usuário após {_geracoesNestaSessao:N0} gerações em {TempoDecorridoTexto}.",
            GeracaoAtual,
            TipoEventoTreinamento.Informacao);
    }

    /// <summary>
    /// Zera tudo que é medido POR SESSÃO — contadores, série do gráfico, espécies e indicadores de
    /// aprendizado. Chamado em "Iniciar" e "Resetar" para que nenhum número da execução anterior
    /// sobreviva misturado ao da nova (era exatamente o que acontecia com o gráfico, alimentado
    /// pelo histórico persistido do banco).
    /// </summary>
    private void ZerarIndicadoresDaSessao()
    {
        GeracaoAtual = 0;
        MelhorFitness = 0;
        FitnessMedio = 0;
        MelhorFitnessDaSessao = 0;
        GeracaoDoMelhorFitness = 0;
        FitnessMedioExecucao = 0;
        TaxaDeAbateExecucao = null;
        FitnessMediaMovel = 0;
        VariacaoNaJanela = null;
        GeracoesSemCampeaoNovo = 0;
        ComplexidadeMelhor = 0;
        ComplexidadeMedia = 0;
        ComplexidadeMaxima = 0;
        QuantidadeEspecies = 0;
        Simplificando = false;
        TotalAvaliacoes = 0;
        AvaliacoesPorSegundo = 0;
        TempoDecorrido = TimeSpan.Zero;
        OcultosDoCampeao = 0;
        ConexoesDiretasEntradaSaida = 0;
        IdDoCampeao = 0;
        GeracaoDeNascimentoDoCampeao = 0;
        AtivacoesOcultasDisponiveis = false;
        AlvosAbatidos = 0;
        AlvosEscapados = 0;
        AlvosNascidos = 0;
        TirosErrados = 0;
        TaxaDeAcerto = null;
        TaxaDeCobertura = null;
        OportunidadesDeDisparo = 0;
        DisparosRecusadosPelaRede = 0;
        TaxaDeRecusa = null;
        Falhou = false;

        _somaFitnessMedioBloco = 0;
        _melhorFitnessBloco = 0;
        _contagemGeracoesNoBloco = 0;
        _somaFitnessMedioPonderadaExecucao = 0;
        _somaIndividuosAvaliadosExecucao = 0;
        _totalAlvosAbatidosExecucao = 0;
        _totalAlvosEscapadosExecucao = 0;
        _geracoesNestaSessao = 0;
        _cronometroSessao.Reset();

        _ordemDoOcultoPorId.Clear();
        _ocultosDaRemontagemAnterior.Clear();
        _conexoesDaRemontagemAnterior.Clear();

        HistoricoDaSessao.Clear();
        Especies.Clear();
    }

    /// <summary>
    /// Para o treinamento (se estiver rodando) e volta tudo ao estado inicial: geração, fitness,
    /// genoma, diagrama da rede, visualizações no Canvas e mensagem de status. NÃO apaga o
    /// histórico já gravado em <see cref="IHistoricoTreinamentoRepository"/> — é um reinício da
    /// sessão atual na tela, não uma limpeza de dados persistidos.
    /// </summary>
    private void Resetar()
    {
        _pausaLiberada.Set();
        EmPausa = false;
        _cts?.Cancel();
        _timerPreview.Stop();
        _timerEstado.Stop();
        EmExecucao = false;

        ParametrosTreinamento parametros = ParametrosAtuais;
        _ambiente = new AmbienteTreinamento(parametros);
        _treinador = null;
        _melhorGenomaAtual = null;
        _melhorRedeAtual = null;
        _episodioPreview = null;
        _previewContinuo = true;

        ZerarIndicadoresDaSessao();
        TetoComplexidadeEmUso = parametros.TetoComplexidade;
        AtualizarParametrosEmUso(parametros, treinador: null);

        // O histórico de execução NÃO é apagado aqui, pelo mesmo princípio já aplicado ao
        // histórico por geração: "Resetar" reinicia a sessão na tela, não apaga dados gravados.
        // Apagar de vez é ação explícita da tela Configurações → Resetar treinamento da IA.
        RegistrarEvento("Treinamento reiniciado — geração, fitness e genoma voltaram ao zero.", geracao: null, TipoEventoTreinamento.Informacao);

        DesenharTorresFixas();
        LimparDiagramaRedeNeural();
        AlvosVisuais.Clear();
        MirasVisuais.Clear();

        MostrarStatus("Treinamento reiniciado — geração, fitness e genoma voltaram ao zero.", sucesso: true);
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

        if (!GarantirRedeParaPrevia()) return;

        _previewContinuo = false;
        _episodioPreview = _ambiente.NovoEpisodio();
        AlvosVisuais.Clear();
        MirasVisuais.Clear();
        _timerPreview.Start();
    }

    /// <summary>
    /// Igual a <see cref="RodarEpisodioUnico"/>, mas força o caso tático escolhido em
    /// <see cref="CenarioOpcaoSelecionada"/> (ver <see cref="CatalogoCenarios"/>) em vez do
    /// sorteio padrão — para inspecionar isoladamente como o melhor genoma atual se comporta
    /// numa situação específica (enxame, disputa de torre, zona morta etc.).
    /// </summary>
    private void SimularCenarioSelecionado()
    {
        if (EmExecucao) return;

        if (!GarantirRedeParaPrevia()) return;

        _previewContinuo = false;
        _episodioPreview = _ambiente.NovoEpisodio(cenario: CenarioOpcaoSelecionada.Tipo);
        AlvosVisuais.Clear();
        MirasVisuais.Clear();
        _timerPreview.Start();
    }

    private void RemoverSpawnPersonalizado(object? parametro)
    {
        if (parametro is SpawnPersonalizadoEditavel linha) SpawnsPersonalizados.Remove(linha);
    }

    /// <summary>
    /// Roda, de forma visível, exatamente o cenário desenhado à mão em
    /// <see cref="SpawnsPersonalizados"/> — nenhuma posição ou velocidade é sorteada; cada linha
    /// vira um nascimento (ver <see cref="NeuralEvolution.AmbienteTreinamento.NovoEpisodioComSpawnsPersonalizados"/>),
    /// na direção da base (mesma convenção dos cenários catalogados em <see cref="CatalogoCenarios"/>).
    /// </summary>
    private void SimularCenarioPersonalizado()
    {
        if (EmExecucao) return;

        if (SpawnsPersonalizados.Count == 0)
        {
            MostrarStatus("Adicione pelo menos um alvo ao cenário personalizado antes de simular.", sucesso: false);
            return;
        }

        List<SpawnScriptado> spawns = SpawnsPersonalizados
            .OrderBy(linha => linha.InstanteSegundos)
            .Select(linha =>
            {
                double raioNascimento = Math.Sqrt(linha.X * linha.X + linha.Y * linha.Y);
                double anguloGraus = Math.Atan2(linha.Y, linha.X) * 180.0 / Math.PI;
                double fracaoRaio = raioNascimento / _ambiente.Parametros.RaioMundoMetros;

                return new SpawnScriptado(
                    InstanteSegundos: Math.Max(0, linha.InstanteSegundos),
                    AnguloNascimentoGraus: anguloGraus,
                    VelocidadeMetrosPorSegundo: Math.Max(0, linha.VelocidadeMetrosPorSegundo),
                    CertezaBase: 0.5,
                    TaxaCrescimentoCertezaPorSegundo: 0.15,
                    FracaoRaioNascimento: fracaoRaio,
                    AlturaMetros: Math.Clamp(linha.AlturaMetros, 0.0, _ambiente.Parametros.AlturaMaximaAlvoMetros));
            })
            .ToList();

        if (!GarantirRedeParaPrevia()) return;

        _previewContinuo = false;
        _episodioPreview = _ambiente.NovoEpisodioComSpawnsPersonalizados(spawns);
        AlvosVisuais.Clear();
        MirasVisuais.Clear();
        MostrarStatus($"Simulando cenário personalizado ({spawns.Count} alvo(s)).", sucesso: true);
        _timerPreview.Start();
        CenarioPersonalizadoIniciado?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Garante que há uma rede para a prévia animar. Antes bastava sortear um vetor de pesos da
    /// arquitetura fixa; com NEAT um genoma só existe dentro de uma população (ele carrega ids
    /// de inovação, espécie e histórico), então a única forma honesta de obter um é pedir ao
    /// próprio framework: rodamos UMA geração, que cria e avalia a população inicial.
    /// </summary>
    /// <returns><c>false</c> (com status na tela) se não foi possível — nesse caso não há o que animar.</returns>
    private bool GarantirRedeParaPrevia()
    {
        if (_melhorRedeAtual is not null) return true;

        try
        {
            _treinador ??= new TreinadorNeat(
                _ambiente,
                tamanhoPopulacao: ParametrosAtuais.TamanhoPopulacao,
                quantidadeEspecies: ParametrosAtuais.QuantidadeEspecies,
                tetoComplexidade: ParametrosAtuais.TetoComplexidade,
                probabilidadeAdicionarNo: ParametrosAtuais.ProbabilidadeAdicionarNo);

            int[] seeds = Enumerable.Range(0, Math.Max(1, _ambiente.Parametros.EpisodiosPorAvaliacao)).ToArray();
            ResultadoGeracao inicial = _treinador.EvoluirGeracao(seeds, CatalogoCenarios.TodosComScript);

            AdotarCampeao(inicial.MelhorGenoma);

            RunOnUi(() =>
            {
                GeracaoAtual = inicial.Geracao;
                MelhorFitness = inicial.MelhorFitness;
                FitnessMedio = inicial.FitnessMedio;
                ComplexidadeMelhor = inicial.ComplexidadeMelhor;
                ComplexidadeMedia = inicial.ComplexidadeMedia;
                ComplexidadeMaxima = inicial.ComplexidadeMaxima;
                QuantidadeEspecies = inicial.QuantidadeEspecies;
                Simplificando = inicial.Simplificando;
                OcultosDoCampeao = inicial.NeuroniosOcultosDoCampeao;
                IdDoCampeao = inicial.MelhorGenoma.Id;
                GeracaoDeNascimentoDoCampeao = inicial.GeracaoDeNascimentoDoCampeao;
                TetoComplexidadeEmUso = _ambiente.Parametros.TetoComplexidade;
                AtualizarEspecies(inicial);
                AtualizarParametrosEmUso(_ambiente.Parametros, _treinador);
            });

            RegistrarEvento(
                "População inicial criada para a prévia — uma geração foi avaliada para obter um campeão (um genoma NEAT só existe dentro de uma população).",
                inicial.Geracao,
                TipoEventoTreinamento.Informacao);

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível criar a população inicial para a prévia: {ex.Message}");
            MostrarStatus($"Não foi possível criar a população inicial: {ex.Message}", sucesso: false);
            RegistrarEvento($"Não foi possível criar a população inicial: {ex.Message}", geracao: null, TipoEventoTreinamento.Falha);
            return false;
        }
    }

    /// <summary>
    /// Guarda o campeão da geração e decodifica a rede. O diagrama e as regras só são refeitos
    /// quando <paramref name="remontarDiagrama"/> permite (ver
    /// <see cref="IntervaloMinimoRemontagemDiagrama"/>) — a decodificação em si é barata e precisa
    /// acontecer sempre, porque a prévia animada e a exportação usam esta rede.
    /// </summary>
    private void AdotarCampeao(NeatGenome<double> genoma, bool remontarDiagrama = true)
    {
        _melhorGenomaAtual = genoma;
        _melhorRedeAtual = _treinador!.Decodificar(genoma);

        if (!remontarDiagrama) return;

        RemontarDiagramaRedeNeural(genoma, _melhorRedeAtual);
    }

    /// <summary>Consome a janela do <i>throttle</i> do diagrama: devolve <c>true</c> (e reinicia a contagem) só quando já passou tempo suficiente desde a última remontagem.</summary>
    private bool PodeRemontarDiagrama()
    {
        if (_cronometroDiagrama.Elapsed < IntervaloMinimoRemontagemDiagrama) return false;

        _cronometroDiagrama.Restart();
        return true;
    }

    /// <summary>
    /// Grava o campeão atual no formato <c>.net</c> do próprio SharpNEAT (lista de conexões +
    /// funções de ativação) — e não mais num JSON com arquitetura + vetor de pesos, que não
    /// representa uma topologia evoluída. É este arquivo que o target MSBuild
    /// <c>EmbutirRedeTreinada</c> procura para embutir no executável (ver
    /// <see cref="Services.ControladorTreinadoProvider"/>).
    /// </summary>
    private void ExportarMelhorGenoma()
    {
        if (_melhorGenomaAtual is null) return;

        try
        {
            AppDataPaths.EnsureTreinamentoFolderExists();
            string caminho = Path.Combine(AppDataPaths.TreinamentoFolder, $"rede-geracao-{GeracaoAtual}.net");

            NeatGenomeSaver.Save(_melhorGenomaAtual, caminho);

            _logger.Success($"Rede exportada: {caminho}");
            MostrarStatus($"Rede exportada com sucesso! ({Path.GetFileName(caminho)})", sucesso: true);
            RegistrarEvento(
                $"Rede exportada ({Path.GetFileName(caminho)}) — {ComplexidadeMelhor:0} conexões, {OcultosDoCampeao} ocultos, fitness {MelhorFitness:0.0}.",
                GeracaoAtual,
                TipoEventoTreinamento.Informacao);
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível exportar a rede: {ex.Message}");
            MostrarStatus($"Não foi possível exportar a rede: {ex.Message}", sucesso: false);
            RegistrarEvento($"Não foi possível exportar a rede: {ex.Message}", GeracaoAtual, TipoEventoTreinamento.Falha);
        }
    }

    // ---------------------------------------------------------------- Laço de treinamento (thread de fundo)

    /// <summary>
    /// Envelope do laço: qualquer exceção aqui matava a thread de fundo em silêncio, deixando a
    /// tela anunciando "treinando" com os números congelados. Agora a falha é registrada no
    /// histórico de execução, aparece no selo de estado e libera os botões.
    /// </summary>
    private void LacoDeTreinamento(CancellationToken token)
    {
        try
        {
            Evoluir(token);
        }
        catch (OperationCanceledException)
        {
            // Parada pedida pelo usuário — nada a relatar.
        }
        catch (Exception ex)
        {
            _logger.Error($"O treinamento foi interrompido por um erro: {ex}");

            RunOnUi(() =>
            {
                Falhou = true;
                EmExecucao = false;
                _timerPreview.Stop();
                _timerEstado.Stop();
                _cronometroSessao.Stop();
                MostrarStatus($"O treinamento parou por um erro: {ex.Message}", sucesso: false);
                RegistrarEvento($"Treinamento interrompido por erro: {ex.Message}", GeracaoAtual, TipoEventoTreinamento.Falha);
            });
        }
    }

    private void Evoluir(CancellationToken token)
    {
        TreinadorNeat algoritmo = _treinador!;

        while (!token.IsCancellationRequested)
        {
            // Bloqueia aqui — ANTES de começar a próxima geração, nunca no meio de uma — enquanto
            // EmPausa estiver ligado (ver Pausar/Continuar). Lança OperationCanceledException se
            // "Parar" cancelar o token enquanto pausado, tratada pelo catch em LacoDeTreinamento.
            _pausaLiberada.Wait(token);

            // MESMAS seeds para todos os indivíduos desta geração: sem isso, cada genoma
            // enfrentava um episódio sorteado diferente (spawn/velocidade/certeza), e o fitness
            // comparava sorte, não qualidade do genoma — seleção quase aleatória, média sem
            // evoluir de verdade. Mais de uma seed por geração (ver EpisodiosPorAvaliacao)
            // reduz ainda mais esse ruído: um único cenário por geração continuava deixando
            // margem para um genoma "sortudo" vencer um melhor só por calhar de um cenário mais
            // fácil. As seeds mudam de geração para geração para a rede não ficar especializada
            // num punhado fixo de cenários.
            int[] seedsDaGeracao = Enumerable.Range(0, Math.Max(1, _ambiente.Parametros.EpisodiosPorAvaliacao))
                .Select(i => algoritmo.GeracaoAtual * 1000 + i)
                .ToArray();
            // Além dos cenários aleatórios acima, toda geração também avalia TODOS os casos
            // táticos mapeados (ver CatalogoCenarios) — a IA não pode ignorar um caso difícil só
            // porque o sorteio raramente o produz.
            ResultadoGeracao resultado = algoritmo.EvoluirGeracao(seedsDaGeracao, CatalogoCenarios.TodosComScript);

            // O diagrama e as regras são custosos de remontar e só valem por geração exibida —
            // ver IntervaloMinimoRemontagemDiagrama. O campeão em si é sempre adotado, porque a
            // prévia animada e a exportação dependem dele.
            AdotarCampeao(resultado.MelhorGenoma, remontarDiagrama: PodeRemontarDiagrama());

            // Reavalia o melhor genoma uma vez mais só para capturar como ele se comportou
            // (abatidos/escapados/recusas) — o fitness em si já saiu de dentro da evolução, mas
            // esses números não são devolvidos por ela.
            EpisodioResultado comportamento = _ambiente.ExecutarEpisodioRapido(_melhorRedeAtual!, seed: resultado.Geracao);
            RegistrarHistorico(resultado, comportamento);
            RegistrarMarcoFitnessSeCompletouBloco(resultado);
            RegistrarInstantaneoDeComportamentoSeCompletouBloco(resultado, comportamento);

            RunOnUi(() => AplicarResultadoNaTela(resultado, comportamento));

            if (MetaGeracoes > 0 && resultado.Geracao >= MetaGeracoes)
            {
                RunOnUi(() => ConcluirPorMeta(resultado.Geracao));
                return;
            }
        }
    }

    /// <summary>Publica na tela tudo o que a geração produziu — roda na thread de UI.</summary>
    private void AplicarResultadoNaTela(ResultadoGeracao resultado, EpisodioResultado comportamento)
    {
        bool fasesTrocaram = Simplificando != resultado.Simplificando;

        GeracaoAtual = resultado.Geracao;
        MelhorFitness = resultado.MelhorFitness;
        FitnessMedio = resultado.FitnessMedio;
        ComplexidadeMelhor = resultado.ComplexidadeMelhor;
        ComplexidadeMedia = resultado.ComplexidadeMedia;
        ComplexidadeMaxima = resultado.ComplexidadeMaxima;
        QuantidadeEspecies = resultado.QuantidadeEspecies;
        Simplificando = resultado.Simplificando;
        TotalAvaliacoes = resultado.TotalAvaliacoes;
        AvaliacoesPorSegundo = resultado.AvaliacoesPorSegundo;
        OcultosDoCampeao = resultado.NeuroniosOcultosDoCampeao;
        IdDoCampeao = resultado.MelhorGenoma.Id;
        GeracaoDeNascimentoDoCampeao = resultado.GeracaoDeNascimentoDoCampeao;
        GeracoesSemCampeaoNovo = resultado.GeracoesSemCampeaoNovo;

        AlvosAbatidos = comportamento.AlvosAbatidos;
        AlvosEscapados = comportamento.AlvosEscapados;
        AlvosNascidos = comportamento.AlvosNascidos;
        TirosErrados = comportamento.TirosErrados;
        OportunidadesDeDisparo = comportamento.OportunidadesDeDisparo;
        DisparosRecusadosPelaRede = comportamento.DisparosRecusadosPelaRede;

        int tentativas = comportamento.AlvosAbatidos + comportamento.TirosErrados;
        TaxaDeAcerto = tentativas > 0 ? comportamento.AlvosAbatidos / (double)tentativas : null;

        int apareceram = comportamento.AlvosAbatidos + comportamento.AlvosEscapados;
        TaxaDeCobertura = apareceram > 0 ? comportamento.AlvosAbatidos / (double)apareceram : null;

        TaxaDeRecusa = comportamento.OportunidadesDeDisparo > 0
            ? comportamento.DisparosRecusadosPelaRede / (double)comportamento.OportunidadesDeDisparo
            : null;

        AtualizarIndicadoresDaExecucaoCompleta(resultado, comportamento);

        _geracoesNestaSessao++;
        TempoDecorrido = _cronometroSessao.Elapsed;

        AtualizarEspecies(resultado);
        AtualizarIndicadoresDeAprendizado(resultado);

        if (fasesTrocaram)
        {
            RegistrarEvento(
                resultado.Simplificando
                    ? $"A evolução passou a SIMPLIFICAR — a complexidade média ({resultado.ComplexidadeMedia:0.0} conexões) ultrapassou o teto de {TetoComplexidadeEmUso}."
                    : "A evolução voltou a COMPLEXIFICAR — a rede pode ganhar neurônios e conexões novamente.",
                resultado.Geracao,
                TipoEventoTreinamento.Atencao);
        }
    }

    /// <summary>
    /// Acumula esta geração nos contadores de EXECUÇÃO INTEIRA (ver os campos declarados logo
    /// acima de <see cref="FitnessMedioExecucao"/>) e republica <see cref="FitnessMedioExecucao"/>
    /// e <see cref="TaxaDeAbateExecucao"/> — diferente de <see cref="AtualizarIndicadoresDeAprendizado"/>,
    /// que só olha a JANELA de <see cref="HistoricoDaSessao"/>. Roda na thread de UI (chamado de
    /// dentro de <see cref="AplicarResultadoNaTela"/>).
    /// </summary>
    private void AtualizarIndicadoresDaExecucaoCompleta(ResultadoGeracao resultado, EpisodioResultado comportamento)
    {
        int populacaoDestaGeracao = Math.Max(1, _ambiente.Parametros.TamanhoPopulacao);

        _somaFitnessMedioPonderadaExecucao += resultado.FitnessMedio * populacaoDestaGeracao;
        _somaIndividuosAvaliadosExecucao += populacaoDestaGeracao;
        FitnessMedioExecucao = _somaIndividuosAvaliadosExecucao > 0
            ? _somaFitnessMedioPonderadaExecucao / _somaIndividuosAvaliadosExecucao
            : 0.0;

        _totalAlvosAbatidosExecucao += comportamento.AlvosAbatidos;
        _totalAlvosEscapadosExecucao += comportamento.AlvosEscapados;
        long totalFinalizadosExecucao = _totalAlvosAbatidosExecucao + _totalAlvosEscapadosExecucao;
        TaxaDeAbateExecucao = totalFinalizadosExecucao > 0
            ? _totalAlvosAbatidosExecucao / (double)totalFinalizadosExecucao
            : null;
    }

    /// <summary>Para o treinamento por ter alcançado <see cref="MetaGeracoes"/> — roda na thread de UI.</summary>
    private void ConcluirPorMeta(int geracao)
    {
        _cts?.Cancel();
        _timerEstado.Stop();
        _cronometroSessao.Stop();
        TempoDecorrido = _cronometroSessao.Elapsed;
        EmExecucao = false;

        MostrarStatus($"Meta alcançada: {geracao:N0} gerações. Melhor fitness da sessão: {MelhorFitnessDaSessao:0.0}.", sucesso: true);
        RegistrarEvento(
            $"Meta de {MetaGeracoes:N0} gerações alcançada em {TempoDecorridoTexto} — melhor fitness da sessão {MelhorFitnessDaSessao:0.0} (geração {GeracaoDoMelhorFitness:N0}).",
            geracao,
            TipoEventoTreinamento.Avanco);
    }

    /// <summary>
    /// Atualiza melhor da sessão, média móvel e variação na janela a partir da série desta sessão
    /// (<see cref="HistoricoDaSessao"/>, alimentada em <see cref="RegistrarHistorico"/>). Roda na
    /// thread de UI.
    /// </summary>
    private void AtualizarIndicadoresDeAprendizado(ResultadoGeracao resultado)
    {
        if (GeracaoDoMelhorFitness == 0 || resultado.MelhorFitness > MelhorFitnessDaSessao)
        {
            MelhorFitnessDaSessao = resultado.MelhorFitness;
            GeracaoDoMelhorFitness = resultado.Geracao;

            // Só vira evento depois da primeira geração DESTA sessão: o "recorde" inicial é apenas
            // o ponto de partida, não um avanço, e anunciá-lo encheria o histórico de ruído.
            // Contado pela sessão, não pelo número da geração, porque a numeração do framework
            // começa em 0 (ver TreinadorNeat.EvoluirGeracao).
            if (_geracoesNestaSessao > 1)
            {
                RegistrarEvento($"Novo melhor fitness da sessão: {resultado.MelhorFitness:0.0}.", resultado.Geracao, TipoEventoTreinamento.Avanco);
            }
        }

        int total = HistoricoDaSessao.Count;
        if (total == 0) return;

        int tamanhoJanela = Math.Min(JanelaIndicadoresAprendizado, total);
        double somaRecente = 0;
        for (int i = total - tamanhoJanela; i < total; i++) somaRecente += HistoricoDaSessao[i].MelhorFitness;
        double mediaRecente = somaRecente / tamanhoJanela;
        FitnessMediaMovel = mediaRecente;

        // A comparação só existe quando há duas janelas inteiras para comparar — com menos que
        // isso, qualquer número seria média de amostras de tamanhos diferentes.
        if (total < JanelaIndicadoresAprendizado * 2)
        {
            VariacaoNaJanela = null;
            return;
        }

        double somaAnterior = 0;
        int inicioAnterior = total - (JanelaIndicadoresAprendizado * 2);
        for (int i = inicioAnterior; i < inicioAnterior + JanelaIndicadoresAprendizado; i++) somaAnterior += HistoricoDaSessao[i].MelhorFitness;

        VariacaoNaJanela = mediaRecente - (somaAnterior / JanelaIndicadoresAprendizado);
    }

    /// <summary>Largura máxima, em pixels, da barra de tamanho de espécie no card (ver <see cref="LinhaEspecieTreinamento"/>).</summary>
    private const double LarguraMaximaBarraEspeciePx = 150.0;

    /// <summary>Republica as espécies da geração. Roda na thread de UI.</summary>
    private void AtualizarEspecies(ResultadoGeracao resultado)
    {
        Especies.Clear();
        if (resultado.Especies is null) return;

        int maiorTamanho = 0;
        foreach (ResumoEspecie especie in resultado.Especies) maiorTamanho = Math.Max(maiorTamanho, especie.Tamanho);

        foreach (ResumoEspecie especie in resultado.Especies)
        {
            Especies.Add(LinhaEspecieTreinamento.De(especie, maiorTamanho, LarguraMaximaBarraEspeciePx));
        }
    }

    /// <summary>Limite de linhas guardadas no histórico de execução — o suficiente para rolar e entender a sessão sem crescer sem limite numa execução de horas.</summary>
    private const int LimiteEventosExecucao = 200;

    /// <summary>
    /// Grava o evento (ver <see cref="IEventoTreinamentoRepository"/>) e o acrescenta ao topo do
    /// histórico de execução na tela.
    /// </summary>
    /// <remarks>
    /// A linha aparece na tela mesmo que a gravação falhe: o acontecimento é real, e esconder um
    /// erro de persistência justamente na hora de relatar um problema seria o pior momento
    /// possível para a tela ficar em silêncio. A falha de gravação em si vai para o
    /// <see cref="ILoggingService"/> — e não vira outro evento, para um banco indisponível não
    /// gerar uma cascata de eventos sobre não conseguir gravar eventos.
    /// </remarks>
    private void RegistrarEvento(string mensagem, int? geracao, TipoEventoTreinamento tipo)
    {
        // ExecucaoId/EhSessaoAtual: todo evento registrado AGORA pertence, por definição, à
        // sessão em curso (mesmo Guid usado em MarcoFitnessTreinamento/InstantaneoComportamento
        // para esta execução — ver _execucaoId). Eventos de execuções passadas só chegam via
        // CarregarEventos, que marca EhSessaoAtual comparando o ExecucaoId lido do banco.
        var evento = new EventoTreinamento { Mensagem = mensagem, Geracao = geracao, Tipo = tipo, ExecucaoId = _execucaoId, EhSessaoAtual = true };

        try
        {
            _eventoRepository.Registrar(evento);
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível gravar o evento de treinamento: {ex.Message}");
        }

        RunOnUi(() =>
        {
            EventosExecucao.Insert(0, evento);
            while (EventosExecucao.Count > LimiteEventosExecucao) EventosExecucao.RemoveAt(EventosExecucao.Count - 1);
        });
    }

    /// <summary>
    /// Monta o card "parâmetros em uso". <paramref name="treinador"/> é <c>null</c> antes do
    /// primeiro "Iniciar" — nesse caso elitismo e paralelismo mostram o padrão que SERÁ aplicado,
    /// não um valor em vigor.
    /// </summary>
    private void AtualizarParametrosEmUso(ParametrosTreinamento parametros, TreinadorNeat? treinador)
    {
        ParametrosEmUso.Clear();

        void Adicionar(string nome, string valor, string explicacao) =>
            ParametrosEmUso.Add(new ParametroEmUso(nome, valor, explicacao));

        Adicionar("População", parametros.TamanhoPopulacao.ToString("N0"),
            "Indivíduos avaliados por geração.");
        Adicionar("Espécies", parametros.QuantidadeEspecies.ToString("N0"),
            "Em quantas espécies a população é dividida. Protege uma topologia recém-criada de competir de igual para igual com redes já otimizadas.");
        Adicionar("Teto de complexidade", $"{parametros.TetoComplexidade:N0} conexões",
            "Acima deste número de conexões a evolução passa a simplificar em vez de crescer.");
        Adicionar("Mutação estrutural", parametros.ProbabilidadeAdicionarNo.ToString("P1"),
            "Probabilidade de um descendente ganhar um neurônio novo. Com NEAT, mutação de peso e mutação de estrutura são eventos distintos — este é o segundo.");
        Adicionar("Episódios por avaliação", parametros.EpisodiosPorAvaliacao.ToString("N0"),
            "Quantos episódios independentes são rodados e cuja média vira o fitness de um indivíduo. Reduz o ruído de sorteio do ambiente.");
        Adicionar("Casos táticos fixos", CatalogoCenarios.TodosComScript.Count.ToString("N0"),
            "Cenários roteirizados avaliados em TODA geração, além dos episódios sorteados.");
        Adicionar("Elitismo", (treinador?.ProporcaoElitismo ?? 0.05).ToString("P0"),
            "Fração dos melhores de cada espécie preservada sem alteração de uma geração para a outra.");
        Adicionar("Paralelismo", treinador is null or { GrauParalelismo: -1 } ? "todos os núcleos" : treinador.GrauParalelismo.ToString("N0"),
            "Quantos indivíduos são avaliados ao mesmo tempo.");
        Adicionar("Rede", $"{ControladorHibrido.QuantidadeEntradas} entradas + bias → {ControladorHibrido.QuantidadeSaidas} saídas",
            "Entradas e saídas são fixas (é o contrato com o controlador); tudo entre elas é evoluído.");
        Adicionar("Ativação", $"{TreinadorNeat.FuncaoAtivacao} · {(TreinadorNeat.Aciclica ? "acíclica" : "cíclica")}",
            "TanH mantém as saídas entre -1 e 1, a faixa que o controlador híbrido assume. Acíclica = sem memória entre ativações.");
        Adicionar("Máx. alvos por quadrante", parametros.MaxAlvosPorQuadrante.ToString("N0"),
            "Teto de alvos ativos simultâneos no mesmo quadrante.");
        Adicionar("Alcance de mira", $"{parametros.DistanciaEngajamentoMetros:0.##} m",
            "Vem de Configurações → Radar e mira. Vale a partir do próximo Iniciar/Resetar.");
        Adicionar("Certeza mínima", parametros.LimiarConfiancaDisparoFracao.ToString("P0"),
            "Vem de Configurações → Disparo. Veto inegociável: abaixo disso nenhum disparo é autorizado, decida a rede o que decidir.");
        Adicionar("Duração do episódio", parametros.SemLimiteDeDuracao ? "sem limite" : $"{parametros.DuracaoEpisodioSegundos:0.#} s",
            "Tempo simulado de cada episódio de avaliação.");
    }

    /// <summary>
    /// Acumula o fitness médio desta geração no bloco de 500 em andamento e, ao completar o
    /// bloco (geração múltipla de <see cref="TamanhoBlocoMarco"/>), grava um marco em
    /// <see cref="IMarcoFitnessTreinamentoRepository"/> com a média do bloco — não da geração
    /// mais recente só, para o painel de longo prazo mostrar a tendência, não ruído geração a
    /// geração. Acumulador em memória (zera a cada marco e também em Iniciar/Resetar): assim
    /// como <see cref="TreinadorNeat.GeracaoAtual"/>, não sobrevive a um reinício do
    /// aplicativo no meio de um bloco — mesma limitação já aceita pelo histórico de 300 linhas.
    /// </summary>
    private void RegistrarMarcoFitnessSeCompletouBloco(ResultadoGeracao resultado)
    {
        _somaFitnessMedioBloco += resultado.FitnessMedio;
        _melhorFitnessBloco = Math.Max(_melhorFitnessBloco, resultado.MelhorFitness);
        _contagemGeracoesNoBloco++;

        // Geração 0 é a população INICIAL (ver TreinadorNeat.EvoluirGeracao) e também satisfaz
        // "múltiplo de 500" — sem esta guarda, todo "Iniciar" gravava na hora um marco de um
        // bloco de uma única geração, poluindo o painel de longo prazo com um ponto que não
        // representa 500 gerações de nada.
        if (resultado.Geracao <= 0 || resultado.Geracao % TamanhoBlocoMarco != 0) return;

        var marco = new MarcoFitnessTreinamento
        {
            GeracaoMarco = resultado.Geracao,
            FitnessMedioDoBloco = _somaFitnessMedioBloco / Math.Max(1, _contagemGeracoesNoBloco),
            MelhorFitnessDoBloco = _melhorFitnessBloco,
            DataHora = DateTime.Now,
            ExecucaoId = _execucaoId
        };

        try
        {
            _marcoFitnessRepository.Registrar(marco);
            _logger.Success($"Marco de treinamento registrado: geração {marco.GeracaoMarco}, fitness médio do bloco {marco.FitnessMedioDoBloco:0.0}.");
            RegistrarEvento(
                $"Marco gravado — fitness médio do bloco {marco.FitnessMedioDoBloco:0.0}, melhor do bloco {marco.MelhorFitnessDoBloco:0.0}.",
                marco.GeracaoMarco,
                TipoEventoTreinamento.Informacao);
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível registrar o marco de fitness da geração {marco.GeracaoMarco}: {ex.Message}");
            RegistrarEvento($"Não foi possível gravar o marco de fitness: {ex.Message}", marco.GeracaoMarco, TipoEventoTreinamento.Falha);
        }

        _somaFitnessMedioBloco = 0;
        _melhorFitnessBloco = 0;
        _contagemGeracoesNoBloco = 0;
    }

    /// <summary>
    /// A cada <see cref="TamanhoBlocoInstantaneo"/> gerações, fotografa o comportamento completo
    /// do melhor genoma ATUAL (decisões/acertos/erros/genoma inteiro) e persiste — nunca
    /// sobrescrito por gerações seguintes (ver <see cref="IInstantaneoComportamentoRepository"/>),
    /// então dá para comparar marco a marco depois e verificar se a evolução é real: se a taxa de
    /// acerto/cobertura não melhora de um marco para o outro, é sinal de estagnação de verdade,
    /// não só impressão.
    /// </summary>
    private void RegistrarInstantaneoDeComportamentoSeCompletouBloco(ResultadoGeracao resultado, EpisodioResultado comportamento)
    {
        // Mesma guarda do marco: a geração 0 (população inicial) é múltipla de qualquer bloco.
        if (resultado.Geracao <= 0 || resultado.Geracao % TamanhoBlocoInstantaneo != 0) return;

        int tentativasDeDisparo = comportamento.AlvosAbatidos + comportamento.TirosErrados;
        int alvosQueApareceram = comportamento.AlvosAbatidos + comportamento.AlvosEscapados;

        var instantaneo = new InstantaneoComportamentoMelhorGene
        {
            GeracaoMarco = resultado.Geracao,
            Fitness = resultado.MelhorFitness,
            AlvosAbatidos = comportamento.AlvosAbatidos,
            AlvosEscapados = comportamento.AlvosEscapados,
            TirosErrados = comportamento.TirosErrados,
            TaxaDeAcerto = tentativasDeDisparo > 0 ? (double)comportamento.AlvosAbatidos / tentativasDeDisparo : null,
            TaxaDeCobertura = alvosQueApareceram > 0 ? (double)comportamento.AlvosAbatidos / alvosQueApareceram : null,
            // O grafo NEAT não é serializável como objeto simples (carrega ids de inovação e
            // referências à população); o formato .net do framework é a representação canônica
            // da rede, então é ele que guardamos — em texto, na mesma coluna.
            GenomaJson = SerializarRede(resultado.MelhorGenoma),
            DataHora = DateTime.Now,
            ExecucaoId = _execucaoId
        };

        try
        {
            _instantaneoRepository.Registrar(instantaneo);
            _logger.Success(
                $"Instantâneo de comportamento registrado: geração {instantaneo.GeracaoMarco}, " +
                $"acerto {instantaneo.TaxaDeAcerto:P0}, cobertura {instantaneo.TaxaDeCobertura:P0}.");
            RegistrarEvento(
                $"Instantâneo de comportamento gravado — acerto {instantaneo.TaxaDeAcerto:P0}, cobertura {instantaneo.TaxaDeCobertura:P0}.",
                instantaneo.GeracaoMarco,
                TipoEventoTreinamento.Informacao);
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível registrar o instantâneo de comportamento da geração {instantaneo.GeracaoMarco}: {ex.Message}");
            RegistrarEvento($"Não foi possível gravar o instantâneo de comportamento: {ex.Message}", instantaneo.GeracaoMarco, TipoEventoTreinamento.Falha);
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
            ComplexidadeMelhor = resultado.ComplexidadeMelhor,
            QuantidadeEspecies = resultado.QuantidadeEspecies,
            DataHora = DateTime.Now
        };

        bool gravou = true;

        try
        {
            _historicoRepository.Registrar(registro);
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível registrar o histórico de treinamento: {ex.Message}");
            gravou = false;
        }

        // A série da sessão (que alimenta o gráfico e os indicadores de aprendizado) é atualizada
        // mesmo que a gravação no banco falhe: o número foi medido de verdade, e perder o gráfico
        // inteiro por causa de um erro de persistência seria esconder informação válida.
        RunOnUi(() =>
        {
            HistoricoDaSessao.Add(registro);
            while (HistoricoDaSessao.Count > LimiteSerieDaSessao) HistoricoDaSessao.RemoveAt(0);

            if (!gravou) return;

            HistoricoRecente.Add(registro);
            while (HistoricoRecente.Count > 300) HistoricoRecente.RemoveAt(0);
        });
    }

    /// <summary>
    /// Gerações mantidas na série do gráfico. Igual ao limite do histórico persistido (300) por
    /// coerência: as duas visões mostram a mesma janela de tempo, uma por sessão e outra do banco.
    /// </summary>
    private const int LimiteSerieDaSessao = 300;

    /// <summary>Serializa a rede no formato <c>.net</c> do SharpNEAT, em memória — ver <see cref="RegistrarInstantaneoDeComportamentoSeCompletouBloco"/>.</summary>
    private static string SerializarRede(NeatGenome<double> genoma)
    {
        using var buffer = new MemoryStream();
        NeatGenomeSaver.Save(genoma, buffer);
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    // ---------------------------------------------------------------- Diagrama da rede neural

    private const double DiagramaMargemHorizontal = 60.0;

    /// <summary>Folga no topo e na base do diagrama, para o primeiro e o último neurônio de uma coluna não encostarem na borda.</summary>
    private const double DiagramaMargemVertical = 26.0;

    /// <summary>
    /// Espaço vertical mínimo por neurônio de ENTRADA/SAÍDA. Maior que o diâmetro deles (32px)
    /// porque entradas e saídas carregam rótulo fixo logo abaixo do círculo.
    /// </summary>
    private const double DiagramaEspacoPorEntradaSaida = 46.0;

    /// <summary>Espaço vertical mínimo por neurônio OCULTO — só o diâmetro (26px) mais uma folga, já que ocultos não têm rótulo permanente.</summary>
    private const double DiagramaEspacoPorOculto = 32.0;

    /// <summary>Distância horizontal entre colunas consecutivas do diagrama (entrada, ocultas, saída).</summary>
    private const double DiagramaEspacoEntreColunas = 150.0;

    /// <summary>
    /// Altura a partir da qual os ocultos passam a ser distribuídos em MAIS DE UMA coluna em vez
    /// de continuar esticando o diagrama para baixo. Sem esse teto, uma rede com dezenas de
    /// ocultos viraria uma tira vertical altíssima — rolável, mas ilegível.
    /// </summary>
    private const double DiagramaAlturaMaximaDeUmaColuna = 640.0;

    private const double DiagramaLarguraMinima = 620.0;
    private const double DiagramaAlturaMinima = 300.0;

    private double _diagramaLarguraPx = DiagramaLarguraMinima;
    /// <summary>
    /// Largura do Canvas do diagrama, em pixels. Cresce com o número de COLUNAS de neurônios
    /// ocultos — antes era fixa em 620, o que espremia tudo num espaço que não cabia.
    /// </summary>
    public double DiagramaLarguraPx { get => _diagramaLarguraPx; private set => SetProperty(ref _diagramaLarguraPx, value); }

    private double _diagramaAlturaPx = DiagramaAlturaMinima;
    /// <summary>Altura do Canvas do diagrama, em pixels — dimensionada pela coluna mais populosa (entradas, ocultos ou saídas).</summary>
    public double DiagramaAlturaPx { get => _diagramaAlturaPx; private set => SetProperty(ref _diagramaAlturaPx, value); }

    /// <summary>
    /// Cenário de referência usado para "ligar" a rede e mostrar as saídas no diagrama — mesmo
    /// ponto típico (torre do próprio quadrante, alvo a meia distância, velocidade média) usado
    /// por <see cref="ExtratorDeRegras"/>. É fixo de propósito: assim o que muda no diagrama
    /// geração a geração é a REDE evoluindo, não ruído de um cenário diferente a cada
    /// atualização.
    /// </summary>
    private static readonly double[] EntradaDeReferenciaParaDiagrama = [0.5, 1.0, 0.0, 0.5, 0.5, 0.0, 1.0];

    /// <summary>Dimensões calculadas do diagrama para uma topologia específica — ver <see cref="CalcularLayoutDoDiagrama"/>.</summary>
    /// <param name="ColunasOcultas">Em quantas colunas os neurônios ocultos foram distribuídos (0 quando a rede não tem nenhum).</param>
    /// <param name="PorColunaOculta">Quantos ocultos cabem em cada coluna.</param>
    internal readonly record struct LayoutDiagrama(double Largura, double Altura, int ColunasOcultas, int PorColunaOculta);

    /// <summary>
    /// Dimensiona o diagrama para a topologia atual: altura pela coluna mais populosa e largura
    /// pelo número de colunas de ocultos.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O diagrama tinha tamanho fixo (620x280) e uma única coluna de ocultos, herdados da rede
    /// 7-8-2 de topologia constante do método anterior. Com NEAT a quantidade de ocultos é
    /// resultado da evolução e cresce ao longo do treinamento: a partir de cerca de dez neurônios
    /// o espaçamento ficava menor que o próprio diâmetro deles, e os nós passavam a se sobrepor e
    /// a sair da área visível.
    /// </para>
    /// <para>
    /// Acima de <see cref="DiagramaAlturaMaximaDeUmaColuna"/> os ocultos passam a ocupar mais de
    /// uma coluna, em vez de o diagrama continuar esticando para baixo: uma tira vertical de
    /// centenas de neurônios é rolável, mas não deixa a arquitetura ser compreendida de relance,
    /// que é justamente o que o card existe para permitir.
    /// </para>
    /// </remarks>
    internal static LayoutDiagrama CalcularLayoutDoDiagrama(int totalEntradas, int totalSaidas, int totalOcultos)
    {
        double alturaUtilMaxima = Math.Max(DiagramaEspacoPorOculto, DiagramaAlturaMaximaDeUmaColuna - (2 * DiagramaMargemVertical));
        int maximoPorColuna = Math.Max(1, (int)(alturaUtilMaxima / DiagramaEspacoPorOculto));

        int colunasOcultas = totalOcultos == 0 ? 0 : (int)Math.Ceiling(totalOcultos / (double)maximoPorColuna);
        // Distribui igualmente entre as colunas em vez de encher uma e deixar a última com um ou
        // dois nós soltos.
        int porColuna = colunasOcultas == 0 ? 0 : (int)Math.Ceiling(totalOcultos / (double)colunasOcultas);

        double alturaNecessaria = Math.Max(
            Math.Max(totalEntradas * DiagramaEspacoPorEntradaSaida, totalSaidas * DiagramaEspacoPorEntradaSaida),
            porColuna * DiagramaEspacoPorOculto);

        double altura = Math.Max(DiagramaAlturaMinima, alturaNecessaria + (2 * DiagramaMargemVertical));

        // Uma coluna de entrada + as de ocultos + uma de saída, com o espaçamento entre elas.
        double largura = Math.Max(
            DiagramaLarguraMinima,
            (2 * DiagramaMargemHorizontal) + ((colunasOcultas + 1) * DiagramaEspacoEntreColunas));

        return new LayoutDiagrama(largura, altura, colunasOcultas, porColuna);
    }

    /// <summary>Distribui <paramref name="total"/> neurônios uniformemente na vertical, centralizados na altura disponível.</summary>
    internal static double PosicaoVerticalNaColuna(int indice, int total, double altura) =>
        total <= 0 ? altura / 2.0 : altura * (indice + 1) / (total + 1);

    /// <summary>Esvazia o diagrama — usado em "Iniciar"/"Resetar", quando ainda não existe campeão nenhum para desenhar.</summary>
    private void LimparDiagramaRedeNeural()
    {
        RunOnUi(() =>
        {
            NeuroniosVisuais.Clear();
            ConexoesVisuais.Clear();
            DiagramaLarguraPx = DiagramaLarguraMinima;
            DiagramaAlturaPx = DiagramaAlturaMinima;
        });
    }

    /// <summary>
    /// Remonta o diagrama INTEIRO a partir do grafo do campeão atual.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Antes o diagrama era montado uma vez (a arquitetura 7-8-2 nunca mudava) e só os pesos
    /// eram repintados a cada geração. Com NEAT isso não é possível: a topologia muda de uma
    /// geração para a outra — neurônios aparecem, conexões surgem e são removidas — então a
    /// estrutura precisa ser reconstruída junto.
    /// </para>
    /// <para>
    /// Layout em três colunas (entradas · ocultos · saídas). Não tenta posicionar os ocultos por
    /// profundidade real no grafo: numa rede NEAT os ocultos encadeiam entre si em profundidades
    /// arbitrárias, e um layout por camadas daria a impressão falsa de que a rede ainda é
    /// organizada em camadas. As conexões desenhadas são as REAIS, inclusive as que pulam
    /// direto da entrada para a saída (comuns no NEAT, impossíveis na rede anterior).
    /// </para>
    /// <para>
    /// A ativação de cada neurônio OCULTO voltou a ser exibida (era um recurso da rede própria
    /// anterior, perdido na migração porque a caixa-preta do SharpNEAT expõe apenas entradas e
    /// saídas): <see cref="AtivacoesRedeNeat"/> refaz a propagação sobre a mesma topologia e os
    /// mesmos pesos, então o valor mostrado é o real, não uma estimativa. Se a rede não for do
    /// tipo que aquela propagação sabe reproduzir, os ocultos voltam à cor neutra e
    /// <see cref="AtivacoesOcultasDisponiveis"/> avisa o motivo na legenda.
    /// </para>
    /// <para>
    /// Duas coisas são preservadas entre remontagens para a tela não "piscar": a posição vertical
    /// de cada oculto que continua existindo (<see cref="_yDoOcultoPorId"/>) e a lembrança do que
    /// havia antes, usada para destacar neurônios e conexões que acabaram de surgir.
    /// </para>
    /// </remarks>
    private void RemontarDiagramaRedeNeural(NeatGenome<double> genoma, RedeNeat rede)
    {
        NetFileModel modelo = NeatGenomeConverter.ToNetFileModel(genoma);
        double[] saidas = rede.Ativar(EntradaDeReferenciaParaDiagrama);

        // Entradas na convenção do SharpNEAT (bias na posição 0) — é o que a propagação espera.
        double[] entradasComBias = new double[modelo.InputCount];
        entradasComBias[0] = 1.0;
        for (int i = 1; i < entradasComBias.Length && i - 1 < EntradaDeReferenciaParaDiagrama.Length; i++)
        {
            entradasComBias[i] = EntradaDeReferenciaParaDiagrama[i - 1];
        }

        IReadOnlyDictionary<int, double>? ativacoes = AtivacoesRedeNeat.Calcular(modelo, entradasComBias);
        bool temAtivacoesOcultas = ativacoes is not null;

        // Convenção de ids do SharpNEAT: [0, InputCount) são entradas (0 = bias),
        // [InputCount, InputCount+OutputCount) são saídas, e qualquer id acima disso é oculto.
        int totalEntradas = modelo.InputCount;
        int primeiroIdSaida = totalEntradas;
        int primeiroIdOculto = totalEntradas + modelo.OutputCount;

        var idsOcultos = new List<int>();
        int conexoesDiretas = 0;

        foreach (ConnectionLine conexao in modelo.Connections)
        {
            if (conexao.SourceId >= primeiroIdOculto && !idsOcultos.Contains(conexao.SourceId)) idsOcultos.Add(conexao.SourceId);
            if (conexao.TargetId >= primeiroIdOculto && !idsOcultos.Contains(conexao.TargetId)) idsOcultos.Add(conexao.TargetId);

            if (conexao.SourceId < totalEntradas && conexao.TargetId >= primeiroIdSaida && conexao.TargetId < primeiroIdOculto)
            {
                conexoesDiretas++;
            }
        }

        // Ordenar pela posição que o nó já ocupava antes de cair no id: assim os ocultos que
        // sobreviveram mantêm a ordem de antes, e os novos entram nos espaços restantes em vez de
        // empurrar todos os outros.
        idsOcultos.Sort((a, b) =>
        {
            int ordemA = _ordemDoOcultoPorId.TryGetValue(a, out int va) ? va : int.MaxValue;
            int ordemB = _ordemDoOcultoPorId.TryGetValue(b, out int vb) ? vb : int.MaxValue;
            int comparacao = ordemA.CompareTo(ordemB);
            return comparacao != 0 ? comparacao : a.CompareTo(b);
        });

        // Dimensiona o diagrama ANTES de posicionar: a quantidade de ocultos é resultado da
        // evolução e muda a cada geração, então largura, altura e número de colunas não podem
        // mais ser constantes — era exatamente isso que fazia os ocultos se sobreporem e saírem
        // da área visível quando a rede crescia.
        LayoutDiagrama layout = CalcularLayoutDoDiagrama(totalEntradas, modelo.OutputCount, idsOcultos.Count);

        DiagramaLarguraPx = layout.Largura;
        DiagramaAlturaPx = layout.Altura;

        var posicoes = new Dictionary<int, (double X, double Y)>();
        var novosNeuronios = new List<NeuronioVisualTreinamento>();

        double xEntrada = DiagramaMargemHorizontal;
        double xSaida = layout.Largura - DiagramaMargemHorizontal;

        // Entradas. O índice 0 é o bias — ele existe de verdade na rede e é desenhado como tal,
        // em vez de escondido: faz parte da topologia que a evolução pode (ou não) usar.
        for (int i = 0; i < totalEntradas; i++)
        {
            double y = PosicaoVerticalNaColuna(i, totalEntradas, layout.Altura);
            posicoes[i] = (xEntrada, y);

            bool ehBias = i == 0;
            string rotulo = ehBias ? "Bias (constante 1,0)" : ControladorHibrido.RotulosEntradas[i - 1];
            double valor = ehBias ? 1.0 : EntradaDeReferenciaParaDiagrama[i - 1];

            novosNeuronios.Add(new NeuronioVisualTreinamento
            {
                Camada = 0,
                Indice = i,
                CanvasX = xEntrada,
                CanvasY = y,
                Raio = 16,
                Cor = CorComIntensidade("86EFAC", Math.Abs(valor)),
                Rotulo = $"ENTRADA — {rotulo}\nValor no cenário de referência: {valor:0.00}",
                RotuloCurto = rotulo
            });
        }

        // Ocultos — quantos e quais são resultado da evolução, não configuração. Distribuídos em
        // colunas (ver CalcularLayoutDoDiagrama): com uma coluna só, qualquer rede um pouco maior
        // empilhava neurônios a poucos pixels um do outro.
        var novaOrdemDosOcultos = new Dictionary<int, int>(idsOcultos.Count);

        for (int i = 0; i < idsOcultos.Count; i++)
        {
            int idOculto = idsOcultos[i];
            int coluna = layout.PorColunaOculta > 0 ? i / layout.PorColunaOculta : 0;
            int indiceNaColuna = layout.PorColunaOculta > 0 ? i % layout.PorColunaOculta : i;

            // Quantos nós esta coluna realmente tem — a última pode ficar incompleta, e usar o
            // total da coluna cheia deixaria os poucos nós dela desalinhados para cima.
            int nestaColuna = Math.Min(layout.PorColunaOculta, idsOcultos.Count - (coluna * layout.PorColunaOculta));

            double xOculto = xEntrada + ((xSaida - xEntrada) * (coluna + 1) / (layout.ColunasOcultas + 1));
            double y = PosicaoVerticalNaColuna(indiceNaColuna, nestaColuna, layout.Altura);

            posicoes[idOculto] = (xOculto, y);
            novaOrdemDosOcultos[idOculto] = i;

            bool recemSurgido = _ocultosDaRemontagemAnterior.Count > 0 && !_ocultosDaRemontagemAnterior.Contains(idOculto);
            string detalheAtivacao = ativacoes is not null && ativacoes.TryGetValue(idOculto, out double ativacaoOculto)
                ? $"Ativação no cenário de referência: {ativacaoOculto:0.00}"
                : "Ativação indisponível para este tipo de rede.";

            novosNeuronios.Add(new NeuronioVisualTreinamento
            {
                Camada = 1,
                Indice = i,
                CanvasX = xOculto,
                CanvasY = y,
                // Nó novo ganha um pouco mais de raio, além da cor de destaque: é o sinal visual
                // de mutação estrutural acontecendo, que no método anterior nunca existia.
                Raio = recemSurgido ? 17 : 13,
                Cor = recemSurgido
                    ? "#FFF472B6"
                    : ativacoes is not null && ativacoes.TryGetValue(idOculto, out double intensidade)
                        ? CorComIntensidade("93C5FD", Math.Abs(intensidade))
                        : CorComIntensidade("93C5FD", 0.5),
                Rotulo = $"NEURÔNIO OCULTO #{i + 1} (nó {idOculto})\n{detalheAtivacao}" +
                         (recemSurgido ? "\nSURGIU NESTA ATUALIZAÇÃO — mutação estrutural do NEAT." : "\nCriado pela evolução, não configurado."),
                RotuloCurto = string.Empty
            });
        }

        // Saídas.
        for (int i = 0; i < modelo.OutputCount; i++)
        {
            double y = PosicaoVerticalNaColuna(i, modelo.OutputCount, layout.Altura);
            posicoes[primeiroIdSaida + i] = (xSaida, y);

            double ativacao = i < saidas.Length ? saidas[i] : 0.0;
            string descricao = i == 0
                ? "Soma à pontuação heurística para decidir qual torre persegue o alvo."
                : "Decide o instante do disparo dentro do alcance de engajamento — nunca aciona nada por conta própria no sistema real.";

            novosNeuronios.Add(new NeuronioVisualTreinamento
            {
                Camada = 2,
                Indice = i,
                CanvasX = xSaida,
                CanvasY = y,
                Raio = 16,
                Cor = CorComIntensidade("FDE68A", Math.Abs(ativacao)),
                Rotulo = $"SAÍDA — {ControladorHibrido.RotulosSaidas[i]}\nAtivação atual: {ativacao:0.00}\n{descricao}",
                RotuloCurto = ControladorHibrido.RotulosSaidas[i]
            });
        }

        var novasConexoes = new List<ConexaoVisualTreinamento>();
        var chavesDasConexoes = new HashSet<(int Origem, int Destino)>();

        foreach (ConnectionLine conexao in modelo.Connections)
        {
            if (!posicoes.TryGetValue(conexao.SourceId, out (double X, double Y) origem)) continue;
            if (!posicoes.TryGetValue(conexao.TargetId, out (double X, double Y) destino)) continue;

            (int, int) chave = (conexao.SourceId, conexao.TargetId);
            chavesDasConexoes.Add(chave);
            bool recemSurgida = _conexoesDaRemontagemAnterior.Count > 0 && !_conexoesDaRemontagemAnterior.Contains(chave);

            novasConexoes.Add(new ConexaoVisualTreinamento
            {
                X1 = origem.X,
                Y1 = origem.Y,
                X2 = destino.X,
                Y2 = destino.Y,
                Cor = recemSurgida ? "#F472B6" : conexao.Weight >= 0 ? "#3B82F6" : "#EF4444",
                Espessura = Math.Clamp(0.5 + Math.Abs(conexao.Weight) * 1.5, 0.5, 4.0)
            });
        }

        _ordemDoOcultoPorId.Clear();
        foreach ((int id, int ordem) in novaOrdemDosOcultos) _ordemDoOcultoPorId[id] = ordem;

        _ocultosDaRemontagemAnterior.Clear();
        foreach (int id in idsOcultos) _ocultosDaRemontagemAnterior.Add(id);

        _conexoesDaRemontagemAnterior.Clear();
        foreach ((int Origem, int Destino) chave in chavesDasConexoes) _conexoesDaRemontagemAnterior.Add(chave);

        RunOnUi(() =>
        {
            AtivacoesOcultasDisponiveis = temAtivacoesOcultas;
            ConexoesDiretasEntradaSaida = conexoesDiretas;

            NeuroniosVisuais.Clear();
            foreach (NeuronioVisualTreinamento neuronio in novosNeuronios) NeuroniosVisuais.Add(neuronio);

            ConexoesVisuais.Clear();
            foreach (ConexaoVisualTreinamento conexao in novasConexoes) ConexoesVisuais.Add(conexao);
        });
    }

    /// <summary>Usa o canal alfa para mostrar a intensidade da ativação (0 = quase transparente/neutro, 1 = cor cheia) sem precisar de uma segunda escala de cor.</summary>
    private static string CorComIntensidade(string corBaseRgbHex, double intensidade01)
    {
        int alfa = 60 + (int)Math.Round(Math.Clamp(intensidade01, 0, 1) * 195);
        return $"#{alfa:X2}{corBaseRgbHex}";
    }


    // ---------------------------------------------------------------- Prévia visual (thread de UI)

    private void AvancarPreview()
    {
        if (_melhorRedeAtual is null) return;

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

        var controlador = new ControladorHibrido(_melhorRedeAtual, _ambiente.Parametros.RaioMundoMetros, _ambiente.Parametros.VelocidadeMaximaAlvoMetrosPorSegundo, tempoDeteccaoRelevanteSegundos: _ambiente.Parametros.TempoDeteccaoRelevanteSegundos);

        _ambiente.Avancar(_episodioPreview, controlador, dt: IntervaloPreviaSegundos);
        RedesenharAlvos(_episodioPreview);
        AtualizarMiras(_episodioPreview);
        AtualizarCamposDeVisao(_episodioPreview);
        PublicarTelemetria(_episodioPreview);

        if (_episodioPreview.Finalizado && !_previewContinuo)
        {
            _timerPreview.Stop();
        }
    }

    /// <summary>
    /// Entrega o estado atual dos alvos a <see cref="ITelemetriaSimulacaoService"/> — o ponto por
    /// onde a futura ponte com o Arduino vai consumir a simulação.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Publicado daqui, e não do laço de treinamento, porque é a prévia que roda em TEMPO NORMAL:
    /// <see cref="IntervaloPreviaSegundos"/> de tempo simulado por tique de relógio do mesmo
    /// tamanho. O laço de treinamento avança o mais rápido possível de propósito — milhares de
    /// episódios para avaliar fitness —, e o tempo simulado dele não corresponde a nada que possa
    /// ser enviado ao hardware "ao vivo".
    /// </para>
    /// <para>
    /// Ainda não há transmissão nenhuma: isto apenas disponibiliza os dados, já com a referência
    /// temporal, para quem for implementá-la.
    /// </para>
    /// </remarks>
    private void PublicarTelemetria(EstadoEpisodio estado)
    {
        var alvos = new List<AlvoTelemetria>(estado.Alvos.Count);

        foreach (AlvoTreinamento alvo in estado.Alvos)
        {
            if (!alvo.Ativo) continue;

            alvos.Add(new AlvoTelemetria(
                Id: alvo.Id,
                X: alvo.X,
                Y: alvo.Y,
                AlturaMetros: alvo.AlturaMetros,
                VelocidadeMetrosPorSegundo: alvo.VelocidadeMetrosPorSegundo,
                Quadrante: alvo.Quadrante,
                VisivelPorTodasAsTorres: alvo.VisivelPorTodasAsTorres,
                TorreComprometidaId: alvo.TorreSelecionadaId,
                Certeza: alvo.Certeza));
        }

        _telemetria.Publicar(new QuadroTelemetriaSimulacao(
            TempoSimulacaoSegundos: estado.TempoDecorridoSegundos,
            DataHora: DateTime.Now,
            EixoZHabilitado: _ambiente.Parametros.EixoZHabilitado,
            Alvos: alvos));
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

    /// <summary>Raio (m) do mundo simulado — fronteira onde os alvos nascem (círculo externo do Canvas).</summary>
    public double RaioMundoMetros => _ambiente.Parametros.RaioMundoMetros;

    /// <summary>Alcance de engajamento (m) — limite até onde as torres conseguem mirar/disparar, a partir de cada torre (mesmo raio do cone, ver <see cref="ConstruirSetorDeVisao"/>).</summary>
    public double DistanciaEngajamentoMetros => _ambiente.Parametros.DistanciaEngajamentoMetros;

    // O arco de alcance de mira centrado na base saiu do Canvas (atrapalhava a leitura dos cones,
    // das linhas de mira e dos alvos), e com ele saíram as propriedades que existiam só para
    // desenhá-lo: AlcanceEngajamentoTotalMetros, DiametroEngajamentoPx e OffsetEngajamentoPx.
    // DistanciaEngajamentoMetros CONTINUA — é o raio real usado pelos cones de visão e pelo
    // controlador; o que foi removido era a representação redundante dele, não o alcance.

    private void NotificarGeometriaDoMundoAlterada()
    {
        OnPropertyChanged(nameof(RaioMundoMetros));
        OnPropertyChanged(nameof(DistanciaEngajamentoMetros));
    }

    private void DesenharTorresFixas()
    {
        NotificarGeometriaDoMundoAlterada();

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
                Cor = "#334155",
                AnguloGraus = AnguloPadraoDaTorre(torre)
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

            // O cano do canhão (ver DataTemplate de TorresVisuais) gira junto com o campo de
            // visão — mesma torre, mesmo ângulo.
            PontoVisualTreinamento? torreVisualCanhao = TorresVisuais.FirstOrDefault(t => t.Id == torre.Id);
            if (torreVisualCanhao is not null) torreVisualCanhao.AnguloGraus = anguloCentralGraus;
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
            // Alvo alto o bastante para ser visto por todas as torres ganha contorno próprio: a
            // altura muda o comportamento da simulação, então precisa ser visível na tela.
            visual.Destacado = alvo.VisivelPorTodasAsTorres;
            // Atualizada no mesmo passo da posição (este método roda a cada tique da prévia),
            // então a etiqueta acompanha o alvo em tempo real em vez de mostrar onde ele estava.
            visual.Coordenadas = $"#{alvo.Id} {alvo.X:0.0} ; {alvo.Y:0.0}";
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
        _timerEstado.Stop();
        _cronometroSessao.Stop();
    }
}
