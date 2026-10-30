using System;
using System.Collections.Generic;
using SharpNeat;
using SharpNeat.Evaluation;
using SharpNeat.Experiments;
using SharpNeat.Neat;
using SharpNeat.Neat.ComplexityRegulation;
using SharpNeat.Neat.EvolutionAlgorithm;
using SharpNeat.Neat.Genome;
using SharpNeat.Neat.Genome.Double;
using SharpNeat.NeuralNets;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>Resultado da avaliação de uma geração: usado pela UI para alimentar o gráfico de evolução.</summary>
/// <param name="MelhorFitness">Fitness BRUTO do melhor genoma (sem o deslocamento interno do framework — ver <see cref="AvaliadorAmbiente.DeslocamentoFitness"/>).</param>
/// <param name="MelhorGenoma">O genoma campeão desta geração — um GRAFO (nós + conexões), não mais um vetor de pesos.</param>
/// <param name="ComplexidadeMelhor">Quantidade de conexões do campeão — com NEAT isso cresce ao longo da evolução e é uma métrica de interesse própria (não existia no algoritmo genético anterior, onde a topologia era fixa).</param>
public sealed record ResultadoGeracao(
    int Geracao,
    double MelhorFitness,
    double FitnessMedio,
    NeatGenome<double> MelhorGenoma,
    double ComplexidadeMelhor,
    int QuantidadeEspecies);

/// <summary>
/// Comanda a evolução pelo SharpNEAT — substitui o algoritmo genético próprio
/// (<c>AlgoritmoGenetico</c>) que evoluía um vetor de pesos de topologia fixa.
///
/// A diferença de método, e não só de biblioteca: o NEAT parte de redes MÍNIMAS (só entradas
/// ligadas às saídas) e vai acrescentando neurônios e conexões ao longo das gerações, mantendo
/// as inovações protegidas em ESPÉCIES para que uma estrutura nova não seja eliminada antes de
/// ter tempo de ser otimizada. Nada disso existia antes: a rede era sempre 7→8→2 e a evolução
/// só mexia nos pesos.
/// </summary>
public sealed class TreinadorNeat
{
    private readonly ContextoAvaliacaoNeat _contexto;
    private readonly NeatEvolutionAlgorithm<double> _algoritmo;
    private readonly IGenomeDecoder<NeatGenome<double>, IBlackBox<double>> _decodificador;
    private bool _inicializado;

    /// <summary>Função de ativação usada em todos os nós — <c>TanH</c> mantém as saídas em -1..1, a mesma faixa que a rede anterior produzia e que <see cref="ControladorHibrido"/> assume (confiança de disparo &gt; 0 significa "dispare").</summary>
    public const ActivationFunctionId FuncaoAtivacao = ActivationFunctionId.TanH;

    /// <summary>Redes acíclicas: o controlador toma uma decisão independente por alvo/passo, não precisa de memória entre ativações — e acíclico é mais rápido de avaliar, o que importa com 12 episódios por indivíduo.</summary>
    public const bool Aciclica = true;

    public int GeracaoAtual => _algoritmo.Stats.Generation;

