using System;
using System.Collections.Generic;
using System.Linq;
using RadarTorres.App.Helpers;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// Segunda versão do controlador híbrido, lado a lado com <see cref="ControladorHibrido"/> (V1) —
/// NÃO o substitui. V1 continua sendo a referência (produção e baseline do estudo comparativo);
/// esta classe é o experimento: mais entradas (cooperação, disponibilidade, cinemática, eixo Z,
/// histórico de decisão) e uma saída nova (prioridade de cooperação), avaliada pelo MESMO
/// <see cref="AmbienteTreinamento"/> através dos métodos "V2" (ver
/// <see cref="AmbienteTreinamento.ExecutarEpisodioRapidoV2"/>).
///
/// A camada heurística (próprio quadrante → adjacente → sobressalente) é idêntica à V1 — o que
/// muda é só o vetor de entrada da rede e a saída extra, não a regra de elegibilidade de torre.
/// </summary>
public sealed class ControladorHibridoV2
{
    /// <summary>
    /// distância, mesmo quadrante?, quadrante adjacente?, ângulo de mira, velocidade, tempo
    /// detectado sem abate, fator da camada heurística (7, igual à V1) + quantidade de torres
    /// também acompanhando o alvo, disponibilidade da torre candidata, direção do movimento do
    /// alvo, tempo estimado até ele saír do alcance, altura, visibilidade por todas as torres,
    /// acompanhamento prévio (esta torre já é quem acompanha este alvo) e trocas recentes de
    /// alvo desta torre (8 novas) = 15.
    /// </summary>
    public const int QuantidadeEntradas = 15;

    /// <summary>ajuste de pontuação da torre candidata, confiança de disparo, prioridade de cooperação.</summary>
    public const int QuantidadeSaidas = 3;

    public static readonly string[] RotulosEntradas =
    [
        "Distância até o alvo",
        "Mesmo quadrante do alvo",
        "Quadrante adjacente",
        "Ângulo de mira",
        "Velocidade do alvo",
        "Tempo detectado sem abate",
        "Camada heurística",
        "Torres também acompanhando o alvo",
        "Disponibilidade da torre candidata",
        "Direção do movimento do alvo",
        "Tempo estimado até saída de alcance",
        "Altura do alvo (eixo Z)",
        "Visível por todas as torres (eixo Z)",
        "Acompanhamento prévio (mesma torre)",
        "Trocas recentes de alvo desta torre",
    ];

    public static readonly string[] RotulosSaidas =
    [
        "Ajuste de pontuação da torre candidata",
        "Confiança de disparo",
        "Prioridade de cooperação",
    ];

    /// <summary>Mesmo veto inegociável da V1 — ver <see cref="ControladorHibrido.TempoMinimoDeMiraSegundos"/>.</summary>
    public const double TempoMinimoDeMiraSegundos = ControladorHibrido.TempoMinimoDeMiraSegundos;

    private readonly RedeNeat? _rede;
    private readonly RedeNeatRecorrente? _redeRecorrente;
    private readonly double _raioMundoMetros;
    private readonly double _velocidadeMaximaAlvoMetrosPorSegundo;
    private readonly double _tempoDeteccaoRelevanteSegundos;
    private readonly double _alturaMaximaAlvoMetros;
    private readonly double _janelaTrocaDeAlvoSegundos;

    /// <summary>Construtor para rede SEM estado entre ativações (padrão) — mesma semântica de <see cref="ControladorHibrido"/>.</summary>
    public ControladorHibridoV2(
        RedeNeat rede, double raioMundoMetros, double velocidadeMaximaAlvoMetrosPorSegundo,
        double tempoDeteccaoRelevanteSegundos, double alturaMaximaAlvoMetros, double janelaTrocaDeAlvoSegundos)
    {
        _rede = rede;
        _raioMundoMetros = raioMundoMetros;
        _velocidadeMaximaAlvoMetrosPorSegundo = velocidadeMaximaAlvoMetrosPorSegundo;
        _tempoDeteccaoRelevanteSegundos = tempoDeteccaoRelevanteSegundos;
        _alturaMaximaAlvoMetros = alturaMaximaAlvoMetros;
        _janelaTrocaDeAlvoSegundos = janelaTrocaDeAlvoSegundos;
    }

    /// <summary>Construtor para rede RECORRENTE com estado isolado por torre (ver <see cref="RedeNeatRecorrente"/>) — experimental, usado só fora do laço evolutivo (replay/avaliação isolada), ver observação em <see cref="RedeNeatRecorrente"/>.</summary>
    public ControladorHibridoV2(
        RedeNeatRecorrente redeRecorrente, double raioMundoMetros, double velocidadeMaximaAlvoMetrosPorSegundo,
        double tempoDeteccaoRelevanteSegundos, double alturaMaximaAlvoMetros, double janelaTrocaDeAlvoSegundos)
    {
        _redeRecorrente = redeRecorrente;
        _raioMundoMetros = raioMundoMetros;
        _velocidadeMaximaAlvoMetrosPorSegundo = velocidadeMaximaAlvoMetrosPorSegundo;
        _tempoDeteccaoRelevanteSegundos = tempoDeteccaoRelevanteSegundos;
        _alturaMaximaAlvoMetros = alturaMaximaAlvoMetros;
        _janelaTrocaDeAlvoSegundos = janelaTrocaDeAlvoSegundos;
    }

