namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// Nível de dificuldade do mundo simulado dentro do curriculum progressivo (ver
/// <see cref="CurriculoTreinamento"/>) — cada etapa é um conjunto fixo de parâmetros de
/// <see cref="ParametrosTreinamento"/> (ritmo de nascimento, velocidade máxima, curvatura da
/// trajetória), não um ajuste contínuo. Discreto de propósito: mais fácil de auditar e testar que
/// variar gradualmente geração a geração.
/// </summary>
public enum EtapaCurriculo
{
    /// <summary>Spawn lento, alvos devagar, sem curva — ponto de partida do curriculum.</summary>
    Facil,

    /// <summary>Valores de fábrica de <see cref="ParametrosTreinamento"/> — mesma dificuldade usada até aqui sem curriculum ligado.</summary>
    Medio,

    /// <summary>Spawn rápido, velocidade perto do teto físico derivado, curva acentuada.</summary>
    Dificil,
}
