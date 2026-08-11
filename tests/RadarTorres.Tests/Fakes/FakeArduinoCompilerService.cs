using System;
using System.Threading;
using System.Threading.Tasks;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.Tests.Fakes;

/// <summary>Dublê de <see cref="IArduinoCompilerService"/> — reporta um número configurável de linhas e retorna um resultado fixo, sem invocar nenhum processo real.</summary>
public sealed class FakeArduinoCompilerService : IArduinoCompilerService
{
    public int LinesToReport { get; set; }
    public ArduinoCompileResult ResultToReturn { get; set; } = new() { Status = ArduinoCompileStatus.Success, ExitCode = 0, Duration = TimeSpan.FromSeconds(1) };
    public ArduinoCompileRequest? LastRequest { get; private set; }

    public Task<ArduinoCompileResult> CompileAsync(ArduinoCompileRequest request, IProgress<ArduinoCliOutputLine> output, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        for (int i = 0; i < LinesToReport; i++)
        {
            output.Report(new ArduinoCliOutputLine { Stream = ArduinoCliOutputStream.StdOut, Text = $"linha {i}" });
        }
        return Task.FromResult(ResultToReturn);
    }
}
