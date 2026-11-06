using System;
using System.Collections.Generic;
using SharpNeat;
using SharpNeat.Evaluation;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// O que muda de uma geração para a outra na avaliação: as seeds sorteadas para esta geração e
/// os casos táticos fixos. É um objeto mutável compartilhado porque o SharpNEAT constrói o
/// esquema de avaliação UMA vez (no início do treinamento) e o reutiliza em todas as gerações —
/// não há como passar as seeds da geração por parâmetro. Quem comanda o treinamento
/// (<see cref="TreinadorNeat"/>) atualiza isto antes de cada <c>PerformOneGeneration</c>.
/// </summary>
public sealed class ContextoAvaliacaoNeat
{
    /// <summary>Seeds dos episódios aleatórios desta geração — as MESMAS para todos os indivíduos, para a comparação dentro da geração continuar justa.</summary>
    public IReadOnlyList<int> Seeds { get; set; } = [0];

    /// <summary>Casos táticos mapeados somados aos episódios aleatórios (ver <see cref="CatalogoCenarios"/>), ou <c>null</c>.</summary>
    public IReadOnlyList<TipoCenarioTreinamento>? CenariosFixos { get; set; }

    /// <summary>Modo de decisão avaliado — produção é <see cref="ControladorHibrido.ModoDecisao.Hibrido"/>; os demais só para o estudo de ablação.</summary>
    public ControladorHibrido.ModoDecisao Modo { get; set; } = ControladorHibrido.ModoDecisao.Hibrido;
}

/// <summary>
/// Liga o <see cref="AmbienteTreinamento"/> (nossa simulação e nossa função de fitness) ao
/// SharpNEAT. É a peça que o framework exige: ele sabe evoluir grafos, mas não sabe nada sobre
/// torres, alvos ou quadrantes — tudo isso continua sendo nosso.
/// </summary>
public sealed class EsquemaAvaliacaoAmbiente : IBlackBoxEvaluationScheme<double>
{
    private readonly AmbienteTreinamento _ambiente;
    private readonly ContextoAvaliacaoNeat _contexto;

    public EsquemaAvaliacaoAmbiente(AmbienteTreinamento ambiente, ContextoAvaliacaoNeat contexto)
    {
        _ambiente = ambiente;
        _contexto = contexto;
    }

    /// <summary>Entradas reais + o nó de bias que o SharpNEAT exige (ver <see cref="RedeNeat"/>).</summary>
    public int InputCount => RedeNeat.QuantidadeEntradasComBias;

    public int OutputCount => ControladorHibrido.QuantidadeSaidas;

    /// <summary>
    /// <c>false</c> de propósito: o mesmo genoma avaliado em duas gerações diferentes enfrenta
    /// seeds diferentes (ver <see cref="ContextoAvaliacaoNeat.Seeds"/>), então o fitness NÃO é
    /// uma função fixa do genoma. Declarar <c>true</c> faria o SharpNEAT reaproveitar o fitness
    /// de gerações anteriores para os campeões, congelando uma nota tirada em outro cenário —
    /// o mesmo viés de "genoma sortudo" que levou <see cref="ParametrosTreinamento.EpisodiosPorAvaliacao"/>
    /// a subir de 3 para 12.
    /// </summary>
    public bool IsDeterministic => false;

    public IComparer<FitnessInfo> FitnessComparer => PrimaryFitnessInfoComparer.Singleton;

    public FitnessInfo NullFitness => FitnessInfo.DefaultFitnessInfo;

    /// <summary>O avaliador só lê o contexto e cria a própria rede/controlador por chamada — sem estado mutável próprio, logo seguro para o avaliador paralelo do framework.</summary>
    public bool EvaluatorsHaveState => false;

    public IPhenomeEvaluator<IBlackBox<double>> CreateEvaluator()
        => new AvaliadorAmbiente(_ambiente, _contexto);

    /// <summary>Não existe "fitness perfeito" nesta tarefa (o episódio é aberto e sorteado), então o treinamento nunca para sozinho — quem decide parar é o usuário na tela.</summary>
    public bool TestForStopCondition(FitnessInfo fitnessInfo) => false;
}

/// <summary>
/// Roda os episódios de <see cref="AmbienteTreinamento.AvaliarFitness"/> para uma rede já
/// decodificada pelo SharpNEAT e devolve a nota no formato que o framework entende.
/// </summary>
public sealed class AvaliadorAmbiente : IPhenomeEvaluator<IBlackBox<double>>
{
    private readonly AmbienteTreinamento _ambiente;
    private readonly ContextoAvaliacaoNeat _contexto;

    public AvaliadorAmbiente(AmbienteTreinamento ambiente, ContextoAvaliacaoNeat contexto)
    {
        _ambiente = ambiente;
        _contexto = contexto;
    }

    public FitnessInfo Evaluate(IBlackBox<double> caixaPreta)
    {
        double fitness = _ambiente.AvaliarFitness(
            new RedeNeat(caixaPreta),
            _contexto.Seeds,
            _contexto.CenariosFixos,
            _contexto.Modo);

        // O SharpNEAT exige fitness >= 0 (a especiação distribui descendentes em proporção ao
        // fitness médio da espécie; nota negativa quebraria esse rateio). Nosso fitness pode ser
        // negativo — as penalidades de fuga, invasão de torre e recusa de disparo são subtraídas
        // sem piso (ver ParametrosTreinamento). O deslocamento abaixo move a escala inteira para
        // o positivo preservando a ORDEM entre genomas, que é o que a seleção usa; o que fica
        // abaixo do piso é truncado em 0 (são genomas catastróficos, já descartados na prática).
        double fitnessDeslocado = fitness + DeslocamentoFitness;
        return new FitnessInfo(Math.Max(0.0, fitnessDeslocado), [fitness]);
    }

    /// <summary>
    /// Deslocamento somado ao fitness bruto para garantir nota não-negativa (ver
    /// <see cref="Evaluate"/>). Dimensionado acima do pior caso realista: um episódio que só
    /// acumula penalidades (invasão de torre = 16, escape = 8, perda de ponto = 3, recusas de
    /// disparo = 3 cada) raramente passa de algumas dezenas negativas, porque a invasão encerra
    /// o episódio. O fitness BRUTO (sem deslocamento) continua disponível em
    /// <see cref="FitnessInfo.AuxFitnessScores"/>[0] e é o que a tela e o artigo reportam — o
    /// deslocamento é detalhe interno do framework, não deve vazar para os gráficos.
    /// </summary>
    public const double DeslocamentoFitness = 500.0;
}
