using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Chamado de ajuda/problema aberto pelo usuário através do botão "Ajuda" da barra superior.
/// Usuário e data de envio são preenchidos automaticamente pelo sistema (ver
/// <c>HelpDeskFormViewModel</c>) — nunca digitados manualmente.
/// </summary>
public class ChamadoAjuda
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }

    /// <summary>Nome do usuário no momento do envio, guardado junto para exibição simples em CSV.</summary>
    public string UsuarioNome { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public string Descricao { get; set; } = string.Empty;

    public string Categoria { get; set; } = string.Empty;

    /// <summary>Tela/módulo relacionado (ex.: "Monitoramento"), quando informado.</summary>
    public string? ModuloRelacionado { get; set; }

    /// <summary>Mensagem de erro relatada pelo usuário, quando aplicável.</summary>
    public string? MensagemErro { get; set; }

    public DateTime DataHoraEnvio { get; set; }

    public StatusChamado Status { get; set; } = StatusChamado.Aberto;

    /// <summary>Resposta/observação do administrador ao tratar o chamado.</summary>
    public string? RespostaAdmin { get; set; }

    public DateTime? DataResolucao { get; set; }
}
