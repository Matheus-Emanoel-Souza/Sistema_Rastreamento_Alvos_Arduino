namespace RadarTorres.App.Models;

/// <summary>
/// Uma caixa detectada pelo <see cref="Services.IObjectDetectionService"/> num quadro de
/// câmera — já em coordenadas de pixel da imagem original (não do tensor 416×416 usado
/// internamente pelo modelo). A classe COCO do YOLO (pessoa, carro, etc.) é ignorada pelo
/// restante do pipeline: o modelo serve apenas para propor regiões candidatas; quem decide se
/// a região é "o alvo" é <see cref="Services.IAlvoVisualService"/>, comparando cor/formato/
/// tamanho com o <see cref="PerfilAlvo"/> ativo.
/// </summary>
public sealed record VisionDetection(int BoxXPx, int BoxYPx, int BoxWidthPx, int BoxHeightPx, double Score);
