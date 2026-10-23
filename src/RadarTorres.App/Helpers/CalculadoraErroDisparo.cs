using System;

namespace RadarTorres.App.Helpers;

/// <summary>
/// Probabilidade de um disparo autorizado errar o alvo, crescendo exponencialmente com a
/// velocidade dele — alvo parado praticamente não erra, alvo rápido demanda mais de uma torre
/// disparando para compensar (ver <see cref="Models.Target.EngagingTowers"/>/
/// <see cref="Services.TowerSelectionService"/>, que já permite mais de um canhão no mesmo
/// alvo). Usada tanto pelo disparo real (<see cref="Services.FireControlService"/>) quanto pelo
/// treinamento (<see cref="NeuralEvolution.AmbienteTreinamento"/>), para a rede aprender a mesma
/// regra que vale em produção.
/// </summary>
/// <remarks>
/// Fórmula: <c>p = 1 - e^(-k · velocidade)</c> — crescimento exponencial saturando em 1 (nunca
/// 100% de certeza de erro, mas se aproxima). Com <see cref="FatorCrescimentoPorVelocidade"/> =
/// 0,35: alvo parado → ~0%; 1 m/s → ~30%; 2,5 m/s (velocidade máxima simulada no treinamento) →
/// ~59%.
/// </remarks>
public static class CalculadoraErroDisparo
{
    public const double FatorCrescimentoPorVelocidade = 0.35;

    public static double ProbabilidadeDeErro(double velocidadeMetrosPorSegundo) =>
        1.0 - Math.Exp(-FatorCrescimentoPorVelocidade * Math.Max(0, velocidadeMetrosPorSegundo));
}
