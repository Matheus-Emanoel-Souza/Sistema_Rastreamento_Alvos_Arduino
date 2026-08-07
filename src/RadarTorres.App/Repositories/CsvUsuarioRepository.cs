using System.Collections.Generic;
using System.Linq;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>Implementação em CSV (<c>usuarios.csv</c>) de <see cref="IUsuarioRepository"/>.</summary>
public sealed class CsvUsuarioRepository : IUsuarioRepository
{
    private readonly CsvTableStore<Usuario> _store;

    public CsvUsuarioRepository()
    {
        _store = new CsvTableStore<Usuario>(AppDataPaths.GetCsvPath("usuarios"),
        [
            new CsvColumn<Usuario>("Id", u => u.Id.ToString(), (u, v) => u.Id = CsvConvert.ToInt(v)),
            new CsvColumn<Usuario>("Nome", u => u.Nome, (u, v) => u.Nome = v),
            new CsvColumn<Usuario>("Login", u => u.Login, (u, v) => u.Login = v),
            new CsvColumn<Usuario>("SenhaHash", u => u.SenhaHash, (u, v) => u.SenhaHash = v),
            new CsvColumn<Usuario>("SenhaSalt", u => u.SenhaSalt, (u, v) => u.SenhaSalt = v),
            new CsvColumn<Usuario>("Perfil", u => CsvConvert.From(u.Perfil), (u, v) => u.Perfil = CsvConvert.ToEnum(v, PerfilUsuario.Visualizador)),
            new CsvColumn<Usuario>("Ativo", u => CsvConvert.From(u.Ativo), (u, v) => u.Ativo = CsvConvert.ToBool(v)),
            new CsvColumn<Usuario>("DataCriacao", u => CsvConvert.From(u.DataCriacao), (u, v) => u.DataCriacao = CsvConvert.ToDateTime(v)),
            new CsvColumn<Usuario>("UltimoAcesso", u => CsvConvert.From(u.UltimoAcesso), (u, v) => u.UltimoAcesso = CsvConvert.ToNullableDateTime(v)),
        ]);
    }

    public IReadOnlyList<Usuario> GetAll() => _store.ReadAll();

    public Usuario? GetByLogin(string login) =>
        _store.ReadAll().FirstOrDefault(u => string.Equals(u.Login, login, System.StringComparison.OrdinalIgnoreCase));

    public Usuario? GetById(int id) => _store.ReadAll().FirstOrDefault(u => u.Id == id);

    public Usuario Add(Usuario usuario)
    {
        usuario.Id = _store.GetNextId(u => u.Id);
        _store.Append(usuario);
        return usuario;
    }

    public void Update(Usuario usuario)
    {
        List<Usuario> all = _store.ReadAll();
        int index = all.FindIndex(u => u.Id == usuario.Id);
        if (index < 0) return;

        all[index] = usuario;
        _store.WriteAll(all);
    }
}
