using System;
using System.IO;
using RadarTorres.App.Configuration;
using RadarTorres.App.Services;
using Xunit;

namespace RadarTorres.Tests;

public sealed class ArduinoSettingsRepositoryTests : IDisposable
{
    private readonly string _filePath = Path.Combine(Path.GetTempPath(), "RadarTorresTests_" + Guid.NewGuid(), "arduino-settings.json");

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var repo = new ArduinoSettingsRepository(_filePath);

        ArduinoCliSettings settings = repo.Load();

        Assert.Null(settings.CliPath);
        Assert.Equal(9600, settings.BaudRate);
        Assert.True(settings.ConsoleAutoScroll);
        Assert.True(settings.ConsoleShowTimestamps);
    }

    [Fact]
    public void Save_Then_Load_RoundTripsAllFields()
    {
        var repo = new ArduinoSettingsRepository(_filePath);
        var original = new ArduinoCliSettings
        {
            CliPath = @"C:\tools\arduino-cli.exe",
            LastSketchPath = @"C:\sketches\Meu.ino",
            SelectedFqbn = "arduino:avr:uno",
            LastPort = "COM7",
            BaudRate = 115200,
            ConsoleAutoScroll = false,
            ConsoleShowTimestamps = false,
        };

        repo.Save(original);
        ArduinoCliSettings reloaded = repo.Load();

        Assert.Equal(original.CliPath, reloaded.CliPath);
        Assert.Equal(original.LastSketchPath, reloaded.LastSketchPath);
        Assert.Equal(original.SelectedFqbn, reloaded.SelectedFqbn);
        Assert.Equal(original.LastPort, reloaded.LastPort);
        Assert.Equal(original.BaudRate, reloaded.BaudRate);
        Assert.Equal(original.ConsoleAutoScroll, reloaded.ConsoleAutoScroll);
        Assert.Equal(original.ConsoleShowTimestamps, reloaded.ConsoleShowTimestamps);
    }

    [Fact]
    public void Load_CorruptedJson_ReturnsDefaultsWithoutThrowing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, "{ isto nao é json válido ;;;");

        var repo = new ArduinoSettingsRepository(_filePath);

        ArduinoCliSettings settings = repo.Load();

        Assert.Null(settings.CliPath);
        Assert.Equal(9600, settings.BaudRate);
    }

    [Fact]
    public void Save_CreatesParentDirectoryWhenMissing()
    {
        var repo = new ArduinoSettingsRepository(_filePath);
        Assert.False(Directory.Exists(Path.GetDirectoryName(_filePath)));

        repo.Save(new ArduinoCliSettings());

        Assert.True(File.Exists(_filePath));
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_filePath)!, recursive: true); } catch { /* best effort */ }
    }
}
