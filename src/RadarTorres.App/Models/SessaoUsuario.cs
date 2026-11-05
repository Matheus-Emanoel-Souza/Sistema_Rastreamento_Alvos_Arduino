using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Uma sessão de uso do sistema: aberta no login (uma por aba — ver
/// <c>App.xaml.cs.OpenNewSession</c>/<c>StartLoginFlow</c>, que permite vários usuários logados
/// simultaneamente em abas diferentes) e encerrada no logout ou ao fechar a aba. É o "código de
/// sessão" referenciado pelas tabelas de auditoria (<c>acoes_realizadas</c>,
/// <c>ocorrencia_estados</c>, <c>modo_atual_torre</c>, <c>classificacoes_alvo</c>) para recuperar,
/// a partir de uma ação auditada, quem estava no sistema e em que janela de tempo.
/// </summary>
public sealed class SessaoUsuario
{
    /// <summary>Código gerado no login (<see cref="Guid.NewGuid"/> em formato "N", mesmo padrão de <see cref="ObjetoDetectado.RastreamentoId"/>).</summary>
    public string Id { get; set; } = string.Empty;

    public int UsuarioId { get; set; }

    public DateTime DataHoraInicio { get; set; }

    /// <summary><c>null</c> enquanto a sessão está aberta (aba/login ainda ativo).</summary>
    public DateTime? DataHoraFim { get; set; }
}
