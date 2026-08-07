using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RadarTorres.App.Configuration;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementa o algoritmo de seleção de torre descrito no enunciado do TCC:
/// 1) considera apenas torres disponíveis; 2) prioriza torres cujo quadrante de cobertura
/// coincide com o do alvo; 3) dentre as candidatas, escolhe a de menor distância Euclidiana
/// até o alvo; 4) se nenhuma torre do quadrante preferencial estiver disponível, cai para
/// qualquer torre disponível do sistema (para não deixar um alvo sem cobertura).
/// </summary>
/// <remarks>
/// Deliberadamente livre de qualquer referência a WPF/XAML — é pura lógica de negócio,
/// testável isoladamente e reaproveitável caso a interface mude no futuro.
/// A quantidade de torres não é fixa: é lida de <see cref="AppSettings.Towers"/>, então
/// adicionar/remover torres é apenas uma questão de editar o appsettings.json.
/// </remarks>
public sealed class TowerSelectionService : ITowerSelectionService
{
    private readonly ILoggingService _logger;

    public ObservableCollection<Tower> Towers { get; } = new();

    public TowerSelectionService(ILoggingService logger)
    {
        _logger = logger;
        LoadTowersFromConfig();
    }

    private void LoadTowersFromConfig()
    {
        Towers.Clear();
        foreach (TowerDefinition def in AppConfig.Current.Towers)
        {
            var tower = new Tower
            {
                Id = def.Id,
                Name = def.Name,
                X = def.X,
                Y = def.Y,
                PreferredQuadrant = QuadrantHelper.Determine(def.X, def.Y)
            };
            Towers.Add(tower);
        }
    }

    public TowerSelectionResult SelectTowerFor(Target target)
    {
        List<Tower> available = Towers.Where(t => t.IsAvailable && t.State != TowerState.Offline && t.State != TowerState.Unavailable).ToList();

        if (available.Count == 0)
        {
            const string reason = "NENHUMA TORRE DISPONÍVEL";
            _logger.Warning($"Alvo #{target.Id:D2}: {reason}");
            target.SelectedTower = null;
            target.DistanceToSelectedTower = 0;
            return new TowerSelectionResult(false, null, 0, reason);
        }

        // Passo 1: prioriza torres do mesmo quadrante do alvo.
        List<Tower> preferred = available.Where(t => t.PreferredQuadrant == target.Quadrant).ToList();
        List<Tower> candidates = preferred.Count > 0 ? preferred : available;

        // Passo 2: dentre as candidatas, calcula a distância Euclidiana e escolhe a menor.
        Tower? best = null;
        double bestDistance = double.MaxValue;
        foreach (Tower candidate in candidates)
        {
            double distance = DistanceCalculator.Between(target, candidate);
            candidate.DistanceToTarget = distance;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        if (best is null)
        {
            const string reason = "NENHUMA TORRE ADEQUADA ENCONTRADA";
            _logger.Warning($"Alvo #{target.Id:D2}: {reason}");
            target.SelectedTower = null;
            target.DistanceToSelectedTower = 0;
            return new TowerSelectionResult(false, null, 0, reason);
        }

        target.SelectedTower = best;
        target.DistanceToSelectedTower = bestDistance;

        string usedFallback = preferred.Count > 0 ? "quadrante compatível" : "fallback (fora do quadrante preferencial)";
        _logger.Info($"{best.Name} selecionada para o alvo #{target.Id:D2} ({usedFallback}, dist={bestDistance:0.00} m)");

        return new TowerSelectionResult(true, best, bestDistance, "OK");
    }

    public void RecomputeTowerStates(IEnumerable<Target> activeTargets)
    {
        var selectedTowerIds = activeTargets
            .Where(t => t.IsActive && t.SelectedTower is not null)
            .Select(t => t.SelectedTower!.Id)
            .ToHashSet();

        foreach (Tower tower in Towers)
        {
            if (tower.State is TowerState.Unavailable or TowerState.Offline or TowerState.Firing)
            {
                continue; // Estados administrativos ou de acionamento em andamento não são sobrescritos aqui.
            }

            tower.State = selectedTowerIds.Contains(tower.Id) ? TowerState.Selected : TowerState.Idle;
        }
    }
}
