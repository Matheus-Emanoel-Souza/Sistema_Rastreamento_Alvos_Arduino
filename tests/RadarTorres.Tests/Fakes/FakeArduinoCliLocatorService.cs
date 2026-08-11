using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.Tests.Fakes;

/// <summary>Dublê de <see cref="IArduinoCliLocatorService"/> com resultado configurável, sem tocar no disco/PATH real.</summary>
public sealed class FakeArduinoCliLocatorService : IArduinoCliLocatorService
{
    public ArduinoCliInfo LocateResult { get; set; } = new() { Found = true, ExecutablePath = "C:\\fake\\arduino-cli.exe", Source = ArduinoCliSource.ConfiguracaoSalva };
    public string? VersionResult { get; set; } = "arduino-cli  Version: 0.35.0";
    public IReadOnlyList<ArduinoBoardOption> BoardsResult { get; set; } = System.Array.Empty<ArduinoBoardOption>();

    public ArduinoCliInfo Locate(string? savedPath) => LocateResult;

    public Task<string?> GetVersionAsync(string cliPath, CancellationToken cancellationToken = default) => Task.FromResult(VersionResult);

    public Task<IReadOnlyList<ArduinoBoardOption>> ListInstalledBoardsAsync(string cliPath, CancellationToken cancellationToken = default) =>
        Task.FromResult(BoardsResult);
}
