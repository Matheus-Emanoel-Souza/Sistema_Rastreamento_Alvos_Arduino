using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>Preferências de layout/tema/idioma por usuário (uma linha por <see cref="Usuario.Id"/>).</summary>
public interface IPreferenciasUsuarioRepository
{
    PreferenciasUsuario? GetByUsuarioId(int usuarioId);

    /// <summary>Insere ou substitui as preferências do usuário (upsert).</summary>
    void Salvar(PreferenciasUsuario preferencias);
}
