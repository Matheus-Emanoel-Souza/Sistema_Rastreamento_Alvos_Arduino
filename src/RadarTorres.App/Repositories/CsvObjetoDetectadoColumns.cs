using System.Collections.Generic;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Mapeamento de colunas de <see cref="ObjetoDetectado"/> para CSV, usado exclusivamente pela
/// exportação manual da tela "Objetos Detectados" (<see cref="Services.ObjetoDetectadoExportService.ExportCsv"/>)
/// — uma ação explícita do usuário, não persistência do sistema (que grava só em SQLite, ver
/// <see cref="SqliteObjetoDetectadoRepository"/>).
/// </summary>
public static class CsvObjetoDetectadoColumns
{
    public static IReadOnlyList<CsvColumn<ObjetoDetectado>> BuildColumns() =>
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
    ];
}
