using System.Collections.Generic;
using RadarTorres.App.Configuration;
using RadarTorres.App.NeuralEvolution;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobertura do comando da evolução (ver NeuralEvolution/TreinadorNeat.cs). Substitui
/// <c>AlgoritmoGeneticoTests</c>, que testava a mecânica do AG próprio (torneio, crossover,
/// elitismo) — isso agora é responsabilidade do SharpNEAT e não cabe a nós retestar. O que
/// precisa de teste é a NOSSA integração: a rede produzida tem o formato que o controlador
/// espera, as gerações avançam, e o fitness relatado é o bruto (sem o deslocamento interno
/// exigido pelo framework).
/// </summary>
public sealed class TreinadorNeatTests
{
    private static readonly List<TowerDefinition> SemTorresConfiguradas = [];

    /// <summary>Ambiente minúsculo — o que se testa aqui é a integração, não a qualidade evolutiva.</summary>
    private static AmbienteTreinamento AmbienteRapido() =>
        new(new ParametrosTreinamento { DuracaoEpisodioSegundos = 5, EpisodiosPorAvaliacao = 1 }, SemTorresConfiguradas);

    private static TreinadorNeat TreinadorRapido(AmbienteTreinamento ambiente) =>
        new(ambiente, tamanhoPopulacao: 12, quantidadeEspecies: 3, grauParalelismo: 1);

    [Fact]
    public void EvoluirGeracao_PrimeiraChamada_DevolveGeracaoUm()
    {
        TreinadorNeat treinador = TreinadorRapido(AmbienteRapido());

        ResultadoGeracao resultado = treinador.EvoluirGeracao([1]);

        Assert.Equal(1, resultado.Geracao);
        Assert.Equal(1, treinador.GeracaoAtual);
    }

    [Fact]
    public void EvoluirGeracao_ChamadasSucessivas_AvancamAContagem()
    {
        TreinadorNeat treinador = TreinadorRapido(AmbienteRapido());

        treinador.EvoluirGeracao([1]);
        treinador.EvoluirGeracao([2]);
        ResultadoGeracao terceira = treinador.EvoluirGeracao([3]);

        Assert.Equal(3, terceira.Geracao);
    }

    /// <summary>
    /// O NEAT parte de redes mínimas: o campeão da primeira geração já tem que ser uma rede
    /// válida, com conexões, decodificável no formato que <see cref="ControladorHibrido"/> usa.
    /// </summary>
    [Fact]
    public void Decodificar_DoCampeao_ProduzRedeUsavelPeloControlador()
    {
        AmbienteTreinamento ambiente = AmbienteRapido();
        TreinadorNeat treinador = TreinadorRapido(ambiente);

        ResultadoGeracao resultado = treinador.EvoluirGeracao([7]);
        RedeNeat rede = treinador.Decodificar(resultado.MelhorGenoma);

        double[] saidas = rede.Ativar([0.5, 1.0, 0.0, 0.5, 0.5, 0.0, 1.0]);

        Assert.Equal(ControladorHibrido.QuantidadeSaidas, saidas.Length);
        Assert.All(saidas, s => Assert.True(double.IsFinite(s)));
        Assert.True(resultado.ComplexidadeMelhor > 0);
    }

    /// <summary>
    /// O deslocamento somado ao fitness para satisfazer a exigência de não-negatividade do
    /// SharpNEAT (ver <see cref="AvaliadorAmbiente.DeslocamentoFitness"/>) é detalhe interno do
    /// framework: o número relatado tem que ser o mesmo que
    /// <see cref="AmbienteTreinamento.AvaliarFitness"/> devolveria, senão a tela e o artigo
    /// passariam a reportar uma escala inflada em centenas de pontos.
    /// </summary>
    [Fact]
    public void EvoluirGeracao_RelataOFitnessBrutoSemODeslocamentoInterno()
    {
        AmbienteTreinamento ambiente = AmbienteRapido();
        TreinadorNeat treinador = TreinadorRapido(ambiente);

        ResultadoGeracao resultado = treinador.EvoluirGeracao([11]);

        RedeNeat campeao = treinador.Decodificar(resultado.MelhorGenoma);
        double fitnessRecalculado = ambiente.AvaliarFitness(campeao, [11]);

        Assert.Equal(fitnessRecalculado, resultado.MelhorFitness, precision: 6);
        Assert.True(resultado.MelhorFitness < AvaliadorAmbiente.DeslocamentoFitness);
    }

    [Fact]
    public void EvoluirGeracao_SeparaAPopulacaoEmEspecies()
    {
        TreinadorNeat treinador = TreinadorRapido(AmbienteRapido());

        ResultadoGeracao resultado = treinador.EvoluirGeracao([5]);

        Assert.True(resultado.QuantidadeEspecies > 0);
    }
}
