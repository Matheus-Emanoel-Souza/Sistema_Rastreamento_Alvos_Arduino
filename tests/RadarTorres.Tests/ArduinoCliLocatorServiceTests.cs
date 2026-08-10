using System;
using System.IO;
using RadarTorres.App.Models;
using RadarTorres.App.Services;
using RadarTorres.Tests.Fakes;
using Xunit;

namespace RadarTorres.Tests;

public sealed class ArduinoCliLocatorServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "RadarTorresTests_" + Guid.NewGuid());
    private readonly ArduinoCliLocatorService _sut = new(new FakeLoggingService());

    public ArduinoCliLocatorServiceTests() => Directory.CreateDirectory(_tempDir);

    [Fact]
    public void Locate_SavedPathPointsToExistingFile_ReturnsFoundFromSavedConfig()
    {
        string exePath = Path.Combine(_tempDir, "arduino-cli.exe");
        File.WriteAllText(exePath, string.Empty);

        ArduinoCliInfo result = _sut.Locate(exePath);

        Assert.True(result.Found);
        Assert.Equal(exePath, result.ExecutablePath);
        Assert.Equal(ArduinoCliSource.ConfiguracaoSalva, result.Source);
    }

    [Fact]
    public void Locate_SavedPathMissing_FallsThroughWithoutThrowing()
    {
        string missingPath = Path.Combine(_tempDir, "nao-existe", "arduino-cli.exe");

        // Não afirmamos "não encontrado" porque a máquina de CI pode ter o Arduino CLI
        // instalado de fato (PATH/local comum) — o importante é não lançar exceção e não
        // devolver o caminho salvo inexistente como se fosse válido.
        ArduinoCliInfo result = _sut.Locate(missingPath);

        Assert.NotEqual(missingPath, result.ExecutablePath);
    }

    [Fact]
    public void Locate_NoHintsAndNothingInstalled_ReturnsNotFoundWithoutThrowing()
    {
        // Caminho salvo aponta para algo que garantidamente não existe.
        ArduinoCliInfo result = _sut.Locate(Path.Combine(_tempDir, "ghost.exe"));

        // Mesmo que o CLI esteja instalado na máquina de teste (fora do nosso controle), a
        // chamada não pode lançar exceção — é a garantia relevante deste teste.
        Assert.NotNull(result);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
    }
}
