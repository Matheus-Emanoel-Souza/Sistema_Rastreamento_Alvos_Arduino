using System;
using System.Collections.Generic;
using System.Linq;
using RadarTorres.App.Helpers;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// O controlador híbrido propriamente dito: combina a MESMA heurística em camadas usada por
/// <see cref="Services.TowerSelectionService"/> (torre do próprio quadrante → adjacente →
/// cobertura) com o ajuste de uma <see cref="RedeNeural"/> evoluída por
/// <see cref="AlgoritmoGenetico"/>. A heurística garante que a decisão nunca seja absurda
/// (nunca escolhe uma torre ocupada, nunca ignora a cooperação entre quadrantes); a rede
/// neural refina a pontuação dentro do que a heurística já considerou viável — e também decide
/// o instante do disparo, dado o alvo já mirado.
/// </summary>
public sealed class ControladorHibrido
{
    /// <summary>distância normalizada, mesmo quadrante?, quadrante adjacente?, ângulo normalizado, velocidade normalizada, fator da camada heurística.</summary>
    public const int QuantidadeEntradas = 6;

    /// <summary>ajuste de pontuação da torre candidata, confiança de disparo.</summary>
    public const int QuantidadeSaidas = 2;

    public static readonly int[] ArquiteturaPadrao = { QuantidadeEntradas, 8, QuantidadeSaidas };

    private const double TempoMinimoDeMiraSegundos = 0.3;

    private readonly RedeNeural _rede;
    private readonly double _raioMundoMetros;
    private readonly double _velocidadeMaximaAlvoMetrosPorSegundo;

    public ControladorHibrido(RedeNeural rede, double raioMundoMetros, double velocidadeMaximaAlvoMetrosPorSegundo)
    {
        _rede = rede;
        _raioMundoMetros = raioMundoMetros;
        _velocidadeMaximaAlvoMetrosPorSegundo = velocidadeMaximaAlvoMetrosPorSegundo;
    }

    /// <summary>
    /// Escolhe, para um alvo ainda sem torre comprometida, qual torre livre deve assumi-lo —
    /// ou <c>null</c> se nenhuma torre está disponível. Espelha as três camadas de
    /// <see cref="Services.TowerSelectionService.SelectTowerFor"/>: só dentro da camada
    /// escolhida (próprio quadrante, depois adjacente, depois sobressalente) a rede neural
    /// decide qual candidata específica vence.
    /// </summary>
    public TorreTreinamento? SelecionarTorre(AlvoTreinamento alvo, List<TorreTreinamento> torres, List<AlvoTreinamento> outrosAlvosAtivos)
    {
        List<TorreTreinamento> livres = torres.Where(t => t.Livre).ToList();
        if (livres.Count == 0) return null;

        HashSet<Models.Quadrant> quadrantesComOutrosAlvos = outrosAlvosAtivos
            .Where(a => a.Id != alvo.Id)
            .Select(a => a.Quadrante)
            .ToHashSet();

        List<TorreTreinamento> doProprioQuadrante = livres.Where(t => t.QuadrantePreferido == alvo.Quadrante).ToList();

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
            double[] entradas = ConstruirEntradas(alvo, torre, fatorCamada);
            double pontuacaoFinal = Pontuar(_rede, entradas, fatorCamada);

            if (pontuacaoFinal > melhorPontuacao)
            {
                melhorPontuacao = pontuacaoFinal;
                melhor = torre;
            }
        }

        return melhor;
    }

    /// <summary>
    /// Decide se a torre já comprometida com o alvo deve disparar agora. A heurística impõe os
    /// limites inegociáveis (alcance de engajamento, tempo mínimo de mira); dentro deles, a
    /// rede neural decide o instante exato — ela pode esperar o alvo ficar mais exposto em vez
    /// de disparar na primeira oportunidade.
    /// </summary>
    public bool DecidirDisparo(AlvoTreinamento alvo, TorreTreinamento torre, double distanciaEngajamentoMetros)
    {
        if (alvo.TempoDesdeSelecao < TempoMinimoDeMiraSegundos) return false;

        double distancia = DistanceCalculator.Euclidean(alvo.X, alvo.Y, torre.X, torre.Y);
        if (distancia > distanciaEngajamentoMetros) return false;

        double[] entradas = ConstruirEntradas(alvo, torre, fatorCamada: 1.0);
        double confiancaDisparo = _rede.Ativar(entradas)[1];

        return confiancaDisparo > 0;
    }

    private double[] ConstruirEntradas(AlvoTreinamento alvo, TorreTreinamento torre, double fatorCamada)
    {
        double distancia = DistanceCalculator.Euclidean(alvo.X, alvo.Y, torre.X, torre.Y);
        double anguloGraus = (Math.Atan2(alvo.X - torre.X, alvo.Y - torre.Y) * 180.0 / Math.PI + 360.0) % 360.0;
        bool mesmoQuadrante = torre.QuadrantePreferido == alvo.Quadrante;
        bool quadranteAdjacente = QuadrantHelper.AreAdjacent(torre.QuadrantePreferido, alvo.Quadrante);

        return ConstruirEntradas(distancia, _raioMundoMetros, mesmoQuadrante, quadranteAdjacente, anguloGraus, alvo.VelocidadeMetrosPorSegundo, _velocidadeMaximaAlvoMetrosPorSegundo, fatorCamada);
    }

    /// <summary>
    /// Monta o vetor de entradas da rede a partir de valores primitivos — forma reaproveitada
    /// pelo sistema real (ver <see cref="Services.TowerSelectionService"/>), que não tem
    /// <see cref="AlvoTreinamento"/>/<see cref="TorreTreinamento"/> (tipos exclusivos do
    /// ambiente de treinamento), mas precisa produzir EXATAMENTE a mesma entrada que treinou o
    /// genoma — qualquer divergência de fórmula aqui faz o genoma treinado não valer nada fora
    /// da simulação.
    /// </summary>
    public static double[] ConstruirEntradas(
        double distanciaMetros,
        double raioMundoMetros,
        bool mesmoQuadrante,
        bool quadranteAdjacente,
        double anguloGraus,
        double velocidadeMetrosPorSegundo,
        double velocidadeMaximaMetrosPorSegundo,
        double fatorCamada)
    {
        double distanciaNormalizada = Math.Clamp(distanciaMetros / raioMundoMetros, 0, 1);
        double anguloNormalizado = anguloGraus / 360.0;
        double velocidadeNormalizada = Math.Clamp(velocidadeMetrosPorSegundo / velocidadeMaximaMetrosPorSegundo, 0, 1);

        return
        [
            distanciaNormalizada,
            mesmoQuadrante ? 1.0 : 0.0,
            quadranteAdjacente ? 1.0 : 0.0,
            anguloNormalizado,
            velocidadeNormalizada,
            fatorCamada
        ];
    }

    /// <summary>
    /// Combina a pontuação heurística (quanto mais perto e quanto "melhor" a camada, maior) com
    /// o ajuste da rede neural — a MESMA combinação usada para treinar (ver
    /// <see cref="SelecionarTorre"/>) e para decidir em produção (ver
    /// <see cref="Services.TowerSelectionService"/>).
    /// </summary>
    public static double Pontuar(RedeNeural rede, double[] entradas, double fatorCamada)
    {
        double ajusteRedeNeural = rede.Ativar(entradas)[0];
        double distanciaNormalizada = entradas[0];
        double pontuacaoHeuristica = (1.0 - distanciaNormalizada) + fatorCamada;
        return pontuacaoHeuristica + ajusteRedeNeural;
    }
}