    private double[] Ativar(TorreTreinamento torre, double[] entradas) =>
        _redeRecorrente is not null ? _redeRecorrente.Ativar(torre.Id, entradas) : _rede!.Ativar(entradas);

    /// <summary>Mesma camada heurística da V1 (próprio quadrante → adjacente → sobressalente); só o vetor de entrada da rede muda.</summary>
    public TorreTreinamento? SelecionarTorre(AlvoTreinamento alvo, List<TorreTreinamento> torres, List<AlvoTreinamento> outrosAlvosAtivos)
    {
        List<TorreTreinamento> livres = torres.Where(t => t.Livre).ToList();
        if (livres.Count == 0) return null;

        HashSet<Models.Quadrant> quadrantesComOutrosAlvos = outrosAlvosAtivos
            .Where(a => a.Id != alvo.Id)
            .Select(a => a.Quadrante)
            .ToHashSet();

        List<TorreTreinamento> doProprioQuadrante = alvo.VisivelPorTodasAsTorres
            ? livres
            : livres.Where(t => t.QuadrantePreferido == alvo.Quadrante).ToList();

        List<TorreTreinamento> adjacentes = doProprioQuadrante.Count > 0
            ? []
            : livres.Where(t =>
                QuadrantHelper.AreAdjacent(t.QuadrantePreferido, alvo.Quadrante) &&
                !quadrantesComOutrosAlvos.Contains(t.QuadrantePreferido)).ToList();

        List<TorreTreinamento> sobressalentes = doProprioQuadrante.Count == 0 && adjacentes.Count == 0
            ? livres
            : [];

        (List<TorreTreinamento> candidatas, double fatorCamada) = doProprioQuadrante.Count > 0
            ? (doProprioQuadrante, 1.0)
            : adjacentes.Count > 0
                ? (adjacentes, 0.5)
                : (sobressalentes, 0.0);

        TorreTreinamento? melhor = null;
        double melhorPontuacao = double.NegativeInfinity;

        foreach (TorreTreinamento torre in candidatas)
        {
            double[] entradas = ConstruirEntradas(alvo, torre, torres, fatorCamada);
            double pontuacaoFinal = Pontuar(Ativar(torre, entradas), entradas[0], fatorCamada);

            if (pontuacaoFinal > melhorPontuacao)
            {
                melhorPontuacao = pontuacaoFinal;
                melhor = torre;
            }
        }

        return melhor;
    }

    /// <summary>
    /// Mesmo veto inegociável de tempo mínimo de mira da V1. Devolve, além da decisão de disparo,
    /// a prioridade de cooperação (saída nova) — quem decide o que fazer com ela é
    /// <see cref="AmbienteTreinamento.DecidirDisparosV2"/>, não este método.
    /// </summary>
    public (bool Disparar, double PrioridadeCooperacao) DecidirDisparo(AlvoTreinamento alvo, TorreTreinamento torre, List<TorreTreinamento> todasTorres)
    {
        if (alvo.TempoDesdeSelecao < TempoMinimoDeMiraSegundos) return (false, 0.0);

        double[] entradas = ConstruirEntradas(alvo, torre, todasTorres, fatorCamada: 1.0);
        double[] saidas = Ativar(torre, entradas);

        return (saidas[1] > 0, saidas[2]);
    }

