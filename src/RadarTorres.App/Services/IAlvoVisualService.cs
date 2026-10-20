using System.Drawing;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Decide, para um quadro de câmera, se alguma detecção bate com o <see cref="PerfilAlvo"/>
/// ativo (cor/formato/tamanho — ver <see cref="IPerfilAlvoService.PerfilAtivo"/>) e, se bater,
/// estima a distância do alvo. Fica entre <see cref="IObjectDetectionService"/> (só propõe
/// caixas) e a ViewModel do card "Detecção por Câmera" — pura lógica de negócio, sem
/// referência a WPF, mesmo princípio de <see cref="TowerSelectionService"/>.
/// </summary>
public interface IAlvoVisualService
{
    /// <summary>
    /// Avalia o quadro informado contra o perfil de alvo ativo. <see cref="AlvoVisualAvaliacao.Resultado"/>
    /// é <c>null</c> se não há perfil ativo ou se nenhuma detecção do quadro bate com cor/
    /// formato o suficiente (ver limiares em <see cref="Configuration.VisionSettings"/>) — os
    /// demais campos de <see cref="AlvoVisualAvaliacao"/> explicam em qual etapa parou.
    /// </summary>
    AlvoVisualAvaliacao AvaliarFrame(Bitmap frame);
}
