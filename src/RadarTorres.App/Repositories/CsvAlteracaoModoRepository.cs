using System.Collections.Generic;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>Implementação em CSV (<c>alteracoes_modo.csv</c>) de <see cref="IAlteracaoModoRepository"/>.</summary>
public sealed class CsvAlteracaoModoRepository : IAlteracaoModoRepository
{
    private readonly CsvTableStore<AlteracaoModo> _store;

    public CsvAlteracaoModoRepository()
    {
        _store = new CsvTableStore<AlteracaoModo>(AppDataPaths.GetCsvPath("alteracoes_modo"),
        [
            new CsvColumn<AlteracaoModo>("Id", a => a.Id.ToString(), (a, v) => a.Id = CsvConvert.ToInt(v)),
            new CsvColumn<AlteracaoModo>("ModoAnterior", a => a.ModoAnterior, (a, v) => a.ModoAnterior = v),
            new CsvColumn<AlteracaoModo>("NovoModo", a => a.NovoModo, (a, v) => a.NovoModo = v),
            new CsvColumn<AlteracaoModo>("DataHoraSolicitacao", a => CsvConvert.From(a.DataHoraSolicitacao), (a, v) => a.DataHoraSolicitacao = CsvConvert.ToDateTime(v)),
            new CsvColumn<AlteracaoModo>("UsuarioSolicitante", a => a.UsuarioSolicitante, (a, v) => a.UsuarioSolicitante = v),
            new CsvColumn<AlteracaoModo>("DataHoraExecucao", a => CsvConvert.From(a.DataHoraExecucao), (a, v) => a.DataHoraExecucao = CsvConvert.ToNullableDateTime(v)),
            new CsvColumn<AlteracaoModo>("Resultado", a => CsvConvert.From(a.Resultado), (a, v) => a.Resultado = CsvConvert.ToEnum(v, ResultadoAlteracaoModo.Erro)),
            new CsvColumn<AlteracaoModo>("Observacao", a => a.Observacao ?? "", (a, v) => a.Observacao = v),
        ]);
    }

    public IReadOnlyList<AlteracaoModo> GetAll() => _store.ReadAll();

    public AlteracaoModo Add(AlteracaoModo alteracao)
    {
        alteracao.Id = _store.GetNextId(a => a.Id);
        _store.Append(alteracao);
        return alteracao;
    }
}
