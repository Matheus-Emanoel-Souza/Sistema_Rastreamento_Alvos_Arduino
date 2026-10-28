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
/// pedida no parecer do artigo: duas séries em que se varia UM hiperparâmetro por vez, todo o
/// resto mantido idêntico. Cada configuração concluída é gravada na hora em
/// <see cref="IExperimentoHiperparametrosRepository"/>, de modo que uma interrupção no meio
/// preserva o que já rodou.
/// </summary>
public interface IExecutorExperimentosHiperparametros
{
    /// <summary>True enquanto uma bateria está rodando — bloqueia uma segunda execução simultânea.</summary>
    bool EmExecucao { get; }

    /// <summary>
    /// Roda as configurações de <see cref="ConfiguracaoExperimento.Planejadas"/> que ainda não
    /// estiverem em <paramref name="jaRegistradas"/>, em uma thread de fundo.
    /// <paramref name="aoConcluirConfiguracao"/> é chamado a cada configuração gravada e
    /// <paramref name="aoProgredir"/> a cada avanço relevante (ambos fora da thread de UI —
    /// quem chama é responsável por despachar).
    /// </summary>
    Task ExecutarAsync(
        IReadOnlyCollection<ExperimentoHiperparametros> jaRegistradas,
        Action<string> aoProgredir,
        Action<ExperimentoHiperparametros> aoConcluirConfiguracao,
        CancellationToken token);
}

