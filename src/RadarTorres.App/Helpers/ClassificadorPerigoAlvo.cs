using RadarTorres.App.Models;

namespace RadarTorres.App.Helpers;

/// <summary>
/// Classifica o grau de perigosidade de um alvo pela velocidade estimada — mesma tabela usada
/// pelo sistema real (<see cref="Target.SpeedMetersPerSecond"/>) e pelo treinamento
/// (<see cref="NeuralEvolution.AlvoTreinamento.VelocidadeMetrosPorSegundo"/>), persistida em
/// <c>classificacoes_alvo</c> (ver <see cref="Repositories.IClassificacaoPerigoRepository"/>).
/// </summary>
/// <remarks>
/// Tabela de referência (velocidade do alvo, m/s → grau):
/// <list type="table">
/// <item><term>&lt; 0,5</term><description>Baixo</description></item>
/// <item><term>0,5 – 1,2</term><description>Médio</description></item>
/// <item><term>1,2 – 2,0</term><description>Alto</description></item>
/// <item><term>&gt; 2,0</term><description>Crítico</description></item>
/// </list>
/// 2,0 m/s fica logo abaixo da velocidade máxima de alvo simulada no treinamento (2,5 m/s — ver
/// <see cref="NeuralEvolution.ParametrosTreinamento.VelocidadeMaximaAlvoMetrosPorSegundo"/>), para
/// que só a fração mais rápida do espectro simulado caia em "Crítico".
/// </remarks>
public static class ClassificadorPerigoAlvo
{
    public const double LimiarBaixoMetrosPorSegundo = 0.5;
    public const double LimiarMedioMetrosPorSegundo = 1.2;
    public const double LimiarAltoMetrosPorSegundo = 2.0;

    public static GrauPerigoAlvo Classificar(double velocidadeMetrosPorSegundo) => velocidadeMetrosPorSegundo switch
    {
        < LimiarBaixoMetrosPorSegundo => GrauPerigoAlvo.Baixo,
        < LimiarMedioMetrosPorSegundo => GrauPerigoAlvo.Medio,
        < LimiarAltoMetrosPorSegundo => GrauPerigoAlvo.Alto,
        _ => GrauPerigoAlvo.Critico
    };
}