    /// <param name="ambiente">A simulação que define a função de fitness.</param>
    /// <param name="tamanhoPopulacao">Indivíduos por geração.</param>
    /// <param name="quantidadeEspecies">Em quantas espécies a população é dividida (mecanismo central do NEAT — sem equivalente no algoritmo anterior).</param>
    /// <param name="proporcaoElitismo">Fração dos melhores de cada espécie preservada sem alteração.</param>
    /// <param name="tetoComplexidade">Número de conexões a partir do qual a evolução passa a SIMPLIFICAR em vez de crescer, evitando redes inchadas que só ficam lentas sem melhorar o fitness.</param>
    /// <param name="probabilidadeAdicionarNo">Com que frequência um descendente ganha um neurônio novo — o knob de crescimento ESTRUTURAL, que não tinha equivalente no algoritmo anterior (lá a topologia era fixa).</param>
    /// <param name="grauParalelismo">Quantos indivíduos avaliados em paralelo; -1 usa todos os núcleos (equivale ao <c>AsParallel()</c> do algoritmo anterior).</param>
    public TreinadorNeat(
        AmbienteTreinamento ambiente,
        int tamanhoPopulacao = 60,
        int quantidadeEspecies = 6,
        double proporcaoElitismo = 0.05,
        int tetoComplexidade = 60,
        double probabilidadeAdicionarNo = 0.01,
        int grauParalelismo = -1)
    {
        ArgumentNullException.ThrowIfNull(ambiente);
        if (tamanhoPopulacao < 4) throw new ArgumentOutOfRangeException(nameof(tamanhoPopulacao), "A população precisa de pelo menos 4 indivíduos.");
        if (quantidadeEspecies < 1) throw new ArgumentOutOfRangeException(nameof(quantidadeEspecies));

        _contexto = new ContextoAvaliacaoNeat();

        var esquema = new EsquemaAvaliacaoAmbiente(ambiente, _contexto);

        var experimento = new NeatExperiment<double>(esquema, "radartorres-controlador")
        {
            Name = "Controlador híbrido RadarTorres",
            Description = "Seleção de torre e decisão de disparo — heurística em camadas + rede evoluída por NEAT.",
            IsAcyclic = Aciclica,
            ActivationFnName = FuncaoAtivacao.ToString(),
            PopulationSize = tamanhoPopulacao,
            DegreeOfParallelism = grauParalelismo,
            // Regulação de complexidade: enquanto a rede tiver menos conexões que o teto, o NEAT
            // pode crescer; ao passar disso entra em modo de simplificação. Sem isto a topologia
            // cresce indefinidamente e cada avaliação (12 episódios) fica progressivamente mais
            // cara sem ganho de fitness correspondente.
            ComplexityRegulationStrategy = new AbsoluteComplexityRegulationStrategy(10, tetoComplexidade),
        };

        experimento.EvolutionAlgorithmSettings.SpeciesCount = quantidadeEspecies;
        experimento.EvolutionAlgorithmSettings.ElitismProportion = proporcaoElitismo;
        experimento.ReproductionAsexualSettings.AddNodeMutationProbability = probabilidadeAdicionarNo;

        _algoritmo = NeatUtils.CreateNeatEvolutionAlgorithm(experimento);
        _decodificador = NeatGenomeDecoderFactory.CreateGenomeDecoder(Aciclica, enableHardwareAcceleratedNeuralNets: false);
    }

    /// <summary>
    /// Avalia a população atual com as seeds desta geração e produz a próxima. Mesmo contrato do
    /// <c>AlgoritmoGenetico.EvoluirGeracao</c> anterior, só que agora quem faz o trabalho é o
    /// framework — seleção, crossover, mutação de pesos, mutação ESTRUTURAL (acrescentar/remover
    /// nó e conexão) e especiação.
    /// </summary>
    public ResultadoGeracao EvoluirGeracao(
        IReadOnlyList<int> seeds,
        IReadOnlyList<TipoCenarioTreinamento>? cenariosFixos = null,
        ControladorHibrido.ModoDecisao modo = ControladorHibrido.ModoDecisao.Hibrido)
    {
        // Precisa ser atualizado ANTES de Initialise(), que já avalia a população inicial.
        _contexto.Seeds = seeds is { Count: > 0 } ? seeds : [0];
        _contexto.CenariosFixos = cenariosFixos;
        _contexto.Modo = modo;

        if (!_inicializado)
        {
            _algoritmo.Initialise();
            _inicializado = true;
        }
        else
        {
            _algoritmo.PerformOneGeneration();
        }

        NeatPopulation<double> populacao = _algoritmo.Population;
        NeatGenome<double> campeao = populacao.BestGenome;

        return new ResultadoGeracao(
            Geracao: _algoritmo.Stats.Generation,
            MelhorFitness: FitnessBruto(campeao.FitnessInfo),
            FitnessMedio: populacao.Stats.MeanFitness - AvaliadorAmbiente.DeslocamentoFitness,
            MelhorGenoma: campeao,
            ComplexidadeMelhor: campeao.Complexity,
            QuantidadeEspecies: populacao.SpeciesArray?.Length ?? 0);
    }

    /// <summary>Transforma um genoma (grafo) na rede pronta para uso — é o que a tela usa para animar o campeão e o que <see cref="ExtratorDeRegras"/> analisa.</summary>
    public RedeNeat Decodificar(NeatGenome<double> genoma)
        => new(_decodificador.Decode(genoma));

    /// <summary>
    /// O fitness que interessa a nós é o BRUTO — o <see cref="AvaliadorAmbiente"/> guarda o valor
    /// original em <see cref="FitnessInfo.AuxFitnessScores"/>[0] justamente para o deslocamento
    /// exigido pelo framework não contaminar os números reportados na tela e no artigo.
    /// </summary>
    private static double FitnessBruto(FitnessInfo info)
        => info.AuxFitnessScores is { Length: > 0 } aux
            ? aux[0]
            : info.PrimaryFitness - AvaliadorAmbiente.DeslocamentoFitness;
}
