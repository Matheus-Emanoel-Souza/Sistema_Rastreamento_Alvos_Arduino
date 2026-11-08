using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="IFontesDeDadosDashboardService"/> — cinco fontes, todas lendo de
/// repositórios já existentes (nenhuma consulta nova ao banco além do que essas telas já faziam).
/// Cada fonte declara, explicitamente (sem reflexão), quais campos podem virar eixo X/métrica —
/// escolha deliberada: um dicionário de delegates por campo é fácil de auditar e testar, e evita
/// expor campos que não fazem sentido num gráfico (ex.: <c>GenomaJson</c>, <c>Id</c> interno).
/// </summary>
public sealed class FontesDeDadosDashboardService : IFontesDeDadosDashboardService
{
    private const int LimiteHistoricoGeracoes = 100_000; // maior que o buffer circular real (300) — "traga tudo que houver".

    private readonly IMarcoFitnessTreinamentoRepository _marcos;
    private readonly IInstantaneoComportamentoRepository _instantaneos;
    private readonly IHistoricoTreinamentoRepository _historico;
    private readonly IExperimentoHiperparametrosRepository _experimentosHiperparametros;
    private readonly IExperimentoRedeNeuralRepository _experimentosRedeNeural;

    public FontesDeDadosDashboardService(
        IMarcoFitnessTreinamentoRepository marcos,
        IInstantaneoComportamentoRepository instantaneos,
        IHistoricoTreinamentoRepository historico,
        IExperimentoHiperparametrosRepository experimentosHiperparametros,
        IExperimentoRedeNeuralRepository experimentosRedeNeural)
    {
        _marcos = marcos;
        _instantaneos = instantaneos;
        _historico = historico;
        _experimentosHiperparametros = experimentosHiperparametros;
        _experimentosRedeNeural = experimentosRedeNeural;
    }

    // ---------------------------------------------------------------- Catálogo

    private const string FonteMarcos = "MarcosFitness";
    private const string FonteInstantaneos = "InstantaneosComportamento";
    private const string FonteHistorico = "HistoricoGeracoes";
    private const string FonteExperimentosHiperparametros = "ExperimentosHiperparametros";
    private const string FonteExperimentosRedeNeural = "ExperimentosRedeNeural";

    public IReadOnlyList<FonteDadosDashboard> ObterFontes() =>
    [
        new FonteDadosDashboard(FonteMarcos, "Marcos de fitness (a cada 500 gerações)",
            CamposEixoXMarcos.Select(ParaCampo).ToList(),
            CamposMetricaMarcos.Select(ParaCampo).ToList()),

        new FonteDadosDashboard(FonteInstantaneos, "Instantâneos de comportamento (a cada 1.000 gerações)",
            CamposEixoXInstantaneos.Select(ParaCampo).ToList(),
            CamposMetricaInstantaneos.Select(ParaCampo).ToList()),

        new FonteDadosDashboard(FonteHistorico, "Histórico recente de gerações (últimas 300)",
            CamposEixoXHistorico.Select(ParaCampo).ToList(),
            CamposMetricaHistorico.Select(ParaCampo).ToList()),

        new FonteDadosDashboard(FonteExperimentosHiperparametros, "Experimentos de hiperparâmetros",
            CamposEixoXExpHiper.Select(ParaCampo).ToList(),
            CamposMetricaExpHiper.Select(ParaCampo).ToList()),

        new FonteDadosDashboard(FonteExperimentosRedeNeural, "Experimentos de comparação entre versões da rede neural",
            CamposEixoXExpRede.Select(ParaCampo).ToList(),
            CamposMetricaExpRede.Select(ParaCampo).ToList()),
    ];

    private static CampoDashboard ParaCampo(KeyValuePair<string, string> par) => new(par.Key, par.Value);

    public IReadOnlyList<PontoSerieDashboard> ObterSerie(string fonteChave, string campoEixoXChave, string campoMetricaChave) => fonteChave switch
    {
        FonteMarcos => Construir(_marcos.ObterTodos(), EixoXMarcos, MetricaMarcos, campoEixoXChave, campoMetricaChave),
        FonteInstantaneos => Construir(_instantaneos.ObterTodos(), EixoXInstantaneos, MetricaInstantaneos, campoEixoXChave, campoMetricaChave),
        FonteHistorico => Construir(_historico.ObterUltimos(LimiteHistoricoGeracoes), EixoXHistorico, MetricaHistorico, campoEixoXChave, campoMetricaChave),
        FonteExperimentosHiperparametros => Construir(_experimentosHiperparametros.ObterTodos(), EixoXExpHiper, MetricaExpHiper, campoEixoXChave, campoMetricaChave),
        FonteExperimentosRedeNeural => Construir(_experimentosRedeNeural.ObterTodos(), EixoXExpRede, MetricaExpRede, campoEixoXChave, campoMetricaChave),
        _ => Array.Empty<PontoSerieDashboard>(),
    };

