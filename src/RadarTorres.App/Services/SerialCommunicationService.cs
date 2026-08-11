using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação real da comunicação serial com o Arduino, usando <see cref="System.IO.Ports.SerialPort"/>.
/// </summary>
/// <remarks>
/// <para><b>Como a concorrência é resolvida aqui</b> (ver também Docs/COMUNICACAO_ARDUINO.md):</para>
/// <list type="bullet">
/// <item>A abertura da porta (<see cref="SerialPort.Open"/>) é bloqueante, então é executada dentro de
/// <see cref="Task.Run(Action)"/> a partir de <see cref="ConnectAsync"/>, que é <c>async</c> e nunca
/// bloqueia a thread de UI que a chamou.</item>
/// <item>A leitura contínua roda em uma <b>Task de fundo dedicada</b> (<see cref="_readLoopTask"/>) que faz
/// leitura linha-a-linha com um <see cref="SerialPort.ReadTimeout"/> curto; o timeout é usado apenas
/// para permitir checar periodicamente o <see cref="CancellationToken"/> e encerrar o laço de forma limpa
/// — nunca para tratar timeout como erro de protocolo.</item>
/// <item>Um <see cref="System.Threading.Timer"/> (watchdog) verifica periodicamente se algum byte foi
/// recebido dentro da janela configurada (<c>ConnectionWatchdogMs</c>); se não, a conexão é considerada
/// perdida e o evento <see cref="ConnectionStateChanged"/> é dessa forma disparado mesmo sem exceção alguma
/// do SerialPort (útil para desconexões "silenciosas", ex.: cabo USB removido sem o SO notificar a tempo).</item>
/// <item>Todos os eventos públicos são disparados na thread de fundo de leitura/monitoramento. Os
/// consumidores (ex.: <see cref="TargetTrackingService"/>, <see cref="LoggingService"/>) são responsáveis
/// por despachar para a thread de UI quando necessário — cada um já faz isso internamente, então a
/// ViewModel nunca precisa lidar com threads diretamente.</item>
/// <item>Envio de comandos (<see cref="SendCommandAsync"/>) é serializado com um <see cref="SemaphoreSlim"/>
/// para que duas chamadas concorrentes nunca escrevam bytes intercalados na porta.</item>
/// </list>
/// </remarks>
public sealed class SerialCommunicationService : ISerialCommunicationService
{
    private readonly ILoggingService _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    private SerialPort? _serialPort;
    private CancellationTokenSource? _readLoopCts;
    private Task? _readLoopTask;
    private Timer? _watchdogTimer;
    private DateTime _lastByteReceivedAt;
    private int _watchdogMs = 3000;

    private ConnectionState _state = ConnectionState.Disconnected;

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

