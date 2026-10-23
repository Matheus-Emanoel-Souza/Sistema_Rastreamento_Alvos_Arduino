using RadarTorres.App.Models;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>Alvo simulado dentro de um episódio de treinamento — move em linha reta a partir da borda do mundo em direção à base.</summary>
public sealed class AlvoTreinamento
{
    public int Id { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double VelocidadeX { get; init; }
    public double VelocidadeY { get; init; }
    public double VelocidadeMetrosPorSegundo { get; init; }

    public Quadrant Quadrante { get; set; }
    public bool Ativo { get; set; } = true;

    public int? TorreSelecionadaId { get; set; }

    /// <summary>Tempo (s) desde que uma torre foi comprometida com este alvo — usado como "tempo de mira" mínimo antes de poder disparar.</summary>
    public double TempoDesdeSelecao { get; set; }

    public bool Abatido { get; set; }
}
