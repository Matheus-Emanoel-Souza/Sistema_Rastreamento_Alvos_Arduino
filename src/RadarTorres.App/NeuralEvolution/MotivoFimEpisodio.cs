namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// Por que um episódio de treinamento terminou — diagnóstico (ver <see cref="EstadoEpisodio.MotivoFim"/>),
/// não influencia o cálculo de fitness. Existia antes só implicitamente (qual método chamava
/// <c>FinalizarEpisodio</c>); exposto explicitamente para permitir contar separadamente quantos
/// episódios de uma geração terminam por falha de controle (invasão) versus por bater o teto de
/// segurança contra loop infinito (controlador bom o bastante para nunca ser invadido).
/// </summary>
public enum MotivoFimEpisodio
{
    /// <summary>Episódio ainda em andamento — valor antes de <see cref="EstadoEpisodio.Finalizado"/> virar <c>true</c>.</summary>
    AindaEmAndamento,

    /// <summary>Um alvo chegou a menos de <see cref="ParametrosTreinamento.DistanciaInvasaoTorreMetros"/> de alguma torre.</summary>
    InvasaoDeTorre,

    /// <summary>Episódio atingiu <see cref="ParametrosTreinamento.DuracaoMaximaSegurancaSegundos"/> sem nenhuma invasão.</summary>
    TetoDeSeguranca
}