    // ---------------------------------------------------------------- MarcosFitness

    private static readonly Dictionary<string, string> CamposEixoXMarcos = new() { ["GeracaoMarco"] = "Geração" };
    private static readonly Dictionary<string, string> CamposMetricaMarcos = new()
    {
        ["FitnessMedioDoBloco"] = "Fitness médio do bloco",
        ["MelhorFitnessDoBloco"] = "Melhor fitness do bloco",
    };
    private static readonly Dictionary<string, Func<MarcoFitnessTreinamento, double>> EixoXMarcos = new()
    {
        ["GeracaoMarco"] = m => m.GeracaoMarco,
    };
    private static readonly Dictionary<string, Func<MarcoFitnessTreinamento, double?>> MetricaMarcos = new()
    {
        ["FitnessMedioDoBloco"] = m => m.FitnessMedioDoBloco,
        ["MelhorFitnessDoBloco"] = m => m.MelhorFitnessDoBloco,
    };

    // ---------------------------------------------------------------- InstantaneosComportamento

    private static readonly Dictionary<string, string> CamposEixoXInstantaneos = new() { ["GeracaoMarco"] = "Geração" };
    private static readonly Dictionary<string, string> CamposMetricaInstantaneos = new()
    {
        ["Fitness"] = "Fitness",
        ["AlvosAbatidos"] = "Alvos abatidos",
        ["AlvosEscapados"] = "Alvos escapados",
        ["TirosErrados"] = "Tiros errados",
        ["TaxaDeAcerto"] = "Taxa de acerto",
        ["TaxaDeCobertura"] = "Taxa de cobertura",
    };
    private static readonly Dictionary<string, Func<InstantaneoComportamentoMelhorGene, double>> EixoXInstantaneos = new()
    {
        ["GeracaoMarco"] = i => i.GeracaoMarco,
    };
    private static readonly Dictionary<string, Func<InstantaneoComportamentoMelhorGene, double?>> MetricaInstantaneos = new()
    {
        ["Fitness"] = i => i.Fitness,
        ["AlvosAbatidos"] = i => i.AlvosAbatidos,
        ["AlvosEscapados"] = i => i.AlvosEscapados,
        ["TirosErrados"] = i => i.TirosErrados,
        ["TaxaDeAcerto"] = i => i.TaxaDeAcerto,
        ["TaxaDeCobertura"] = i => i.TaxaDeCobertura,
    };

    // ---------------------------------------------------------------- HistoricoGeracoes

    private static readonly Dictionary<string, string> CamposEixoXHistorico = new() { ["Geracao"] = "Geração" };
    private static readonly Dictionary<string, string> CamposMetricaHistorico = new()
    {
        ["MelhorFitness"] = "Melhor fitness",
        ["FitnessMedio"] = "Fitness médio",
        ["ComplexidadeMelhor"] = "Conexões do campeão",
        ["QuantidadeEspecies"] = "Quantidade de espécies",
    };
    private static readonly Dictionary<string, Func<HistoricoGeracaoTreinamento, double>> EixoXHistorico = new()
    {
        ["Geracao"] = h => h.Geracao,
    };
    private static readonly Dictionary<string, Func<HistoricoGeracaoTreinamento, double?>> MetricaHistorico = new()
    {
        ["MelhorFitness"] = h => h.MelhorFitness,
        ["FitnessMedio"] = h => h.FitnessMedio,
        ["ComplexidadeMelhor"] = h => h.ComplexidadeMelhor,
        ["QuantidadeEspecies"] = h => h.QuantidadeEspecies,
    };

    // ---------------------------------------------------------------- ExperimentosHiperparametros

    private static readonly Dictionary<string, string> CamposEixoXExpHiper = new()
    {
        ["Id"] = "Id do experimento",
        ["TamanhoPopulacao"] = "Tamanho da população",
        ["Geracoes"] = "Gerações",
        ["NeuroniosOcultos"] = "Neurônios ocultos / teto de complexidade",
    };
    private static readonly Dictionary<string, string> CamposMetricaExpHiper = new()
    {
        ["FitnessFinal"] = "Fitness final",
        ["TempoSegundos"] = "Tempo (s)",
        ["GeracaoDoPico"] = "Geração do pico",
        ["FitnessGeracao25"] = "Fitness na geração 25",
        ["FitnessGeracao50"] = "Fitness na geração 50",
        ["FitnessGeracao100"] = "Fitness na geração 100",
        ["FitnessGeracao200"] = "Fitness na geração 200",
    };
    private static readonly Dictionary<string, Func<ExperimentoHiperparametros, double>> EixoXExpHiper = new()
    {
        ["Id"] = e => e.Id,
        ["TamanhoPopulacao"] = e => e.TamanhoPopulacao,
        ["Geracoes"] = e => e.Geracoes,
        ["NeuroniosOcultos"] = e => e.NeuroniosOcultos,
    };
    private static readonly Dictionary<string, Func<ExperimentoHiperparametros, double?>> MetricaExpHiper = new()
    {
        ["FitnessFinal"] = e => e.FitnessFinal,
        ["TempoSegundos"] = e => e.TempoSegundos,
        ["GeracaoDoPico"] = e => e.GeracaoDoPico,
        ["FitnessGeracao25"] = e => e.FitnessGeracao25,
        ["FitnessGeracao50"] = e => e.FitnessGeracao50,
        ["FitnessGeracao100"] = e => e.FitnessGeracao100,
        ["FitnessGeracao200"] = e => e.FitnessGeracao200,
    };

