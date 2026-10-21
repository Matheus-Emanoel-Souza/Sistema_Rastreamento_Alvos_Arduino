using System;
using RadarTorres.App.Models;

namespace RadarTorres.App.Helpers;

/// <summary>
/// Calcula o ângulo (convenção bússola — 0°=Norte, sentido horário, ver
/// <see cref="CoordinateConverter.PolarToCartesian"/>) que uma torre precisa apontar para encarar um
/// alvo. É o inverso de <see cref="CoordinateConverter.PolarToCartesian"/>: lá, ângulo+distância vira
/// (X, Y); aqui, (X, Y) relativo à torre volta a ser um ângulo.
/// </summary>
public static class BearingCalculator
{
    /// <summary>Ângulo de mira (0°-360°) da torre até o alvo.</summary>
    public static double AngleTo(Tower tower, Target target)
    {
        double dx = target.X - tower.X;
        double dy = target.Y - tower.Y;

        double angleDegrees = Math.Atan2(dx, dy) * (180.0 / Math.PI);
        return (angleDegrees + 360.0) % 360.0;
    }
}
