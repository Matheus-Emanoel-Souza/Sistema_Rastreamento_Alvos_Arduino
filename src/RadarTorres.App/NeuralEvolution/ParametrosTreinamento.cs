using System.Collections.Generic;
using RadarTorres.App.Models;

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

    /// <summary>Piso absoluto do limiar de certeza — mesma regra do sistema real (ver <see cref="Configuration.ConfiguracoesAplicativo.ConfiancaMinimaDisparoPercentual"/>).</summary>
    public const double LimiarConfiancaMinimaFracao = 0.40;

    /// <summary>Certeza mínima (0 a 1) para autorizar disparo — equivalente a <see cref="Configuration.ConfiguracoesAplicativo.LimiarConfiancaDisparoPercentual"/>, mas em fração.</summary>
    public double LimiarConfiancaDisparoFracao { get; init; } = 0.70;

    /// <summary>Quadrantes tratados como zona morta neste episódio — vazio por padrão (nenhuma restrição). Espelha <see cref="Services.IZonaMortaService"/> do sistema real, só que fixo por episódio em vez de configurável em tela.</summary>
    public IReadOnlySet<Quadrant> ZonasMortasQuadrantes { get; init; } = new HashSet<Quadrant>();

    /// <summary>Distância (m) da base abaixo da qual o alvo, mesmo sem escapar de fato, já custa pontos à geração — aviso de proximidade perigosa.</summary>
    public double DistanciaPerdaPontoMetros { get; init; } = 0.5;

    /// <summary>Penalidade de fitness aplicada uma única vez por alvo ao cruzar <see cref="DistanciaPerdaPontoMetros"/> — menor que <see cref="PenalidadeEscape"/> porque o alvo pode ainda vir a ser abatido depois.</summary>
    public double PenalidadePerdaPonto { get; init; } = 3.0;

    public int TamanhoPopulacao { get; init; } = 60;
    public double TaxaMutacao { get; init; } = 0.08;
}
