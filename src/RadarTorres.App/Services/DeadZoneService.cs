using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="IDeadZoneService"/>. Deliberadamente livre de qualquer
/// referência a WPF/XAML, mesmo princípio de <see cref="TowerSelectionService"/> — pura lógica
/// de negócio, testável isoladamente. Carrega as zonas salvas uma única vez na construção e
/// grava o arquivo inteiro a cada mudança (poucas zonas, poucas mudanças — I/O irrelevante).
/// </summary>
public sealed class DeadZoneService : IDeadZoneService
{
    private readonly IDeadZoneRepository _repository;
    private readonly ILoggingService _logger;
    private int _nextId;

    public ObservableCollection<DeadZone> Zones { get; } = new();

    public DeadZoneService(IDeadZoneRepository repository, ILoggingService logger)
    {
        _repository = repository;
        _logger = logger;

        List<DeadZone> saved = _repository.Load();
        foreach (DeadZone zone in saved)
        {
            Zones.Add(zone);
        }
        _nextId = saved.Count > 0 ? saved.Max(z => z.Id) + 1 : 1;
    }

    public DeadZone AddQuadrantZone(string name, Quadrant quadrant)
    {
        var zone = new DeadZone
        {
            Id = _nextId++,
            Name = name,
            Type = DeadZoneType.Quadrant,
            Quadrant = quadrant
        };
        return AddAndPersist(zone);
    }

    public DeadZone AddDistanceRangeZone(string name, double minDistance, double maxDistance)
    {
        var zone = new DeadZone
        {
            Id = _nextId++,
            Name = name,
            Type = DeadZoneType.DistanceRange,
            MinDistance = minDistance,
            MaxDistance = maxDistance
        };
        return AddAndPersist(zone);
    }

    private DeadZone AddAndPersist(DeadZone zone)
    {
        Zones.Add(zone);
        Persist();
        _logger.Info($"Zona morta criada: \"{zone.Name}\" ({zone.Description})");
        return zone;
    }

    public void SetEnabled(DeadZone zone, bool enabled)
    {
        if (zone.Enabled == enabled) return;

        zone.Enabled = enabled;
        Persist();
        _logger.Info($"Zona morta \"{zone.Name}\" {(enabled ? "ativada" : "desativada")}");
    }

    public void Remove(DeadZone zone)
    {
        if (!Zones.Remove(zone)) return;

        Persist();
        _logger.Info($"Zona morta \"{zone.Name}\" removida");
    }

    public DeadZone? FindBlockingZone(Target target)
    {
        foreach (DeadZone zone in Zones)
        {
            if (!zone.Enabled) continue;

            bool blocked = zone.Type == DeadZoneType.Quadrant
                ? zone.Quadrant == target.Quadrant
                : target.Distance >= zone.MinDistance && target.Distance <= zone.MaxDistance;

            if (blocked) return zone;
        }

        return null;
    }

    private void Persist() => _repository.Save(Zones.ToList());
}
