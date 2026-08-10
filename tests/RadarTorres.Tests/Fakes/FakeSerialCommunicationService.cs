using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.Tests.Fakes;

/// <summary>
/// Dublê de <see cref="ISerialCommunicationService"/> para testar a disputa pela porta serial
/// e o fluxo do monitor sem depender de hardware real. Permite disparar os eventos
/// publicamente para simular mensagens/erros vindos da "porta".
/// </summary>
public sealed class FakeSerialCommunicationService : ISerialCommunicationService
{
    private ConnectionState _state = ConnectionState.Disconnected;

    public int ConnectCallCount { get; private set; }
    public int DisconnectCallCount { get; private set; }
    public IReadOnlyList<string> PortsToReturn { get; set; } = Array.Empty<string>();

    public ConnectionState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            ConnectionStateChanged?.Invoke(this, value);
        }
    }

    public string? CurrentPortName { get; private set; }
    public int CurrentBaudRate { get; private set; }

    public event EventHandler<SerialMessage>? MessageReceived;
    public event EventHandler<ConnectionState>? ConnectionStateChanged;
    public event EventHandler<string>? CommunicationError;

    public IReadOnlyList<string> GetAvailablePorts() => PortsToReturn;

    public Task<bool> ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken = default)
    {
        ConnectCallCount++;
        CurrentPortName = portName;
        CurrentBaudRate = baudRate;
        State = ConnectionState.Connected;
        return Task.FromResult(true);
    }

    public void Disconnect()
    {
        DisconnectCallCount++;
        CurrentPortName = null;
        State = ConnectionState.Disconnected;
    }

    public Task<bool> SendCommandAsync(string command) => Task.FromResult(true);

    public void RaiseMessageReceived(SerialMessage message) => MessageReceived?.Invoke(this, message);

    public void RaiseCommunicationError(string message) => CommunicationError?.Invoke(this, message);

    public void Dispose()
    {
    }
}