    // ---------------------------------------------------------------- ExperimentosRedeNeural

    private static readonly Dictionary<string, string> CamposEixoXExpRede = new()
    {
        ["Id"] = "Id do experimento",
        ["RedeNeuralId"] = "Versão da rede (Id)",
        ["Repeticao"] = "Repetição",
    };
    private static readonly Dictionary<string, string> CamposMetricaExpRede = new()
    {
        ["FitnessTreinoPico"] = "Fitness de treino (pico)",
        ["GeracaoDoPico"] = "Geração do pico",
        ["SegundosTreino"] = "Tempo de treino (s)",
        ["ValFitnessMedio"] = "Fitness médio (validação)",
        ["ValAbatidosMedio"] = "Abatidos (médio, validação)",
        ["ValEscapadosMedio"] = "Escapados (médio, validação)",
        ["ValTirosErradosMedio"] = "Tiros errados (médio, validação)",
        ["ValTempoRespostaMedioSegundos"] = "Tempo de resposta médio (s)",
        ["ValTrocasPenalizadasMedia"] = "Trocas de alvo penalizadas (média)",
        ["ValCooperacoesConcedidasMedia"] = "Cooperações concedidas (média)",
        ["ValCooperacoesNegadasMedia"] = "Cooperações negadas (média)",
    };
    private static readonly Dictionary<string, Func<ExperimentoRedeNeural, double>> EixoXExpRede = new()
    {
        ["Id"] = e => e.Id,
        ["RedeNeuralId"] = e => e.RedeNeuralId,
        ["Repeticao"] = e => e.Repeticao,
    };
    private static readonly Dictionary<string, Func<ExperimentoRedeNeural, double?>> MetricaExpRede = new()
    {
        ["FitnessTreinoPico"] = e => e.FitnessTreinoPico,
        ["GeracaoDoPico"] = e => e.GeracaoDoPico,
        ["SegundosTreino"] = e => e.SegundosTreino,
        ["ValFitnessMedio"] = e => e.ValFitnessMedio,
        ["ValAbatidosMedio"] = e => e.ValAbatidosMedio,
        ["ValEscapadosMedio"] = e => e.ValEscapadosMedio,
        ["ValTirosErradosMedio"] = e => e.ValTirosErradosMedio,
        ["ValTempoRespostaMedioSegundos"] = e => e.ValTempoRespostaMedioSegundos,
        ["ValTrocasPenalizadasMedia"] = e => e.ValTrocasPenalizadasMedia,
        ["ValCooperacoesConcedidasMedia"] = e => e.ValCooperacoesConcedidasMedia,
        ["ValCooperacoesNegadasMedia"] = e => e.ValCooperacoesNegadasMedia,
    };

    // ---------------------------------------------------------------- Montagem genérica

    /// <summary>
    /// Resolve uma lista já carregada (<paramref name="itens"/>) para pontos de série, usando os
    /// delegates de eixo X/métrica declarados para aquela fonte. Pontos cuja métrica é
    /// <c>null</c> (campo opcional não medido naquela linha) são descartados em vez de virarem
    /// zero — um zero ali seria um dado inventado, não um dado real.
    /// </summary>
    private static IReadOnlyList<PontoSerieDashboard> Construir<T>(
        IReadOnlyList<T> itens,
        IReadOnlyDictionary<string, Func<T, double>> eixosX,
        IReadOnlyDictionary<string, Func<T, double?>> metricas,
        string campoEixoXChave,
        string campoMetricaChave)
    {
        if (!eixosX.TryGetValue(campoEixoXChave, out Func<T, double>? eixoXFn) ||
            !metricas.TryGetValue(campoMetricaChave, out Func<T, double?>? metricaFn))
        {
            return Array.Empty<PontoSerieDashboard>();
        }

        var pontos = new List<PontoSerieDashboard>();
        foreach (T item in itens)
        {
            double? valor = metricaFn(item);
            if (valor is null) continue;

            double ordinal = eixoXFn(item);
            pontos.Add(new PontoSerieDashboard(ordinal.ToString("0.###", CultureInfo.InvariantCulture), ordinal, valor.Value));
        }

        return pontos.OrderBy(p => p.OrdinalEixoX).ToList();
    }
}
