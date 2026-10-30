using RadarTorres.App.Models;
using RadarTorres.App.NeuralEvolution;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Fixa, em teste, as conclusões do estudo experimental do controlador neuroevolutivo
/// (ver tools/RadarTorres.Experimentos e Resultados/resultados.json):
/// <list type="bullet">
/// <item>o fitness tem um TETO imposto pelo ambiente (quantos alvos chegam a nascer num episódio), não pela qualidade da rede;</item>
/// <item><see cref="ParametrosTreinamento.MaxAlvosPorQuadrante"/> não mexe nesse teto;</item>
/// <item>o modo <see cref="ControladorHibrido.ModoDecisao.RedePura"/> realmente remove a heurística das decisões, e o modo híbrido (produção) continua inalterado.</item>
/// </list>
/// </summary>
public class ExperimentosRedeNeuralTests
{
    /// <summary>Rede inerte: saídas sempre 0, logo o controlador híbrido se comporta como a heurística pura na seleção e nunca autoriza disparo por conta própria.</summary>
    private static RedeNeat RedeNula() => RedesDeTeste.Zerada();

    /// <summary>
    /// Rede com peso positivo do bias para a saída de confiança: a confiança de disparo é
    /// sempre > 0 ("quer atirar sempre"), independentemente das entradas. Isola a decisão
    /// heurística da neural.
    /// </summary>
    private static RedeNeat RedeSempreAtira() => RedesDeTeste.ComPesosParaSaidas(pesoAjuste: 1.0, pesoConfianca: 1.0);

    // ------------------------------------------------------------- teto

    /// <summary>
    /// O número de alvos que nascem num episódio é limitado pelo relógio de nascimento
    /// (duração / intervalo mínimo), e cada abate vale no máximo <see cref="ParametrosTreinamento.RecompensaAbate"/>.
    /// Logo o fitness de um episódio tem um teto duro — com os parâmetros de fábrica, o intervalo
    /// de nascimento encolhe ao longo do episódio (escalada de dificuldade, ver
    /// <see cref="ParametrosTreinamento.IntervaloSpawnMinFinalSegundos"/>), então o pior caso usa
    /// o intervalo MÍNIMO do FIM do episódio (45 / 0,6 = 75 alvos). Na média o teto fica muito
    /// abaixo disso.
    /// </summary>
    [Fact]
    public void AlvosNascidosPorEpisodioRespeitamOTetoDoRelogioDeNascimento()
    {
        var p = new ParametrosTreinamento();
        var ambiente = new AmbienteTreinamento(p);
        int tetoDeNascimentos = (int)Math.Ceiling(p.DuracaoEpisodioSegundos / p.IntervaloSpawnMinFinalSegundos);

        RedeNeat rede = RedeSempreAtira();
        var nascidos = new List<int>();
        for (int seed = 0; seed < 30; seed++)
        {
            EpisodioResultado r = ambiente.ExecutarEpisodioRapido(rede, seed);
            Assert.InRange(r.AlvosNascidos, 1, tetoDeNascimentos);
            Assert.True(r.FitnessTotal <= r.AlvosNascidos * p.RecompensaAbate);
            nascidos.Add(r.AlvosNascidos);
        }

        // Teto MÉDIO observado — é ele, e não o teto absoluto, que limita o fitness alcançável
        // por um treinamento. Com a invasão de torre (ver AmbienteTreinamento.TratarInvasaoDeTorre)
        // encerrando o episódio assim que QUALQUER torre é rompida, a média CAI em relação ao
        // comportamento anterior (sem fim antecipado): episódios costumam acabar bem antes da
        // duração total, nascendo poucos alvos — valor observado na ordem de 6 a 9.
        Assert.InRange(nascidos.Average(), 3.0, 12.0);
    }

