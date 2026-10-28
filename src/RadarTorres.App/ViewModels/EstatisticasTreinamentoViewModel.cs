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
/// Concentra também a ANÁLISE EXPERIMENTAL DE HIPERPARÂMETROS pedida no parecer do artigo: duas
/// tabelas comparativas no mesmo painel — uma variando só a camada oculta (4/8/16 neurônios) e
/// outra variando só a população (30/60/120 indivíduos) — alimentadas exclusivamente por
/// execuções reais registradas em <see cref="IExperimentoHiperparametrosRepository"/>.
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

    /// <summary>Experimento 1 — varia a camada oculta (4/8/16), população travada no baseline.</summary>
    public ObservableCollection<LinhaExperimentoComparativo> ComparativoCamadaOculta { get; } = new();

    /// <summary>Experimento 2 — varia a população (30/60/120), camada oculta travada no baseline.</summary>
    public ObservableCollection<LinhaExperimentoComparativo> ComparativoPopulacao { get; } = new();

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

    public int PopulacaoBaseline => ConfiguracaoExperimento.PopulacaoBaseline;

    public int NeuroniosBaseline => ConfiguracaoExperimento.NeuroniosBaseline;

    /// <summary>Quantas das configurações previstas no plano já têm execução real registrada ("3 de 5").</summary>
    public string CoberturaDoPlano
    {
        get
        {
            int executadas = ConfiguracaoExperimento.Planejadas.Count(c =>
                ComparativoCamadaOculta.Concat(ComparativoPopulacao)
                    .Any(l => l.Executado && l.TamanhoPopulacao == c.TamanhoPopulacao && l.NeuroniosOcultos == c.NeuroniosOcultos));
            return $"{executadas} de {ConfiguracaoExperimento.Planejadas.Count} configurações do plano já foram treinadas.";
        }
    }

    /// <summary>
    /// Monta as duas tabelas a partir das execuções reais gravadas. A qual série cada registro
    /// pertence é DERIVADO dos próprios valores (população = baseline → série da camada oculta;
    /// neurônios = baseline → série da população) em vez de gravado: assim qualquer execução já
    /// existente entra automaticamente na comparação, e a configuração de referência
    /// (baseline × baseline) aparece nas duas tabelas sem ser treinada duas vezes. Quando há mais
    /// de uma execução da mesma configuração, vale a mais recente.
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

        Preencher(
            ComparativoCamadaOculta,
            ConfiguracaoExperimento.NeuroniosDaSerie.Select(n => new ConfiguracaoExperimento(ConfiguracaoExperimento.PopulacaoBaseline, n)),
            registros);

        Preencher(
            ComparativoPopulacao,
            ConfiguracaoExperimento.PopulacoesDaSerie.Select(p => new ConfiguracaoExperimento(p, ConfiguracaoExperimento.NeuroniosBaseline)),
            registros);

        OnPropertyChanged(nameof(CoberturaDoPlano));
        OnPropertyChanged(nameof(ConclusaoCamadaOculta));
        OnPropertyChanged(nameof(ConclusaoPopulacao));
    }

    private static void Preencher(
        ObservableCollection<LinhaExperimentoComparativo> destino,
        IEnumerable<ConfiguracaoExperimento> configuracoes,
        IReadOnlyList<ExperimentoHiperparametros> registros)
    {
        var linhas = new List<LinhaExperimentoComparativo>();

        foreach (ConfiguracaoExperimento configuracao in configuracoes)
        {
            bool ehBaseline = configuracao.TamanhoPopulacao == ConfiguracaoExperimento.PopulacaoBaseline
                              && configuracao.NeuroniosOcultos == ConfiguracaoExperimento.NeuroniosBaseline;

            ExperimentoHiperparametros? maisRecente = registros
                .Where(r => r.TamanhoPopulacao == configuracao.TamanhoPopulacao
                            && r.NeuroniosOcultos == configuracao.NeuroniosOcultos)
                .OrderByDescending(r => r.DataHora)
                .FirstOrDefault();

            linhas.Add(maisRecente is null
                ? LinhaExperimentoComparativo.Pendente(configuracao.TamanhoPopulacao, configuracao.NeuroniosOcultos, ehBaseline)
                : LinhaExperimentoComparativo.Executada(maisRecente, ehBaseline));
        }

        // Destaques calculados por TABELA (não globalmente): a pergunta que cada tabela responde
        // é "qual valor deste hiperparâmetro é o melhor", então comparar com a outra série
        // misturaria os dois efeitos.
        List<LinhaExperimentoComparativo> executadas = linhas.Where(l => l.Executado).ToList();
        if (executadas.Count > 0)
        {
            LinhaExperimentoComparativo melhorFitness = executadas.OrderByDescending(l => l.FitnessFinal).First();
            melhorFitness.MelhorFitness = true;

            LinhaExperimentoComparativo? melhorCusto = executadas
                .Where(l => l.FitnessPorSegundo is not null)
                .OrderByDescending(l => l.FitnessPorSegundo)
                .FirstOrDefault();
            if (melhorCusto is not null) melhorCusto.MelhorCustoBeneficio = true;
        }

        destino.Clear();
        foreach (LinhaExperimentoComparativo linha in linhas) destino.Add(linha);
    }

    public string ConclusaoCamadaOculta => MontarConclusao(ComparativoCamadaOculta, "neurônios na camada oculta", l => l.NeuroniosOcultos);

    public string ConclusaoPopulacao => MontarConclusao(ComparativoPopulacao, "indivíduos na população", l => l.TamanhoPopulacao);

    private static string MontarConclusao(
        IEnumerable<LinhaExperimentoComparativo> linhas,
        string unidade,
        Func<LinhaExperimentoComparativo, int> valorVariado)
    {
        List<LinhaExperimentoComparativo> executadas = linhas.Where(l => l.Executado).ToList();
        if (executadas.Count == 0) return "Nenhuma configuração desta série foi executada ainda.";
        if (executadas.Count == 1) return $"Só uma configuração desta série foi executada ({valorVariado(executadas[0])} {unidade}) — rode as demais para que a comparação tenha sentido.";

        LinhaExperimentoComparativo melhor = executadas.OrderByDescending(l => l.FitnessFinal).First();
        LinhaExperimentoComparativo? custo = executadas
            .Where(l => l.FitnessPorSegundo is not null)
            .OrderByDescending(l => l.FitnessPorSegundo)
            .FirstOrDefault();

        string texto = $"Melhor fitness final: {valorVariado(melhor)} {unidade} ({melhor.FitnessFinalTexto}).";
        if (custo is not null)
        {
            texto += custo.MelhorFitness
                ? " Também é o melhor custo-benefício (maior fitness por segundo de treinamento)."
                : $" Melhor custo-benefício: {valorVariado(custo)} {unidade} ({custo.FitnessPorSegundoTexto} de fitness por segundo).";
        }

        return texto;
    }

    private void ExecutarExperimentos(bool refazer)
    {
        if (ExperimentosEmExecucao) return;

        IReadOnlyList<ExperimentoHiperparametros> jaRegistradas;
        try
        {
            jaRegistradas = _experimentoRepository.ObterTodos();
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível consultar os experimentos já registrados: {ex.Message}");
            StatusExperimentos = $"Não foi possível consultar os experimentos já registrados: {ex.Message}";
            return;
        }

        if (refazer)
        {
            if (!ConfirmarRefazer(jaRegistradas)) return;
            jaRegistradas = [];
        }

        ExperimentosEmExecucao = true;
        StatusExperimentos = "Preparando o plano experimental...";
        _ctsExperimentos = new CancellationTokenSource();
        CancellationToken token = _ctsExperimentos.Token;

        _ = _executorExperimentos
            .ExecutarAsync(
                jaRegistradas,
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
    /// "Refazer tudo" apaga as execuções anteriores das configurações do plano — é destrutivo e
    /// cada reexecução leva vários minutos, então exige confirmação explícita.
    /// </summary>
    private bool ConfirmarRefazer(IReadOnlyList<ExperimentoHiperparametros> jaRegistradas)
    {
        List<ExperimentoHiperparametros> aRemover = jaRegistradas
            .Where(r => ConfiguracaoExperimento.Planejadas.Any(c =>
                c.TamanhoPopulacao == r.TamanhoPopulacao && c.NeuroniosOcultos == r.NeuroniosOcultos))
            .ToList();

        if (aRemover.Count > 0)
        {
            MessageBoxResult resposta = MessageBox.Show(
                $"Isto apaga {aRemover.Count} execução(ões) já registrada(s) e treina todas as " +
                $"{ConfiguracaoExperimento.Planejadas.Count} configurações do plano de novo " +
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
        }

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
