using System.Collections.ObjectModel;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.Tests.Fakes;

/// <summary>Dublê simples de <see cref="ILoggingService"/> — apenas acumula entradas, sem despachar para nenhuma UI thread.</summary>
public sealed class FakeLoggingService : ILoggingService
{
    public ObservableCollection<LogEntry> Entries { get; } = new();

    public void Info(string message) => Entries.Add(new LogEntry { Level = LogLevel.Info, Message = message });
    public void Success(string message) => Entries.Add(new LogEntry { Level = LogLevel.Success, Message = message });
    public void Warning(string message) => Entries.Add(new LogEntry { Level = LogLevel.Warning, Message = message });
    public void Error(string message) => Entries.Add(new LogEntry { Level = LogLevel.Error, Message = message });
    public void Clear() => Entries.Clear();
}
