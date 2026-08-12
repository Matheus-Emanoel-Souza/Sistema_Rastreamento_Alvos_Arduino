using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RadarTorres.App.Models;
using RadarTorres.App.Services;
using Xunit;

namespace RadarTorres.Tests;

public sealed class ArduinoCompilerServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "RadarTorresTests_" + Guid.NewGuid());

    public ArduinoCompilerServiceTests() => Directory.CreateDirectory(_tempDir);

    [Fact]
    public void ResolveSketchFolder_InoFile_ReturnsContainingFolder()
    {
        string sketchFolder = Path.Combine(_tempDir, "MeuSketch");
        Directory.CreateDirectory(sketchFolder);
        string inoPath = Path.Combine(sketchFolder, "MeuSketch.ino");
        File.WriteAllText(inoPath, "// sketch");

        string resolved = ArduinoCompilerService.ResolveSketchFolder(inoPath);

        Assert.Equal(Path.GetFullPath(sketchFolder), resolved);
    }

    [Fact]
    public void ResolveSketchFolder_FolderPath_ReturnsSameFolder()
    {
        string sketchFolder = Path.Combine(_tempDir, "OutroSketch");
        Directory.CreateDirectory(sketchFolder);

        string resolved = ArduinoCompilerService.ResolveSketchFolder(sketchFolder);

        Assert.Equal(Path.GetFullPath(sketchFolder), resolved);
    }

    [Fact]
    public void ResolveSketchFolder_EmptyPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => ArduinoCompilerService.ResolveSketchFolder(""));
    }

    [Fact]
    public void BuildCompileProcessStartInfo_UsesArgumentListNotConcatenatedCommand()
    {
        // Requisito de segurança: os argumentos precisam ir por ArgumentList (cada um como um
        // token isolado), nunca concatenados em uma única string interpretada por um shell —
        // isso é o que impede injeção de comando via caminho de sketch/FQBN maliciosos.
        string sketchFolder = @"C:\algum sketch com espaço";
        string fqbn = "arduino:avr:uno";

        ProcessStartInfo psi = ArduinoCompilerService.BuildCompileProcessStartInfo(@"C:\tools\arduino-cli.exe", fqbn, sketchFolder);

        Assert.False(psi.UseShellExecute);
        Assert.Equal(new List<string> { "compile", "--fqbn", fqbn, sketchFolder }, psi.ArgumentList);
        Assert.True(psi.RedirectStandardOutput);
        Assert.True(psi.RedirectStandardError);
    }

    [Theory]
    [InlineData(0, ArduinoCompileStatus.Success)]
    [InlineData(1, ArduinoCompileStatus.Failed)]
    [InlineData(255, ArduinoCompileStatus.Failed)]
    public void DetermineStatus_UsesExitCodeOnly(int exitCode, ArduinoCompileStatus expected)
    {
        Assert.Equal(expected, ArduinoCompilerService.DetermineStatus(exitCode));
    }

    [Fact]
    public async Task ExecuteAsync_NonZeroExitCode_ReturnsFailedWithExitCode()
    {
        // Usa powershell.exe (presente em qualquer Windows) como um "processo qualquer" para
        // validar a interpretação de código de saída de ponta a ponta, sem depender do
        // Arduino CLI estar instalado na máquina de teste.
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add("Write-Output 'saida'; exit 3");

        var progress = new Progress<ArduinoCliOutputLine>();

        ArduinoCompileResult result = await ArduinoCompilerService.ExecuteAsync(psi, progress, CancellationToken.None);

        Assert.Equal(ArduinoCompileStatus.Failed, result.Status);
        Assert.Equal(3, result.ExitCode);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_KillsProcessAndReturnsCancelled()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add("Start-Sleep -Seconds 30");

        var progress = new Progress<ArduinoCliOutputLine>();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(300));

        var stopwatch = Stopwatch.StartNew();
        ArduinoCompileResult result = await ArduinoCompilerService.ExecuteAsync(psi, progress, cts.Token);
        stopwatch.Stop();

        Assert.Equal(ArduinoCompileStatus.Cancelled, result.Status);
        Assert.Null(result.ExitCode);
        // O processo de 30s deve ter sido morto bem antes do fim natural.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Cancelamento demorou {stopwatch.Elapsed} — processo pode não ter sido finalizado.");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }
}
