using RadarTorres.App.Models;
using RadarTorres.App.NeuralEvolution;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Fixa, em teste, as conclusões do estudo experimental do controlador neuroevolutivo
/// (ver tools/RadarTorres.Experimentos e Resultados/resultados.json):
/// <list type="bullet">
/// <item>o fitness tem um TETO imposto pelo ambiente (quantos alvos chegam a nascer num episódio), não pela qualidade da rede;</item>
/// <item><see cref="ParametrosTreinamento.MaxAlvosSimultaneos"/> não mexe nesse teto;</item>
/// <item>o modo <see cref="ControladorHibrido.ModoDecisao.RedePura"/> realmente remove a heurística das decisões, e o modo híbrido (produção) continua inalterado.</item>
/// </list>
/// </summary>
public class ExperimentosRedeNeuralTests
{
    private static double[] GenomaNulo() => new double[RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao)];

    /// <summary>
    /// Genoma com todos os genes = 1: toda ativação fica positiva, então a saída de confiança
    /// de disparo é sempre > 0 ("quer atirar sempre"). Isola a decisão heurística da neural.
    /// </summary>
    private static double[] GenomaSempreAtira()
    {
        var g = new double[RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao)];
        Array.Fill(g, 1.0);
        return g;
    }

    private static RedeNeural Rede(double[] genoma) => new(ControladorHibrido.ArquiteturaPadrao, genoma);

    // ------------------------------------------------------------- teto

    /// <summary>
    /// O número de alvos que nascem num episódio é limitado pelo relógio de nascimento
    /// (duração / intervalo mínimo), e cada abate vale no máximo <see cref="ParametrosTreinamento.RecompensaAbate"/>.
    /// Logo o fitness de um episódio tem um teto duro — com os parâmetros de fábrica,
    /// 45 / 1,5 = 30 alvos, ou seja 300 pontos em um cenário impossível (todo intervalo sorteado
    /// no mínimo E todo alvo abatido na borda do mundo). Na média o teto fica muito abaixo disso.
    /// </summary>
    [Fact]
    public void AlvosNascidosPorEpisodioRespeitamOTetoDoRelogioDeNascimento()
    {
        var p = new ParametrosTreinamento();
        var ambiente = new AmbienteTreinamento(p);
        int tetoDeNascimentos = (int)Math.Ceiling(p.DuracaoEpisodioSegundos / p.IntervaloSpawnMinSegundos);

        double[] genoma = GenomaSempreAtira();
        var nascidos = new List<int>();
        for (int seed = 0; seed < 30; seed++)
        {
            EpisodioResultado r = ambiente.ExecutarEpisodioRapido(genoma, seed);
            Assert.InRange(r.AlvosNascidos, 1, tetoDeNascimentos);
            Assert.True(r.FitnessTotal <= r.AlvosNascidos * p.RecompensaAbate);
            nascidos.Add(r.AlvosNascidos);
        }

        // Teto MÉDIO observado — é ele, e não o teto absoluto, que limita o fitness alcançável
        // por um treinamento: com intervalo sorteado em 1,5..4,0 s a média fica perto de
        // 45 / 2,75 ≈ 16 alvos, e não dos 30 do pior caso.
        Assert.InRange(nascidos.Average(), 10.0, 20.0);
    }

    /// <summary>
    /// Elevar <see cref="ParametrosTreinamento.MaxAlvosSimultaneos"/> NÃO eleva o teto de
    /// fitness: esse parâmetro só descarta nascimentos quando o controlador já deixou alvos
    /// demais vivos ao mesmo tempo. Para um controlador que abate, o gargalo é o relógio de
    /// nascimento, idêntico nos dois casos.
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void AumentarMaxAlvosSimultaneosNaoMudaOEpisodioDeUmControladorQueAbate(int maxAlvos)
    {
        double[] genoma = GenomaSempreAtira();
        var referencia = new AmbienteTreinamento(new ParametrosTreinamento { MaxAlvosSimultaneos = 5 });
        var ampliado = new AmbienteTreinamento(new ParametrosTreinamento { MaxAlvosSimultaneos = maxAlvos });

        for (int seed = 0; seed < 10; seed++)
        {
            EpisodioResultado a = referencia.ExecutarEpisodioRapido(genoma, seed);
            EpisodioResultado b = ampliado.ExecutarEpisodioRapido(genoma, seed);
            Assert.Equal(a.AlvosNascidos, b.AlvosNascidos);
            Assert.Equal(a.FitnessTotal, b.FitnessTotal, 6);
        }
    }

    // ------------------------------------------------- isolamento da rede

    /// <summary>O construtor sem modo explícito continua sendo o híbrido — o comportamento de produção não muda.</summary>
    [Fact]
    public void ModoPadraoDoControladorEhHibrido()
    {
        var controlador = new ControladorHibrido(Rede(GenomaNulo()), 10.0, 2.5);
        Assert.Equal(ControladorHibrido.ModoDecisao.Hibrido, controlador.Modo);
    }

    /// <summary>
    /// Prova direta de que a heurística some no modo rede pura: o tempo mínimo de mira é uma
    /// regra heurística, e só o híbrido a aplica. Mesmo alvo, mesma rede, decisões opostas.
    /// </summary>
    [Fact]
    public void RedePuraIgnoraOTempoMinimoDeMiraImpostoPelaHeuristica()
    {
        var alvo = new AlvoTreinamento { Id = 1, X = 3, Y = 3, Quadrante = Quadrant.Q1, TempoDesdeSelecao = 0.0 };
        var torre = new TorreTreinamento { Id = 1, X = 1.75, Y = 1.75, QuadrantePreferido = Quadrant.Q1 };
        double[] genoma = GenomaSempreAtira();

        var hibrido = new ControladorHibrido(Rede(genoma), 10.0, 2.5);
        var pura = new ControladorHibrido(Rede(genoma), 10.0, 2.5, ControladorHibrido.ModoDecisao.RedePura);

        Assert.False(hibrido.DecidirDisparo(alvo, torre));
        Assert.True(pura.DecidirDisparo(alvo, torre));
    }

    /// <summary>
    /// Prova direta na seleção de torre: o híbrido só considera as torres do quadrante do alvo
    /// (camada 1 da heurística), então sempre devolve a torre do próprio quadrante. A rede pura
    /// considera TODAS as torres livres e, com um genoma que pontua negativamente a entrada
    /// "mesmo quadrante", escolhe outra — o que o híbrido nunca faria.
    /// </summary>
    [Fact]
    public void RedePuraPodeEscolherTorreForaDoQuadranteDoAlvo()
    {
        // Genoma cirúrgico: única conexão viva é entrada[1] ("mesmo quadrante") -> oculto[0]
        // com peso -3, e oculto[0] -> saída[0] com peso +3. Assim a pontuação da rede é
        // ~-0,99 para a torre do mesmo quadrante e 0 para as demais.
        var genoma = new double[RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao)];
        const int ocultos = 8;
        genoma[1 * ocultos + 0] = -3.0;                                       // entrada[1] -> oculto[0]
        genoma[ControladorHibrido.QuantidadeEntradas * ocultos + ocultos] = 3.0; // oculto[0] -> saída[0]

        var alvo = new AlvoTreinamento { Id = 1, X = 3, Y = 3, Quadrante = Quadrant.Q1 };
        var torres = new List<TorreTreinamento>
        {
            new() { Id = 1, X = 1.75, Y = 1.75, QuadrantePreferido = Quadrant.Q1 },
            new() { Id = 3, X = -1.75, Y = -1.75, QuadrantePreferido = Quadrant.Q3 },
        };

        var hibrido = new ControladorHibrido(Rede(genoma), 10.0, 2.5);
        var pura = new ControladorHibrido(Rede(genoma), 10.0, 2.5, ControladorHibrido.ModoDecisao.RedePura);

        Assert.Equal(1, hibrido.SelecionarTorre(alvo, torres, [alvo])!.Id);
        Assert.Equal(3, pura.SelecionarTorre(alvo, torres, [alvo])!.Id);
    }

    /// <summary>
    /// A heurística pura ignora a rede: com genoma nulo (rede inerte, que no modo híbrido nunca
    /// autoriza disparo) ela ainda abate alvos, servindo de baseline inferior do estudo.
    /// </summary>
    [Fact]
    public void HeuristicaPuraAbateMesmoComRedeInerte()
    {
        var ambiente = new AmbienteTreinamento();
        double[] nulo = GenomaNulo();

        EpisodioResultado inerte = ambiente.ExecutarEpisodioRapido(nulo, seed: 7);
        EpisodioResultado heuristica = ambiente.ExecutarEpisodioRapido(
            nulo, seed: 7, TipoCenarioTreinamento.Aleatorio, ControladorHibrido.ModoDecisao.HeuristicaPura);

        Assert.Equal(0, inerte.AlvosAbatidos);
        Assert.True(heuristica.AlvosAbatidos > 0);
    }

    /// <summary>
    /// Os vetos de segurança (zona morta) NÃO são heurística de seleção: valem em qualquer modo,
    /// inclusive rede pura — removê-los mudaria a tarefa avaliada, não o controlador.
    /// </summary>
    [Fact]
    public void ZonaMortaContinuaVetandoDisparoEmModoRedePura()
    {
        var parametros = new ParametrosTreinamento
        {
            ZonasMortasQuadrantes = new HashSet<Quadrant> { Quadrant.Q1, Quadrant.Q2, Quadrant.Q3, Quadrant.Q4 }
        };
        var ambiente = new AmbienteTreinamento(parametros);

        EpisodioResultado r = ambiente.ExecutarEpisodioRapido(
            GenomaSempreAtira(), seed: 3, TipoCenarioTreinamento.Aleatorio, ControladorHibrido.ModoDecisao.RedePura);

        Assert.Equal(0, r.AlvosAbatidos);
        Assert.Equal(0, r.TirosErrados);
    }
}
