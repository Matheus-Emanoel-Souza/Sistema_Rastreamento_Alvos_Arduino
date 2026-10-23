using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>Itens de navegação da barra lateral (Requisito 2).</summary>
public enum MenuItem
{
    PainelPrincipal,
    Monitoramento,

    /// <summary>Aba "Desempenho" — métricas operacionais em tempo de execução (ver DesempenhoView).</summary>
    Desempenho,
    ObjetosDetectados,
    AcoesRealizadas,
    HistoricoModos,
    Usuarios,

    /// <summary>Módulo "Câmeras" — visualização em tempo real das webcams conectadas (ver CamerasView).</summary>
    Cameras,

    /// <summary>Aba "Configurações do Arduino" — ambiente/compilação/monitor serial (ver ArduinoSettingsView).</summary>
    ConfiguracoesArduino,

    /// <summary>Tela "Perfis de Alvo" — cor/formato/tamanho que a detecção deve travar (ver PerfilAlvoView).</summary>
    PerfilAlvo,

    /// <summary>Tela "Configurações" — preferências desta máquina: janela, idioma, tema, comportamento (ver ConfiguracoesView).</summary>
    Configuracoes,

    /// <summary>Tela "Treinamento do Controlador" — neuroevolução do controlador híbrido heurística+rede neural (ver TreinamentoControladorView).</summary>
    TreinamentoControlador,

    /// <summary>Tela "Estatísticas de Treinamento" — exclusiva do Administrador: evolução do fitness médio a cada 1.000 gerações (ver EstatisticasTreinamentoView).</summary>
    EstatisticasTreinamento
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

    /// <summary>Se o perfil pode criar/selecionar/remover perfis de alvo (cor/formato/tamanho
    /// a travar). Mesmo critério de <see cref="PodeExecutarAcoes"/> — é uma decisão
    /// operacional do dia a dia, não uma configuração administrativa restrita (diferente de
    /// <see cref="PodeGerenciarZonasMortas"/>/<see cref="PodeGerenciarUsuarios"/>).</summary>
    bool PodeGerenciarPerfisAlvo(PerfilUsuario perfil);
}
