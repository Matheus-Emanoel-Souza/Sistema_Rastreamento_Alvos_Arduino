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

    /// <summary>
    /// Certeza (0 a 1) de que isto é realmente um alvo — sorteada no nascimento, simula a
    /// variação de confiança da identificação por câmera no sistema real. Só acima do limiar
    /// configurado (<see cref="ParametrosTreinamento.LimiarConfiancaDisparoFracao"/>) o disparo é
    /// permitido, pela mesma regra do sistema real.
    /// </summary>
    public double Certeza { get; init; } = 1.0;

    /// <summary>Já aplicada a penalidade de "perda de ponto da geração" por ter chegado perto demais da base sem ser abatido nem ter escapado de fato — evita descontar a cada passo enquanto o alvo continua dentro do raio.</summary>
    public bool PenalidadeProximidadeAplicada { get; set; }
}
