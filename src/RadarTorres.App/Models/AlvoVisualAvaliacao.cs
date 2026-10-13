namespace RadarTorres.App.Models;

/// <summary>
/// Resultado diagnóstico completo de <see cref="Services.IAlvoVisualService.AvaliarFrame"/> —
/// não só se bateu (<see cref="Resultado"/>), mas quantas caixas o YOLO propôs
/// (<see cref="TotalDeteccoes"/>) e a melhor pontuação encontrada entre elas
/// (<see cref="MelhorPontuacao"/>, similaridade visual com a foto de referência quando o
/// perfil tem uma — <see cref="UsouFotoReferencia"/> —, ou fração de pixels na cor do perfil
/// caso contrário), mesmo quando nada bate — é o que permite a tela "Detecção por Câmera"
/// mostrar exatamente em qual etapa a detecção está parando, em vez de só "não identificado".
/// </summary>
public sealed record AlvoVisualAvaliacao(int TotalDeteccoes, double MelhorPontuacao, bool UsouFotoReferencia, AlvoVisualResultado? Resultado);
