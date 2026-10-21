using System;

namespace RadarTorres.App.Models;

/// <summary>Como uma compilação terminou — usado para decidir a mensagem final exibida ao usuário.</summary>
public enum ArduinoCompileStatus
{
    Success,
    Failed,
    Cancelled
}

/// <summary>
/// Resultado de uma compilação via <c>arduino-cli compile</c>. A determinação de sucesso/falha
/// usa exclusivamente o código de saída do processo (e o cancelamento explícito do usuário) —
/// nunca a simples presença de texto no stderr, que o Arduino CLI também usa para avisos.
/// </summary>
public sealed class ArduinoCompileResult
{
    public required ArduinoCompileStatus Status { get; init; }

    /// <summary>Código de saída do processo <c>arduino-cli</c>. Nulo quando cancelado antes de o processo iniciar.</summary>
    public int? ExitCode { get; init; }

    public TimeSpan Duration { get; init; }

    public bool Success => Status == ArduinoCompileStatus.Success;
}
