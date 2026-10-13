namespace RadarTorres.App.Models;

/// <summary>
/// Formato geométrico do objeto que a detecção por visão computacional deve travar (ver
/// <see cref="PerfilAlvo"/>). Restrito às três formas básicas por decisão do usuário.
/// </summary>
public enum FormatoAlvo
{
    Quadrado,
    Redondo,
    Retangular
}
