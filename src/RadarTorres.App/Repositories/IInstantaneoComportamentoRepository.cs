using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Histórico comparativo do comportamento do melhor genoma a cada 1.000 gerações (ver
/// <see cref="InstantaneoComportamentoMelhorGene"/>). Propositalmente expõe apenas
/// <see cref="Registrar"/> e <see cref="ObterTodos"/> — sem Update/Delete nem limite de linhas
/// (diferente de <see cref="IHistoricoTreinamentoRepository"/>, que descarta as mais antigas): é
/// exatamente a consulta posterior, marco a marco, sem ninguém sobrescrever nada, que permite
/// verificar se houve evolução real.
/// </summary>
public interface IInstantaneoComportamentoRepository
{
    void Registrar(InstantaneoComportamentoMelhorGene instantaneo);

    /// <summary>Todos os marcos já registrados, em ordem cronológica (geração crescente).</summary>
    IReadOnlyList<InstantaneoComportamentoMelhorGene> ObterTodos();
}