    /// <summary>
    /// Elevar <see cref="ParametrosTreinamento.MaxAlvosPorQuadrante"/> NÃO eleva o teto de
    /// fitness: esse parâmetro só descarta nascimentos quando o controlador já deixou alvos
    /// demais vivos ao mesmo tempo. Para um controlador que abate, o gargalo é o relógio de
    /// nascimento, idêntico nos dois casos.
    /// </summary>
    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    public void AumentarMaxAlvosPorQuadranteNaoMudaOEpisodioDeUmControladorQueAbate(int maxAlvos)
    {
        RedeNeat rede = RedeSempreAtira();
        var referencia = new AmbienteTreinamento(new ParametrosTreinamento { MaxAlvosPorQuadrante = 5 });
        var ampliado = new AmbienteTreinamento(new ParametrosTreinamento { MaxAlvosPorQuadrante = maxAlvos });

        for (int seed = 0; seed < 10; seed++)
        {
            EpisodioResultado a = referencia.ExecutarEpisodioRapido(rede, seed);
            EpisodioResultado b = ampliado.ExecutarEpisodioRapido(rede, seed);
            Assert.Equal(a.AlvosNascidos, b.AlvosNascidos);
            Assert.Equal(a.FitnessTotal, b.FitnessTotal, 6);
        }
    }

    // ------------------------------------------------- isolamento da rede

    /// <summary>O construtor sem modo explícito continua sendo o híbrido — o comportamento de produção não muda.</summary>
    [Fact]
    public void ModoPadraoDoControladorEhHibrido()
    {
        var controlador = new ControladorHibrido(RedeNula(), 10.0, 2.5);
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
        RedeNeat rede = RedeSempreAtira();

        var hibrido = new ControladorHibrido(rede, 10.0, 2.5);
        var pura = new ControladorHibrido(rede, 10.0, 2.5, ControladorHibrido.ModoDecisao.RedePura);

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
        // Rede cirúrgica: única conexão viva vai da entrada[1] ("mesmo quadrante") para a
        // saída[0] (ajuste de pontuação), com peso -3. A rede PENALIZA a torre do próprio
        // quadrante: pontuação ~-0,99 para ela e 0 para as demais. No modo híbrido a camada
        // heurística filtra as candidatas antes disso e a escolha não muda; no modo RedePura
        // todas as torres livres concorrem e a penalização decide.
        RedeNeat rede = RedesDeTeste.DeUmaEntradaParaUmaSaida(indiceEntrada: 1, indiceSaida: 0, peso: -3.0);

        var alvo = new AlvoTreinamento { Id = 1, X = 3, Y = 3, Quadrante = Quadrant.Q1 };
        var torres = new List<TorreTreinamento>
        {
            new() { Id = 1, X = 1.75, Y = 1.75, QuadrantePreferido = Quadrant.Q1 },
            new() { Id = 3, X = -1.75, Y = -1.75, QuadrantePreferido = Quadrant.Q3 },
        };

        var hibrido = new ControladorHibrido(rede, 10.0, 2.5);
        var pura = new ControladorHibrido(rede, 10.0, 2.5, ControladorHibrido.ModoDecisao.RedePura);

        Assert.Equal(1, hibrido.SelecionarTorre(alvo, torres, [alvo])!.Id);
        Assert.Equal(3, pura.SelecionarTorre(alvo, torres, [alvo])!.Id);
    }

    /// <summary>
    /// A heurística pura ignora a rede: com a rede inerte (que no modo híbrido nunca
    /// autoriza disparo) ela ainda abate alvos, servindo de baseline inferior do estudo.
    /// </summary>
    [Fact]
    public void HeuristicaPuraAbateMesmoComRedeInerte()
    {
        var ambiente = new AmbienteTreinamento();
        RedeNeat nulo = RedeNula();

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
            RedeSempreAtira(), seed: 3, TipoCenarioTreinamento.Aleatorio, ControladorHibrido.ModoDecisao.RedePura);

        Assert.Equal(0, r.AlvosAbatidos);
        Assert.Equal(0, r.TirosErrados);
    }
}
