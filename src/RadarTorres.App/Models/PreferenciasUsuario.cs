namespace RadarTorres.App.Models;

/// <summary>Tema de interface preferido pelo usuário.</summary>
public enum TemaPreferido
{
    Claro,
    Escuro,

    /// <summary>Acompanha a configuração de tema claro/escuro do Windows.</summary>
    Sistema
}

/// <summary>
/// Preferências de interface de um usuário, salvas individualmente e restauradas
/// automaticamente no próximo login (Requisito "Personalização do layout"). Campos de
/// personalização mais granular (ordem dos cartões, colunas por tabela) ficam para a Etapa 2
/// deste trabalho; esta classe já reserva a chave primária (<see cref="UsuarioId"/>) e a
/// estrutura para receber essas colunas sem quebrar o CSV existente.
/// </summary>
public class PreferenciasUsuario
{
    /// <summary>Chave estrangeira para <see cref="Usuario.Id"/> — uma linha por usuário.</summary>
    public int UsuarioId { get; set; }

    /// <summary>Código de idioma (ex.: "pt-BR", "en-US") — ver <see cref="Services.ILocalizationService"/>.</summary>
    public string Idioma { get; set; } = "pt-BR";

    public TemaPreferido Tema { get; set; } = TemaPreferido.Escuro;

    public bool SidebarRecolhida { get; set; }

    /// <summary>Chave da tela inicial preferida (ex.: "PainelPrincipal"). Vazia = padrão do sistema.</summary>
    public string? TelaInicial { get; set; }

    /// <summary>Quantidade de registros por página nas telas com paginação (Etapa 2).</summary>
    public int RegistrosPorPagina { get; set; } = 25;

    /// <summary>Valores padrão de fábrica — usados por "Restaurar layout padrão".</summary>
    public static PreferenciasUsuario PadraoPara(int usuarioId) => new()
    {
        UsuarioId = usuarioId,
        Idioma = "pt-BR",
        Tema = TemaPreferido.Escuro,
        SidebarRecolhida = false,
        TelaInicial = null,
        RegistrosPorPagina = 25
    };
}
