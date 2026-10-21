using System;
using System.Collections.ObjectModel;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

public interface ITargetTrackingService
{
    /// <summary>Coleção observável de alvos ativos, pronta para binding no radar e nas listas da UI.</summary>
    ObservableCollection<Target> Targets { get; }

    /// <summary>Processa uma leitura (do Arduino ou do simulador), criando ou atualizando o alvo correspondente.</summary>
    void ProcessReading(SensorReading reading);

    /// <summary>Remove (marca como inativos e descarta) alvos sem atualização há mais que o timeout configurado.</summary>
    void PurgeStaleTargets();

    /// <summary>Remove todos os alvos imediatamente — usado pelo botão "Limpar Radar".</summary>
    void ClearAll();

    event EventHandler<Target>? TargetCreated;
    event EventHandler<Target>? TargetUpdated;
    event EventHandler<Target>? TargetRemoved;
}
