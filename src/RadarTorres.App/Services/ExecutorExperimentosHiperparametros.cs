using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RadarTorres.App.Models;
using RadarTorres.App.NeuralEvolution;
using RadarTorres.App.Repositories;

namespace RadarTorres.App.Services;

/// <summary>
/// Executa, de verdade (sem UI e sem dado simulado), a análise experimental de hiperparâmetros
/// pedida no parecer do artigo: séries em que se varia UM hiperparâmetro por vez (teto de
/// complexidade, tamanho da população ou probabilidade de mutação estrutural), todo o resto
/// mantido idêntico. Cada configuração concluída é gravada na hora em
/// <see cref="IExperimentoHiperparametrosRepository"/>, de modo que uma interrupção no meio
/// preserva o que já rodou.
/// </summary>
public interface IExecutorExperimentosHiperparametros
{
    /// <summary>True enquanto uma bateria está rodando — bloqueia uma segunda execução simultânea.</summary>
    bool EmExecucao { get; }

    /// <summary>
    /// Treina, em uma thread de fundo, exatamente as configurações de
    /// <paramref name="aExecutar"/> — quem decide o que está pendente é a tela, que conhece as
    /// séries efetivamente exibidas (ver <c>EstatisticasTreinamentoViewModel</c>).
    /// <paramref name="aoConcluirConfiguracao"/> é chamado a cada configuração gravada e
    /// <paramref name="aoProgredir"/> a cada avanço relevante (ambos fora da thread de UI —
    /// quem chama é responsável por despachar).
    /// </summary>
    Task ExecutarAsync(
        IReadOnlyList<ConfiguracaoExperimento> aExecutar,
        Action<string> aoProgredir,
        Action<ExperimentoHiperparametros> aoConcluirConfiguracao,
        CancellationToken token);
}

/// <summary>
/// Uma configuração do plano experimental: população × teto de complexidade × probabilidade de
/// mutação estrutural. Uma mesma configuração pode pertencer a mais de uma tabela da tela (a
/// configuração baseline pertence às três) — ela é treinada UMA vez e reaproveitada em todas,
/// nunca em duplicidade.
/// </summary>
/// <remarks>
/// Os dois últimos eixos MUDARAM DE SIGNIFICADO na migração para o SharpNEAT. Antes eram
/// "neurônios na camada oculta" e "taxa de mutação por gene" — ambos inexistentes no NEAT, onde
/// a topologia é evoluída e não configurada. Os substitutos são os hiperparâmetros NEAT que
/// desempenham o papel equivalente: quanto a rede pode crescer, e com que frequência ela ganha
/// estrutura nova. As execuções antigas gravadas no banco são de OUTRO método e não são
/// comparáveis linha a linha com as novas — por isso cada registro passa a gravar
/// <see cref="Models.ExperimentoHiperparametros.Metodo"/>.
/// </remarks>
public readonly record struct ConfiguracaoExperimento(int TamanhoPopulacao, int TetoComplexidade, double ProbabilidadeAdicionarNo)
{
    /// <summary>População de referência — o padrão da tela de Treinamento do Controlador.</summary>
    public const int PopulacaoBaseline = 60;

    /// <summary>Teto de complexidade de referência — o mesmo padrão de <see cref="TreinadorNeat"/>.</summary>
    public const int TetoComplexidadeBaseline = 60;

    /// <summary>Probabilidade de mutação estrutural de referência — o padrão do SharpNEAT para acrescentar um nó.</summary>
    public const double ProbabilidadeAdicionarNoBaseline = 0.01;

    /// <summary>Gerações rodadas por configuração — fixo e igual para todas, único jeito de isolar o efeito do hiperparâmetro variado (todo mundo para no mesmo ponto).</summary>
    public const int GeracoesPorExperimento = 300;

    /// <summary>Gerações em que o melhor fitness é fotografado, para montar a curva de convergência (ver <see cref="ExperimentoHiperparametros.FitnessGeracao25"/>).</summary>
    public static readonly IReadOnlyList<int> GeracoesDaCurva = [25, 50, 100, 200];

    /// <summary>Série A — varia só o teto de complexidade: metade, padrão e dobro.</summary>
    public static readonly IReadOnlyList<int> TetosComplexidadeDaSerie = [30, 60, 120];

    /// <summary>Série B — varia só a população.</summary>
    public static readonly IReadOnlyList<int> PopulacoesDaSerie = [30, 60, 120];

    /// <summary>Série C — varia só a probabilidade de mutação estrutural: metade, padrão e dobro do valor do sistema.</summary>
    public static readonly IReadOnlyList<double> ProbabilidadesAdicionarNoDaSerie = [0.005, 0.01, 0.02];

    /// <summary>União das três séries de referência, sem repetir a configuração baseline (que pertence às três).</summary>
    public static IReadOnlyList<ConfiguracaoExperimento> Planejadas { get; } =
        TetosComplexidadeDaSerie.Select(t => new ConfiguracaoExperimento(PopulacaoBaseline, t, ProbabilidadeAdicionarNoBaseline))
            .Concat(PopulacoesDaSerie.Select(p => new ConfiguracaoExperimento(p, TetoComplexidadeBaseline, ProbabilidadeAdicionarNoBaseline)))
            .Concat(ProbabilidadesAdicionarNoDaSerie.Select(pr => new ConfiguracaoExperimento(PopulacaoBaseline, TetoComplexidadeBaseline, pr)))
            .Distinct()
            .ToList();

    public override string ToString() =>
        $"população={TamanhoPopulacao}, teto de complexidade={TetoComplexidade}, mutação estrutural={ProbabilidadeAdicionarNo:0.###}";
}

