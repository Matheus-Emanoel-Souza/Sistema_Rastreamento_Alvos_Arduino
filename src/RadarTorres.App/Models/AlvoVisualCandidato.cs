namespace RadarTorres.App.Models;

/// <summary>De onde veio a caixa candidata que está sendo avaliada.</summary>
public enum OrigemCandidato
{
    /// <summary>Proposta pelo modelo ONNX (YOLO26n) — um objeto de alguma das 80 classes COCO.</summary>
    Detector,

    /// <summary>Proposta por segmentação da cor do perfil — mancha contígua da cor procurada.</summary>
    Cor
}

/// <summary>
/// Uma região que foi avaliada num quadro, com o veredito e os números que levaram a ele.
/// Existe para a tela poder DESENHAR sobre o vídeo tudo o que foi considerado — não só o alvo
/// aceito —, que é a única forma prática de o operador ver que o sistema está travando no
/// objeto certo e não em algo parecido ao fundo.
/// </summary>
/// <param name="Deteccao">Caixa em pixels do quadro original.</param>
/// <param name="Pontuacao">Similaridade com a foto de referência, ou fração de cor quando o perfil não tem foto.</param>
/// <param name="FracaoCor">Fração de pixels da caixa na cor cadastrada no perfil.</param>
/// <param name="Aceito">Se esta região passou em todos os critérios e é o alvo.</param>
/// <param name="Origem">Quem propôs a região.</param>
public sealed record AlvoVisualCandidato(
    VisionDetection Deteccao,
    double Pontuacao,
    double FracaoCor,
    bool Aceito,
    OrigemCandidato Origem);
