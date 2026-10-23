namespace RadarTorres.App.NeuralEvolution;

/// <summary>Resumo estatístico de um episódio completo — usado tanto como fitness (algoritmo genético) quanto como relatório na tela.</summary>
public sealed record EpisodioResultado(
    double FitnessTotal,
    int AlvosAbatidos,
    int AlvosEscapados);
