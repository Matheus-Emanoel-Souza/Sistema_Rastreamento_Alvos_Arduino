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
        _ => true
    };

    public bool PodeExecutarAcoes(PerfilUsuario perfil) => perfil != PerfilUsuario.Visualizador;

    public bool PodeGerenciarUsuarios(PerfilUsuario perfil) => perfil == PerfilUsuario.Administrador;
}