/// <inheritdoc cref="IExecutorExperimentosHiperparametros"/>
public sealed class ExecutorExperimentosHiperparametros : IExecutorExperimentosHiperparametros
{
    private readonly IExperimentoHiperparametrosRepository _repositorio;
    private readonly ILoggingService _logger;
    private int _emExecucao;

    public ExecutorExperimentosHiperparametros(
        IExperimentoHiperparametrosRepository repositorio,
        ILoggingService logger)
    {
        _repositorio = repositorio;
        _logger = logger;
    }

    public bool EmExecucao => Volatile.Read(ref _emExecucao) == 1;

    public Task ExecutarAsync(
        IReadOnlyList<ConfiguracaoExperimento> aExecutar,
        Action<string> aoProgredir,
        Action<ExperimentoHiperparametros> aoConcluirConfiguracao,
        CancellationToken token)
    {
        if (Interlocked.CompareExchange(ref _emExecucao, 1, 0) == 1)
        {
            aoProgredir("Já existe uma bateria de experimentos em andamento.");
            return Task.CompletedTask;
        }

        return Task.Run(() =>
        {
            try
            {
                Executar(aExecutar, aoProgredir, aoConcluirConfiguracao, token);
            }
            catch (Exception ex)
            {
                _logger.Error($"Falha na execução do plano experimental: {ex.Message}");
                aoProgredir($"Falha na execução do plano experimental: {ex.Message}");
            }
            finally
            {
                Volatile.Write(ref _emExecucao, 0);
            }
        }, CancellationToken.None);
    }

    private void Executar(
        IReadOnlyList<ConfiguracaoExperimento> aExecutar,
        Action<string> aoProgredir,
        Action<ExperimentoHiperparametros> aoConcluirConfiguracao,
        CancellationToken token)
    {
        // Parâmetros de fábrica (não os da tela): todas as séries precisam rodar sob condições
        // idênticas para a diferença de fitness ser atribuível ao hiperparâmetro variado, e não
        // a um ajuste que o usuário tenha feito em "Treinamento do Controlador" entre uma
        // execução e outra. TamanhoPopulacao é sobrescrito por configuração logo abaixo.
        var parametrosBase = new ParametrosTreinamento();

        if (aExecutar.Count == 0)
        {
            aoProgredir("Todas as configurações exibidas já possuem execução registrada — use \"Refazer tudo\" para treiná-las de novo.");
            return;
        }

        int indice = 0;
        foreach (ConfiguracaoExperimento configuracao in aExecutar)
        {
            indice++;
            if (token.IsCancellationRequested) break;

            ExperimentoHiperparametros? registro = TreinarConfiguracao(
                configuracao, parametrosBase, indice, aExecutar.Count, aoProgredir, token);

            // Configuração cancelada antes de completar as gerações previstas: descartada em vez
            // de virar uma linha mais curta (e portanto incomparável) na tabela.
            if (registro is null) break;

            try
            {
                _repositorio.Registrar(registro);
                aoConcluirConfiguracao(registro);
                _logger.Success(
                    $"Experimento concluído ({configuracao}): gerações={registro.Geracoes}, " +
                    $"fitness final={registro.FitnessFinal:0.0} (pico na geração {registro.GeracaoDoPico}), " +
                    $"tempo={registro.TempoSegundos:0.0}s.");
            }
            catch (Exception ex)
            {
                _logger.Error($"Não foi possível registrar o experimento ({configuracao}): {ex.Message}");
            }
        }

        aoProgredir(token.IsCancellationRequested
            ? "Execução interrompida — as configurações já concluídas continuam registradas."
            : $"Plano experimental concluído: {aExecutar.Count} configuração(ões) treinada(s) e registrada(s).");
    }

