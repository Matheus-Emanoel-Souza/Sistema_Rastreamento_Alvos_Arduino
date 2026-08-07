using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using RadarTorres.App.Configuration;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Mantém o estado dos alvos atualmente monitorados. É a única classe que decide se uma
/// leitura recebida corresponde a um alvo novo ou a uma atualização de um alvo já existente —
/// evitando, conforme pedido no enunciado, criar um novo <see cref="Target"/> a cada pacote
/// recebido quando o mesmo ID continua sendo informado.
/// </summary>
/// <remarks>
/// <b>Concorrência:</b> leituras chegam de threads de fundo distintas (thread de leitura serial
/// ou timer do simulador), potencialmente ao mesmo tempo. Um <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// é usado como fonte da verdade para a busca por ID (thread-safe, sem locks explícitos), e toda
/// mutação da <see cref="Targets"/> (ObservableCollection, que o WPF não permite alterar fora da
/// thread de UI) é despachada via <see cref="Dispatcher"/>. Um <see cref="Timer"/> interno varre
/// periodicamente os alvos em busca de timeout, removendo do radar quem parou de ser detectado.
/// </remarks>
public sealed class TargetTrackingService : ITargetTrackingService, IDisposable
{
    private readonly ConcurrentDictionary<int, Target> _targetsById = new();
    private readonly ILoggingService _logger;
    private readonly Dispatcher _dispatcher;
    private readonly Timer _timeoutTimer;

    public ObservableCollection<Target> Targets { get; } = new();

    public event EventHandler<Target>? TargetCreated;
    public event EventHandler<Target>? TargetUpdated;
    public event EventHandler<Target>? TargetRemoved;

    public TargetTrackingService(ILoggingService logger, Dispatcher? dispatcher = null)
    {
        _logger = logger;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;

        // Varredura de timeout a cada segundo — independe da taxa de chegada de leituras.
        _timeoutTimer = new Timer(_ => PurgeStaleTargets(), null, 1000, 1000);
    }

    public void ProcessReading(SensorReading reading)
    {
        var (x, y) = CoordinateConverter.PolarToCartesian(reading.Angle, reading.Distance);
        Quadrant quadrant = QuadrantHelper.Determine(x, y);
        DateTime now = DateTime.Now;

        RunOnDispatcher(() =>
        {
            if (_targetsById.TryGetValue(reading.TargetId, out Target? existing))
            {
                existing.Angle = reading.Angle;
                existing.Distance = reading.Distance;
                existing.X = x;
                existing.Y = y;
                existing.Quadrant = quadrant;
                existing.LastUpdate = now;
                existing.IsActive = true;

                TargetUpdated?.Invoke(this, existing);
            }
            else
            {
                var target = new Target
                {
                    Id = reading.TargetId,
                    Angle = reading.Angle,
                    Distance = reading.Distance,
                    X = x,
                    Y = y,
                    Quadrant = quadrant,
                    Timestamp = now,
                    LastUpdate = now,
                    IsActive = true
                };

                _targetsById[reading.TargetId] = target;
                Targets.Add(target);

                _logger.Info($"Alvo #{target.Id:D2} detectado");
                _logger.Info($"Posição: X={target.X:0.0} Y={target.Y:0.0}");
                _logger.Info($"Quadrante: {QuadrantHelper.ToDisplayLabel(target.Quadrant)}");

                TargetCreated?.Invoke(this, target);
            }
        });
    }

    public void PurgeStaleTargets()
    {
        int timeoutMs = AppConfig.Current.RadarSettings.TargetTimeoutMs;
        DateTime cutoff = DateTime.Now.AddMilliseconds(-timeoutMs);

        var stale = _targetsById.Values.Where(t => t.LastUpdate < cutoff).ToList();
        if (stale.Count == 0) return;

        RunOnDispatcher(() =>
        {
            foreach (Target target in stale)
            {
                if (!_targetsById.TryRemove(target.Id, out _)) continue;

                target.IsActive = false;
                Targets.Remove(target);
                _logger.Warning($"Alvo #{target.Id:D2} perdido (sem leituras há mais de {timeoutMs} ms)");
                TargetRemoved?.Invoke(this, target);
            }
        });
    }

    public void ClearAll()
    {
        RunOnDispatcher(() =>
        {
            foreach (Target target in _targetsById.Values.ToList())
            {
                TargetRemoved?.Invoke(this, target);
            }
            _targetsById.Clear();
            Targets.Clear();
        });
    }

    private void RunOnDispatcher(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            _dispatcher.Invoke(action);
        }
    }

    public void Dispose() => _timeoutTimer.Dispose();
}
