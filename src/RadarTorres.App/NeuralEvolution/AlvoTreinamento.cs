using RadarTorres.App.Models;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>Alvo simulado dentro de um episódio de treinamento — move em linha reta a partir da borda do mundo em direção à base.</summary>
public sealed class AlvoTreinamento
{
    public int Id { get; init; }
    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>
    /// Altura do alvo acima do solo, em metros (eixo Z). Sempre <c>0</c> quando o eixo Z está
    /// desligado (ver <see cref="ParametrosTreinamento.EixoZHabilitado"/>), o que reproduz
    /// exatamente o comportamento bidimensional anterior.
    /// </summary>
    /// <remarks>
    /// Acompanha o alvo por todo o episódio, igual a <see cref="X"/> e <see cref="Y"/>: é parte do
    /// estado dele, não um atributo calculado na hora de decidir. Constante ao longo da
    /// trajetória — o alvo nasce numa altura e a mantém, porque o que se quer medir aqui é o
    /// efeito da altura sobre a visibilidade, não um modelo de voo.
    /// </remarks>
    public double AlturaMetros { get; set; }
    public double VelocidadeX { get; set; }
    public double VelocidadeY { get; set; }
    public double VelocidadeMetrosPorSegundo { get; init; }

    /// <summary>
    /// Direção do movimento agora, em radianos (mesma convenção de <see cref="System.Math.Cos"/>/
    /// <see cref="System.Math.Sin"/> usada no nascimento) — ponto de partida para a trajetória
    /// ALEATÓRIA deste alvo: a cada passo, <see cref="AmbienteTreinamento.MoverAlvos"/> perturba
    /// este ângulo por uma quantidade sorteada (ver <see cref="ParametrosTreinamento.VariacaoMaximaDirecaoGrausPorSegundo"/>)
    /// e recalcula <see cref="VelocidadeX"/>/<see cref="VelocidadeY"/> a partir dele — a
    /// MAGNITUDE da velocidade (<see cref="VelocidadeMetrosPorSegundo"/>) nunca muda, só a
    /// direção. <see langword="null"/> para alvos de cenário scriptado (ver
    /// <see cref="AmbienteTreinamento.TalvezNascerAlvoScriptado"/>), que continuam em linha reta
    /// de propósito — a trajetória aleatória só se aplica ao sorteio padrão.
    /// </summary>
    public double? AnguloMovimentoRadianos { get; set; }

    /// <summary>
    /// Se este alvo está alto o bastante para ser enxergado por TODAS as torres, e não apenas
    /// pelas do próprio quadrante e adjacentes (ver
    /// <see cref="ParametrosTreinamento.AlturaVisivelPorTodasAsTorresMetros"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolvido uma única vez, no nascimento do alvo, por <see cref="AmbienteTreinamento"/> — é
    /// ele que conhece os parâmetros do episódio, e <see cref="AlturaMetros"/> não muda ao longo
    /// da trajetória. Assim <see cref="ControladorHibrido"/> consulta um booleano já decidido em
    /// vez de precisar receber os parâmetros de altura, o que manteria o contrato dele atado a
    /// uma regra de mundo que não lhe pertence.
    /// </para>
    /// <para>
    /// Sempre <c>false</c> com o eixo Z desligado, que é o que garante o comportamento
    /// bidimensional idêntico ao anterior.
    /// </para>
    /// </remarks>
    public bool VisivelPorTodasAsTorres { get; set; }

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

    /// <summary>
    /// Certeza usada para AUTORIZAR disparo. Duas regras de distância, aplicadas nesta ordem:
    /// <list type="number">
    /// <item>
    /// Alcance de confirmação garantida (<paramref name="distanciaCertezaMaximaMetros"/>, padrão
    /// <see cref="ParametrosTreinamento.DistanciaCertezaMaximaMetros"/> = 1,2 m): a essa distância
    /// ou mais perto, a torre enxerga o alvo bem o bastante para confirmar com 100% de certeza,
    /// não importa quanto tempo ele já está sendo observado — <see cref="Certeza"/> é ignorada.
    /// </item>
    /// <item>
    /// Fora desse alcance, <see cref="Certeza"/> é penalizada quando o alvo está mais perto da
    /// torre do que <paramref name="distanciaMinimaBoaCertezaMetros"/> (ver
    /// <see cref="ParametrosTreinamento.DistanciaMinimaBoaCertezaMetros"/>) — câmera num meio-termo
    /// de distância perde definição e tempo de observação sozinho não deveria compensar isso. A
    /// penalização é proporcional (não um corte abrupto): a 1m de uma mínima de 2m, por exemplo, a
    /// certeza já cai pela metade.
    /// </item>
    /// </list>
    /// As duas regras coexistem de propósito — descrevem câmeras/situações diferentes (confirmação
    /// de perto vs. perda de definição a meia distância) — então HÁ uma descontinuidade proposital
    /// bem no limite de <paramref name="distanciaCertezaMaximaMetros"/>: um passo antes dele a
    /// certeza é 100% garantida; um passo depois, volta a valer só a penalização proporcional
    /// (que pode ser bem menor que 100%). Não é um bug de suavização pendente — é a regra tal como
    /// especificada: "até 1,2 m, certeza 100%", sem transição gradual declarada para além disso.
    /// </summary>
    public double CertezaEfetiva(double distanciaAteTorreMetros, double distanciaMinimaBoaCertezaMetros, double distanciaCertezaMaximaMetros = ParametrosTreinamento.DistanciaCertezaMaximaMetrosPadrao)
    {
        if (distanciaAteTorreMetros <= distanciaCertezaMaximaMetros) return 1.0;

        if (distanciaMinimaBoaCertezaMetros <= 0) return Certeza;

        double fatorDistancia = System.Math.Clamp(distanciaAteTorreMetros / distanciaMinimaBoaCertezaMetros, 0.0, 1.0);
        return Certeza * fatorDistancia;
    }

    /// <summary>Já aplicada a penalidade de "perda de ponto da geração" por ter chegado perto demais da base sem ser abatido nem ter escapado de fato — evita descontar a cada passo enquanto o alvo continua dentro do raio.</summary>
    public bool PenalidadeProximidadeAplicada { get; set; }

    /// <summary>
    /// Já contabilizado no "tempo de resposta" médio do episódio (ver
    /// <see cref="EstadoEpisodio.SomaTemposRespostaSegundos"/>) — marcado no PRIMEIRO disparo
    /// contra este alvo (acerto ou erro, tanto faz: o que importa é quanto tempo levou para a
    /// torre reagir), nunca de novo depois disso.
    /// </summary>
    public bool TempoDeRespostaRegistrado { get; set; }
}
