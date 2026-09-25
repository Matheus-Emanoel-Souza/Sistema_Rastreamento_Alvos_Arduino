using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="IZonaMortaService"/>. Deliberadamente livre de qualquer
/// referência a WPF/XAML, mesmo princípio de <see cref="TowerSelectionService"/> — pura lógica
/// de negócio, testável isoladamente. Carrega as zonas salvas uma única vez na construção e
/// grava o arquivo inteiro a cada mudança (poucas zonas, poucas mudanças — I/O irrelevante).
/// </summary>
public sealed class ZonaMortaService : IZonaMortaService
{
    private readonly IZonaMortaRepository _repository;
    private readonly ILoggingService _logger;
    private int _nextId;

    public ObservableCollection<ZonaMorta> Zones { get; } = new();

    public ZonaMortaService(IZonaMortaRepository repository, ILoggingService logger)
    {
        _repository = repository;
        _logger = logger;

        List<ZonaMorta> saved = _repository.Load();
        foreach (ZonaMorta zone in saved)
        {
            Zones.Add(zone);
        }
        _nextId = saved.Count > 0 ? saved.Max(z => z.Id) + 1 : 1;
    }

    public ZonaMorta AddQuadrantZone(string name, Quadrant quadrant)
    {
        var zone = new ZonaMorta
        {
            Id = _nextId++,
            Name = name,
            Type = ZonaMortaType.Quadrant,
            Quadrant = quadrant
        };
        return AddAndPersist(zone);
    }

    public ZonaMorta AddDistanceRangeZone(string name, double minDistance, double maxDistance)
    {
        var zone = new ZonaMorta
        {
            Id = _nextId++,
            Name = name,
            Type = ZonaMortaType.DistanceRange,
            MinDistance = minDistance,
            MaxDistance = maxDistance
        };
        return AddAndPersist(zone);
    }

    private ZonaMorta AddAndPersist(ZonaMorta zone)
    {
        Zones.Add(zone);
        Persist();
        _logger.Info($"Zona morta criada: \"{zone.Name}\" ({zone.Description})");
        return zone;
    }

    public void SetEnabled(ZonaMorta zone, bool enabled)
    {
        if (zone.Enabled == enabled) return;

        zone.Enabled = enabled;
        Persist();
        _logger.Info($"Zona morta \"{zone.Name}\" {(enabled ? "ativada" : "desativada")}");
    }

    public void Remove(ZonaMorta zone)
    {
        if (!Zones.Remove(zone)) return;

        Persist();
        _logger.Info($"Zona morta \"{zone.Name}\" removida");
    }

    public ZonaMorta? FindBlockingZone(Target target)
    {
        foreach (ZonaMorta zone in Zones)
        {
            if (!zone.Enabled) continue;

            bool blocked = zone.Type == ZonaMortaType.Quadrant
                ? zone.Quadrant == target.Quadrant
                : target.Distance >= zone.MinDistance && target.Distance <= zone.MaxDistance;

            if (blocked) return zone;
        }

        return null;
    }

    private void Persist() => _repository.Save(Zones.ToList());
}
