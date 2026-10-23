using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RadarTorres.App.Configuration;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.NeuralEvolution;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementa o algoritmo de seleção de torre descrito no enunciado do TCC:
/// 1) considera apenas torres disponíveis; 2) prioriza torres cujo quadrante de cobertura
/// coincide com o do alvo; 3) dentre as candidatas, escolhe a melhor avaliada; 4) se nenhuma
/// torre do quadrante preferencial estiver disponível, cai para qualquer torre disponível do
/// sistema (para não deixar um alvo sem cobertura).
/// </summary>
/// <remarks>
/// Deliberadamente livre de qualquer referência a WPF/XAML — é pura lógica de negócio,
/// testável isoladamente e reaproveitável caso a interface mude no futuro.
/// A quantidade de torres não é fixa: é lida de <see cref="AppSettings.Towers"/>, então
/// adicionar/remover torres é apenas uma questão de editar o appsettings.json.
///
/// <para>
/// Dentro de cada camada, a escolha entre candidatas é feita por
/// <see cref="ControladorHibrido.Pontuar"/> — a mesma combinação heurística+rede neural
/// treinada por neuroevolução na tela "Treinamento do Controlador" (ver
/// <see cref="IControladorTreinadoProvider"/>) — e cai para a heurística pura (menor distância)
/// se nenhum controlador treinado estiver embutido. Isto decide SÓ qual torre persegue o alvo;
/// o disparo em si continua exclusivamente manual (<see cref="IFireControlService"/>), por
/// decisão de projeto — a rede neural não aciona nada por conta própria.
/// </para>
/// <para>
/// Limitação conhecida: o sistema real não mede a velocidade do alvo (só posição), então a
/// entrada "velocidade do alvo" da rede usa um valor neutro fixo
/// (<see cref="VelocidadeAlvoDesconhecidaMetrosPorSegundo"/>) em vez de uma medição real — é a
/// mesma rede treinada no ambiente de simulação, que tinha essa informação.
/// </para>
/// </remarks>
public sealed class TowerSelectionService : ITowerSelectionService
{
    /// <summary>Ver nota de limitação na documentação da classe.</summary>
    private const double VelocidadeAlvoDesconhecidaMetrosPorSegundo = 1.25;

    /// <summary>Mesmo padrão usado como velocidade máxima no ambiente de treinamento (ver <see cref="ParametrosTreinamento"/>) — só normaliza a entrada acima.</summary>
    private const double VelocidadeMaximaAlvoMetrosPorSegundo = 2.5;

    private readonly ILoggingService _logger;
    private readonly IZonaMortaService _zonaMortaService;
    private readonly IControladorTreinadoProvider _controladorTreinado;

    public ObservableCollection<Tower> Towers { get; } = new();

