using System;
using System.Linq;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>Resultado da avaliação de uma geração: usado pela UI para alimentar o gráfico de evolução.</summary>
public sealed record ResultadoGeracao(int Geracao, double MelhorFitness, double FitnessMedio, double[] MelhorGenoma);

/// <summary>
/// Algoritmo genético que evolui uma população de genomas (pesos de <see cref="RedeNeural"/>)
/// a partir de uma função de fitness fornecida por quem chama (aqui, <see cref="AmbienteTreinamento"/>).
/// Implementação própria — seleção por torneio, crossover uniforme, mutação gaussiana e elitismo —
/// sem depender de nenhuma biblioteca de IA, para manter o algoritmo auditável/explicável no TCC.
/// </summary>
public sealed class AlgoritmoGenetico
{
    private readonly int _tamanhoGenoma;
    private readonly int _tamanhoPopulacao;
    private readonly double _taxaMutacao;
    private readonly double _forcaMutacao;
    private readonly int _elitismo;
    private readonly Random _random;

    private double[][] _populacao;
    private int _geracaoAtual;

    public int GeracaoAtual => _geracaoAtual;

    /// <param name="tamanhoGenoma">Quantidade de genes por indivíduo (ver <see cref="RedeNeural.TamanhoGenoma"/>).</param>
    /// <param name="tamanhoPopulacao">Quantidade de indivíduos avaliados por geração.</param>
    /// <param name="taxaMutacao">Probabilidade (0-1) de cada gene sofrer mutação ao gerar um filho.</param>
    /// <param name="forcaMutacao">Desvio padrão do ruído gaussiano aplicado a um gene mutado.</param>
    /// <param name="elitismo">Quantidade dos melhores indivíduos copiados sem alteração para a próxima geração.</param>
    public AlgoritmoGenetico(
        int tamanhoGenoma,
        int tamanhoPopulacao = 60,
        double taxaMutacao = 0.08,
        double forcaMutacao = 0.5,
        int elitismo = 3,
        int? seed = null)
    {
        if (tamanhoGenoma <= 0) throw new ArgumentOutOfRangeException(nameof(tamanhoGenoma));
        if (tamanhoPopulacao < 4) throw new ArgumentOutOfRangeException(nameof(tamanhoPopulacao), "A população precisa de pelo menos 4 indivíduos para seleção por torneio funcionar.");
        if (elitismo < 0 || elitismo >= tamanhoPopulacao) throw new ArgumentOutOfRangeException(nameof(elitismo));

        _tamanhoGenoma = tamanhoGenoma;
        _tamanhoPopulacao = tamanhoPopulacao;
        _taxaMutacao = taxaMutacao;
        _forcaMutacao = forcaMutacao;
        _elitismo = elitismo;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();

        _populacao = new double[_tamanhoPopulacao][];
        for (int i = 0; i < _tamanhoPopulacao; i++)
        {
            _populacao[i] = GerarGenomaAleatorio();
        }
    }

    private double[] GerarGenomaAleatorio()
    {
        var genoma = new double[_tamanhoGenoma];
        for (int i = 0; i < _tamanhoGenoma; i++)
        {
            genoma[i] = (_random.NextDouble() * 2.0) - 1.0; // -1..1
        }
        return genoma;
    }

    /// <summary>
    /// Avalia a população atual com <paramref name="avaliarFitness"/> e produz a próxima geração.
    /// Quanto maior o fitness retornado, melhor — cabe a quem chama definir a escala (ver
    /// <see cref="AmbienteTreinamento.ExecutarEpisodio"/>).
    /// </summary>
    public ResultadoGeracao EvoluirGeracao(Func<double[], double> avaliarFitness)
    {
        double[] fitness = _populacao.AsParallel().Select(avaliarFitness).ToArray();

        int[] ordemDecrescente = Enumerable.Range(0, _tamanhoPopulacao)
            .OrderByDescending(i => fitness[i])
            .ToArray();

        var proximaPopulacao = new double[_tamanhoPopulacao][];

        // Elitismo: os melhores passam direto, sem mutação — garante que a geração nunca regride.
        for (int i = 0; i < _elitismo; i++)
        {
            proximaPopulacao[i] = (double[])_populacao[ordemDecrescente[i]].Clone();
        }

        for (int i = _elitismo; i < _tamanhoPopulacao; i++)
        {
            double[] pai = SelecionarPorTorneio(fitness);
            double[] mae = SelecionarPorTorneio(fitness);
            double[] filho = Cruzar(pai, mae);
            Mutar(filho);
            proximaPopulacao[i] = filho;
        }

        _populacao = proximaPopulacao;
        _geracaoAtual++;

        int melhorIndice = ordemDecrescente[0];
        return new ResultadoGeracao(
            Geracao: _geracaoAtual,
            MelhorFitness: fitness[melhorIndice],
            FitnessMedio: fitness.Average(),
            MelhorGenoma: (double[])_populacao[0].Clone());
    }

    /// <summary>Torneio de 3: sorteia 3 indivíduos e devolve o de maior fitness entre eles.</summary>
    private double[] SelecionarPorTorneio(double[] fitness)
    {
        int melhorIndice = _random.Next(_tamanhoPopulacao);
        for (int i = 0; i < 2; i++)
        {
            int candidato = _random.Next(_tamanhoPopulacao);
            if (fitness[candidato] > fitness[melhorIndice]) melhorIndice = candidato;
        }
        return _populacao[melhorIndice];
    }

    /// <summary>Crossover uniforme: cada gene do filho vem do pai ou da mãe com 50% de chance.</summary>
    private double[] Cruzar(double[] pai, double[] mae)
    {
        var filho = new double[_tamanhoGenoma];
        for (int i = 0; i < _tamanhoGenoma; i++)
        {
            filho[i] = _random.NextDouble() < 0.5 ? pai[i] : mae[i];
        }
        return filho;
    }

    /// <summary>Mutação gaussiana: cada gene tem <see cref="_taxaMutacao"/> de chance de receber ruído.</summary>
    private void Mutar(double[] genoma)
    {
        for (int i = 0; i < genoma.Length; i++)
        {
            if (_random.NextDouble() < _taxaMutacao)
            {
                genoma[i] += AmostraGaussiana() * _forcaMutacao;
            }
        }
    }

    /// <summary>Amostra de uma normal padrão via transformação de Box-Muller (não há suporte nativo no .NET).</summary>
    private double AmostraGaussiana()
    {
        double u1 = 1.0 - _random.NextDouble();
        double u2 = _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }
}