    private double[] ConstruirEntradas(AlvoTreinamento alvo, TorreTreinamento torre, List<TorreTreinamento> todasTorres, double fatorCamada)
    {
        double distancia = DistanceCalculator.Euclidean(alvo.X, alvo.Y, torre.X, torre.Y);
        double anguloMiraGraus = (Math.Atan2(alvo.X - torre.X, alvo.Y - torre.Y) * 180.0 / Math.PI + 360.0) % 360.0;
        bool mesmoQuadrante = torre.QuadrantePreferido == alvo.Quadrante;
        bool quadranteAdjacente = QuadrantHelper.AreAdjacent(torre.QuadrantePreferido, alvo.Quadrante);

        int torresAcompanhando = todasTorres.Count(t =>
            t.Id != torre.Id &&
            (alvo.VisivelPorTodasAsTorres || t.QuadrantePreferido == alvo.Quadrante || QuadrantHelper.AreAdjacent(t.QuadrantePreferido, alvo.Quadrante)));
        double torresAcompanhandoNormalizado = todasTorres.Count > 1
            ? Math.Clamp((double)torresAcompanhando / (todasTorres.Count - 1), 0, 1)
            : 0.0;

        double direcaoMovimentoGraus = (Math.Atan2(alvo.VelocidadeX, alvo.VelocidadeY) * 180.0 / Math.PI + 360.0) % 360.0;

        double distanciaBase = DistanceCalculator.Euclidean(alvo.X, alvo.Y, 0, 0);
        double tempoEstimadoSaidaAlcanceSegundos = distanciaBase / Math.Max(alvo.VelocidadeMetrosPorSegundo, 0.01);
        // Normaliza pelo pior caso plausível: atravessar o raio inteiro do mundo na velocidade
        // mínima configurada — além disso, satura em 1 (alvo "não vai saír tão cedo").
        double tempoEstimadoSaidaAlcanceNormalizado = Math.Clamp(
            tempoEstimadoSaidaAlcanceSegundos / Math.Max(_raioMundoMetros / Math.Max(0.01, _velocidadeMaximaAlvoMetrosPorSegundo * 0.2), 0.01),
            0, 1);

        double alturaNormalizada = _alturaMaximaAlvoMetros > 0 ? Math.Clamp(alvo.AlturaMetros / _alturaMaximaAlvoMetros, 0, 1) : 0.0;

        bool acompanhamentoPrevio = alvo.TorreSelecionadaId == torre.Id;
        int trocasRecentes = torre.ContarTrocasRecentes(alvo.TempoDeVidaSegundos, _janelaTrocaDeAlvoSegundos);

        return ConstruirEntradas(
            distancia, _raioMundoMetros, mesmoQuadrante, quadranteAdjacente, anguloMiraGraus,
            alvo.VelocidadeMetrosPorSegundo, _velocidadeMaximaAlvoMetrosPorSegundo,
            alvo.TempoDeVidaSegundos, _tempoDeteccaoRelevanteSegundos, fatorCamada,
            torresAcompanhandoNormalizado, torre.Livre ? 1.0 : 0.0, direcaoMovimentoGraus,
            tempoEstimadoSaidaAlcanceNormalizado, alturaNormalizada, alvo.VisivelPorTodasAsTorres,
            acompanhamentoPrevio, trocasRecentes);
    }

    /// <summary>Monta o vetor de 15 entradas a partir de valores primitivos — mesmo espírito do overload estático de <see cref="ControladorHibrido"/>, generalizado para o contrato V2.</summary>
    public static double[] ConstruirEntradas(
        double distanciaMetros, double raioMundoMetros, bool mesmoQuadrante, bool quadranteAdjacente,
        double anguloMiraGraus, double velocidadeMetrosPorSegundo, double velocidadeMaximaMetrosPorSegundo,
        double tempoDeteccaoSegundos, double tempoDeteccaoRelevanteSegundos, double fatorCamada,
        double torresAcompanhandoNormalizado, double disponibilidadeTorreCandidata, double direcaoMovimentoGraus,
        double tempoEstimadoSaidaAlcanceNormalizado, double alturaNormalizada, bool visivelPorTodasAsTorres,
        bool acompanhamentoPrevio, int trocasRecentesDaTorre)
    {
        double distanciaNormalizada = Math.Clamp(distanciaMetros / raioMundoMetros, 0, 1);
        double anguloMiraNormalizado = anguloMiraGraus / 360.0;
        double velocidadeNormalizada = Math.Clamp(velocidadeMetrosPorSegundo / velocidadeMaximaMetrosPorSegundo, 0, 1);
        double tempoDeteccaoNormalizado = tempoDeteccaoRelevanteSegundos > 0
            ? Math.Clamp(tempoDeteccaoSegundos / tempoDeteccaoRelevanteSegundos, 0, 1)
            : 0.0;
        double direcaoMovimentoNormalizada = direcaoMovimentoGraus / 360.0;

        // Satura em 1 com 3 trocas dentro da janela — a quarta não "piora" mais a leitura da
        // rede, só o fitness (ver ParametrosTreinamento.LimiteTrocasAntesDePenalizar).
        double trocasRecentesNormalizadas = Math.Clamp(trocasRecentesDaTorre / 3.0, 0, 1);

        return
        [
            distanciaNormalizada,
            mesmoQuadrante ? 1.0 : 0.0,
            quadranteAdjacente ? 1.0 : 0.0,
            anguloMiraNormalizado,
            velocidadeNormalizada,
            tempoDeteccaoNormalizado,
            fatorCamada,
            torresAcompanhandoNormalizado,
            disponibilidadeTorreCandidata,
            direcaoMovimentoNormalizada,
            tempoEstimadoSaidaAlcanceNormalizado,
            alturaNormalizada,
            visivelPorTodasAsTorres ? 1.0 : 0.0,
            acompanhamentoPrevio ? 1.0 : 0.0,
            trocasRecentesNormalizadas,
        ];
    }

    /// <summary>Mesma combinação heurística+rede da V1 (ver <see cref="ControladorHibrido.Pontuar"/>), lendo a saída 0 de um vetor já calculado.</summary>
    public static double Pontuar(double[] saidasRede, double distanciaNormalizada, double fatorCamada)
    {
        double pontuacaoHeuristica = (1.0 - distanciaNormalizada) + fatorCamada;
        return pontuacaoHeuristica + saidasRede[0];
    }
}
