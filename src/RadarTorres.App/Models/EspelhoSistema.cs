using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Uma fotografia do sistema inteiro num instante — o "espelho" gravado de tempos em tempos
/// (ver <c>EspelhoSistemaService</c>, padrão de 30 s).
/// </summary>
/// <remarks>
/// Não substitui nenhuma tabela: todas continuam gravando seus eventos como sempre. O espelho
/// responde a pergunta que as tabelas de evento não respondem sozinhas — "como estava o sistema
/// às 14:37?" — sem reconstruir o estado somando o histórico inteiro a cada consulta. Por isso as
/// colunas são agregados (contagens por estado e quadrante) e não cópias de linha.
/// </remarks>
public sealed class EspelhoSistema
{
    public int Id { get; set; }

    /// <summary>Instante da amostra.</summary>
    public DateTime DataHora { get; set; }

    // ------------------------------------------------------------------ Operacional

    /// <summary>Último <c>ID_MODO</c> de TABMOD no instante da amostra; NULL se nenhum modo foi registrado ainda.</summary>
    public int? ModoId { get; set; }

    /// <summary>Nome do modo vigente, repetido aqui para ler o espelho sem join com TABMOD.</summary>
    public string? ModoAtual { get; set; }

    public int AlvosDetectados { get; set; }
    public int AlvosAbatePendente { get; set; }
    public int AlvosAbatidos { get; set; }

    public int AlvosQ1 { get; set; }
    public int AlvosQ2 { get; set; }
    public int AlvosQ3 { get; set; }
    public int AlvosQ4 { get; set; }

    public int CamerasCadastradas { get; set; }

    /// <summary>Sessões de usuário sem <c>DataHoraFim</c> — quem estava logado no instante da amostra.</summary>
    public int SessoesAbertas { get; set; }

    /// <summary>Acionamentos (TABACA) ocorridos ENTRE o espelho anterior e este.</summary>
    public int AcoesNoIntervalo { get; set; }

    /// <summary>Mudanças para grau "Critico" (TABCLA) ocorridas entre o espelho anterior e este.</summary>
    public int ClassificacoesCriticasNoIntervalo { get; set; }

    // ------------------------------------------------------------------ Treinamento

    /// <summary>Execução de treinamento em andamento no instante da amostra; NULL quando não havia nenhuma.</summary>
    public string? ExecucaoId { get; set; }

    public int? GeracaoAtual { get; set; }
    public double? MelhorFitness { get; set; }
    public double? FitnessMedio { get; set; }
}
