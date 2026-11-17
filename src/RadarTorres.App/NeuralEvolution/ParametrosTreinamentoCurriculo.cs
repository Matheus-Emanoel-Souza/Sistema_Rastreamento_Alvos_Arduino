using System;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// Traduz uma <see cref="EtapaCurriculo"/> num conjunto de <see cref="ParametrosTreinamento"/> —
/// só mexe nos parâmetros de DIFICULDADE DO MUNDO (ritmo de nascimento, velocidade máxima,
/// curvatura), nunca nos termos da fórmula de fitness: o objetivo é variar o quão difícil é o
/// cenário, não mudar o que conta pontos, para a comparação entre etapas continuar honesta.
/// </summary>
public static class ParametrosTreinamentoCurriculo
{
    /// <summary>Piso absoluto para qualquer intervalo de spawn calculado — evita gerar um valor zero/negativo ao encolher o intervalo na etapa difícil.</summary>
    private const double IntervaloSpawnMinimoAbsolutoSegundos = 0.1;

    public static ParametrosTreinamento AplicarEtapa(this ParametrosTreinamento baseParams, EtapaCurriculo etapa)
    {
        ArgumentNullException.ThrowIfNull(baseParams);

        return etapa switch
        {
            EtapaCurriculo.Facil => baseParams with
            {
                IntervaloSpawnMinSegundos = baseParams.IntervaloSpawnMinSegundos * 1.8,
                IntervaloSpawnMaxSegundos = baseParams.IntervaloSpawnMaxSegundos * 1.8,
                IntervaloSpawnMinFinalSegundos = baseParams.IntervaloSpawnMinFinalSegundos * 1.8,
                IntervaloSpawnMaxFinalSegundos = baseParams.IntervaloSpawnMaxFinalSegundos * 1.8,
                VelocidadeMaximaAlvoMetrosPorSegundo = baseParams.VelocidadeMaximaAlvoMetrosPorSegundo * 0.6,
                VariacaoMaximaDirecaoGrausPorSegundo = 0.0,
            },

            EtapaCurriculo.Medio => baseParams,

            EtapaCurriculo.Dificil => baseParams with
            {
                IntervaloSpawnMinSegundos = Math.Max(IntervaloSpawnMinimoAbsolutoSegundos, baseParams.IntervaloSpawnMinSegundos * 0.6),
                IntervaloSpawnMaxSegundos = Math.Max(IntervaloSpawnMinimoAbsolutoSegundos, baseParams.IntervaloSpawnMaxSegundos * 0.6),
                IntervaloSpawnMinFinalSegundos = Math.Max(IntervaloSpawnMinimoAbsolutoSegundos, baseParams.IntervaloSpawnMinFinalSegundos * 0.6),
                IntervaloSpawnMaxFinalSegundos = Math.Max(IntervaloSpawnMinimoAbsolutoSegundos, baseParams.IntervaloSpawnMaxFinalSegundos * 0.6),
                VariacaoMaximaDirecaoGrausPorSegundo = baseParams.VariacaoMaximaDirecaoGrausPorSegundo * 1.6,
                // Velocidade máxima NÃO sobe na etapa difícil: já é derivada do teto físico da
                // torre (ver VelocidadeMaximaAlvoDerivadaMetrosPorSegundo) — ultrapassar isso
                // criaria cenários que a torre real não consegue acompanhar nem em tese.
            },

            _ => throw new ArgumentOutOfRangeException(nameof(etapa), etapa, null),
        };
    }
}
