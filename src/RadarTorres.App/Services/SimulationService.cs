using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using RadarTorres.App.Configuration;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Gera alvos fictícios que se movimentam de forma pseudo-aleatória, permitindo executar e
/// demonstrar o software inteiro (radar, quadrantes, seleção de torres, acionamento) sem
/// nenhum Arduino conectado. Produz o mesmo DTO (<see cref="SensorReading"/>) que a leitura
/// real da porta serial gera, então o restante do sistema (<see cref="TargetTrackingService"/>
/// em diante) não sabe nem precisa saber se está lidando com hardware real ou simulado.
/// </summary>
/// <remarks>
/// Concorrência: usa <see cref="System.Threading.Timer"/> (thread pool) para gerar leituras em
/// segundo plano e <see cref="Random.Shared"/> (thread-safe desde o .NET 6) para os números
/// pseudo-aleatórios, evitando qualquer necessidade de lock manual.
/// </remarks>
public sealed class SimulationService : ISimulationService, IDisposable
{
    private sealed class SimulatedTarget
    {
        public int Id;
        public double Angle;
        public double Distance;
    }

    private readonly ConcurrentDictionary<int, SimulatedTarget> _simulated = new();
    private readonly ILoggingService _logger;
    private Timer? _timer;
    private int _nextId = 1;

    public bool IsRunning { get; private set; }

    public IReadOnlyCollection<int> ActiveSimulatedTargetIds => _simulated.Keys.ToList();

    public event EventHandler<SensorReading>? ReadingGenerated;

    public SimulationService(ILoggingService logger)
    {
        _logger = logger;
    }

    public void Start(int? initialTargetCount = null)
    {
        if (IsRunning) Stop();

        int count = initialTargetCount ?? AppConfig.Current.SimulationSettings.DefaultTargetCount;
        _simulated.Clear();
        _nextId = 1;

        for (int i = 0; i < count; i++)
        {
            AddRandomTarget();
        }

        int intervalMs = AppConfig.Current.SimulationSettings.GenerationIntervalMs;
        _timer = new Timer(_ => Tick(), null, 0, intervalMs);
        IsRunning = true;

        _logger.Success($"Modo de simulação ativado ({count} alvo(s) fictício(s))");
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        IsRunning = false;
        _logger.Info("Modo de simulação desativado");
    }

    public int AddRandomTarget()
    {
        int max = AppConfig.Current.SimulationSettings.MaxSimultaneousTargets;
        if (_simulated.Count >= max)
        {
            _logger.Warning($"Limite de alvos simulados atingido ({max})");
            return -1;
        }

        double maxDistance = AppConfig.Current.RadarSettings.MaxDetectionDistanceMeters;
        var target = new SimulatedTarget
        {
            Id = _nextId++,
            Angle = Random.Shared.NextDouble() * 360.0,
            Distance = Random.Shared.NextDouble() * maxDistance * 0.8 + 0.5
        };

        _simulated[target.Id] = target;
        EmitReading(target);
        return target.Id;
    }

    public void RemoveTarget(int targetId)
    {
        _simulated.TryRemove(targetId, out _);
    }

    private void Tick()
    {
        double maxDistance = AppConfig.Current.RadarSettings.MaxDetectionDistanceMeters;

        foreach (SimulatedTarget target in _simulated.Values)
        {
            double angleDelta = (Random.Shared.NextDouble() - 0.5) * 12.0; // até ±6° por ciclo
            double distDelta = (Random.Shared.NextDouble() - 0.5) * 0.6;   // até ±0.3 m por ciclo

            target.Angle = NormalizeAngle(target.Angle + angleDelta);
            target.Distance = Math.Clamp(target.Distance + distDelta, 0.3, maxDistance);

            EmitReading(target);
        }
    }

    private void EmitReading(SimulatedTarget target)
    {
        var reading = new SensorReading
        {
            TargetId = target.Id,
            Angle = target.Angle,
            Distance = target.Distance,
            Source = DataSource.Simulation
        };
        ReadingGenerated?.Invoke(this, reading);
    }

    private static double NormalizeAngle(double degrees)
    {
        double normalized = degrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }

    public void Dispose() => _timer?.Dispose();
}
