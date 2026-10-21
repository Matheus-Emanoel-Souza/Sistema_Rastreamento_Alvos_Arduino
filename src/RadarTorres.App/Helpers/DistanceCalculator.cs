using System;
using RadarTorres.App.Models;

namespace RadarTorres.App.Helpers;

/// <summary>
/// Cálculos de distância reutilizados pelo algoritmo de seleção de torres e pela camada de segurança.
/// Mantido como classe estática e sem estado — é matemática pura, deve ser trivial de testar isoladamente.
/// </summary>
public static class DistanceCalculator
{
    /// <summary>Distância Euclidiana entre dois pontos no plano cartesiano de mundo (metros).</summary>
    public static double Euclidean(double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1;
        double dy = y2 - y1;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Distância Euclidiana entre um alvo e uma torre.</summary>
    public static double Between(Target target, Tower tower) =>
        Euclidean(target.X, target.Y, tower.X, tower.Y);

    /// <summary>Distância do alvo até a base (origem, 0,0) — equivalente ao campo DIST do protocolo.</summary>
    public static double DistanceFromBase(Target target) =>
        Euclidean(0, 0, target.X, target.Y);
}
