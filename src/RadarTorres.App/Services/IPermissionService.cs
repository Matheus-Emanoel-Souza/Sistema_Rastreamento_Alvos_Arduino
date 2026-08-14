using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>Itens de navegação da barra lateral (Requisito 2).</summary>
public enum MenuItem
{
    PainelPrincipal,
    Monitoramento,
    ObjetosDetectados,
    AcoesRealizadas,
    HistoricoModos,
    Usuarios,
    ChamadosAjuda,
    Configuracoes,

    /// <summary>Aba "Configurações do Arduino" — ambiente/compilação/monitor serial (ver ArduinoSettingsView).</summary>
    ConfiguracoesArduino
}

/// <summary>
/// Regras de controle de acesso por perfil (Requisito 7/11). Centralizadas aqui para não
/// espalhar checagens de perfil pelo código — sidebar, telas e comandos consultam este
/// serviço em vez de comparar <see cref="PerfilUsuario"/> diretamente.
/// </summary>
public interface IPermissionService
{
    /// <summary>Se o item deve aparecer na barra lateral para o perfil informado.</summary>
    bool PodeVerMenu(PerfilUsuario perfil, MenuItem item);

    /// <summary>
    /// Se o perfil pode executar ações que alteram o estado do sistema (acionamento manual,
    /// troca de modo etc.). Visualizador é somente-consulta.
    /// </summary>
    bool PodeExecutarAcoes(PerfilUsuario perfil);

    /// <summary>Se o perfil pode criar/editar/inativar outros usuários.</summary>
    bool PodeGerenciarUsuarios(PerfilUsuario perfil);

    /// <summary>Se o perfil pode criar/ativar/desativar/remover zonas mortas. Demais perfis
    /// continuam vendo a lista (transparência de onde o sistema não vai atirar), só não editam.</summary>
    bool PodeGerenciarZonasMortas(PerfilUsuario perfil);
}
