namespace RadarTorres.App.Models;

/// <summary>
/// Grau de perigosidade de um alvo conforme a velocidade estimada (ver
/// <see cref="Helpers.ClassificadorPerigoAlvo"/>) — alvo mais rápido é mais perigoso porque sobra
/// menos tempo de reação e a chance de erro no disparo (<see cref="Services.FireControlService"/>)
/// é maior.
/// </summary>
public enum GrauPerigoAlvo
{
    Baixo,
    Medio,
    Alto,
    Critico
}
