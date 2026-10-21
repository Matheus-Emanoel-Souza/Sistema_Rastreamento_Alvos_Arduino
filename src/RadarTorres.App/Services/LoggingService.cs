using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

public interface ILoggingService
{
    /// <summary>Coleção observável pronta para binding direto no ListBox/ItemsControl do console de eventos.</summary>
    ObservableCollection<LogEntry> Entries { get; }

    void Info(string message);
    void Success(string message);
    void Warning(string message);
    void Error(string message);
    void Clear();
}

/// <summary>
/// Console de eventos com timestamp, exibido na parte inferior direita da interface.
/// Todas as camadas do sistema (comunicação serial, rastreamento de alvos, seleção de
/// torres, simulador) registram eventos aqui em vez de escrever diretamente na UI —
/// isso mantém a lógica de negócio livre de qualquer referência a componentes visuais.
/// </summary>
/// <remarks>
/// Como eventos podem ser gerados a partir de threads de fundo (leitura serial, timers do
/// simulador), toda escrita em <see cref="Entries"/> é despachada para a thread de UI via
/// <see cref="Dispatcher"/>, evitando a exceção clássica do WPF
/// "The calling thread cannot access this object because a different thread owns it".
/// Um limite de linhas é aplicado para evitar crescimento ilimitado de memória em sessões longas.
/// </remarks>
public sealed class LoggingService : ILoggingService
{
    private const int MaxEntries = 500;
    private readonly Dispatcher _dispatcher;

    public ObservableCollection<LogEntry> Entries { get; } = new();

    public LoggingService(Dispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public void Info(string message) => Add(LogLevel.Info, message);
    public void Success(string message) => Add(LogLevel.Success, message);
    public void Warning(string message) => Add(LogLevel.Warning, message);
    public void Error(string message) => Add(LogLevel.Error, message);

    public void Clear()
    {
        RunOnDispatcher(() => Entries.Clear());
    }

    private void Add(LogLevel level, string message)
    {
        var entry = new LogEntry { Level = level, Message = message };

        RunOnDispatcher(() =>
        {
            Entries.Add(entry);
            while (Entries.Count > MaxEntries)
            {
                Entries.RemoveAt(0);
            }
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
            _dispatcher.BeginInvoke(action);
        }
    }
}
