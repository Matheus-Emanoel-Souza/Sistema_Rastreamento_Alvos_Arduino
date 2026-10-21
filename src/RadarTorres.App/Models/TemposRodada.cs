namespace RadarTorres.App.Models;

/// <summary>
/// Quanto cada etapa de uma rodada de detecção levou, em milissegundos. Medido dentro de
/// <see cref="Services.IAlvoVisualService.AvaliarFrame"/> e repassado adiante em
/// <see cref="AlvoVisualAvaliacao"/> para o relatório de desempenho
/// (<see cref="Services.IPerformanceMonitor"/>) poder gravar o detalhe sem que o serviço de
/// visão precise conhecer arquivo nenhum.
/// </summary>
/// <remarks>
/// A separação por etapa não é enfeite: foi medindo etapa a etapa que apareceu que o preparo da
/// entrada do modelo custava mais que metade do tempo do detector, e que contar cor em
/// resolução cheia custava dezenas de vezes mais que sobre o quadro reduzido. Manter a medição
/// ligada em produção é o que permite repetir esse tipo de achado com câmera e alvo reais, em
/// vez de com quadro simulado.
/// </remarks>
/// <param name="TotalMs">Tempo total da rodada.</param>
/// <param name="PreparoMs">Redução do quadro para a resolução de trabalho.</param>
/// <param name="ReferenciaMs">Obtenção da assinatura da foto do perfil (≈0 quando vem do cache).</param>
/// <param name="SegmentacaoCorMs">Segmentação por cor e agrupamento em componentes conectados.</param>
/// <param name="DetectorMs">Inferência do modelo ONNX. Zero quando a cor resolveu e ele não rodou.</param>
/// <param name="ComparacaoMs">Assinatura e similaridade de todos os candidatos.</param>
/// <param name="DetectorExecutou">Se o modelo ONNX chegou a rodar nesta rodada.</param>
public sealed record TemposRodada(
    double TotalMs,
    double PreparoMs,
    double ReferenciaMs,
    double SegmentacaoCorMs,
    double DetectorMs,
    double ComparacaoMs,
    bool DetectorExecutou)
{
    public static TemposRodada Zero { get; } = new(0, 0, 0, 0, 0, 0, false);
}