/// <summary>
/// Uma configuração do plano experimental: população × neurônios ocultos. As duas séries
/// compartilham a configuração de referência (<see cref="PopulacaoBaseline"/> ×
/// <see cref="NeuroniosBaseline"/>), que por isso só é treinada uma vez e aparece nas duas
/// tabelas — 5 execuções no total, não 6.
/// </summary>
public readonly record struct ConfiguracaoExperimento(int TamanhoPopulacao, int NeuroniosOcultos)
{
    /// <summary>População de referência — o padrão da tela de Treinamento do Controlador.</summary>
    public const int PopulacaoBaseline = 60;

    /// <summary>Camada oculta de referência — a de <see cref="ControladorHibrido.ArquiteturaPadrao"/>, isto é, a que o sistema usa em produção.</summary>
    public const int NeuroniosBaseline = 8;

    /// <summary>Gerações rodadas por configuração — fixo e igual para todas, único jeito de isolar o efeito do hiperparâmetro variado (todo mundo para no mesmo ponto).</summary>
    public const int GeracoesPorExperimento = 300;

    /// <summary>Série A — varia só a camada oculta, população travada no baseline.</summary>
    public static readonly IReadOnlyList<int> NeuroniosDaSerie = [4, 8, 16];

    /// <summary>Série B — varia só a população, camada oculta travada no baseline.</summary>
    public static readonly IReadOnlyList<int> PopulacoesDaSerie = [30, 60, 120];

    /// <summary>União das duas séries, sem repetir o baseline (que pertence às duas).</summary>
    public static IReadOnlyList<ConfiguracaoExperimento> Planejadas { get; } =
        NeuroniosDaSerie.Select(n => new ConfiguracaoExperimento(PopulacaoBaseline, n))
            .Concat(PopulacoesDaSerie.Select(p => new ConfiguracaoExperimento(p, NeuroniosBaseline)))
            .Distinct()
            .ToList();

    public override string ToString() => $"população={TamanhoPopulacao}, neurônios ocultos={NeuroniosOcultos}";
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
        IReadOnlyCollection<ExperimentoHiperparametros> jaRegistradas,
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
                Executar(jaRegistradas, aoProgredir, aoConcluirConfiguracao, token);
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
        IReadOnlyCollection<ExperimentoHiperparametros> jaRegistradas,
        Action<string> aoProgredir,
        Action<ExperimentoHiperparametros> aoConcluirConfiguracao,
        CancellationToken token)
    {
        // Parâmetros de fábrica (não os da tela): as duas séries precisam rodar sob condições
        // idênticas para a diferença de fitness ser atribuível ao hiperparâmetro variado, e não
        // a um ajuste que o usuário tenha feito em "Treinamento do Controlador" entre uma
        // execução e outra. TamanhoPopulacao é sobrescrito por configuração logo abaixo.
        var parametrosBase = new ParametrosTreinamento();

        List<ConfiguracaoExperimento> pendentes = ConfiguracaoExperimento.Planejadas
            .Where(c => !jaRegistradas.Any(r =>
                r.TamanhoPopulacao == c.TamanhoPopulacao &&
                r.NeuroniosOcultos == c.NeuroniosOcultos &&
                r.Geracoes >= ConfiguracaoExperimento.GeracoesPorExperimento))
            .ToList();

        if (pendentes.Count == 0)
        {
            aoProgredir("Todas as configurações do plano experimental já possuem execução registrada — use \"Refazer tudo\" para treiná-las de novo.");
            return;
        }

        int indice = 0;
        foreach (ConfiguracaoExperimento configuracao in pendentes)
        {
            indice++;
            if (token.IsCancellationRequested) break;

            ExperimentoHiperparametros? registro = TreinarConfiguracao(
                configuracao, parametrosBase, indice, pendentes.Count, aoProgredir, token);

            // Configuração cancelada antes de completar as gerações previstas: descartada em vez
            // de virar uma linha mais curta (e portanto incomparável) na tabela.
            if (registro is null) break;

            try
            {
                _repositorio.Registrar(registro);
                aoConcluirConfiguracao(registro);
                _logger.Success(
                    $"Experimento concluído ({configuracao}): gerações={registro.Geracoes}, " +
                    $"fitness final={registro.FitnessFinal:0.0}, tempo={registro.TempoSegundos:0.0}s.");
            }
            catch (Exception ex)
            {
                _logger.Error($"Não foi possível registrar o experimento ({configuracao}): {ex.Message}");
            }
        }

        aoProgredir(token.IsCancellationRequested
            ? "Execução interrompida — as configurações já concluídas continuam registradas."
            : $"Plano experimental concluído: {pendentes.Count} configuração(ões) treinada(s) e registrada(s).");
    }

    private static ExperimentoHiperparametros? TreinarConfiguracao(
        ConfiguracaoExperimento configuracao,
        ParametrosTreinamento parametrosBase,
        int indice,
        int total,
        Action<string> aoProgredir,
        CancellationToken token)
    {
        int[] arquitetura =
        [
            ControladorHibrido.QuantidadeEntradas,
            configuracao.NeuroniosOcultos,
            ControladorHibrido.QuantidadeSaidas
        ];

        ParametrosTreinamento parametros = parametrosBase with { TamanhoPopulacao = configuracao.TamanhoPopulacao };
        var ambiente = new AmbienteTreinamento(parametros, arquitetura: arquitetura);

        // Elitismo proporcional (10% da população, no mínimo 2) — mesma regra do treinamento
        // normal; um elitismo fixo seria uma fração grande demais em população 30 e pequena
        // demais em 120, virando na prática um segundo hiperparâmetro variando junto.
        var algoritmo = new AlgoritmoGenetico(
            tamanhoGenoma: RedeNeural.TamanhoGenoma(arquitetura),
            tamanhoPopulacao: configuracao.TamanhoPopulacao,
            taxaMutacao: parametros.TaxaMutacao,
            elitismo: Math.Max(2, configuracao.TamanhoPopulacao / 10));

        var cronometro = Stopwatch.StartNew();
        ResultadoGeracao? ultimoResultado = null;

        for (int g = 0; g < ConfiguracaoExperimento.GeracoesPorExperimento && !token.IsCancellationRequested; g++)
        {
            // Mesma regra do treinamento normal: seeds fixas por geração, para a seleção comparar
            // qualidade de genoma e não sorte de cenário.
            int[] seedsDaGeracao = Enumerable.Range(0, Math.Max(1, parametros.EpisodiosPorAvaliacao))
                .Select(i => algoritmo.GeracaoAtual * 1000 + i)
                .ToArray();

            ultimoResultado = algoritmo.EvoluirGeracao(
                genoma => ambiente.AvaliarFitness(genoma, seedsDaGeracao, CatalogoCenarios.TodosComScript));

            if (ultimoResultado.Geracao % 25 == 0 || ultimoResultado.Geracao == ConfiguracaoExperimento.GeracoesPorExperimento)
            {
                ResultadoGeracao progresso = ultimoResultado;
                aoProgredir(
                    $"Configuração {indice}/{total} ({configuracao}): " +
                    $"geração {progresso.Geracao}/{ConfiguracaoExperimento.GeracoesPorExperimento}, " +
                    $"melhor fitness={progresso.MelhorFitness:0.0} · {cronometro.Elapsed.TotalSeconds:0}s");
            }
        }

        cronometro.Stop();

        if (ultimoResultado is null) return null;
        if (ultimoResultado.Geracao < ConfiguracaoExperimento.GeracoesPorExperimento) return null;

        return new ExperimentoHiperparametros
        {
            TamanhoPopulacao = configuracao.TamanhoPopulacao,
            NeuroniosOcultos = configuracao.NeuroniosOcultos,
            Geracoes = ultimoResultado.Geracao,
            FitnessFinal = ultimoResultado.MelhorFitness,
            TempoSegundos = cronometro.Elapsed.TotalSeconds,
            DataHora = DateTime.Now
        };
    }
}
