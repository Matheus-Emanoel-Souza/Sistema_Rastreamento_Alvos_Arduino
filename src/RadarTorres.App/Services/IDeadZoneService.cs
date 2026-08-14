using System.Collections.ObjectModel;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Gerencia as zonas mortas configuradas (quadrante ou faixa de distância) e decide, para
/// cada alvo, se ele está dentro de alguma zona ativa — consultado por
/// <see cref="ITowerSelectionService"/> (não seleciona torre) e <see cref="IFireControlService"/>
/// (bloqueia acionamento) antes de qualquer outra regra.
/// </summary>
public interface IDeadZoneService
{
    ObservableCollection<DeadZone> Zones { get; }

    /// <summary>Cria e persiste uma nova zona morta por quadrante.</summary>
    DeadZone AddQuadrantZone(string name, Quadrant quadrant);

    /// <summary>Cria e persiste uma nova zona morta por faixa de distância (m) da base.</summary>
    DeadZone AddDistanceRangeZone(string name, double minDistance, double maxDistance);

    /// <summary>Liga/desliga uma zona existente sem removê-la, persistindo a mudança.</summary>
    void SetEnabled(DeadZone zone, bool enabled);

    /// <summary>Remove definitivamente uma zona.</summary>
    void Remove(DeadZone zone);

    /// <summary>
    /// Se o alvo estiver dentro de alguma zona ativa, devolve essa zona (a primeira
    /// encontrada, quando há mais de uma se sobrepondo); caso contrário, <c>null</c>.
    /// </summary>
    DeadZone? FindBlockingZone(Target target);
}
