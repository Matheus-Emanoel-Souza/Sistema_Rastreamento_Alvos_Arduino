using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Contrato da camada de comunicação serial com o Arduino. Definido como interface para permitir
/// substituir a implementação real (<see cref="SerialCommunicationService"/>) por um dublê em
/// testes automatizados, sem depender de hardware físico.
/// </summary>
public interface ISerialCommunicationService : IDisposable
{
    ConnectionState State { get; }
    string? CurrentPortName { get; }
    int CurrentBaudRate { get; }

    /// <summary>Disparado na thread de fundo de leitura sempre que uma mensagem válida (ou de erro) é interpretada.</summary>
    event EventHandler<SerialMessage>? MessageReceived;

    /// <summary>Disparado sempre que o estado da conexão muda (conectando, conectado, perdido, erro...).</summary>
    event EventHandler<ConnectionState>? ConnectionStateChanged;

    /// <summary>Disparado quando ocorre um erro de comunicação (porta ocupada, IO, etc.) que não derruba o programa.</summary>
    event EventHandler<string>? CommunicationError;

    /// <summary>Lista as portas COM atualmente disponíveis no sistema operacional.</summary>
    IReadOnlyList<string> GetAvailablePorts();

    Task<bool> ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken = default);

    void Disconnect();

    Task<bool> SendCommandAsync(string command);
}
