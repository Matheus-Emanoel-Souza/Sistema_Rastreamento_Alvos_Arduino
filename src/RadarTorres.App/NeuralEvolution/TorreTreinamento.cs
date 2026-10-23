using RadarTorres.App.Models;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// Torre dentro de um episódio de treinamento. Posição fixa durante todo o episódio (mesma
/// premissa de <see cref="Tower"/> no sistema real) — só muda se está livre para receber um
/// novo alvo.
/// </summary>
public sealed class TorreTreinamento
{
    public int Id { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public Quadrant QuadrantePreferido { get; init; }

    /// <summary>Id do alvo atualmente comprometido com esta torre, ou <c>null</c> se livre.</summary>
    public int? AlvoComprometidoId { get; set; }

    public bool Livre => AlvoComprometidoId is null;

    /// <summary>
    /// Instante (<see cref="EstadoEpisodio.TempoDecorridoSegundos"/>) do último disparo desta
    /// torre, ou <c>null</c> se ainda não disparou neste episódio. Usado só para a tela destacar
    /// visualmente "disparando" por uma janela curta — ver <see cref="ControladorHibrido.DecidirDisparo"/>.
    /// </summary>
    public double? DisparadoEmSegundos { get; set; }

    /// <summary>Posição do alvo no instante do último disparo — guardada porque o alvo abatido é removido do episódio no mesmo passo em que dispara.</summary>
    public double UltimoAlvoDisparoX { get; set; }
    public double UltimoAlvoDisparoY { get; set; }
}
