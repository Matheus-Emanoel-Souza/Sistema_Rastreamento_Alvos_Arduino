using RadarTorres.App.Models;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>Alvo simulado dentro de um episódio de treinamento — move em linha reta a partir da borda do mundo em direção à base.</summary>
public sealed class AlvoTreinamento
{
    public int Id { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double VelocidadeX { get; init; }
    public double VelocidadeY { get; init; }
    public double VelocidadeMetrosPorSegundo { get; init; }

    public Quadrant Quadrante { get; set; }
    public bool Ativo { get; set; } = true;

    public int? TorreSelecionadaId { get; set; }

    /// <summary>Tempo (s) desde que uma torre foi comprometida com este alvo — usado como "tempo de mira" mínimo antes de poder disparar.</summary>
    public double TempoDesdeSelecao { get; set; }

    public bool Abatido { get; set; }

    /// <summary>
    /// Certeza (0 a 1) com que a câmera identificou o alvo no instante do nascimento — ponto de
    /// partida de <see cref="Certeza"/>, que SOBE com o tempo de observação (ver
    /// <see cref="TaxaCrescimentoCertezaPorSegundo"/>). Não é a certeza final: usar sempre
    /// <see cref="Certeza"/> para decidir disparo.
    /// </summary>
    public double CertezaBase { get; init; }

    /// <summary>
    /// Velocidade (fração de certeza por segundo) com que a identificação do alvo fica mais
    /// confiável quanto mais tempo ele é observado — simula a câmera "confirmando" o alvo ao
    /// longo de vários quadros, como no sistema real
    /// (<see cref="Services.IOcorrenciaDeteccaoService.ConfiancaAtualizada"/>). Sorteada no
    /// nascimento (ver <see cref="AmbienteTreinamento"/>) para variar de alvo a alvo.
    /// </summary>
    public double TaxaCrescimentoCertezaPorSegundo { get; init; }

    /// <summary>Tempo (s) desde o nascimento deste alvo — incrementado em <see cref="AmbienteTreinamento"/>, alimenta <see cref="Certeza"/>.</summary>
    public double TempoDeVidaSegundos { get; set; }

    /// <summary>
    /// Certeza ATUAL (0 a 1) de que isto é realmente um alvo — cresce com <see cref="TempoDeVidaSegundos"/>
    /// a partir de <see cref="CertezaBase"/>. Diferente de uma certeza fixa sorteada uma vez (que
    /// deixaria uma fração fixa de alvos permanentemente abaixo do limiar, sem relação nenhuma
    /// com a qualidade do controlador), crescer com o tempo dá à rede uma decisão real para
    /// aprender: esperar a certeza subir custa tempo de reação, então vale a pena só até certo
    /// ponto — ver <see cref="ParametrosTreinamento.LimiarConfiancaDisparoFracao"/>, mesma regra
    /// de disparo do sistema real.
    /// </summary>
    public double Certeza => System.Math.Clamp(CertezaBase + TaxaCrescimentoCertezaPorSegundo * TempoDeVidaSegundos, 0.0, 1.0);

    /// <summary>Já aplicada a penalidade de "perda de ponto da geração" por ter chegado perto demais da base sem ser abatido nem ter escapado de fato — evita descontar a cada passo enquanto o alvo continua dentro do raio.</summary>
    public bool PenalidadeProximidadeAplicada { get; set; }
}
