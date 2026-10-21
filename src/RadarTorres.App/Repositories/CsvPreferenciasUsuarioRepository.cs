using System.Collections.Generic;
using System.Linq;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>Implementação em CSV (<c>preferencias_usuario.csv</c>) de <see cref="IPreferenciasUsuarioRepository"/>.</summary>
public sealed class CsvPreferenciasUsuarioRepository : IPreferenciasUsuarioRepository
{
    private readonly CsvTableStore<PreferenciasUsuario> _store;

    public CsvPreferenciasUsuarioRepository()
    {
        _store = new CsvTableStore<PreferenciasUsuario>(AppDataPaths.GetCsvPath("preferencias_usuario"),
        [
            new CsvColumn<PreferenciasUsuario>("UsuarioId", p => p.UsuarioId.ToString(), (p, v) => p.UsuarioId = CsvConvert.ToInt(v)),
            new CsvColumn<PreferenciasUsuario>("Idioma", p => p.Idioma, (p, v) => p.Idioma = v),
            new CsvColumn<PreferenciasUsuario>("Tema", p => CsvConvert.From(p.Tema), (p, v) => p.Tema = CsvConvert.ToEnum(v, TemaPreferido.Escuro)),
            new CsvColumn<PreferenciasUsuario>("SidebarRecolhida", p => CsvConvert.From(p.SidebarRecolhida), (p, v) => p.SidebarRecolhida = CsvConvert.ToBool(v)),
            new CsvColumn<PreferenciasUsuario>("TelaInicial", p => p.TelaInicial ?? "", (p, v) => p.TelaInicial = string.IsNullOrEmpty(v) ? null : v),
            new CsvColumn<PreferenciasUsuario>("RegistrosPorPagina", p => p.RegistrosPorPagina.ToString(), (p, v) => p.RegistrosPorPagina = CsvConvert.ToInt(v)),
        ]);
    }

    public PreferenciasUsuario? GetByUsuarioId(int usuarioId) =>
        _store.ReadAll().FirstOrDefault(p => p.UsuarioId == usuarioId);

    public void Salvar(PreferenciasUsuario preferencias)
    {
        List<PreferenciasUsuario> all = _store.ReadAll();
        int index = all.FindIndex(p => p.UsuarioId == preferencias.UsuarioId);
        if (index >= 0) all[index] = preferencias;
        else all.Add(preferencias);
        _store.WriteAll(all);
    }
}
