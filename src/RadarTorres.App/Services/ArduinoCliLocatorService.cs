using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>Implementação real de <see cref="IArduinoCliLocatorService"/> — apenas leitura do sistema de arquivos/PATH e execução do CLI já instalado, nunca download.</summary>
public sealed class ArduinoCliLocatorService : IArduinoCliLocatorService
{
    private const string ExecutableName = "arduino-cli.exe";
    private readonly ILoggingService _logger;

    public ArduinoCliLocatorService(ILoggingService logger)
    {
        _logger = logger;
    }

    public ArduinoCliInfo Locate(string? savedPath)
    {
        // 1) Caminho salvo/informado pelo usuário.
        if (!string.IsNullOrWhiteSpace(savedPath) && IsExistingExecutable(savedPath))
        {
            return Found(savedPath, ArduinoCliSource.ConfiguracaoSalva);
        }

        // 2) Cópia local na pasta do aplicativo (ex.: "tools\arduino-cli.exe" ao lado do .exe).
        string baseDirectory = AppContext.BaseDirectory;
        foreach (string candidate in new[]
        {
            Path.Combine(baseDirectory, ExecutableName),
            Path.Combine(baseDirectory, "tools", ExecutableName),
            Path.Combine(baseDirectory, "arduino-cli", ExecutableName),
        })
        {
            if (IsExistingExecutable(candidate))
            {
                return Found(candidate, ArduinoCliSource.PastaDoAplicativo);
            }
        }

        // 3) Variável de ambiente PATH — sem invocar shell, apenas lendo os diretórios listados.
        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathVariable))
        {
            foreach (string directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate;
                try
                {
                    candidate = Path.Combine(directory.Trim(), ExecutableName);
                }
                catch (ArgumentException)
                {
                    continue; // Entradas malformadas no PATH não podem derrubar a detecção.
                }

                if (IsExistingExecutable(candidate))
                {
                    return Found(candidate, ArduinoCliSource.VariavelPath);
                }
            }
        }

        // 4) Locais comuns de instalação no Windows — só checagem de existência, sem I/O de rede.
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        foreach (string candidate in new[]
        {
            Path.Combine(localAppData, "Programs", "arduino-cli", ExecutableName),
            Path.Combine(localAppData, "Arduino15", ExecutableName),
            Path.Combine(localAppData, "Programs", "Arduino IDE", "resources", "app", "lib", "backend", "resources", ExecutableName),
            Path.Combine(programFiles, "Arduino CLI", ExecutableName),
            Path.Combine(programFiles, "Arduino IDE", "resources", "app", "lib", "backend", "resources", ExecutableName),
            Path.Combine(programFilesX86, "Arduino CLI", ExecutableName),
        })
        {
            if (IsExistingExecutable(candidate))
            {
                return Found(candidate, ArduinoCliSource.LocalComumDeInstalacao);
            }
        }

        return ArduinoCliInfo.NotFound;
    }

    public async Task<string?> GetVersionAsync(string cliPath, CancellationToken cancellationToken = default)
    {
        try
        {
            (int exitCode, string stdOut, _) = await RunCaptureAsync(cliPath, ["version"], cancellationToken).ConfigureAwait(false);
            if (exitCode != 0) return null;

            // Saída típica: "arduino-cli  Version: 0.35.3 Commit: ..." — extrai só o essencial.
            string firstLine = stdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? stdOut.Trim();
            return string.IsNullOrWhiteSpace(firstLine) ? null : firstLine;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Não foi possível determinar a versão do Arduino CLI: {ex.Message}");
            return null;
        }
    }

    public async Task<IReadOnlyList<ArduinoBoardOption>> ListInstalledBoardsAsync(string cliPath, CancellationToken cancellationToken = default)
    {
        try
        {
            (int exitCode, string stdOut, _) = await RunCaptureAsync(cliPath, ["board", "listall"], cancellationToken).ConfigureAwait(false);
            if (exitCode != 0) return Array.Empty<ArduinoBoardOption>();

            var boards = new List<ArduinoBoardOption>();
            foreach (string line in stdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                // Formato de "arduino-cli board listall": "<Nome da placa>   <FQBN>", colunas
                // separadas por múltiplos espaços; o FQBN é sempre o último token da linha.
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("Board Name", StringComparison.OrdinalIgnoreCase)) continue;

                int lastSpace = trimmed.LastIndexOf(' ');
                if (lastSpace <= 0) continue;

                string fqbn = trimmed[(lastSpace + 1)..].Trim();
                string name = trimmed[..lastSpace].Trim();
                if (fqbn.Contains(':') && name.Length > 0)
                {
                    boards.Add(new ArduinoBoardOption(fqbn, name));
                }
            }

            return boards;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Não foi possível listar as placas instaladas do Arduino CLI: {ex.Message}");
            return Array.Empty<ArduinoBoardOption>();
        }
    }

    private static bool IsExistingExecutable(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private static ArduinoCliInfo Found(string path, ArduinoCliSource source) =>
        new() { Found = true, ExecutablePath = path, Source = source };

    /// <summary>Executa um comando curto do CLI (version/board listall) capturando toda a saída — sem passar por cmd/PowerShell.</summary>
    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCaptureAsync(string cliPath, IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = cliPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string arg in arguments) psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        process.Start();

        Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        string stdOut = await stdOutTask.ConfigureAwait(false);
        string stdErr = await stdErrTask.ConfigureAwait(false);

        return (process.ExitCode, stdOut, stdErr);
    }
}
