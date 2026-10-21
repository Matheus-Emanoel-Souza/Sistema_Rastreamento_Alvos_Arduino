using System.Collections.Generic;
using System.Collections.ObjectModel;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>Resultado de uma tentativa de seleção de torre para um alvo.</summary>
public sealed record TowerSelectionResult(bool Success, Tower? SelectedTower, double Distance, string Reason);

public interface ITowerSelectionService
{
    /// <summary>Torres atualmente cadastradas no sistema (carregadas de appsettings.json).</summary>
    ObservableCollection<Tower> Towers { get; }

    /// <summary>
    /// Executa o algoritmo de seleção de torre (ver Docs/Tecnica/ALGORITMO_SELECAO_TORRE.md) para o
    /// alvo informado, atualiza <see cref="Target.SelectedTower"/> e retorna o resultado da decisão.
    /// </summary>
    /// <param name="target">Alvo para o qual uma torre deve ser escolhida.</param>
    /// <param name="activeTargets">
    /// Todos os alvos ativos no momento (incluindo <paramref name="target"/>) — necessário para saber
    /// quais torres já estão comprometidas com outro alvo e quais quadrantes estão livres, condições
    /// da cooperação entre torres adjacentes.
    /// </param>
    TowerSelectionResult SelectTowerFor(Target target, IEnumerable<Target> activeTargets);

    /// <summary>
    /// Recalcula o <see cref="Tower.State"/> de todas as torres com base no conjunto de alvos
    /// atualmente ativos — deve ser chamado após qualquer rodada de seleção para manter a UI coerente.
    /// </summary>
    void RecomputeTowerStates(IEnumerable<Target> activeTargets);
}
