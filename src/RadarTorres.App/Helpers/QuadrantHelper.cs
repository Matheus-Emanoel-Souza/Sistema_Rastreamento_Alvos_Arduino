using RadarTorres.App.Models;

namespace RadarTorres.App.Helpers;

/// <summary>
/// Responsável exclusivamente por determinar em qual quadrante uma posição cartesiana se encontra.
/// Convenção adotada (fixa para todo o projeto):
/// Q1 = X positivo / Y positivo · Q2 = X negativo / Y positivo ·
/// Q3 = X negativo / Y negativo · Q4 = X positivo / Y negativo.
/// </summary>
public static class QuadrantHelper
{
    /// <summary>
    /// Determina o quadrante de um ponto (X, Y). Pontos exatamente sobre um eixo (X=0 ou Y=0)
    /// são tratados como pertencentes ao quadrante seguinte no sentido horário, para que a
    /// classificação seja sempre determinística (nunca retorna <see cref="Quadrant.None"/>
    /// a menos que o ponto esteja exatamente na origem, ou seja, sobre a própria base).
    /// </summary>
    public static Quadrant Determine(double x, double y)
    {
        if (x == 0 && y == 0) return Quadrant.None;

        bool xPositive = x >= 0;
        bool yPositive = y >= 0;

        return (xPositive, yPositive) switch
        {
            (true, true) => Quadrant.Q1,
            (false, true) => Quadrant.Q2,
            (false, false) => Quadrant.Q3,
            (true, false) => Quadrant.Q4
        };
    }

    /// <summary>Rótulo amigável em português para exibição na interface.</summary>
    public static string ToDisplayLabel(Quadrant quadrant) => quadrant switch
    {
        Quadrant.Q1 => "Q1",
        Quadrant.Q2 => "Q2",
        Quadrant.Q3 => "Q3",
        Quadrant.Q4 => "Q4",
        _ => "—"
    };

    /// <summary>
    /// Verdadeiro se os dois quadrantes forem vizinhos geométricos (compartilham um eixo, ou seja,
    /// uma torre de um deles cobre uma área fisicamente ao lado da área do outro). Usado pela
    /// cooperação entre torres: Q1-Q2 e Q3-Q4 são vizinhos no eixo Y; Q1-Q4 e Q2-Q3 são vizinhos no
    /// eixo X; Q1-Q3 e Q2-Q4 são diagonais (NÃO vizinhos) — cobertos só depois dos vizinhos diretos.
    /// </summary>
    public static bool AreAdjacent(Quadrant a, Quadrant b)
    {
        if (a == Quadrant.None || b == Quadrant.None || a == b) return false;

        return (a, b) is
            (Quadrant.Q1, Quadrant.Q2) or (Quadrant.Q2, Quadrant.Q1) or
            (Quadrant.Q2, Quadrant.Q3) or (Quadrant.Q3, Quadrant.Q2) or
            (Quadrant.Q3, Quadrant.Q4) or (Quadrant.Q4, Quadrant.Q3) or
            (Quadrant.Q4, Quadrant.Q1) or (Quadrant.Q1, Quadrant.Q4);
    }
}
