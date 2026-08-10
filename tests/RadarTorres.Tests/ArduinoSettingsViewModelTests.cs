using System;
using System.IO;
using System.Threading.Tasks;
using RadarTorres.App.Configuration;
using RadarTorres.App.Models;
using RadarTorres.App.Services;
using RadarTorres.App.ViewModels;
using RadarTorres.Tests.Fakes;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Testes da ViewModel da aba Configurações do Arduino usando dublês para todos os
/// serviços — nenhum destes testes toca hardware, disco fora de uma pasta temporária, ou
/// abre diálogos modais (por isso não cobrimos aqui o caminho que exibe o MessageBox de
/// confirmação de troca de porta; ver comentário no teste de disputa abaixo).
/// </summary>
public sealed class ArduinoSettingsViewModelTests : IDisposable
{
    private readonly string _settingsPath = Path.Combine(Path.GetTempPath(), "RadarTorresTests_" + Guid.NewGuid(), "arduino-settings.json");

    private ArduinoSettingsViewModel CreateViewModel(
        FakeSerialCommunicationService? serial = null,
        FakeArduinoCompilerService? compiler = null,
        FakeArduinoCliLocatorService? locator = null)
    {
        return new ArduinoSettingsViewModel(
            locator ?? new FakeArduinoCliLocatorService(),
            compiler ?? new FakeArduinoCompilerService(),
            new ArduinoSettingsRepository(_settingsPath),
            serial ?? new FakeSerialCommunicationService(),
            new FakeLoggingService());
    }

    [Fact]
    public async Task ConnectAsync_AlreadyConnectedOnSamePortAndBaud_ReusesConnectionWithoutReconnecting()
    {
        var serial = new FakeSerialCommunicationService { PortsToReturn = ["COM3"] };
        await serial.ConnectAsync("COM3", 9600); // simula conexão já feita pela tela de Monitoramento
        int callsBeforeCommand = serial.ConnectCallCount;

        var vm = CreateViewModel(serial);
        vm.SelectedPort = "COM3";
        vm.SelectedBaudRate = 9600;

        vm.ConnectCommand.Execute(null);
        await Task.Delay(50); // RelayCommand dispara async void internamente via Execute -> Task

        Assert.Equal(callsBeforeCommand, serial.ConnectCallCount); // não reconectou
        Assert.Equal(0, serial.DisconnectCallCount); // não derrubou a conexão existente
    }

    [Fact]
    public async Task ConnectAsync_NoExistingConnection_ConnectsOnSharedSerialService()
    {
        var serial = new FakeSerialCommunicationService { PortsToReturn = ["COM5"] };
        var vm = CreateViewModel(serial);
        vm.SelectedPort = "COM5";
        vm.SelectedBaudRate = 115200;

        vm.ConnectCommand.Execute(null);
        await Task.Delay(50);

        Assert.Equal(1, serial.ConnectCallCount);
        Assert.Equal("COM5", serial.CurrentPortName);
        Assert.Equal(115200, serial.CurrentBaudRate);
    }

    [Fact]
    public void MonitorLines_ReceivingMoreThanLimit_DropsOldestLines()
    {
        var serial = new FakeSerialCommunicationService();
        var vm = CreateViewModel(serial);

        for (int i = 0; i < 4100; i++)
        {
            serial.RaiseMessageReceived(new UnknownMessage($"linha {i}"));
        }

        Assert.True(vm.MonitorLines.Count <= 4000, $"Esperava no máximo 4000 linhas, obteve {vm.MonitorLines.Count}");
        Assert.Equal(4100, vm.MessageCount); // contador continua exato mesmo com o console limitado
    }

    [Fact]
    public async Task CompileOutputLines_MoreThanLimit_DropsOldestLines()
    {
        var compiler = new FakeArduinoCompilerService { LinesToReport = 4500 };
        var locator = new FakeArduinoCliLocatorService { LocateResult = new ArduinoCliInfo { Found = true, ExecutablePath = @"C:\fake\arduino-cli.exe", Source = ArduinoCliSource.ConfiguracaoSalva } };
        var vm = CreateViewModel(compiler: compiler, locator: locator);

        string sketch = Path.Combine(Path.GetTempPath(), "RadarTorresTests_" + Guid.NewGuid());
        Directory.CreateDirectory(sketch);
        vm.SketchPath = sketch;
        vm.SelectedBoard = ArduinoBoardCatalog.DefaultBoards[0];

        vm.CompileCommand.Execute(null);
        await Task.Delay(200);

        Assert.True(vm.CompileOutputLines.Count <= 4000, $"Esperava no máximo 4000 linhas, obteve {vm.CompileOutputLines.Count}");

        try { Directory.Delete(sketch, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task CompileCommand_CancelledByUser_ReportsCancelledStatus()
    {
        var compiler = new FakeArduinoCompilerService
        {
            ResultToReturn = new ArduinoCompileResult { Status = ArduinoCompileStatus.Cancelled, ExitCode = null, Duration = TimeSpan.FromSeconds(1) }
        };
        var vm = CreateViewModel(compiler: compiler);

        string sketch = Path.Combine(Path.GetTempPath(), "RadarTorresTests_" + Guid.NewGuid());
        Directory.CreateDirectory(sketch);
        vm.SketchPath = sketch;
        vm.SelectedBoard = ArduinoBoardCatalog.DefaultBoards[0];

        vm.CompileCommand.Execute(null);
        await Task.Delay(100);

        Assert.Equal(ArduinoCompileStatus.Cancelled, vm.LastCompileStatus);
        Assert.False(vm.IsCompiling);

        try { Directory.Delete(sketch, recursive: true); } catch { /* best effort */ }
    }

    public void Dispose()
    {
        try { Directory.Delete(Path.GetDirectoryName(_settingsPath)!, recursive: true); } catch { /* best effort */ }
    }
}
