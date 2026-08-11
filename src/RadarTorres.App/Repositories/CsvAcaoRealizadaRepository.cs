using System.Collections.Generic;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>Implementação em CSV (<c>acoes_realizadas.csv</c>) de <see cref="IAcaoRealizadaRepository"/>.</summary>
public sealed class CsvAcaoRealizadaRepository : IAcaoRealizadaRepository
{
    private readonly CsvTableStore<AcaoRealizada> _store;

    public CsvAcaoRealizadaRepository()
    {
        _store = new CsvTableStore<AcaoRealizada>(AppDataPaths.GetCsvPath("acoes_realizadas"),
        [
            new CsvColumn<AcaoRealizada>("Id", a => a.Id.ToString(), (a, v) => a.Id = CsvConvert.ToInt(v)),
            new CsvColumn<AcaoRealizada>("Dispositivo", a => a.Dispositivo, (a, v) => a.Dispositivo = v),
            new CsvColumn<AcaoRealizada>("TipoAcao", a => a.TipoAcao, (a, v) => a.TipoAcao = v),
            new CsvColumn<AcaoRealizada>("X", a => CsvConvert.From(a.X), (a, v) => a.X = CsvConvert.ToDouble(v)),
            new CsvColumn<AcaoRealizada>("Y", a => CsvConvert.From(a.Y), (a, v) => a.Y = CsvConvert.ToDouble(v)),
            new CsvColumn<AcaoRealizada>("Z", a => CsvConvert.From(a.Z), (a, v) => a.Z = CsvConvert.ToNullableDouble(v)),
            new CsvColumn<AcaoRealizada>("DataHora", a => CsvConvert.From(a.DataHora), (a, v) => a.DataHora = CsvConvert.ToDateTime(v)),
            new CsvColumn<AcaoRealizada>("UsuarioResponsavel", a => a.UsuarioResponsavel ?? "", (a, v) => a.UsuarioResponsavel = string.IsNullOrEmpty(v) ? null : v),
            new CsvColumn<AcaoRealizada>("Origem", a => CsvConvert.From(a.Origem), (a, v) => a.Origem = CsvConvert.ToEnum(v, OrigemAcao.Manual)),
            new CsvColumn<AcaoRealizada>("Resultado", a => CsvConvert.From(a.Resultado), (a, v) => a.Resultado = CsvConvert.ToEnum(v, ResultadoAcao.Erro)),
            new CsvColumn<AcaoRealizada>("Observacao", a => a.Observacao ?? "", (a, v) => a.Observacao = v),
        ]);
    }

    public IReadOnlyList<AcaoRealizada> GetAll() => _store.ReadAll();

    public AcaoRealizada Add(AcaoRealizada acao)
    {
        acao.Id = _store.GetNextId(a => a.Id);
        _store.Append(acao);
        return acao;
    }
}
