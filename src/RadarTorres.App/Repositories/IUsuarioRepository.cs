using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Acesso a contas de usuário. Implementação atual: <see cref="CsvUsuarioRepository"/>
/// (ver TODO(SQL) em <see cref="Data.AppDataPaths"/>).
/// </summary>
public interface IUsuarioRepository
{
    IReadOnlyList<Usuario> GetAll();
    Usuario? GetByLogin(string login);
    Usuario? GetById(int id);
    Usuario Add(Usuario usuario);
    void Update(Usuario usuario);
}
