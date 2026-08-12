using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação real de <see cref="IArduinoCompilerService"/>. Executa
/// <c>arduino-cli compile --fqbn &lt;fqbn&gt; &lt;pasta-do-sketch&gt;</c> como processo filho, com
/// argumentos passados via <see cref="ProcessStartInfo.ArgumentList"/> (nunca concatenados em
/// uma linha de comando de shell) e captura assíncrona de stdout/stderr enquanto o processo
/// roda, para alimentar o console de compilação em tempo real sem bloquear a UI.
/// </summary>
/// <remarks>
/// O sucesso/falha é decidido exclusivamente pelo código de saída do processo (e por
/// cancelamento explícito do usuário) — texto em stderr é repassado ao console como
/// possível aviso/erro do próprio <c>arduino-cli</c>, mas nunca usado sozinho para marcar a
/// compilação como falha (o Arduino CLI escreve avisos de compilador em stderr mesmo em
/// builds bem-sucedidos).
/// </remarks>
public sealed class ArduinoCompilerService : IArduinoCompilerService
{
    public async Task<ArduinoCompileResult> CompileAsync(ArduinoCompileRequest request, IProgress<ArduinoCliOutputLine> output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(output);

        string sketchFolder = ResolveSketchFolder(request.SketchPath);

        void Report(ArduinoCliOutputStream stream, string text) =>
            output.Report(new ArduinoCliOutputLine { Stream = stream, Text = text });

        Report(ArduinoCliOutputStream.Info, "Iniciando compilação...");
        Report(ArduinoCliOutputStream.Info, $"Sketch: {sketchFolder}");
        Report(ArduinoCliOutputStream.Info, $"Placa/FQBN: {request.Fqbn}");

        ProcessStartInfo psi = BuildCompileProcessStartInfo(request.CliExecutablePath, request.Fqbn, sketchFolder);

        ArduinoCompileResult result = await ExecuteAsync(psi, output, cancellationToken).ConfigureAwait(false);

        Report(ArduinoCliOutputStream.Info, result.Status switch
        {
            ArduinoCompileStatus.Success => $"Código de saída: {result.ExitCode}. Compilação concluída com sucesso em {FormatDuration(result.Duration)}.",
            ArduinoCompileStatus.Cancelled => $"Compilação cancelada pelo usuário após {FormatDuration(result.Duration)}.",
            _ => $"Código de saída: {result.ExitCode?.ToString() ?? "?"}. Compilação falhou após {FormatDuration(result.Duration)}.",
        });

        return result;
    }

    /// <summary>Monta os argumentos de forma segura (<see cref="ProcessStartInfo.ArgumentList"/> — nunca concatenação de string para shell). Extraído para permitir teste unitário isolado da execução do processo.</summary>
    internal static ProcessStartInfo BuildCompileProcessStartInfo(string cliExecutablePath, string fqbn, string sketchFolder)
    {
        var psi = new ProcessStartInfo
        {
            FileName = cliExecutablePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Directory.Exists(sketchFolder) ? sketchFolder : null,
        };
        psi.ArgumentList.Add("compile");
        psi.ArgumentList.Add("--fqbn");
        psi.ArgumentList.Add(fqbn);
        psi.ArgumentList.Add(sketchFolder);
        return psi;
    }

    /// <summary>
    /// Executa um <see cref="ProcessStartInfo"/> já pronto, capturando stdout/stderr em tempo
    /// real e respeitando cancelamento (mata a árvore de processos). Separado de
    /// <see cref="CompileAsync"/> para permitir testar a interpretação de código de
    /// saída/cancelamento com um processo qualquer, sem depender do Arduino CLI instalado.
    /// </summary>
    internal static async Task<ArduinoCompileResult> ExecuteAsync(ProcessStartInfo psi, IProgress<ArduinoCliOutputLine> output, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        void Report(ArduinoCliOutputStream stream, string text) =>
            output.Report(new ArduinoCliOutputLine { Stream = stream, Text = text });

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) Report(ArduinoCliOutputStream.StdOut, e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) Report(ArduinoCliOutputStream.StdErr, e.Data);
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            Report(ArduinoCliOutputStream.Info, $"Falha ao iniciar o Arduino CLI: {ex.Message}");
            stopwatch.Stop();
            return new ArduinoCompileResult { Status = ArduinoCompileStatus.Failed, ExitCode = null, Duration = stopwatch.Elapsed };
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        bool cancelled = false;
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            Report(ArduinoCliOutputStream.Info, "Cancelamento solicitado — encerrando o processo do Arduino CLI...");
            TryKillProcessTree(process);

            // Aguarda a finalização real do processo (sem CancellationToken, já que o
            // cancelamento é o próprio motivo de estarmos aqui) para não deixar um processo
            // órfão preso ao sketch/porta.
            try
            {
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Já foi finalizado à força; nada mais a fazer.
            }
        }

        stopwatch.Stop();

        if (cancelled)
        {
            return new ArduinoCompileResult { Status = ArduinoCompileStatus.Cancelled, ExitCode = null, Duration = stopwatch.Elapsed };
        }

        return new ArduinoCompileResult
        {
            Status = DetermineStatus(process.ExitCode),
            ExitCode = process.ExitCode,
            Duration = stopwatch.Elapsed,
        };
    }

    /// <summary>
    /// Interpretação do código de saída: 0 = sucesso, qualquer outro valor = falha. Não há
    /// nenhuma inspeção de texto de stderr aqui — de propósito, ver comentário de classe.
    /// </summary>
    internal static ArduinoCompileStatus DetermineStatus(int exitCode) =>
        exitCode == 0 ? ArduinoCompileStatus.Success : ArduinoCompileStatus.Failed;

    /// <summary>O Arduino CLI espera a pasta do sketch, não o arquivo .ino isolado.</summary>
    internal static string ResolveSketchFolder(string sketchPath)
    {
        if (string.IsNullOrWhiteSpace(sketchPath))
        {
            throw new ArgumentException("Caminho do sketch não informado.", nameof(sketchPath));
        }

        if (File.Exists(sketchPath) && sketchPath.EndsWith(".ino", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetDirectoryName(Path.GetFullPath(sketchPath))!;
        }

        return Path.GetFullPath(sketchPath);
    }

    private static void TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // O processo pode já ter terminado entre a checagem e o Kill — condição de corrida
            // inofensiva, sem ação adicional necessária.
        }
    }

    private static string FormatDuration(TimeSpan elapsed) =>
        elapsed.TotalSeconds < 60 ? $"{elapsed.TotalSeconds:0.0}s" : $"{(int)elapsed.TotalMinutes}min {elapsed.Seconds}s";
}
