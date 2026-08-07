using System;
using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

public interface ISimulationService
{
    bool IsRunning { get; }

    /// <summary>IDs dos alvos fictícios atualmente sendo movimentados pelo simulador.</summary>
    IReadOnlyCollection<int> ActiveSimulatedTargetIds { get; }

    /// <summary>Disparado a cada leitura fictícia gerada — mesmo formato usado pelas leituras reais do Arduino.</summary>
    event EventHandler<SensorReading>? ReadingGenerated;

    /// <summary>Inicia a geração periódica de leituras fictícias, criando <paramref name="initialTargetCount"/> alvos.</summary>
    void Start(int? initialTargetCount = null);

    /// <summary>Interrompe a geração de novas leituras (alvos existentes seguem visíveis até o timeout natural).</summary>
    void Stop();

    /// <summary>Adiciona um novo alvo fictício com posição inicial aleatória.</summary>
    int AddRandomTarget();

    /// <summary>Para de movimentar/atualizar um alvo fictício específico.</summary>
    void RemoveTarget(int targetId);
}
