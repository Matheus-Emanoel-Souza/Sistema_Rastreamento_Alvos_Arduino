using System.Collections.Generic;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>Implementação em CSV (<c>objetos_detectados.csv</c>) de <see cref="IObjetoDetectadoRepository"/>.</summary>
public sealed class CsvObjetoDetectadoRepository : IObjetoDetectadoRepository
{
    private readonly CsvTableStore<ObjetoDetectado> _store;

    public CsvObjetoDetectadoRepository()
    {
        _store = new CsvTableStore<ObjetoDetectado>(AppDataPaths.GetCsvPath("objetos_detectados"),
        [
            new CsvColumn<ObjetoDetectado>("Id", o => o.Id.ToString(), (o, v) => o.Id = CsvConvert.ToInt(v)),
            new CsvColumn<ObjetoDetectado>("Tipo", o => o.Tipo, (o, v) => o.Tipo = v),
            new CsvColumn<ObjetoDetectado>("X", o => CsvConvert.From(o.X), (o, v) => o.X = CsvConvert.ToDouble(v)),
            new CsvColumn<ObjetoDetectado>("Y", o => CsvConvert.From(o.Y), (o, v) => o.Y = CsvConvert.ToDouble(v)),
            new CsvColumn<ObjetoDetectado>("Z", o => CsvConvert.From(o.Z), (o, v) => o.Z = CsvConvert.ToNullableDouble(v)),
            new CsvColumn<ObjetoDetectado>("Quadrante", o => o.Quadrante, (o, v) => o.Quadrante = v),
            new CsvColumn<ObjetoDetectado>("DataHora", o => CsvConvert.From(o.DataHora), (o, v) => o.DataHora = CsvConvert.ToDateTime(v)),
            new CsvColumn<ObjetoDetectado>("Dispositivo", o => o.Dispositivo, (o, v) => o.Dispositivo = v),
            new CsvColumn<ObjetoDetectado>("NivelConfianca", o => CsvConvert.From(o.NivelConfianca), (o, v) => o.NivelConfianca = CsvConvert.ToNullableDouble(v)),
            new CsvColumn<ObjetoDetectado>("Observacao", o => o.Observacao ?? "", (o, v) => o.Observacao = v),
            new CsvColumn<ObjetoDetectado>("ReferenciaImagem", o => o.ReferenciaImagem ?? "", (o, v) => o.ReferenciaImagem = v),
        ]);
    }

    public IReadOnlyList<ObjetoDetectado> GetAll() => _store.ReadAll();

    public ObjetoDetectado Add(ObjetoDetectado objeto)
    {
        objeto.Id = _store.GetNextId(o => o.Id);
        _store.Append(objeto);
        return objeto;
    }
}
