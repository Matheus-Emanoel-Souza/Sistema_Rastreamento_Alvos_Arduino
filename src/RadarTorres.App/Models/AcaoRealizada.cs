using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Registro de auditoria de uma ação de acionamento demonstrativo (ex.: "Torre 1 —
/// Acionamento demonstrativo"). Gravado por <see cref="Services.FireControlService"/>
/// a cada tentativa (autorizada e executada, bloqueada ou com erro) — nunca editável ou
/// removível por usuários comuns (ver <see cref="Repositories.IAcaoRealizadaRepository"/>,
/// que propositalmente não expõe Update/Delete).
/// </summary>
public class AcaoRealizada
{
    public int Id { get; set; }

    /// <summary>Torre + ação combinadas num único campo (ex.: "Torre 1 — Acionamento demonstrativo").</summary>
    public string TorreAcao { get; set; } = string.Empty;

    /// <summary>
    /// Referência ao <see cref="ObjetoDetectado"/> que originou esta ação — é a chave para as
    /// coordenadas do alvo (X/Y/Z), que não são duplicadas aqui; quem precisa da posição
    /// consulta <see cref="Repositories.IObjetoDetectadoRepository"/> por este Id (ver
    /// <see cref="ViewModels.AcoesRealizadasViewModel"/>, que já resolve isso para exibição).
    /// <c>null</c> se o alvo não tinha objeto detectado persistido no momento da ação.
    /// </summary>
    public int? ObjetoDetectadoId { get; set; }

    public DateTime DataHora { get; set; }

    /// <summary>Login do usuário que originou a ordem, quando manual. <c>null</c> se automática.</summary>
    public string? UsuarioResponsavel { get; set; }

    public OrigemAcao Origem { get; set; }

    public ResultadoAcao Resultado { get; set; }

    /// <summary>Observação livre ou mensagem de erro (ex.: motivo do bloqueio de segurança).</summary>
    public string? Observacao { get; set; }
}
