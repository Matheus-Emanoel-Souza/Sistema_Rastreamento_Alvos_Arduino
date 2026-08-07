using System;
using System.Windows;

namespace RadarTorres.App.Helpers;

/// <summary>
/// Converte entre os três sistemas de coordenadas usados no projeto:
/// <list type="bullet">
/// <item><b>Polar (sensor)</b>: ângulo em graus (0-360, sentido horário, 0°=Norte) + distância em metros.</item>
/// <item><b>Cartesiano de mundo</b>: X/Y em metros relativos à base (origem), eixo Y crescendo para o Norte.
/// É o sistema usado pelos quadrantes e por toda a lógica de negócio.</item>
/// <item><b>Cartesiano de tela (canvas)</b>: pixels, origem no canto superior esquerdo, Y crescendo para baixo.
/// Usado apenas pelo <see cref="Views.RadarControl"/> para desenhar.</item>
/// </list>
/// Ver <c>Documentation/ALGORITMO_SELECAO_TORRE.md</c> para a dedução matemática completa.
/// </summary>
public static class CoordinateConverter
{
    /// <summary>
    /// Converte uma leitura polar (ângulo/distância) em coordenadas cartesianas de mundo (metros).
    /// Convenção: ângulo 0° aponta para o Norte (eixo Y positivo) e cresce no sentido horário,
    /// como em uma bússola/radar. Por isso:
    /// <c>x = distância · sin(ângulo)</c> e <c>y = distância · cos(ângulo)</c>.
    /// </summary>
    public static (double X, double Y) PolarToCartesian(double angleDegrees, double distance)
    {
        double angleRad = DegreesToRadians(angleDegrees);
        double x = distance * Math.Sin(angleRad);
        double y = distance * Math.Cos(angleRad);
        return (x, y);
    }

    /// <summary>
    /// Converte uma posição cartesiana de mundo (metros) para pixels dentro do canvas do radar.
    /// </summary>
    /// <param name="worldX">Posição X em metros (relativa à base).</param>
    /// <param name="worldY">Posição Y em metros (relativa à base, positivo = Norte).</param>
    /// <param name="canvasSize">Largura/altura do canvas quadrado, em pixels.</param>
    /// <param name="maxDistanceMeters">Distância (m) que corresponde à borda externa do radar.</param>
    public static Point WorldToScreen(double worldX, double worldY, double canvasSize, double maxDistanceMeters)
    {
        if (maxDistanceMeters <= 0) maxDistanceMeters = 1;

        double radiusPx = canvasSize / 2.0;
        double scale = radiusPx / maxDistanceMeters;

        double screenX = radiusPx + worldX * scale;
        // Y de tela cresce para baixo; Y de mundo cresce para o Norte (para cima) -> inverte o sinal.
        double screenY = radiusPx - worldY * scale;

        return new Point(screenX, screenY);
    }

    public static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;

    public static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;

    /// <summary>Normaliza um ângulo para o intervalo [0, 360).</summary>
    public static double NormalizeAngle(double degrees)
    {
        double normalized = degrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }
}
