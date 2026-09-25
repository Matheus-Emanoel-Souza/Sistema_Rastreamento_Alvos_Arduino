using System.Collections.Generic;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>Implementação em CSV (<c>modo_atual_torre.csv</c>) de <see cref="IModoAtualTorreRepository"/>.</summary>
public sealed class CsvModoAtualTorreRepository : IModoAtualTorreRepository
{
    private readonly CsvTableStore<ModoAtualTorre> _store;

    public CsvModoAtualTorreRepository()
    {
        _store = new CsvTableStore<ModoAtualTorre>(AppDataPaths.GetCsvPath("modo_atual_torre"),
        [
            new CsvColumn<ModoAtualTorre>("Id", a => a.Id.ToString(), (a, v) => a.Id = CsvConvert.ToInt(v)),
            new CsvColumn<ModoAtualTorre>("ModoAnterior", a => a.ModoAnterior, (a, v) => a.ModoAnterior = v),
            new CsvColumn<ModoAtualTorre>("NovoModo", a => a.NovoModo, (a, v) => a.NovoModo = v),
            new CsvColumn<ModoAtualTorre>("DataHoraSolicitacao", a => CsvConvert.From(a.DataHoraSolicitacao), (a, v) => a.DataHoraSolicitacao = CsvConvert.ToDateTime(v)),
            new CsvColumn<ModoAtualTorre>("DataHoraExecucao", a => CsvConvert.From(a.DataHoraExecucao), (a, v) => a.DataHoraExecucao = CsvConvert.ToNullableDateTime(v)),
            new CsvColumn<ModoAtualTorre>("Resultado", a => CsvConvert.From(a.Resultado), (a, v) => a.Resultado = CsvConvert.ToEnum(v, ResultadoModoAtualTorre.Erro)),
            new CsvColumn<ModoAtualTorre>("Observacao", a => a.Observacao ?? "", (a, v) => a.Observacao = v),
        ]);
    }

    public IReadOnlyList<ModoAtualTorre> GetAll() => _store.ReadAll();

    public ModoAtualTorre Add(ModoAtualTorre alteracao)
    {
        alteracao.Id = _store.GetNextId(a => a.Id);
        _store.Append(alteracao);
        return alteracao;
    }
}