    public TowerSelectionService(ILoggingService logger, IZonaMortaService zonaMortaService, IControladorTreinadoProvider controladorTreinado)
    {
        _logger = logger;
        _zonaMortaService = zonaMortaService;
        _controladorTreinado = controladorTreinado;
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

    public TowerSelectionResult SelectTowerFor(Target target, IEnumerable<Target> activeTargets)
    {
        ZonaMorta? blockingZone = _zonaMortaService.FindBlockingZone(target);
        if (blockingZone is not null)
        {
            string zoneReason = $"ALVO EM ZONA MORTA ({blockingZone.Name} — {blockingZone.Description}) — NENHUMA TORRE SELECIONADA";
            _logger.Warning($"Alvo #{target.Id:D2}: {zoneReason}");
            target.SelectedTower = null;
            target.DistanceToSelectedTower = 0;
            return new TowerSelectionResult(false, null, 0, zoneReason);
        }

        List<Tower> available = Towers.Where(t => t.IsAvailable && t.State != TowerState.Offline && t.State != TowerState.Unavailable).ToList();

        if (available.Count == 0)
        {
            const string reason = "NENHUMA TORRE DISPONÍVEL";
            _logger.Warning($"Alvo #{target.Id:D2}: {reason}");
            target.SelectedTower = null;
            target.DistanceToSelectedTower = 0;
            return new TowerSelectionResult(false, null, 0, reason);
        }

        List<Target> others = activeTargets.Where(t => t.IsActive && t.Id != target.Id).ToList();

        // Exclusividade: uma torre só aponta para um lugar por vez, então não pode ser escolhida
        // para dois alvos simultaneamente — a torre já comprometida com OUTRO alvo sai das candidatas.
        HashSet<int> committed = others
            .Where(t => t.SelectedTower is not null)
            .Select(t => t.SelectedTower!.Id)
            .ToHashSet();

        List<Tower> free = available.Where(t => !committed.Contains(t.Id)).ToList();

        // Quadrantes com pelo menos um alvo ativo que NÃO seja este — usado para saber se a torre
        // "de casa" de um quadrante vizinho está livre para ajudar (não precisa ficar na própria área).
        HashSet<Quadrant> quadrantsWithOtherTargets = others.Select(t => t.Quadrant).ToHashSet();

        // Camada 1: torre(s) do próprio quadrante do alvo, livres.
        List<Tower> home = free.Where(t => t.PreferredQuadrant == target.Quadrant).ToList();

        // Camada 2: torres de quadrante adjacente, livres, cujo próprio quadrante não tem alvo
        // concorrente agora (só ajudam quem não precisa delas em casa).
        List<Tower> adjacent = home.Count > 0
            ? []
            : free.Where(t =>
                QuadrantHelper.AreAdjacent(t.PreferredQuadrant, target.Quadrant) &&
                !quadrantsWithOtherTargets.Contains(t.PreferredQuadrant)).ToList();

        // Camada 3: qualquer torre livre restante (inclusive diagonal) — a que sobrar cobre.
        List<Tower> coverage = home.Count == 0 && adjacent.Count == 0 ? free : [];

        (List<Tower> candidates, string layerLabel) = home.Count > 0
            ? (home, "torre do próprio quadrante")
            : adjacent.Count > 0
                ? (adjacent, $"torre adjacente cobrindo {QuadrantHelper.ToDisplayLabel(target.Quadrant)}")
                : coverage.Count > 0
                    ? (coverage, "cobertura — torre sobressalente")
                    : (available, "sobrecarregada — nenhuma torre livre");

        (Tower? best, double bestDistance) = EscolherMelhorCandidata(candidates, target, fatorCamada: home.Count > 0 ? 1.0 : adjacent.Count > 0 ? 0.5 : coverage.Count > 0 ? 0.0 : -1.0);

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
        best.AimAngleDegrees = BearingCalculator.AngleTo(best, target);

        _logger.Info($"{best.Name} selecionada para o alvo #{target.Id:D2} ({layerLabel}, dist={bestDistance:0.00} m, mira={best.AimAngleDegrees:0.0}°)");

        return new TowerSelectionResult(true, best, bestDistance, $"OK ({layerLabel})");
    }

    /// <summary>
    /// Escolhe a melhor torre dentro de UMA camada já definida (própria/adjacente/sobressalente)
    /// — usa a rede treinada quando disponível (<see cref="ControladorHibrido.Pontuar"/>, mesma
    /// combinação heurística+rede da tela de treinamento), senão a heurística pura de menor
    /// distância (comportamento anterior, idêntico ao que a rede produz quando o genoma embutido
    /// é o zerado de fábrica).
    /// </summary>
    private (Tower? Best, double BestDistance) EscolherMelhorCandidata(List<Tower> candidates, Target target, double fatorCamada)
    {
        RedeNeural? rede = _controladorTreinado.Rede;
        double raioMundoMetros = AppConfig.Current.RadarSettings.MaxDetectionDistanceMeters;

        Tower? best = null;
        double bestDistance = double.MaxValue;
        double melhorPontuacao = double.NegativeInfinity;

        foreach (Tower candidate in candidates)
        {
            double distance = DistanceCalculator.Between(target, candidate);
            candidate.DistanceToTarget = distance;

            if (rede is null)
            {
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
                continue;
            }

            double[] entradas = ControladorHibrido.ConstruirEntradas(
                distanciaMetros: distance,
                raioMundoMetros: raioMundoMetros,
                mesmoQuadrante: candidate.PreferredQuadrant == target.Quadrant,
                quadranteAdjacente: QuadrantHelper.AreAdjacent(candidate.PreferredQuadrant, target.Quadrant),
                anguloGraus: BearingCalculator.AngleTo(candidate, target),
                velocidadeMetrosPorSegundo: VelocidadeAlvoDesconhecidaMetrosPorSegundo,
                velocidadeMaximaMetrosPorSegundo: VelocidadeMaximaAlvoMetrosPorSegundo,
                fatorCamada: fatorCamada);

            double pontuacao = ControladorHibrido.Pontuar(rede, entradas, fatorCamada);
            if (pontuacao > melhorPontuacao)
            {
                melhorPontuacao = pontuacao;
                bestDistance = distance;
                best = candidate;
            }
        }

        return (best, bestDistance);
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
