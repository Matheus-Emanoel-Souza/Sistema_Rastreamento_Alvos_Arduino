using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Registro de auditoria de uma troca de <see cref="SystemMode"/> confirmada (tentativas
/// canceladas na confirmação só geram log, não linha aqui — sem uma coluna de resultado para
/// distingui-las, ver <c>Docs/Documentos_Entregaveis/Banco_de_Dados/schema.sql</c>). Gravado a
/// partir do setter de <c>MainViewModel.CurrentMode</c>. Espelha a tabela acadêmica
/// <c>modo_atual_torre</c> (ID_MODO, MODOATUAL, MODOANTERIOR, ID_USER, DHALTERACAO).
/// </summary>
public class ModoAtualTorre
{
    public int Id { get; set; }

    /// <summary>Nome de exibição do modo anterior (ex.: "Manual").</summary>
    public string ModoAnterior { get; set; } = string.Empty;

    /// <summary>Nome de exibição do novo modo (ex.: "Automático").</summary>
    public string NovoModo { get; set; } = string.Empty;

    /// <summary>Id do usuário que confirmou a troca (ver <see cref="Usuario.Id"/>). <c>null</c>
    /// se não houver usuário autenticado no momento (não deveria ocorrer em uso normal).</summary>
    public int? UsuarioId { get; set; }

    public DateTime DataHora { get; set; }
}
