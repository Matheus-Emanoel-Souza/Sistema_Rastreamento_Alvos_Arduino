using System;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Amostra periódica do estado do sistema para a tabela de espelhos (TABESP).
/// </summary>
public interface IEspelhoSistemaService
{
    /// <summary>Intervalo entre amostras.</summary>
    TimeSpan Intervalo { get; }

    /// <summary>Se o relógio está rodando.</summary>
    bool EmExecucao { get; }

    /// <summary>Último espelho gravado nesta execução do aplicativo; NULL antes da primeira amostra.</summary>
    EspelhoSistema? UltimoEspelho { get; }

    /// <summary>Disparado a cada espelho gravado — permite uma tela acompanhar sem consultar o banco.</summary>
    event EventHandler<EspelhoSistema>? EspelhoCapturado;

    /// <summary>Começa a amostrar (grava um espelho imediatamente e depois a cada <see cref="Intervalo"/>).</summary>
    void Iniciar();

    /// <summary>Para de amostrar. Idempotente.</summary>
    void Parar();

    /// <summary>Grava um espelho agora, fora do relógio (fechamento do aplicativo, botão de tela).</summary>
    EspelhoSistema? CapturarAgora();
}
