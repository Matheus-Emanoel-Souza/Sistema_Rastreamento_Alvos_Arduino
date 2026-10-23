using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>Implementação de <see cref="IPermissionService"/> com as três permissões do projeto.</summary>
public sealed class PermissionService : IPermissionService
{
    public bool PodeVerMenu(PerfilUsuario perfil, MenuItem item) => item switch
    {
        // "Usuários" é exclusivo do Administrador — os demais itens são visíveis para
        // qualquer perfil autenticado (o controle fino de ação fica em PodeExecutarAcoes).
        MenuItem.Usuarios => perfil == PerfilUsuario.Administrador,

        // Ferramenta de pesquisa/engenharia do controlador (neuroevolução), não uma operação do
        // dia a dia — mesmo critério de "Usuários".
        MenuItem.TreinamentoControlador => perfil == PerfilUsuario.Administrador,

        // Painel de análise de longo prazo do treinamento — mesmo critério/motivo do item acima.
        MenuItem.EstatisticasTreinamento => perfil == PerfilUsuario.Administrador,

        _ => true
    };

    public bool PodeExecutarAcoes(PerfilUsuario perfil) => perfil != PerfilUsuario.Visualizador;

    public bool PodeGerenciarUsuarios(PerfilUsuario perfil) => perfil == PerfilUsuario.Administrador;

    public bool PodeGerenciarZonasMortas(PerfilUsuario perfil) => perfil == PerfilUsuario.Administrador;

    public bool PodeGerenciarPerfisAlvo(PerfilUsuario perfil) => perfil != PerfilUsuario.Visualizador;
}
