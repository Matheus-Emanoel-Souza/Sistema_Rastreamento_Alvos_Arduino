using System.Collections.Generic;
using System.Linq;
using RadarTorres.App.Configuration;
using RadarTorres.App.NeuralEvolution;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobertura do ambiente de treinamento (ver NeuralEvolution/AmbienteTreinamento.cs): garante que
/// um episódio completo roda até o fim sem travar e que o layout padrão de torres (usado quando
/// não há appsettings.json carregado, como aqui nos testes) é montado corretamente.
/// </summary>
public sealed class AmbienteTreinamentoTests
{
    // Lista vazia explícita em todos os testes: força o layout padrão de 4 torres
    // independentemente do que outro teste da suíte tenha deixado em AppConfig.Current (estado
    // estático global) — mesma preocupação de isolamento de TowerSelectionServiceTests.
    private static readonly List<TowerDefinition> SemTorresConfiguradas = [];

    [Fact]
    public void TorresBase_SemConfiguracaoCarregada_UsaLayoutPadraoDeQuatroTorres()
    {
        var ambiente = new AmbienteTreinamento(torres: SemTorresConfiguradas);

        Assert.Equal(4, ambiente.TorresBase.Count);
    }

    [Fact]
    public void ExecutarEpisodioRapido_ComGenomaZerado_RodaAteOFimEDevolveResultadoFinito()
    {
        var parametros = new ParametrosTreinamento { DuracaoEpisodioSegundos = 10 };
        var ambiente = new AmbienteTreinamento(parametros, SemTorresConfiguradas);
        double[] genomaZerado = new double[RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao)];

        EpisodioResultado resultado = ambiente.ExecutarEpisodioRapido(genomaZerado, seed: 123);

        Assert.True(double.IsFinite(resultado.FitnessTotal));
        Assert.True(resultado.AlvosAbatidos >= 0);
        Assert.True(resultado.AlvosEscapados >= 0);
    }

    [Fact]
    public void ExecutarEpisodioRapido_ComMesmaSeed_EDeterministico()
    {
        var ambiente = new AmbienteTreinamento(new ParametrosTreinamento { DuracaoEpisodioSegundos = 8 }, SemTorresConfiguradas);
        double[] genoma = new double[RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao)];
        for (int i = 0; i < genoma.Length; i++) genoma[i] = (i % 5 - 2) * 0.3;

        EpisodioResultado primeiro = ambiente.ExecutarEpisodioRapido(genoma, seed: 99);
        EpisodioResultado segundo = ambiente.ExecutarEpisodioRapido(genoma, seed: 99);

        Assert.Equal(primeiro.FitnessTotal, segundo.FitnessTotal, precision: 10);
        Assert.Equal(primeiro.AlvosAbatidos, segundo.AlvosAbatidos);
        Assert.Equal(primeiro.AlvosEscapados, segundo.AlvosEscapados);
    }

    [Fact]
    public void NovoEpisodio_ComecaSemAlvosEComTorresLivres()
    {
        var ambiente = new AmbienteTreinamento(torres: SemTorresConfiguradas);

        EstadoEpisodio estado = ambiente.NovoEpisodio(seed: 1);

        Assert.Empty(estado.Alvos);
        Assert.All(estado.Torres, t => Assert.True(t.Livre));
    }

    [Fact]
    public void AvaliarFitness_ComMesmasSeeds_EDeterministico()
    {
        var ambiente = new AmbienteTreinamento(new ParametrosTreinamento { DuracaoEpisodioSegundos = 8 }, SemTorresConfiguradas);
        double[] genoma = new double[RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao)];
        int[] seeds = [10, 11, 12];

        double primeiro = ambiente.AvaliarFitness(genoma, seeds);
        double segundo = ambiente.AvaliarFitness(genoma, seeds);

        Assert.Equal(primeiro, segundo, precision: 10);
    }

    /// <summary>
    /// <see cref="AmbienteTreinamento.AvaliarFitness"/> existe para reduzir o ruído de um único
    /// episódio sorteado — tem que ser de fato a média dos episódios individuais, não outra
    /// agregação (soma, melhor caso etc.) que mudaria a escala do fitness sem avisar.
    /// </summary>
    [Fact]
    public void AvaliarFitness_EAMediaAritmeticaDosEpisodiosIndividuais()
    {
        var ambiente = new AmbienteTreinamento(new ParametrosTreinamento { DuracaoEpisodioSegundos = 8 }, SemTorresConfiguradas);
        double[] genoma = new double[RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao)];
        int[] seeds = [20, 21, 22];

        double mediaEsperada = seeds.Average(seed => ambiente.ExecutarEpisodioRapido(genoma, seed).FitnessTotal);
        double mediaObtida = ambiente.AvaliarFitness(genoma, seeds);

        Assert.Equal(mediaEsperada, mediaObtida, precision: 10);
    }
}
