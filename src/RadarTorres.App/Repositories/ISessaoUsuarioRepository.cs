using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Persistência das sessões de uso do sistema (tabela <c>sessoes_usuario</c>) — ver
/// <see cref="SessaoUsuario"/>.
/// </summary>
public interface ISessaoUsuarioRepository
{
    /// <summary>Abre uma sessão nova para <paramref name="usuarioId"/> e devolve o código gerado (<see cref="SessaoUsuario.Id"/>).</summary>
    string Abrir(int usuarioId);

    /// <summary>Marca <see cref="SessaoUsuario.DataHoraFim"/> da sessão. Sem efeito se já estava encerrada ou não existe.</summary>
    void Encerrar(string sessaoId);

    SessaoUsuario? GetById(string sessaoId);
}
