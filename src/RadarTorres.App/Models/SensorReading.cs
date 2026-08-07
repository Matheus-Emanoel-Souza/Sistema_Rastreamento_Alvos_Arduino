using System;

namespace RadarTorres.App.Models;

/// <summary>
/// DTO (Data Transfer Object) que representa uma leitura bruta de sensor já validada
/// pelo <see cref="Services.SerialProtocolParser"/> (ou gerada pelo <see cref="Services.SimulationService"/>),
/// mas ainda não convertida em coordenadas cartesianas nem associada a um <see cref="Target"/> existente.
/// É o formato comum consumido pelo <see cref="Services.TargetTrackingService"/>,
/// independentemente de a leitura ter vindo do Arduino real ou do simulador.
/// </summary>
public sealed class SensorReading
{
    /// <summary>Identificador do alvo reportado pelo sensor (campo ID do protocolo).</summary>
    public required int TargetId { get; init; }

    /// <summary>Ângulo em graus (0-360), sentido horário a partir do Norte.</summary>
    public required double Angle { get; init; }

    /// <summary>Distância em metros até a base.</summary>
    public required double Distance { get; init; }

    /// <summary>Instante em que a leitura foi recebida/gerada.</summary>
    public DateTime ReceivedAt { get; init; } = DateTime.Now;

    /// <summary>Origem do dado: hardware serial ou simulador interno.</summary>
    public DataSource Source { get; init; } = DataSource.Serial;

    public override string ToString() => $"TARGET;ID={TargetId};ANGLE={Angle:0.0};DIST={Distance:0.00}";
}
