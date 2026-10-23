namespace RadarTorres.App.NeuralEvolution;

/// <summary>Parâmetros ajustáveis de um episódio de treinamento — expostos na tela para o usuário experimentar.</summary>
public sealed record ParametrosTreinamento
{
    public double RaioMundoMetros { get; init; } = 10.0;
    public double DistanciaEngajamentoMetros { get; init; } = 3.0;
    public double VelocidadeMinimaAlvoMetrosPorSegundo { get; init; } = 0.5;
    public double VelocidadeMaximaAlvoMetrosPorSegundo { get; init; } = 2.5;
    public double DuracaoEpisodioSegundos { get; init; } = 45.0;
    public double IntervaloSpawnMinSegundos { get; init; } = 1.5;
    public double IntervaloSpawnMaxSegundos { get; init; } = 4.0;
    public int MaxAlvosSimultaneos { get; init; } = 5;
    public double RecompensaAbate { get; init; } = 10.0;
    public double PenalidadeEscape { get; init; } = 8.0;

    public int TamanhoPopulacao { get; init; } = 60;
    public double TaxaMutacao { get; init; } = 0.08;
}
