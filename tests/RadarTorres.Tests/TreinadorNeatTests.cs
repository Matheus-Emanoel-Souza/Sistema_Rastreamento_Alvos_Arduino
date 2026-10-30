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

    /// <summary>
    /// A primeira chamada só cria e avalia a população INICIAL — nenhuma reprodução aconteceu —, e
    /// o SharpNEAT numera isso como geração 0. Fixado em teste porque a numeração vai para o
    /// histórico, para os marcos e para o artigo: se ela deslizar, dois experimentos deixam de ser
    /// comparáveis.
    /// </summary>
    [Fact]
    public void EvoluirGeracao_PrimeiraChamada_AvaliaAPopulacaoInicialComoGeracaoZero()
    {
        TreinadorNeat treinador = TreinadorRapido(AmbienteRapido());

        ResultadoGeracao resultado = treinador.EvoluirGeracao([1]);

        Assert.Equal(0, resultado.Geracao);
        Assert.Equal(0, treinador.GeracaoAtual);
    }

    [Fact]
    public void EvoluirGeracao_ChamadasSucessivas_AvancamAContagem()
    {
        TreinadorNeat treinador = TreinadorRapido(AmbienteRapido());

        treinador.EvoluirGeracao([1]); // população inicial (geração 0)
        treinador.EvoluirGeracao([2]); // geração 1
        ResultadoGeracao terceira = treinador.EvoluirGeracao([3]);

        Assert.Equal(2, terceira.Geracao);
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

    /// <summary>
    /// As métricas que a tela de treinamento usa para mostrar o que está acontecendo (complexidade
    /// da população, fase da regulação, vazão, ocultos do campeão) têm que sair preenchidas de
    /// verdade — antes elas eram calculadas pelo framework e simplesmente descartadas.
    /// </summary>
    [Fact]
    public void EvoluirGeracao_PreencheAsMetricasDeAcompanhamento()
    {
        TreinadorNeat treinador = TreinadorRapido(AmbienteRapido());

        ResultadoGeracao resultado = treinador.EvoluirGeracao([13]);

        Assert.True(resultado.ComplexidadeMedia > 0, "a complexidade média da população não pode ser zero: todo genoma do NEAT nasce com conexões.");
        Assert.True(resultado.ComplexidadeMaxima >= resultado.ComplexidadeMedia);
        Assert.True(resultado.ComplexidadeMaxima >= resultado.ComplexidadeMelhor);
        Assert.True(resultado.TotalAvaliacoes > 0);
        Assert.True(resultado.NeuroniosOcultosDoCampeao >= 0);

        // Na primeira geração o campeão acabou de nascer, então não há estagnação possível.
        Assert.Equal(0, resultado.GeracoesSemCampeaoNovo);

        // Com o teto padrão (60 conexões) e redes mínimas na primeira geração, a evolução está
        // necessariamente na fase de complexificação.
        Assert.False(resultado.Simplificando);
    }

    /// <summary>
    /// O retrato por espécie é a informação que o algoritmo genético anterior não tinha como
    /// produzir (população única). A soma dos tamanhos tem que fechar com a população configurada,
    /// senão a tela mostraria uma divisão que não corresponde à população real.
    /// </summary>
    [Fact]
    public void EvoluirGeracao_ResumeCadaEspecieEExatamenteUmaContemOCampeao()
    {
        TreinadorNeat treinador = TreinadorRapido(AmbienteRapido());

        ResultadoGeracao resultado = treinador.EvoluirGeracao([17]);

        Assert.NotNull(resultado.Especies);
        Assert.Equal(resultado.QuantidadeEspecies, resultado.Especies!.Count);

        int soma = 0;
        int comCampeao = 0;
        foreach (ResumoEspecie especie in resultado.Especies)
        {
            soma += especie.Tamanho;
            if (especie.ContemCampeao) comCampeao++;
        }

        Assert.Equal(12, soma); // mesmo tamanho de população de TreinadorRapido
        Assert.Equal(1, comCampeao);
    }

    /// <summary>
    /// Mesmo motivo de <see cref="EvoluirGeracao_RelataOFitnessBrutoSemODeslocamentoInterno"/>: o
    /// fitness por espécie também é exibido na tela e não pode vir inflado pelo deslocamento que
    /// o framework exige internamente.
    /// </summary>
    [Fact]
    public void EvoluirGeracao_FitnessPorEspecieVemSemODeslocamentoInterno()
    {
        TreinadorNeat treinador = TreinadorRapido(AmbienteRapido());

        ResultadoGeracao resultado = treinador.EvoluirGeracao([19]);

        Assert.NotNull(resultado.Especies);
        Assert.All(resultado.Especies!, e => Assert.True(e.FitnessMedio < AvaliadorAmbiente.DeslocamentoFitness));
    }
}
