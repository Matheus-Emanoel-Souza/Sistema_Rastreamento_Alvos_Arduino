using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Registro das versões/variações da rede neural do <c>ControladorHibrido</c> efetivamente
/// implementadas ou testadas (ver <see cref="RedeNeural"/>) — poucas linhas no total, todas
/// relevantes, sem limite/buffer circular.
/// </summary>
public interface IRedeNeuralRepository
{
    /// <summary>Grava uma versão nova. Não valida duplicidade de <see cref="RedeNeural.Versao"/> — quem chama decide.</summary>
    void Registrar(RedeNeural rede);

    /// <summary>Devolve todas as versões registradas, ordenadas por <see cref="RedeNeural.Id"/> (ordem de registro).</summary>
    IReadOnlyList<RedeNeural> ObterTodos();

    /// <summary>Devolve a versão com este rótulo (ex.: "2", "3", "3b"), ou <c>null</c> se não registrada.</summary>
    RedeNeural? ObterPorVersao(string versao);
}