    private static ExperimentoHiperparametros? TreinarConfiguracao(
        ConfiguracaoExperimento configuracao,
        ParametrosTreinamento parametrosBase,
        int indice,
        int total,
        Action<string> aoProgredir,
        CancellationToken token)
    {
        ParametrosTreinamento parametros = parametrosBase with
        {
            TamanhoPopulacao = configuracao.TamanhoPopulacao,
            ProbabilidadeAdicionarNo = configuracao.ProbabilidadeAdicionarNo,
            TetoComplexidade = configuracao.TetoComplexidade
        };
        var ambiente = new AmbienteTreinamento(parametros);

        var algoritmo = new TreinadorNeat(
            ambiente,
            tamanhoPopulacao: configuracao.TamanhoPopulacao,
            tetoComplexidade: configuracao.TetoComplexidade,
            probabilidadeAdicionarNo: configuracao.ProbabilidadeAdicionarNo);

        var cronometro = Stopwatch.StartNew();
        ResultadoGeracao? ultimoResultado = null;

        // Velocidade de evolução: o melhor fitness já visto, a primeira geração em que ele
        // apareceu, e fotos da curva nas gerações de GeracoesDaCurva. É o que permite distinguir
        // "chegou no mesmo lugar" de "chegou no mesmo lugar MUITO antes".
        double melhorAteAgora = double.NegativeInfinity;
        int geracaoDoPico = 0;
        var curva = new Dictionary<int, double>();

        for (int g = 0; g < ConfiguracaoExperimento.GeracoesPorExperimento && !token.IsCancellationRequested; g++)
        {
            // Mesma regra do treinamento normal: seeds fixas por geração, para a seleção comparar
            // qualidade de genoma e não sorte de cenário.
            int[] seedsDaGeracao = Enumerable.Range(0, Math.Max(1, parametros.EpisodiosPorAvaliacao))
                .Select(i => algoritmo.GeracaoAtual * 1000 + i)
                .ToArray();

            ultimoResultado = algoritmo.EvoluirGeracao(seedsDaGeracao, CatalogoCenarios.TodosComScript);

            if (ultimoResultado.MelhorFitness > melhorAteAgora)
            {
                melhorAteAgora = ultimoResultado.MelhorFitness;
                geracaoDoPico = ultimoResultado.Geracao;
            }

            if (ConfiguracaoExperimento.GeracoesDaCurva.Contains(ultimoResultado.Geracao))
            {
                curva[ultimoResultado.Geracao] = melhorAteAgora;
            }

            if (ultimoResultado.Geracao % 25 == 0 || ultimoResultado.Geracao == ConfiguracaoExperimento.GeracoesPorExperimento)
            {
                aoProgredir(
                    $"Configuração {indice}/{total} ({configuracao}): " +
                    $"geração {ultimoResultado.Geracao}/{ConfiguracaoExperimento.GeracoesPorExperimento}, " +
                    $"melhor fitness={melhorAteAgora:0.0} (pico na geração {geracaoDoPico}) · {cronometro.Elapsed.TotalSeconds:0}s");
            }
        }

        cronometro.Stop();

        if (ultimoResultado is null) return null;
        if (ultimoResultado.Geracao < ConfiguracaoExperimento.GeracoesPorExperimento) return null;

        return new ExperimentoHiperparametros
        {
            TamanhoPopulacao = configuracao.TamanhoPopulacao,

            // As duas colunas abaixo guardam, desde a migração para o SharpNEAT, os
            // hiperparâmetros NEAT equivalentes (ver ConfiguracaoExperimento). Os nomes das
            // colunas foram mantidos para não invalidar as execuções já gravadas; o que separa
            // uma geração de dados da outra é a coluna METODO.
            NeuroniosOcultos = configuracao.TetoComplexidade,
            TaxaMutacao = configuracao.ProbabilidadeAdicionarNo,
            Metodo = ExperimentoHiperparametros.MetodoNeat,
            Geracoes = ultimoResultado.Geracao,

            // O melhor de TODAS as gerações, não o da última: com elitismo o fitness não deveria
            // regredir, mas a avaliação é estocástica (seeds mudam a cada geração), então a
            // última geração pode marcar abaixo do pico já alcançado.
            FitnessFinal = melhorAteAgora,
            GeracaoDoPico = geracaoDoPico,
            FitnessGeracao25 = curva.TryGetValue(25, out double g25) ? g25 : null,
            FitnessGeracao50 = curva.TryGetValue(50, out double g50) ? g50 : null,
            FitnessGeracao100 = curva.TryGetValue(100, out double g100) ? g100 : null,
            FitnessGeracao200 = curva.TryGetValue(200, out double g200) ? g200 : null,
            TempoSegundos = cronometro.Elapsed.TotalSeconds,
            DataHora = DateTime.Now
        };
    }
}