    public SerialCommunicationService(ILoggingService logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> GetAvailablePorts()
    {
        try
        {
            var ports = SerialPort.GetPortNames();
            Array.Sort(ports, StringComparer.OrdinalIgnoreCase);
            return ports;
        }
        catch (Exception ex)
        {
            RaiseError($"Falha ao listar portas COM: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    public async Task<bool> ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken = default)
    {
        if (State is ConnectionState.Connected or ConnectionState.Connecting)
        {
            Disconnect();
        }

        State = ConnectionState.Connecting;
        _watchdogMs = Configuration.AppConfig.Current.SerialSettings.ConnectionWatchdogMs;

        try
        {
            bool opened = await Task.Run(() =>
            {
                var port = new SerialPort(portName, baudRate)
                {
                    NewLine = "\n",
                    ReadTimeout = 250,
                    WriteTimeout = Configuration.AppConfig.Current.SerialSettings.WriteTimeoutMs,
                    Encoding = Encoding.ASCII,
                    DtrEnable = true,
                    RtsEnable = true
                };

                port.Open();
                _serialPort = port;
                return true;
            }, cancellationToken).ConfigureAwait(false);

            if (!opened || _serialPort is null)
            {
                State = ConnectionState.Error;
                return false;
            }

            CurrentPortName = portName;
            CurrentBaudRate = baudRate;
            _lastByteReceivedAt = DateTime.Now;

            _readLoopCts = new CancellationTokenSource();
            _readLoopTask = Task.Run(() => ReadLoop(_readLoopCts.Token));

            _watchdogTimer = new Timer(WatchdogTick, null, _watchdogMs, _watchdogMs);

            State = ConnectionState.Connected;
            _logger.Success($"Arduino conectado em {portName} @ {baudRate} bps");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            State = ConnectionState.Error;
            RaiseError($"Porta {portName} está ocupada por outro programa.");
            return false;
        }
        catch (System.IO.FileNotFoundException)
        {
            State = ConnectionState.Error;
            RaiseError($"Porta {portName} não existe (dispositivo desconectado?).");
            return false;
        }
        catch (Exception ex)
        {
            State = ConnectionState.Error;
            RaiseError($"Falha ao conectar em {portName}: {ex.Message}");
            return false;
        }
    }

    public void Disconnect()
    {
        _watchdogTimer?.Dispose();
        _watchdogTimer = null;

        _readLoopCts?.Cancel();
        try
        {
            _readLoopTask?.Wait(500);
        }
        catch
        {
            // A task pode terminar com OperationCanceledException; é esperado durante o Disconnect.
        }

        try
        {
            if (_serialPort is { IsOpen: true })
            {
                _serialPort.Close();
            }
        }
        catch (Exception ex)
        {
            RaiseError($"Erro ao fechar porta: {ex.Message}");
        }
        finally
        {
            _serialPort?.Dispose();
            _serialPort = null;
        }

        if (State != ConnectionState.Disconnected)
        {
            State = ConnectionState.Disconnected;
            _logger.Info("Conexão serial encerrada.");
        }
    }

    public async Task<bool> SendCommandAsync(string command)
    {
        SerialPort? port = _serialPort;
        if (port is not { IsOpen: true })
        {
            RaiseError("Tentativa de enviar comando sem conexão ativa.");
            return false;
        }

        await _writeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(() => port.WriteLine(command)).ConfigureAwait(false);
            _logger.Info($"Comando enviado: {command}");
            return true;
        }
        catch (Exception ex)
        {
            RaiseError($"Falha ao enviar comando '{command}': {ex.Message}");
            return false;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void ReadLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = _serialPort?.ReadLine();
            }
            catch (TimeoutException)
            {
                // Esperado: nenhuma linha completa dentro do ReadTimeout. Apenas permite checar o token.
                continue;
            }
            catch (System.IO.IOException)
            {
                // Cabo removido / porta fechada abruptamente durante a leitura.
                HandleUnexpectedDisconnect("Porta serial fechada inesperadamente (cabo desconectado?).");
                return;
            }
            catch (InvalidOperationException)
            {
                // A porta foi fechada por outra thread (Disconnect chamado concorrentemente) — encerra silenciosamente.
                return;
            }
            catch (Exception ex)
            {
                RaiseError($"Erro inesperado na leitura serial: {ex.Message}");
                continue;
            }

            if (string.IsNullOrEmpty(line)) continue;

            _lastByteReceivedAt = DateTime.Now;

            // TryParse sempre preenche 'message' (com ErrorMessage/UnknownMessage quando aplicável);
            // o valor de retorno bool só indica "reconhecido e semanticamente válido" e não precisa
            // ser checado aqui, pois todo tipo de mensagem é repassado adiante para quem interessar.
            SerialProtocolParser.TryParse(line, out SerialMessage message);
            MessageReceived?.Invoke(this, message);
        }
    }

    private void WatchdogTick(object? _)
    {
        if (State != ConnectionState.Connected) return;

        double elapsedMs = (DateTime.Now - _lastByteReceivedAt).TotalMilliseconds;
        if (elapsedMs > _watchdogMs)
        {
            HandleUnexpectedDisconnect($"Nenhum dado recebido do Arduino nos últimos {_watchdogMs} ms.");
        }
    }

    private void HandleUnexpectedDisconnect(string reason)
    {
        RaiseError(reason);
        State = ConnectionState.Error;
        Disconnect();
    }

    private void RaiseError(string message)
    {
        _logger.Error(message);
        CommunicationError?.Invoke(this, message);
    }

    public void Dispose()
    {
        Disconnect();
        _writeLock.Dispose();
    }
}
