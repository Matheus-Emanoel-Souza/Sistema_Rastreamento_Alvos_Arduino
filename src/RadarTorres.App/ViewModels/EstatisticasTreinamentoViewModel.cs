using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da tela "Estatísticas de Treinamento" (exclusiva do Administrador — ver
/// <see cref="IPermissionService"/>): evolução de longo prazo do fitness médio a cada 500
/// gerações (<see cref="IMarcoFitnessTreinamentoRepository"/>), separada da tela "Treinamento do
/// Controlador" porque ali o foco é acompanhar o treinamento RODANDO agora (geração a geração,
/// até 300 no histórico recente); aqui é analisar a tendência de longuíssimo prazo sem ruído
/// geração a geração.
///
/// Concentra também a ANÁLISE EXPERIMENTAL DE HIPERPARÂMETROS pedida no parecer do artigo: três
/// tabelas comparativas no mesmo painel — variando só a camada oculta (4/8/16 neurônios), só a
/// população (30/60/120 indivíduos) e só a taxa de mutação (0,04/0,08/0,16) — alimentadas
/// exclusivamente por execuções reais registradas em
/// <see cref="IExperimentoHiperparametrosRepository"/>.
/// </summary>
public sealed class EstatisticasTreinamentoViewModel : ViewModelBase
{
    private readonly IMarcoFitnessTreinamentoRepository _marcoFitnessRepository;
    private readonly IHistoricoTreinamentoRepository _historicoRepository;
    private readonly IInstantaneoComportamentoRepository _instantaneoRepository;
    private readonly IExperimentoHiperparametrosRepository _experimentoRepository;
    private readonly IExecutorExperimentosHiperparametros _executorExperimentos;
    private readonly ILoggingService _logger;

    private CancellationTokenSource? _ctsExperimentos;

    public ObservableCollection<MarcoFitnessTreinamento> Marcos { get; } = new();

    /// <summary>Últimas gerações (até 300), geração a geração — mesma fonte da tela "Treinamento do Controlador" (ver IHistoricoTreinamentoRepository), para o gráfico granular também aparecer aqui.</summary>
    public ObservableCollection<HistoricoGeracaoTreinamento> HistoricoRecente { get; } = new();

    /// <summary>Retrato completo do melhor genoma a cada 1.000 gerações (decisões/acertos/erros/genoma) — ver IInstantaneoComportamentoRepository. Nunca sobrescrito; é o histórico comparativo marco a marco.</summary>
    public ObservableCollection<InstantaneoComportamentoMelhorGene> Instantaneos { get; } = new();

    /// <summary>Tabela 1 — séries em que varia a quantidade de neurônios da camada oculta (população e mutação travadas).</summary>
    public ObservableCollection<SerieExperimental> SeriesCamadaOculta { get; } = new();

    /// <summary>Tabela 2 — séries em que varia o tamanho da população (camada oculta e mutação travadas).</summary>
    public ObservableCollection<SerieExperimental> SeriesPopulacao { get; } = new();

    /// <summary>Tabela 3 — séries em que varia a taxa de mutação (população e camada oculta travadas).</summary>
    public ObservableCollection<SerieExperimental> SeriesTaxaMutacao { get; } = new();

    public RelayCommand AtualizarCommand { get; }
    public RelayCommand ExecutarExperimentosCommand { get; }
    public RelayCommand PararExperimentosCommand { get; }
    public RelayCommand RefazerExperimentosCommand { get; }

    public EstatisticasTreinamentoViewModel(
        IMarcoFitnessTreinamentoRepository marcoFitnessRepository,
        IHistoricoTreinamentoRepository historicoRepository,
        IInstantaneoComportamentoRepository instantaneoRepository,
        IExperimentoHiperparametrosRepository experimentoRepository,
        IExecutorExperimentosHiperparametros executorExperimentos,
        ILoggingService logger)
    {
        _marcoFitnessRepository = marcoFitnessRepository;
        _historicoRepository = historicoRepository;
        _instantaneoRepository = instantaneoRepository;
        _experimentoRepository = experimentoRepository;
        _executorExperimentos = executorExperimentos;
        _logger = logger;

        AtualizarCommand = new RelayCommand(Carregar);
        ExecutarExperimentosCommand = new RelayCommand(() => ExecutarExperimentos(refazer: false), () => !ExperimentosEmExecucao);
        RefazerExperimentosCommand = new RelayCommand(() => ExecutarExperimentos(refazer: true), () => !ExperimentosEmExecucao);
        PararExperimentosCommand = new RelayCommand(PararExperimentos, () => ExperimentosEmExecucao);

        Carregar();
    }

    private void Carregar()
    {
        try
        {
            Marcos.Clear();
            foreach (MarcoFitnessTreinamento marco in _marcoFitnessRepository.ObterTodos())
            {
                Marcos.Add(marco);
            }

            HistoricoRecente.Clear();
            foreach (HistoricoGeracaoTreinamento registro in _historicoRepository.ObterUltimos())
            {
                HistoricoRecente.Add(registro);
            }

            Instantaneos.Clear();
            foreach (InstantaneoComportamentoMelhorGene instantaneo in _instantaneoRepository.ObterTodos())
            {
                Instantaneos.Add(instantaneo);
            }

            OnPropertyChanged(nameof(TemDados));
            OnPropertyChanged(nameof(UltimoMarco));
            OnPropertyChanged(nameof(FitnessMedioGeral));
            OnPropertyChanged(nameof(MelhorFitnessGeral));
            OnPropertyChanged(nameof(MensagemSemDados));
            OnPropertyChanged(nameof(TemInstantaneos));
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível carregar os marcos de treinamento: {ex.Message}");
        }

        CarregarComparativosDeHiperparametros();
    }

    public bool TemInstantaneos => Instantaneos.Count > 0;

    public bool TemDados => Marcos.Count > 0;

    public string MensagemSemDados =>
        "Nenhum marco registrado ainda — o primeiro aparece ao completar a geração 500 na tela \"Treinamento do Controlador\" (um marco a cada 500 gerações).";

    public MarcoFitnessTreinamento? UltimoMarco => Marcos.Count > 0 ? Marcos[^1] : null;

    public double FitnessMedioGeral => Marcos.Count > 0 ? Marcos.Average(m => m.FitnessMedioDoBloco) : 0;

    public double MelhorFitnessGeral => Marcos.Count > 0 ? Marcos.Max(m => m.MelhorFitnessDoBloco) : 0;

    // ------------------------------------------------- Análise experimental de hiperparâmetros

    private string? _statusExperimentos;

    /// <summary>Linha de progresso/resultado da bateria de experimentos (null quando nunca foi rodada nesta sessão).</summary>
    public string? StatusExperimentos
    {
        get => _statusExperimentos;
        private set => SetProperty(ref _statusExperimentos, value);
    }

    private bool _experimentosEmExecucao;
    public bool ExperimentosEmExecucao
    {
        get => _experimentosEmExecucao;
        private set
        {
            if (SetProperty(ref _experimentosEmExecucao, value))
            {
                RelayCommand.RaiseCanExecuteChangedForAll();
            }
        }
    }

    /// <summary>Gerações rodadas em cada configuração — exibido no cabeçalho para deixar claro que a comparação é feita no mesmo ponto de parada.</summary>
    public int GeracoesPorExperimento => ConfiguracaoExperimento.GeracoesPorExperimento;

    /// <summary>Quantas das configurações exibidas nas três tabelas já têm execução real registrada.</summary>
    public string CoberturaDoPlano
    {
        get
        {
            int exibidas = ConfiguracoesExibidas().Count;
            int pendentes = ConfiguracoesPendentes().Count;
            return $"{exibidas - pendentes} de {exibidas} configurações exibidas já foram treinadas.";
        }
    }

    private IEnumerable<SerieExperimental> TodasAsSeries =>
        SeriesCamadaOculta.Concat(SeriesPopulacao).Concat(SeriesTaxaMutacao);

    /// <summary>
    /// Monta as três tabelas a partir das execuções reais gravadas. A qual série cada registro
    /// pertence é DERIVADO dos próprios valores (os hiperparâmetros que NÃO variam identificam a
    /// série), em vez de gravado: assim qualquer execução já existente entra automaticamente na
    /// comparação em que se encaixa, e uma mesma configuração aparece em várias tabelas sem ser
    /// treinada mais de uma vez. Havendo mais de uma execução da mesma configuração, vale a mais
    /// recente.
    /// </summary>
    private void CarregarComparativosDeHiperparametros()
    {
        IReadOnlyList<ExperimentoHiperparametros> registros;
        try
        {
            registros = _experimentoRepository.ObterTodos();
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível carregar os experimentos de hiperparâmetros: {ex.Message}");
            return;
        }

        // Experimento 1 — varia a camada oculta; cada série é identificada por (população, mutação).
        MontarSeries(
            SeriesCamadaOculta,
            registros,
            combinacoesTravadas: Combinar(ConfiguracaoExperimento.PopulacoesDaSerie, ConfiguracaoExperimento.TaxasMutacaoDaSerie),
            ehBaselineTravado: t => t.A == ConfiguracaoExperimento.PopulacaoBaseline && Iguais(t.B, ConfiguracaoExperimento.TaxaMutacaoBaseline),
            valoresVariados: ConfiguracaoExperimento.NeuroniosDaSerie.Select(n => (double)n),
            criarConfiguracao: (t, variado) => new ConfiguracaoExperimento((int)t.A, (int)variado, t.B),
            tituloDaSerie: t => $"População {t.A} · mutação {SerieExperimental.FormatarTaxa(t.B)}",
            unidadeVariada: "neurônios na camada oculta",
            valorVariado: linha => linha.NeuroniosOcultos.ToString());

        // Experimento 2 — varia a população; cada série é identificada por (neurônios, mutação).
        MontarSeries(
            SeriesPopulacao,
            registros,
            combinacoesTravadas: Combinar(ConfiguracaoExperimento.NeuroniosDaSerie, ConfiguracaoExperimento.TaxasMutacaoDaSerie),
            ehBaselineTravado: t => t.A == ConfiguracaoExperimento.NeuroniosBaseline && Iguais(t.B, ConfiguracaoExperimento.TaxaMutacaoBaseline),
            valoresVariados: ConfiguracaoExperimento.PopulacoesDaSerie.Select(p => (double)p),
            criarConfiguracao: (t, variado) => new ConfiguracaoExperimento((int)variado, (int)t.A, t.B),
            tituloDaSerie: t => $"Camada oculta {t.A} · mutação {SerieExperimental.FormatarTaxa(t.B)}",
            unidadeVariada: "indivíduos na população",
            valorVariado: linha => linha.TamanhoPopulacao.ToString());

        // Experimento 3 — varia a taxa de mutação; cada série é identificada por (população, neurônios).
        MontarSeries(
            SeriesTaxaMutacao,
            registros,
            combinacoesTravadas: Combinar(ConfiguracaoExperimento.PopulacoesDaSerie, ConfiguracaoExperimento.NeuroniosDaSerie.Select(n => (double)n).ToList()),
            ehBaselineTravado: t => t.A == ConfiguracaoExperimento.PopulacaoBaseline && (int)t.B == ConfiguracaoExperimento.NeuroniosBaseline,
            valoresVariados: ConfiguracaoExperimento.TaxasMutacaoDaSerie,
            criarConfiguracao: (t, variado) => new ConfiguracaoExperimento((int)t.A, (int)t.B, variado),
            tituloDaSerie: t => $"População {t.A} · camada oculta {(int)t.B}",
            unidadeVariada: "de taxa de mutação",
            valorVariado: linha => SerieExperimental.FormatarTaxa(linha.TaxaMutacao));

        OnPropertyChanged(nameof(CoberturaDoPlano));
    }

    /// <summary>Produto cartesiano dos dois hiperparâmetros travados de uma tabela.</summary>
    private static IReadOnlyList<(double A, double B)> Combinar(IReadOnlyList<int> primeiros, IReadOnlyList<double> segundos) =>
        primeiros.SelectMany(_ => segundos, (a, b) => ((double)a, b)).ToList();

    private static bool Iguais(double a, double b) => Math.Abs(a - b) < 1e-9;

    /// <summary>Uma série adicional só vale a pena exibir se já der para comparar — com um único ponto não há comparação nenhuma, só uma linha solta.</summary>
    private const int MinimoExecucoesParaSerieAdicional = 2;

    /// <summary>
    /// Uma série por combinação de valores travados. A do baseline SEMPRE aparece (é o plano de
    /// referência pedido no parecer, mesmo sem nenhuma execução ainda); as demais só aparecem
    /// quando já têm pelo menos <see cref="MinimoExecucoesParaSerieAdicional"/> execuções reais —
    /// assim nenhum treinamento antigo é desperdiçado, mas a tela também não enche de séries de
    /// um ponto só. Sem esse piso, cada execução avulsa criaria uma série nova em CADA uma das
    /// três tabelas (toda execução tem um valor para os três hiperparâmetros), e a lista de
    /// pendentes cresceria para dezenas de treinamentos que ninguém pediu.
    /// </summary>
    private static void MontarSeries(
        ObservableCollection<SerieExperimental> destino,
        IReadOnlyList<ExperimentoHiperparametros> registros,
        IReadOnlyList<(double A, double B)> combinacoesTravadas,
        Func<(double A, double B), bool> ehBaselineTravado,
        IEnumerable<double> valoresVariados,
        Func<(double A, double B), double, ConfiguracaoExperimento> criarConfiguracao,
        Func<(double A, double B), string> tituloDaSerie,
        string unidadeVariada,
        Func<LinhaExperimentoComparativo, string> valorVariado)
    {
        destino.Clear();

        List<double> variados = valoresVariados.ToList();

        // Baseline primeiro; as adicionais mantêm a ordem natural das listas de valores.
        foreach ((double A, double B) travado in combinacoesTravadas.OrderBy(t => ehBaselineTravado(t) ? 0 : 1))
        {
            var linhas = new List<LinhaExperimentoComparativo>();
            foreach (double variado in variados)
            {
                ConfiguracaoExperimento configuracao = criarConfiguracao(travado, variado);
                ExperimentoHiperparametros? execucao = ExecucaoMaisRecente(registros, configuracao);

                linhas.Add(execucao is null
                    ? LinhaExperimentoComparativo.Pendente(configuracao, EhBaseline(configuracao))
                    : LinhaExperimentoComparativo.Executada(configuracao, execucao, EhBaseline(configuracao)));
            }

            bool ehPlanoPrincipal = ehBaselineTravado(travado);
            if (!ehPlanoPrincipal && linhas.Count(l => l.Executado) < MinimoExecucoesParaSerieAdicional) continue;

            destino.Add(new SerieExperimental(tituloDaSerie(travado), ehPlanoPrincipal, linhas, unidadeVariada, valorVariado));
        }
    }

    private static bool EhBaseline(ConfiguracaoExperimento configuracao) =>
        configuracao.TamanhoPopulacao == ConfiguracaoExperimento.PopulacaoBaseline
        && configuracao.NeuroniosOcultos == ConfiguracaoExperimento.NeuroniosBaseline
        && Iguais(configuracao.TaxaMutacao, ConfiguracaoExperimento.TaxaMutacaoBaseline);

    /// <summary>
    /// A execução mais recente de uma configuração. Registros anteriores ao campo de taxa de
    /// mutação (<c>TaxaMutacao == null</c>) rodaram com o padrão do sistema, então casam com as
    /// séries de mutação 0,08 — é o que permite reaproveitá-los em vez de descartar treinamentos
    /// reais já feitos (a tela marca o valor como presumido, ver
    /// <see cref="LinhaExperimentoComparativo.TaxaMutacaoPresumida"/>).
    /// </summary>
    private static ExperimentoHiperparametros? ExecucaoMaisRecente(
        IReadOnlyList<ExperimentoHiperparametros> registros,
        ConfiguracaoExperimento configuracao) =>
        registros
            .Where(r => r.TamanhoPopulacao == configuracao.TamanhoPopulacao
                        && r.NeuroniosOcultos == configuracao.NeuroniosOcultos
                        && r.Geracoes >= ConfiguracaoExperimento.GeracoesPorExperimento
                        && Iguais(r.TaxaMutacao ?? ConfiguracaoExperimento.TaxaMutacaoBaseline, configuracao.TaxaMutacao))
            .OrderByDescending(r => r.DataHora)
            .FirstOrDefault();

    /// <summary>Todas as configurações que aparecem em alguma tabela, sem repetir as compartilhadas.</summary>
    private IReadOnlyList<ConfiguracaoExperimento> ConfiguracoesExibidas() =>
        TodasAsSeries
            .SelectMany(s => s.Linhas)
            .Select(ConfiguracaoDa)
            .Distinct()
            .ToList();

    /// <summary>
    /// As configurações exibidas que ainda não foram treinadas — é exatamente o que o botão
    /// "Executar configurações pendentes" roda. Uma configuração que aparece em mais de uma
    /// tabela entra uma vez só.
    /// </summary>
    private IReadOnlyList<ConfiguracaoExperimento> ConfiguracoesPendentes() =>
        TodasAsSeries
            .SelectMany(s => s.Linhas)
            .Where(l => !l.Executado)
            .Select(ConfiguracaoDa)
            .Distinct()
            .OrderBy(c => c.TaxaMutacao)
            .ThenBy(c => c.NeuroniosOcultos)
            .ThenBy(c => c.TamanhoPopulacao)
            .ToList();

    private static ConfiguracaoExperimento ConfiguracaoDa(LinhaExperimentoComparativo linha) =>
        new(linha.TamanhoPopulacao, linha.NeuroniosOcultos, linha.TaxaMutacao);

    private void ExecutarExperimentos(bool refazer)
    {
        if (ExperimentosEmExecucao) return;

        if (refazer && !ConfirmarRefazer()) return;

        // Depois do "Refazer", as linhas foram apagadas e as séries recarregadas — então
        // ConfiguracoesPendentes() já devolve tudo de novo, sem precisar de uma segunda lista.
        IReadOnlyList<ConfiguracaoExperimento> aExecutar = ConfiguracoesPendentes();

        if (aExecutar.Count == 0)
        {
            StatusExperimentos = "Todas as configurações exibidas já possuem execução registrada — use \"Refazer tudo\" para treiná-las de novo.";
            return;
        }

        ExperimentosEmExecucao = true;
        StatusExperimentos = $"Preparando {aExecutar.Count} configuração(ões) ({ConfiguracaoExperimento.GeracoesPorExperimento} gerações cada)...";
        _ctsExperimentos = new CancellationTokenSource();
        CancellationToken token = _ctsExperimentos.Token;

        _ = _executorExperimentos
            .ExecutarAsync(
                aExecutar,
                mensagem => RunOnUi(() => StatusExperimentos = mensagem),
                _ => RunOnUi(CarregarComparativosDeHiperparametros),
                token)
            .ContinueWith(_ => RunOnUi(() =>
            {
                ExperimentosEmExecucao = false;
                CarregarComparativosDeHiperparametros();
            }), TaskScheduler.Default);
    }

    /// <summary>
    /// "Refazer tudo" apaga as execuções registradas das configurações EXIBIDAS (as das séries
    /// adicionais incluídas) e devolve true para que todas sejam treinadas de novo. É destrutivo
    /// e cada reexecução leva vários minutos, então exige confirmação explícita.
    /// </summary>
    private bool ConfirmarRefazer()
    {
        IReadOnlyList<ConfiguracaoExperimento> exibidas = ConfiguracoesExibidas();

        List<ExperimentoHiperparametros> aRemover;
        try
        {
            aRemover = _experimentoRepository.ObterTodos()
                .Where(r => exibidas.Any(c =>
                    c.TamanhoPopulacao == r.TamanhoPopulacao
                    && c.NeuroniosOcultos == r.NeuroniosOcultos
                    && Iguais(r.TaxaMutacao ?? ConfiguracaoExperimento.TaxaMutacaoBaseline, c.TaxaMutacao)))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível consultar os experimentos já registrados: {ex.Message}");
            StatusExperimentos = $"Não foi possível consultar os experimentos já registrados: {ex.Message}";
            return false;
        }

        MessageBoxResult resposta = MessageBox.Show(
            $"Isto apaga {aRemover.Count} execução(ões) já registrada(s) e treina as " +
            $"{exibidas.Count} configurações exibidas de novo " +
            $"({ConfiguracaoExperimento.GeracoesPorExperimento} gerações cada). Pode levar vários minutos.\n\nContinuar?",
            "Refazer a análise experimental",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (resposta != MessageBoxResult.Yes) return false;

        foreach (ExperimentoHiperparametros registro in aRemover)
        {
            try
            {
                _experimentoRepository.Remover(registro.Id);
            }
            catch (Exception ex)
            {
                _logger.Error($"Não foi possível remover o experimento #{registro.Id}: {ex.Message}");
            }
        }

        CarregarComparativosDeHiperparametros();
        return true;
    }

    private void PararExperimentos()
    {
        _ctsExperimentos?.Cancel();
        StatusExperimentos = "Interrompendo após a configuração atual...";
    }

    private static void RunOnUi(Action action)
    {
        Dispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
