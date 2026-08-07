using System;

namespace RadarTorres.App.Models;

/// <summary>Nível/severidade de uma entrada no console de eventos.</summary>
public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Uma linha do console de eventos exibido na parte inferior direita da interface.
/// Imutável por design: cada evento do sistema gera uma nova entrada, nunca uma edição.
/// </summary>
public sealed class LogEntry
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public LogLevel Level { get; init; } = LogLevel.Info;
    public required string Message { get; init; }

    /// <summary>Formata como "[HH:mm:ss] mensagem", igual ao exemplo do enunciado do TCC.</summary>
    public override string ToString() => $"[{Timestamp:HH:mm:ss}] {Message}";
}
