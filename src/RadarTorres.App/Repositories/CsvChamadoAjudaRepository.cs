using System.Collections.Generic;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>Implementação em CSV (<c>chamados_ajuda.csv</c>) de <see cref="IChamadoAjudaRepository"/>.</summary>
public sealed class CsvChamadoAjudaRepository : IChamadoAjudaRepository
{
    private readonly CsvTableStore<ChamadoAjuda> _store;

    public CsvChamadoAjudaRepository()
    {
        _store = new CsvTableStore<ChamadoAjuda>(AppDataPaths.GetCsvPath("chamados_ajuda"),
        [
            new CsvColumn<ChamadoAjuda>("Id", c => c.Id.ToString(), (c, v) => c.Id = CsvConvert.ToInt(v)),
            new CsvColumn<ChamadoAjuda>("UsuarioId", c => c.UsuarioId.ToString(), (c, v) => c.UsuarioId = CsvConvert.ToInt(v)),
            new CsvColumn<ChamadoAjuda>("UsuarioNome", c => c.UsuarioNome, (c, v) => c.UsuarioNome = v),
            new CsvColumn<ChamadoAjuda>("Titulo", c => c.Titulo, (c, v) => c.Titulo = v),
            new CsvColumn<ChamadoAjuda>("Descricao", c => c.Descricao, (c, v) => c.Descricao = v),
            new CsvColumn<ChamadoAjuda>("Categoria", c => c.Categoria, (c, v) => c.Categoria = v),
            new CsvColumn<ChamadoAjuda>("ModuloRelacionado", c => c.ModuloRelacionado ?? "", (c, v) => c.ModuloRelacionado = string.IsNullOrEmpty(v) ? null : v),
            new CsvColumn<ChamadoAjuda>("MensagemErro", c => c.MensagemErro ?? "", (c, v) => c.MensagemErro = string.IsNullOrEmpty(v) ? null : v),
            new CsvColumn<ChamadoAjuda>("DataHoraEnvio", c => CsvConvert.From(c.DataHoraEnvio), (c, v) => c.DataHoraEnvio = CsvConvert.ToDateTime(v)),
            new CsvColumn<ChamadoAjuda>("Status", c => CsvConvert.From(c.Status), (c, v) => c.Status = CsvConvert.ToEnum(v, StatusChamado.Aberto)),
            new CsvColumn<ChamadoAjuda>("RespostaAdmin", c => c.RespostaAdmin ?? "", (c, v) => c.RespostaAdmin = string.IsNullOrEmpty(v) ? null : v),
            new CsvColumn<ChamadoAjuda>("DataResolucao", c => CsvConvert.From(c.DataResolucao), (c, v) => c.DataResolucao = CsvConvert.ToNullableDateTime(v)),
        ]);
    }

    public IReadOnlyList<ChamadoAjuda> GetAll() => _store.ReadAll();

    public ChamadoAjuda Add(ChamadoAjuda chamado)
    {
        chamado.Id = _store.GetNextId(c => c.Id);
        _store.Append(chamado);
        return chamado;
    }

    public void Update(ChamadoAjuda chamado)
    {
        List<ChamadoAjuda> all = _store.ReadAll();
        int index = all.FindIndex(c => c.Id == chamado.Id);
        if (index < 0) return;

        all[index] = chamado;
        _store.WriteAll(all);
    }
}
