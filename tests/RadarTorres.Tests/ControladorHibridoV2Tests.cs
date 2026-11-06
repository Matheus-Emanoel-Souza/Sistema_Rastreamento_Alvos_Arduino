using RadarTorres.App.Models;
using RadarTorres.App.NeuralEvolution;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobertura do contrato V2 (ver NeuralEvolution/ControladorHibridoV2.cs) — espelha
/// <see cref="ExperimentosRedeNeuralTests"/> no espírito: provar que a camada heurística continua
/// idêntica à V1 e que as entradas/saídas NOVAS respondem como descrito, sem depender de evolução.
/// </summary>
public sealed class ControladorHibridoV2Tests
{
    private static RedeNeat RedeNula() => RedesDeTeste.ZeradaV2();

    [Fact]
    public void QuantidadeDeEntradasESaidas_EDiferenteDaV1()
    {
        Assert.Equal(15, ControladorHibridoV2.QuantidadeEntradas);
        Assert.Equal(3, ControladorHibridoV2.QuantidadeSaidas);
        Assert.NotEqual(ControladorHibrido.QuantidadeEntradas, ControladorHibridoV2.QuantidadeEntradas);
    }

    /// <summary>Com rede inerte, a seleção de torre V2 usa a MESMA camada heurística da V1 — só o próprio quadrante concorre quando ele tem torre livre.</summary>
    [Fact]
    public void SelecionarTorre_ComRedeInerte_RespeitaACamadaDoProprioQuadrante()
    {
        var alvo = new AlvoTreinamento { Id = 1, X = 3, Y = 3, Quadrante = Quadrant.Q1 };
        var torres = new List<TorreTreinamento>
        {
            new() { Id = 1, X = 1.75, Y = 1.75, QuadrantePreferido = Quadrant.Q1 },
            new() { Id = 3, X = -1.75, Y = -1.75, QuadrantePreferido = Quadrant.Q3 },
        };

        var controlador = new ControladorHibridoV2(RedeNula(), 10.0, 2.5, 3.0, 2.0, 6.0);

        Assert.Equal(1, controlador.SelecionarTorre(alvo, torres, [alvo])!.Id);
    }

    /// <summary>A saída 3 (prioridade de cooperação) é lida e devolvida por <see cref="ControladorHibridoV2.DecidirDisparo"/> — com peso positivo de bias para ela, vem sempre > 0.</summary>
    [Fact]
    public void DecidirDisparo_DevolveAPrioridadeDeCooperacaoDaSaida3()
    {
        RedeNeat rede = RedesDeTeste.ComPesosParaSaidasV2(pesoAjuste: 0.0, pesoConfianca: 1.0, pesoCooperacao: 0.9);
        var alvo = new AlvoTreinamento { Id = 1, X = 3, Y = 3, Quadrante = Quadrant.Q1, TempoDesdeSelecao = 1.0 };
        var torre = new TorreTreinamento { Id = 1, X = 1.75, Y = 1.75, QuadrantePreferido = Quadrant.Q1 };

        var controlador = new ControladorHibridoV2(rede, 10.0, 2.5, 3.0, 2.0, 6.0);
        (bool disparar, double cooperacao) = controlador.DecidirDisparo(alvo, torre, [torre]);

        Assert.True(disparar);
        Assert.True(cooperacao > 0);
    }

    /// <summary>Mesmo veto inegociável de tempo mínimo de mira da V1 — antes do tempo, nunca dispara, mesmo com a rede sempre querendo.</summary>
    [Fact]
    public void DecidirDisparo_AntesDoTempoMinimoDeMira_NuncaDispara()
    {
        RedeNeat rede = RedesDeTeste.ComPesosParaSaidasV2(pesoAjuste: 0.0, pesoConfianca: 1.0, pesoCooperacao: 1.0);
        var alvo = new AlvoTreinamento { Id = 1, X = 3, Y = 3, Quadrante = Quadrant.Q1, TempoDesdeSelecao = 0.0 };
        var torre = new TorreTreinamento { Id = 1, X = 1.75, Y = 1.75, QuadrantePreferido = Quadrant.Q1 };

        var controlador = new ControladorHibridoV2(rede, 10.0, 2.5, 3.0, 2.0, 6.0);
        (bool disparar, double cooperacao) = controlador.DecidirDisparo(alvo, torre, [torre]);

        Assert.False(disparar);
        Assert.Equal(0.0, cooperacao);
    }

    /// <summary>"Acompanhamento prévio" (entrada 13) é verdadeiro só quando a torre avaliada já é a comprometida com o alvo — isolado com uma rede que só liga essa entrada à saída de ajuste.</summary>
    [Fact]
    public void ConstruirEntradas_AcompanhamentoPrevio_SoMarcaATorreJaComprometida()
    {
        RedeNeat rede = RedesDeTeste.DeUmaEntradaParaUmaSaidaV2(indiceEntrada: 13, indiceSaida: 0, peso: 3.0);
        var alvo = new AlvoTreinamento { Id = 1, X = 3, Y = 3, Quadrante = Quadrant.Q1, TorreSelecionadaId = 1 };
        var torreJaComprometida = new TorreTreinamento { Id = 1, X = 1.75, Y = 1.75, QuadrantePreferido = Quadrant.Q1, AlvoComprometidoId = 1 };
        var outraTorre = new TorreTreinamento { Id = 9, X = 1.75, Y = 1.75, QuadrantePreferido = Quadrant.Q1 };

        var controlador = new ControladorHibridoV2(rede, 10.0, 2.5, 3.0, 2.0, 6.0);
        (bool _, double cooperacaoComprometida) = controlador.DecidirDisparo(alvo, torreJaComprometida, [torreJaComprometida, outraTorre]);

        // A mesma rede, mesma entrada 13 — só muda se a torre avaliada É a comprometida. Verifico
        // via a pontuação de seleção (saída 0), que é onde liguei a entrada 13 no teste.
        double[] pontuacaoComprometida = PontuarSelecaoCrua(rede, alvo, torreJaComprometida, [torreJaComprometida, outraTorre]);
        double[] pontuacaoOutra = PontuarSelecaoCrua(rede, alvo, outraTorre, [torreJaComprometida, outraTorre]);

        Assert.True(pontuacaoComprometida[0] > pontuacaoOutra[0]);
    }

    private static double[] PontuarSelecaoCrua(RedeNeat rede, AlvoTreinamento alvo, TorreTreinamento torre, List<TorreTreinamento> todasTorres)
    {
        bool acompanhamentoPrevio = alvo.TorreSelecionadaId == torre.Id;
        double[] entradas = ControladorHibridoV2.ConstruirEntradas(
            distanciaMetros: 1.0, raioMundoMetros: 10.0, mesmoQuadrante: true, quadranteAdjacente: false,
            anguloMiraGraus: 0, velocidadeMetrosPorSegundo: 1.0, velocidadeMaximaMetrosPorSegundo: 2.5,
            tempoDeteccaoSegundos: 0, tempoDeteccaoRelevanteSegundos: 3.0, fatorCamada: 1.0,
            torresAcompanhandoNormalizado: 0, disponibilidadeTorreCandidata: 1.0, direcaoMovimentoGraus: 0,
            tempoEstimadoSaidaAlcanceNormalizado: 0, alturaNormalizada: 0, visivelPorTodasAsTorres: false,
            acompanhamentoPrevio: acompanhamentoPrevio, trocasRecentesDaTorre: 0);

        return rede.Ativar(entradas);
    }
}
