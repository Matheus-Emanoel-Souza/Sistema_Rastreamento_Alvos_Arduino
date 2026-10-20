namespace RadarTorres.App.Models;

/// <summary>
/// Cor do objeto que a detecção por visão computacional deve travar (ver <see cref="PerfilAlvo"/>).
/// Restrita às três cores primárias (RGB) por decisão do usuário — são as únicas que
/// correspondem diretamente a um canal do frame BGR24 capturado pela câmera.
/// </summary>
public enum CorAlvo
{
    Vermelho,
    Verde,
    Azul
}
