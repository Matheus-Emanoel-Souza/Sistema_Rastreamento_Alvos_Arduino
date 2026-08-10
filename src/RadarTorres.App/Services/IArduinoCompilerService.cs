using System;
using System.Threading;
using System.Threading.Tasks;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>Parâmetros de uma compilação, já resolvidos (nunca texto concatenado para shell).</summary>
public sealed class ArduinoCompileRequest
{
    public required string CliExecutablePath { get; init; }

    /// <summary>Caminho do arquivo .ino ou da pasta do sketch — <see cref="ArduinoCompilerService"/> resolve para a pasta.</summary>
    public required string SketchPath { get; init; }

    public required string Fqbn { get; init; }
}

/// <summary>
/// Compila um sketch Arduino via <c>arduino-cli compile</c>, de forma assíncrona e cancelável,
/// repassando a saída (stdout/stderr) em tempo real através de <paramref name="output"/>. Ver
/// <see cref="ArduinoCompilerService"/> para a implementação; não faz gravação/upload de firmware.
/// </summary>
public interface IArduinoCompilerService
{
    Task<ArduinoCompileResult> CompileAsync(ArduinoCompileRequest request, IProgress<ArduinoCliOutputLine> output, CancellationToken cancellationToken = default);
}
