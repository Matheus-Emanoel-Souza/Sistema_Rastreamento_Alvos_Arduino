namespace RadarTorres.App.Models;

/// <summary>
/// Resultado de <see cref="Services.IAlvoVisualService.AvaliarFrame"/> quando uma detecção do
/// quadro atual bate com o <see cref="PerfilAlvo"/> ativo (cor/formato/tamanho) — <c>null</c>
/// quando nenhuma detecção bate.
/// </summary>
public sealed record AlvoVisualResultado(PerfilAlvo Perfil, VisionDetection Deteccao, double DistanciaMetros);
