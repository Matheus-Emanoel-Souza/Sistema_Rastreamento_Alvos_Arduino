using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Marcos de fitness a cada 1.000 gerações de treinamento (ver <see cref="MarcoFitnessTreinamento"/>).
/// Sem limite de linhas — diferente de <see cref="IHistoricoTreinamentoRepository"/>, que é um
/// buffer circular das últimas 300 gerações.
/// </summary>
public interface IMarcoFitnessTreinamentoRepository
{
    void Registrar(MarcoFitnessTreinamento marco);

    /// <summary>Todos os marcos já registrados, em ordem cronológica (geração crescente).</summary>
    IReadOnlyList<MarcoFitnessTreinamento> ObterTodos();
}
