using RadarTorres.App.NeuralEvolution;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobertura do algoritmo genético (seleção por torneio, crossover uniforme, mutação gaussiana,
/// elitismo — ver NeuralEvolution/AlgoritmoGenetico.cs). Não testa a rede neural nem o ambiente
/// de treinamento: usa uma função de fitness sintética (maximizar a soma dos genes) só para
/// verificar que o próprio mecanismo evolutivo melhora geração a geração.
/// </summary>
public sealed class AlgoritmoGeneticoTests
{
    private static double FitnessSomaDosGenes(double[] genoma)
    {
        double soma = 0;
        foreach (double gene in genoma) soma += gene;
        return soma;
    }

    [Fact]
    public void EvoluirGeracao_AoLongoDeVariasGeracoes_MelhoraOFitness()
    {
        var algoritmo = new AlgoritmoGenetico(tamanhoGenoma: 10, tamanhoPopulacao: 40, taxaMutacao: 0.1, seed: 42);

        ResultadoGeracao primeira = algoritmo.EvoluirGeracao(FitnessSomaDosGenes);
        ResultadoGeracao? ultima = null;
        for (int i = 0; i < 29; i++)
        {
            ultima = algoritmo.EvoluirGeracao(FitnessSomaDosGenes);
        }

        Assert.NotNull(ultima);
        Assert.True(ultima!.MelhorFitness >= primeira.MelhorFitness,
            $"Esperava que o melhor fitness não regredisse: primeira={primeira.MelhorFitness}, última={ultima.MelhorFitness}");
    }

    [Fact]
    public void EvoluirGeracao_ComElitismo_NuncaRegrideOMelhorFitness()
    {
        var algoritmo = new AlgoritmoGenetico(tamanhoGenoma: 6, tamanhoPopulacao: 20, elitismo: 2, seed: 7);

        double melhorAnterior = double.NegativeInfinity;
        for (int i = 0; i < 15; i++)
        {
            ResultadoGeracao resultado = algoritmo.EvoluirGeracao(FitnessSomaDosGenes);
            Assert.True(resultado.MelhorFitness >= melhorAnterior);
            melhorAnterior = resultado.MelhorFitness;
        }
    }

    [Fact]
    public void EvoluirGeracao_IncrementaOContadorDeGeracoes()
    {
        var algoritmo = new AlgoritmoGenetico(tamanhoGenoma: 4, tamanhoPopulacao: 8, seed: 1);

        Assert.Equal(0, algoritmo.GeracaoAtual);
        algoritmo.EvoluirGeracao(FitnessSomaDosGenes);
        Assert.Equal(1, algoritmo.GeracaoAtual);
        algoritmo.EvoluirGeracao(FitnessSomaDosGenes);
        Assert.Equal(2, algoritmo.GeracaoAtual);
    }

    [Fact]
    public void EvoluirGeracao_MelhorGenomaTemOTamanhoCorreto()
    {
        var algoritmo = new AlgoritmoGenetico(tamanhoGenoma: 13, tamanhoPopulacao: 10, seed: 3);

        ResultadoGeracao resultado = algoritmo.EvoluirGeracao(FitnessSomaDosGenes);

        Assert.Equal(13, resultado.MelhorGenoma.Length);
    }

    /// <summary>
    /// Regressão: <see cref="ResultadoGeracao.MelhorGenoma"/> devolvido precisa ser o genoma que
    /// REALMENTE tirou <see cref="ResultadoGeracao.MelhorFitness"/> nesta geração — reavaliar o
    /// genoma devolvido com a mesma função de fitness tem que bater com o valor relatado.
    /// Pegava "de graça" quando elitismo>=1 (a troca de população acontecia DEPOIS de capturar o
    /// melhor), mas com elitismo=0 a implementação antiga devolvia um FILHO recém-cruzado da
    /// população nova, não o melhor de verdade — por isso o teste cobre elitismo=0 também.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void EvoluirGeracao_MelhorGenomaDevolvido_ReproduzOMelhorFitnessRelatado(int elitismo)
    {
        var algoritmo = new AlgoritmoGenetico(tamanhoGenoma: 8, tamanhoPopulacao: 16, elitismo: elitismo, seed: 55);

        ResultadoGeracao resultado = algoritmo.EvoluirGeracao(FitnessSomaDosGenes);

        Assert.Equal(resultado.MelhorFitness, FitnessSomaDosGenes(resultado.MelhorGenoma), precision: 10);
    }
}
