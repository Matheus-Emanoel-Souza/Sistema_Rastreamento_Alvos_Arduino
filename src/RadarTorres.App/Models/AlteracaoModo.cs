using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Registro de auditoria de uma solicitação de troca de <see cref="SystemMode"/>, com sucesso
/// ou erro. Gravado a partir de <c>MainViewModel.OnModeChanged</c> — o único ponto do sistema
/// por onde toda troca de modo já passava antes desta funcionalidade existir.
/// </summary>
public class AlteracaoModo
{
    public int Id { get; set; }

    /// <summary>Nome de exibição do modo anterior (ex.: "Manual").</summary>
    public string ModoAnterior { get; set; } = string.Empty;

    /// <summary>Nome de exibição do modo solicitado (ex.: "Automático").</summary>
    public string NovoModo { get; set; } = string.Empty;

    public DateTime DataHoraSolicitacao { get; set; }

    /// <summary>Login do usuário que solicitou a troca.</summary>
    public string UsuarioSolicitante { get; set; } = string.Empty;

    /// <summary>
    /// Momento em que a troca foi efetivamente aplicada. Igual a <see cref="DataHoraSolicitacao"/>
    /// hoje (troca é síncrona), mas mantido separado para acomodar fluxos assíncronos futuros.
    /// </summary>
    public DateTime? DataHoraExecucao { get; set; }

    public ResultadoAlteracaoModo Resultado { get; set; }

    public string? Observacao { get; set; }
}
