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
    private readonly IConfiguracoesAplicativoRepository _configuracoes;
    private readonly DispatcherTimer _timerPreview;

    // Acumulador do bloco de 500 gerações em andamento — zerado a cada marco registrado (ver
    // RegistrarMarcoFitnessSeCompletouBloco) e também em Iniciar/Resetar, para não misturar
    // gerações de sessões diferentes no mesmo marco.
    private double _somaFitnessMedioBloco;
    private double _melhorFitnessBloco;
    private int _contagemGeracoesNoBloco;

    private AmbienteTreinamento _ambiente;
    private TreinadorNeat? _treinador;
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
    public ObservableCollection<string> RegrasAprendidas { get; } = new();
    public ObservableCollection<HistoricoGeracaoTreinamento> HistoricoRecente { get; } = new();
    public ObservableCollection<NeuronioVisualTreinamento> NeuroniosVisuais { get; } = new();
    public ObservableCollection<ConexaoVisualTreinamento> ConexoesVisuais { get; } = new();

    public RelayCommand IniciarCommand { get; }
    public RelayCommand PararCommand { get; }
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
        IConfiguracoesAplicativoRepository configuracoes)
    {
        _logger = logger;
        _historicoRepository = historicoRepository;
        _marcoFitnessRepository = marcoFitnessRepository;
        _instantaneoRepository = instantaneoRepository;
        _telemetria = telemetria;
        _configuracoes = configuracoes;
        _ambiente = new AmbienteTreinamento(ParametrosAtuais);
        DesenharTorresFixas();
        LimparDiagramaRedeNeural();
        _cenarioOpcaoSelecionada = CenariosDisponiveis[0];

        IniciarCommand = new RelayCommand(Iniciar, () => !EmExecucao);
        PararCommand = new RelayCommand(Parar, () => EmExecucao);
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
            }
        }
    }

    private int _geracaoAtual;
    public int GeracaoAtual { get => _geracaoAtual; private set => SetProperty(ref _geracaoAtual, value); }

    private double _melhorFitness;
    public double MelhorFitness { get => _melhorFitness; private set => SetProperty(ref _melhorFitness, value); }

    private double _fitnessMedio;
    public double FitnessMedio { get => _fitnessMedio; private set => SetProperty(ref _fitnessMedio, value); }

    // ---------------------------------------------------------------- Comandos

    private void Iniciar()
    {
        if (EmExecucao) return;

        ParametrosTreinamento parametros = ParametrosAtuais;
        _ambiente = new AmbienteTreinamento(parametros);
        DesenharTorresFixas();
        LimparDiagramaRedeNeural();
        _treinador = new TreinadorNeat(
            _ambiente,
            tamanhoPopulacao: parametros.TamanhoPopulacao,
            quantidadeEspecies: parametros.QuantidadeEspecies,
            tetoComplexidade: parametros.TetoComplexidade,
            probabilidadeAdicionarNo: parametros.ProbabilidadeAdicionarNo);

        GeracaoAtual = 0;
        MelhorFitness = 0;
        FitnessMedio = 0;
        _somaFitnessMedioBloco = 0;
        _melhorFitnessBloco = 0;
        _contagemGeracoesNoBloco = 0;
        _melhorGenomaAtual = null;
        _melhorRedeAtual = null;
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
        _treinador = null;
        _melhorGenomaAtual = null;
        _melhorRedeAtual = null;
        _episodioPreview = null;
        _previewContinuo = true;

        GeracaoAtual = 0;
        MelhorFitness = 0;
        FitnessMedio = 0;
        _somaFitnessMedioBloco = 0;
        _melhorFitnessBloco = 0;
        _contagemGeracoesNoBloco = 0;

        DesenharTorresFixas();
        LimparDiagramaRedeNeural();
        AlvosVisuais.Clear();
        MirasVisuais.Clear();
        RegrasAprendidas.Clear();

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
            RunOnUi(() => GeracaoAtual = inicial.Geracao);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível criar a população inicial para a prévia: {ex.Message}");
            MostrarStatus($"Não foi possível criar a população inicial: {ex.Message}", sucesso: false);
            return false;
        }
    }

    /// <summary>Guarda o campeão da geração, decodifica a rede e atualiza regras e diagrama a partir dela.</summary>
    private void AdotarCampeao(NeatGenome<double> genoma)
    {
        _melhorGenomaAtual = genoma;
        _melhorRedeAtual = _treinador!.Decodificar(genoma);

        AtualizarRegras(_melhorRedeAtual);
        RemontarDiagramaRedeNeural(genoma, _melhorRedeAtual);
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
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível exportar a rede: {ex.Message}");
            MostrarStatus($"Não foi possível exportar a rede: {ex.Message}", sucesso: false);
        }
    }

    // ---------------------------------------------------------------- Laço de treinamento (thread de fundo)

    private void LacoDeTreinamento(CancellationToken token)
    {
        TreinadorNeat algoritmo = _treinador!;

        while (!token.IsCancellationRequested)
        {
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

            AdotarCampeao(resultado.MelhorGenoma);

            // Reavalia o melhor genoma uma vez mais só para capturar como ele se comportou
            // (abatidos/escapados) — o fitness em si já saiu de dentro da evolução, mas esses
            // dois números não são devolvidos por ela.
            EpisodioResultado comportamento = _ambiente.ExecutarEpisodioRapido(_melhorRedeAtual!, seed: resultado.Geracao);
            RegistrarHistorico(resultado, comportamento);
            RegistrarMarcoFitnessSeCompletouBloco(resultado);
            RegistrarInstantaneoDeComportamentoSeCompletouBloco(resultado, comportamento);

            RunOnUi(() =>
            {
                GeracaoAtual = resultado.Geracao;
                MelhorFitness = resultado.MelhorFitness;
                FitnessMedio = resultado.FitnessMedio;
            });
        }
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
        if (resultado.Geracao % TamanhoBlocoInstantaneo != 0) return;

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
            DataHora = DateTime.Now
        };

        try
        {
            _instantaneoRepository.Registrar(instantaneo);
            _logger.Success(
                $"Instantâneo de comportamento registrado: geração {instantaneo.GeracaoMarco}, " +
                $"acerto {instantaneo.TaxaDeAcerto:P0}, cobertura {instantaneo.TaxaDeCobertura:P0}.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível registrar o instantâneo de comportamento da geração {instantaneo.GeracaoMarco}: {ex.Message}");
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

    /// <summary>Serializa a rede no formato <c>.net</c> do SharpNEAT, em memória — ver <see cref="RegistrarInstantaneoDeComportamentoSeCompletouBloco"/>.</summary>
    private static string SerializarRede(NeatGenome<double> genoma)
    {
        using var buffer = new MemoryStream();
        NeatGenomeSaver.Save(genoma, buffer);
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private void AtualizarRegras(RedeNeat rede)
    {
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
    /// Cenário de referência usado para "ligar" a rede e mostrar as saídas no diagrama — mesmo
    /// ponto típico (torre do próprio quadrante, alvo a meia distância, velocidade média) usado
    /// por <see cref="ExtratorDeRegras"/>. É fixo de propósito: assim o que muda no diagrama
    /// geração a geração é a REDE evoluindo, não ruído de um cenário diferente a cada
    /// atualização.
    /// </summary>
    private static readonly double[] EntradaDeReferenciaParaDiagrama = [0.5, 1.0, 0.0, 0.5, 0.5, 0.0, 1.0];

    /// <summary>Esvazia o diagrama — usado em "Iniciar"/"Resetar", quando ainda não existe campeão nenhum para desenhar.</summary>
    private void LimparDiagramaRedeNeural()
    {
        RunOnUi(() =>
        {
            NeuroniosVisuais.Clear();
            ConexoesVisuais.Clear();
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
    /// A ativação individual de cada neurônio OCULTO não é mais exibida: a caixa-preta do
    /// SharpNEAT expõe apenas entradas e saídas, não os valores internos. Entradas e saídas
    /// continuam coloridas pelo valor real no cenário de referência; os ocultos usam cor neutra.
    /// Preferiu-se isso a estimar um valor que o framework não fornece.
    /// </para>
    /// </remarks>
    private void RemontarDiagramaRedeNeural(NeatGenome<double> genoma, RedeNeat rede)
    {
        NetFileModel modelo = NeatGenomeConverter.ToNetFileModel(genoma);
        double[] saidas = rede.Ativar(EntradaDeReferenciaParaDiagrama);

        // Convenção de ids do SharpNEAT: [0, InputCount) são entradas (0 = bias),
        // [InputCount, InputCount+OutputCount) são saídas, e qualquer id acima disso é oculto.
        int totalEntradas = modelo.InputCount;
        int primeiroIdSaida = totalEntradas;
        int primeiroIdOculto = totalEntradas + modelo.OutputCount;

        var idsOcultos = new List<int>();
        foreach (ConnectionLine conexao in modelo.Connections)
        {
            if (conexao.SourceId >= primeiroIdOculto && !idsOcultos.Contains(conexao.SourceId)) idsOcultos.Add(conexao.SourceId);
            if (conexao.TargetId >= primeiroIdOculto && !idsOcultos.Contains(conexao.TargetId)) idsOcultos.Add(conexao.TargetId);
        }
        idsOcultos.Sort();

        var posicoes = new Dictionary<int, (double X, double Y)>();
        var novosNeuronios = new List<NeuronioVisualTreinamento>();

        double xEntrada = DiagramaMargemHorizontal;
        double xOculto = DiagramaLargura / 2.0;
        double xSaida = DiagramaLargura - DiagramaMargemHorizontal;

        // Entradas. O índice 0 é o bias — ele existe de verdade na rede e é desenhado como tal,
        // em vez de escondido: faz parte da topologia que a evolução pode (ou não) usar.
        for (int i = 0; i < totalEntradas; i++)
        {
            double y = DiagramaAltura * (i + 1) / (totalEntradas + 1);
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

        // Ocultos — quantos e quais são resultado da evolução, não configuração.
        for (int i = 0; i < idsOcultos.Count; i++)
        {
            double y = DiagramaAltura * (i + 1) / (idsOcultos.Count + 1);
            posicoes[idsOcultos[i]] = (xOculto, y);

            novosNeuronios.Add(new NeuronioVisualTreinamento
            {
                Camada = 1,
                Indice = i,
                CanvasX = xOculto,
                CanvasY = y,
                Raio = 13,
                Cor = CorComIntensidade("93C5FD", 0.5),
                Rotulo = $"NEURÔNIO OCULTO #{i + 1} (nó {idsOcultos[i]})\nCriado pela evolução — o SharpNEAT não expõe a ativação interna, só entradas e saídas.",
                RotuloCurto = string.Empty
            });
        }

        // Saídas.
        for (int i = 0; i < modelo.OutputCount; i++)
        {
            double y = DiagramaAltura * (i + 1) / (modelo.OutputCount + 1);
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

        foreach (ConnectionLine conexao in modelo.Connections)
        {
            if (!posicoes.TryGetValue(conexao.SourceId, out (double X, double Y) origem)) continue;
            if (!posicoes.TryGetValue(conexao.TargetId, out (double X, double Y) destino)) continue;

            novasConexoes.Add(new ConexaoVisualTreinamento
            {
                X1 = origem.X,
                Y1 = origem.Y,
                X2 = destino.X,
                Y2 = destino.Y,
                Cor = conexao.Weight >= 0 ? "#3B82F6" : "#EF4444",
                Espessura = Math.Clamp(0.5 + Math.Abs(conexao.Weight) * 1.5, 0.5, 4.0)
            });
        }

        RunOnUi(() =>
        {
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

    /// <summary>
    /// Alcance de engajamento "a partir da base" (m) — raio do círculo interno do Canvas,
    /// centralizado na origem do mundo. Soma a distância da base até a torre mais afastada com
    /// <see cref="DistanciaEngajamentoMetros"/> para o círculo refletir até onde as torres
    /// realmente alcançam a partir de SUAS posições (não da origem): sem essa soma, um círculo
    /// de raio = só <see cref="DistanciaEngajamentoMetros"/> fica bem menor do que a ponta dos
    /// cones de visão de cada torre (que começam já deslocados da origem), dando a falsa
    /// impressão de alcance menor do que o real.
    /// </summary>
    public double AlcanceEngajamentoTotalMetros => DistanciaBaseAteTorreMaisLongeMetros + DistanciaEngajamentoMetros;

    private double DistanciaBaseAteTorreMaisLongeMetros =>
        _ambiente.TorresBase.Count == 0 ? 0 : _ambiente.TorresBase.Max(t => Math.Sqrt(t.X * t.X + t.Y * t.Y));

    /// <summary>Diâmetro em pixels do círculo interno (<see cref="AlcanceEngajamentoTotalMetros"/>), já na escala atual do Canvas.</summary>
    public double DiametroEngajamentoPx => AlcanceEngajamentoTotalMetros * 2 * PixelsPorMetro;

    /// <summary>Deslocamento (Canvas.Left/Top) do círculo interno para ficar centralizado no mundo.</summary>
    public double OffsetEngajamentoPx => (TamanhoCanvas - DiametroEngajamentoPx) / 2.0;

    private void NotificarGeometriaDoMundoAlterada()
    {
        OnPropertyChanged(nameof(RaioMundoMetros));
        OnPropertyChanged(nameof(DistanciaEngajamentoMetros));
        OnPropertyChanged(nameof(AlcanceEngajamentoTotalMetros));
        OnPropertyChanged(nameof(DiametroEngajamentoPx));
        OnPropertyChanged(nameof(OffsetEngajamentoPx));
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
