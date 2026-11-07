using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Um resultado real (não simulado/estimado) de uma bateria de treino+validação comparando
/// versões do <c>ControladorHibrido</c> (ver <see cref="RedeNeural"/>) — métricas táticas
/// (abates, escapes, tiros errados, tempo de resposta, trocas de alvo, cooperação entre torres),
/// não só fitness bruto, porque o fitness de versões diferentes pode não estar na mesma escala
/// (ver <see cref="RedeNeuralId"/> e a observação de comparabilidade em cada <see cref="RedeNeural.Observacoes"/>).
/// Cada linha corresponde a UMA repetição de UMA variante dentro de UMA bateria — várias linhas
/// com a mesma <see cref="Variante"/>/<see cref="Populacao"/>/<see cref="Geracoes"/> e
/// <see cref="Repeticao"/> diferente formam a bateria inteira.
/// </summary>
public sealed class ExperimentoRedeNeural
{
    public int Id { get; set; }

    /// <summary>Qual <see cref="RedeNeural.Id"/> esta linha mede.</summary>
    public int RedeNeuralId { get; set; }

    /// <summary>
    /// Rótulo da variante tal como gravado pela ferramenta de origem (ex.: "V1-referencia", "V2",
    /// "V2-recorrente") — preservado verbatim para rastrear até a fonte, mesmo que o mapeamento
    /// para <see cref="RedeNeuralId"/> use outra numeração de versão.
    /// </summary>
    public string Variante { get; set; } = string.Empty;

    public int Repeticao { get; set; }
    public int Populacao { get; set; }
    public int Geracoes { get; set; }
    public int TetoComplexidade { get; set; }
    public bool RedeRecorrente { get; set; }
    public bool FitnessV2 { get; set; }

    public double FitnessTreinoPico { get; set; }
    public int GeracaoDoPico { get; set; }
    public double SegundosTreino { get; set; }

    /// <summary>Fitness médio em validação, em episódios/seeds NUNCA vistos no treino.</summary>
    public double ValFitnessMedio { get; set; }
    public double ValAlvosNascidosMedio { get; set; }
    public double ValAbatidosMedio { get; set; }
    public double ValEscapadosMedio { get; set; }
    public double ValTirosErradosMedio { get; set; }
    public double ValTempoRespostaMedioSegundos { get; set; }
    public double ValTrocasPenalizadasMedia { get; set; }
    public double ValCooperacoesConcedidasMedia { get; set; }
    public double ValCooperacoesNegadasMedia { get; set; }

    /// <summary>
    /// De onde este número real veio (ex.: caminho do JSON de origem) — nunca um valor inventado;
    /// se a proveniência não puder ser rastreada, a linha não deveria existir.
    /// </summary>
    public string FonteDados { get; set; } = string.Empty;

    /// <summary>
    /// Quando esta linha foi IMPORTADA/gravada no banco — não é a data em que o treino rodou de
    /// fato (essa não é gravada pela ferramenta de origem); não confundir as duas.
    /// </summary>
    public DateTime DataRegistro { get; set; }
}
