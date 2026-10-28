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

    /// <summary>Intervalo mínimo de nascimento no FIM do episódio — <see cref="IntervaloSpawnMinSegundos"/> é o do início; a interpolação linear entre os dois ao longo de <see cref="DuracaoEpisodioSegundos"/> é a escalada de dificuldade (mais alvos quanto mais o episódio avança), ver <see cref="AmbienteTreinamento"/>.</summary>
    public double IntervaloSpawnMinFinalSegundos { get; init; } = 0.6;

    /// <summary>Intervalo máximo de nascimento no FIM do episódio — ver <see cref="IntervaloSpawnMinFinalSegundos"/>.</summary>
    public double IntervaloSpawnMaxFinalSegundos { get; init; } = 1.8;

    /// <summary>Teto de alvos ATIVOS simultaneamente no MESMO quadrante — não é mais um teto global: quadrantes diferentes podem estar cada um no próprio limite ao mesmo tempo, forçando o controlador a lidar com concentração local em vez de só a contagem total.</summary>
    public int MaxAlvosPorQuadrante { get; init; } = 5;

    public double RecompensaAbate { get; init; } = 10.0;
    public double PenalidadeEscape { get; init; } = 8.0;

    /// <summary>Normaliza a entrada "tempo detectado sem abate" da rede (<see cref="ControladorHibrido.ConstruirEntradas(double,double,bool,bool,double,double,double,double,double,double)"/>) — satura em 1,0 a partir deste tempo de observação contínua.</summary>
    public double TempoDeteccaoRelevanteSegundos { get; init; } = 3.0;

    /// <summary>Distância (m) de uma torre abaixo da qual um alvo ainda ativo é considerado ter rompido a defesa — distinto de <see cref="DistanciaPerdaPontoMetros"/> (que é em torno da base, só penaliza) ou de <see cref="PenalidadeEscape"/> (fuga pela base): romper uma torre encerra o episódio imediatamente.</summary>
    public double DistanciaInvasaoTorreMetros { get; init; } = 1.0;

    /// <summary>Penalidade de fitness aplicada quando um alvo rompe uma torre (ver <see cref="DistanciaInvasaoTorreMetros"/>) — maior que <see cref="PenalidadeEscape"/> porque, além da penalidade, o episódio é interrompido e perde toda pontuação futura possível.</summary>
    public double PenalidadeInvasaoTorre { get; init; } = 16.0;

    /// <summary>Fator que converte o desvio padrão (segundos) do tempo comprometido entre as torres, ao fim do episódio, em penalidade de fitness — incentiva distribuir alvos entre torres em vez de sobrecarregar sempre a mesma (gestão de recursos), ver <see cref="TorreTreinamento.TempoComprometidoSegundos"/>.</summary>
    public double PenalidadeDesequilibrioTorresPorSegundo { get; init; } = 0.5;

    /// <summary>Piso absoluto do limiar de certeza — mesma regra do sistema real (ver <see cref="Configuration.ConfiguracoesAplicativo.ConfiancaMinimaDisparoPercentual"/>).</summary>
    public const double LimiarConfiancaMinimaFracao = 0.40;

    /// <summary>Certeza mínima (0 a 1) para autorizar disparo — equivalente a <see cref="Configuration.ConfiguracoesAplicativo.LimiarConfiancaDisparoPercentual"/>, mas em fração.</summary>
    public double LimiarConfiancaDisparoFracao { get; init; } = 0.70;

    /// <summary>Distância (m) da torre abaixo da qual a câmera não consegue mais confirmar o alvo com boa certeza (alvo perto demais da lente) — ver <see cref="AlvoTreinamento.CertezaEfetiva"/>.</summary>
    public double DistanciaMinimaBoaCertezaMetros { get; init; } = 2.0;

    /// <summary>Quadrantes tratados como zona morta neste episódio — vazio por padrão (nenhuma restrição). Espelha <see cref="Services.IZonaMortaService"/> do sistema real, só que fixo por episódio em vez de configurável em tela.</summary>
    public IReadOnlySet<Quadrant> ZonasMortasQuadrantes { get; init; } = new HashSet<Quadrant>();

    /// <summary>Distância (m) da base abaixo da qual o alvo, mesmo sem escapar de fato, já custa pontos à geração — aviso de proximidade perigosa.</summary>
    public double DistanciaPerdaPontoMetros { get; init; } = 0.5;

    /// <summary>Penalidade de fitness aplicada uma única vez por alvo ao cruzar <see cref="DistanciaPerdaPontoMetros"/> — menor que <see cref="PenalidadeEscape"/> porque o alvo pode ainda vir a ser abatido depois.</summary>
    public double PenalidadePerdaPonto { get; init; } = 3.0;

    /// <summary>Velocidade (m/s) a partir da qual um alvo aciona ajuda de torre adjacente — mesma regra e mesmo valor de fábrica de <see cref="Configuration.ConfiguracoesAplicativo.VelocidadeRapidaMetrosPorSegundo"/> (ver <see cref="AmbienteTreinamento.DecidirDisparos"/>).</summary>
    public double VelocidadeRapidaMetrosPorSegundo { get; init; } = 1.2;

    public int TamanhoPopulacao { get; init; } = 60;
    public double TaxaMutacao { get; init; } = 0.08;

    /// <summary>
    /// Quantos episódios independentes (seeds diferentes) são rodados e tirada a MÉDIA para
    /// chegar ao fitness de um genoma — ver <see cref="AmbienteTreinamento.AvaliarFitness"/>.
    /// Maior que 1 é necessário porque o ambiente é todo sorteado (nascimento, velocidade,
    /// certeza): com um único episódio, o mesmo genoma pode tirar um cenário fácil ou difícil só
    /// por sorte, e esse ruído de ambiente acaba maior que a diferença real de qualidade entre
    /// genomas — a seleção por torneio e até o elitismo passam a escolher baseado em sorte, não
    /// em mérito, o que parece (e na prática é) estagnação da evolução.
    /// </summary>
    public int EpisodiosPorAvaliacao { get; init; } = 3;
}
