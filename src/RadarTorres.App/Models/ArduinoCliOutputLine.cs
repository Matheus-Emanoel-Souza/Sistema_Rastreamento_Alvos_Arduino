using System;

namespace RadarTorres.App.Models;

/// <summary>Origem de uma linha exibida no console de compilação.</summary>
public enum ArduinoCliOutputStream
{
    /// <summary>Mensagens geradas pelo próprio RadarTorres (início, sketch, FQBN, conclusão...), não pelo processo do CLI.</summary>
    Info,

    /// <summary>Linha recebida do stdout do processo <c>arduino-cli</c>.</summary>
    StdOut,

    /// <summary>Linha recebida do stderr do processo <c>arduino-cli</c> — não é necessariamente um erro (ver Compile).</summary>
    StdErr
}

/// <summary>
/// Uma linha do console de compilação em tempo real. Mesma ideia de <see cref="LogEntry"/>
/// (timestamp + severidade), mas modelada separadamente porque representa a saída bruta de
/// um processo externo, não eventos internos do RadarTorres.
/// </summary>
public sealed class ArduinoCliOutputLine
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public ArduinoCliOutputStream Stream { get; init; } = ArduinoCliOutputStream.Info;
    public required string Text { get; init; }

    public override string ToString() => $"[{Timestamp:HH:mm:ss}] {Text}";
}
