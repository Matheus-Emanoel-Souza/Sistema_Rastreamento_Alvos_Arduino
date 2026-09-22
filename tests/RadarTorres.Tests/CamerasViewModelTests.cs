using System.Linq;
using RadarTorres.App.Models;
using RadarTorres.App.Services;
using RadarTorres.App.ViewModels;
using RadarTorres.Tests.Fakes;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Testes da ViewModel do módulo Câmeras usando <see cref="FakeCameraDeviceEnumerator"/> e
/// <see cref="FakeCameraCaptureServiceFactory"/> — nenhum destes testes toca uma webcam física,
/// seguindo o mesmo racional de <c>ArduinoSettingsViewModelTests</c> para a porta serial. O
/// comportamento de cada painel isoladamente (iniciar/parar/erros/quadros) é coberto por
/// <see cref="CameraSlotViewModelTests"/>; aqui o foco é a orquestração dos 4 painéis: geração da
/// lista de câmeras e a regra de exclusividade entre eles.
/// </summary>
public sealed class CamerasViewModelTests
{
    private static readonly CameraDevice Camera1 = new("\\device\\cam1", "Webcam Integrada");
    private static readonly CameraDevice Camera2 = new("\\device\\cam2", "Webcam USB");
    private static readonly CameraDevice Camera3 = new("\\device\\cam3", "Câmera de Estúdio");

    private static (CamerasViewModel vm, FakeCameraDeviceEnumerator enumerator, FakeCameraCaptureServiceFactory factory) CriarViewModel(
        params CameraDevice[] cameras)
    {
        var enumerator = new FakeCameraDeviceEnumerator { CamerasToReturn = cameras };
        var factory = new FakeCameraCaptureServiceFactory();
        var vm = new CamerasViewModel(enumerator, factory, new FakeLocalizationService());
        return (vm, enumerator, factory);
    }

    [Fact]
    public void Construtor_CriaExatamente4PaineisIndependentes()
    {
        (CamerasViewModel vm, _, FakeCameraCaptureServiceFactory factory) = CriarViewModel();

        Assert.Equal(4, vm.Slots.Count);
        Assert.Equal([1, 2, 3, 4], vm.Slots.Select(s => s.SlotNumber));
        Assert.Equal(4, factory.CreatedServices.Count);
    }

    [Fact]
    public void Construtor_SemCamerasDisponiveis_TodosOsPaineisIndicamNenhumaEncontrada()
    {
        (CamerasViewModel vm, _, _) = CriarViewModel();

        Assert.All(vm.Slots, slot =>
        {
            Assert.False(slot.HasCameras);
            Assert.Equal("Cameras.NenhumaEncontrada", slot.StatusMessage);
        });
    }

    [Fact]
    public void Construtor_ComCamerasDisponiveis_TodosOsPaineisVeemAMesmaListaESelecionamAPrimeira()
    {
        (CamerasViewModel vm, _, _) = CriarViewModel(Camera1, Camera2);

        Assert.All(vm.Slots, slot =>
        {
            Assert.Equal(2, slot.AvailableCameras.Count);
            Assert.Equal(Camera1, slot.SelectedCamera);
        });
    }

    [Fact]
    public void IniciarCameraEmUmPainel_TornaAIndisponivelParaOsDemaisPaineis()
    {
        (CamerasViewModel vm, _, FakeCameraCaptureServiceFactory factory) = CriarViewModel(Camera1, Camera2);
        CameraSlotViewModel slot1 = vm.Slots[0];
        slot1.SelectedCamera = Camera1;

        slot1.StartCommand.Execute(null);

        Assert.DoesNotContain(Camera1, vm.Slots[1].AvailableCameras);
        Assert.DoesNotContain(Camera1, vm.Slots[2].AvailableCameras);
        Assert.DoesNotContain(Camera1, vm.Slots[3].AvailableCameras);
        Assert.Contains(Camera1, slot1.AvailableCameras); // continua visível para quem está usando
        Assert.Equal(1, factory.CreatedServices[0].StartCallCount);
    }

    [Fact]
    public void PararCameraEmUmPainel_DevolveAParaOsDemaisPaineis()
    {
        (CamerasViewModel vm, _, FakeCameraCaptureServiceFactory factory) = CriarViewModel(Camera1, Camera2);
        CameraSlotViewModel slot1 = vm.Slots[0];
        slot1.SelectedCamera = Camera1;
        slot1.StartCommand.Execute(null);

        slot1.StopCommand.Execute(null);

        Assert.Contains(Camera1, vm.Slots[1].AvailableCameras);
        Assert.Contains(Camera1, vm.Slots[2].AvailableCameras);
        Assert.Contains(Camera1, vm.Slots[3].AvailableCameras);
        Assert.Equal(1, factory.CreatedServices[0].StopCallCount);
    }

    [Fact]
    public void DesconexaoFisicaDeUmaCamera_ParaApenasOPainelAfetado_OsDemaisContinuam()
    {
        (CamerasViewModel vm, _, FakeCameraCaptureServiceFactory factory) = CriarViewModel(Camera1, Camera2, Camera3);
        CameraSlotViewModel slot1 = vm.Slots[0];
        CameraSlotViewModel slot2 = vm.Slots[1];
        slot1.SelectedCamera = Camera1;
        slot1.StartCommand.Execute(null);
        slot2.SelectedCamera = Camera2;
        slot2.StartCommand.Execute(null);
        factory.CreatedServices[0].RaiseFrameCaptured(width: 4, height: 4);
        factory.CreatedServices[1].RaiseFrameCaptured(width: 4, height: 4);

        // Simula a Câmera 1 sendo desconectada fisicamente enquanto a Câmera 2 continua rodando.
        factory.CreatedServices[0].RaiseErrorOccurred(new CameraErrorEventArgs(CameraErrorType.DeviceDisconnected, "desconectada"));
        factory.CreatedServices[0].RaiseStopped();

        Assert.False(slot1.IsStreaming);
        Assert.True(slot1.HasError);
        Assert.True(slot2.IsStreaming); // painel 2 não foi afetado pela falha do painel 1
        Assert.False(slot2.HasError);
        Assert.Contains(Camera1, slot2.AvailableCameras); // câmera 1 liberada de volta
    }

    [Fact]
    public void RefreshCommand_AtualizaAListaDeCamerasDeTodosOsPaineis()
    {
        (CamerasViewModel vm, FakeCameraDeviceEnumerator enumerator, _) = CriarViewModel(Camera1);

        enumerator.CamerasToReturn = [Camera1, Camera2, Camera3];
        vm.RefreshCommand.Execute(null);

        Assert.All(vm.Slots, slot => Assert.Equal(3, slot.AvailableCameras.Count));
    }

    [Fact]
    public void OnNavigatedTo_AtualizaAListaDeCamerasDeTodosOsPaineis()
    {
        (CamerasViewModel vm, FakeCameraDeviceEnumerator enumerator, _) = CriarViewModel();

        enumerator.CamerasToReturn = [Camera1];
        vm.OnNavigatedTo();

        Assert.All(vm.Slots, slot => Assert.True(slot.HasCameras));
    }

    [Fact]
    public void ReleaseCamera_ParaTodosOsPaineisEmUso()
    {
        (CamerasViewModel vm, _, FakeCameraCaptureServiceFactory factory) = CriarViewModel(Camera1, Camera2);
        vm.Slots[0].SelectedCamera = Camera1;
        vm.Slots[0].StartCommand.Execute(null);
        vm.Slots[1].SelectedCamera = Camera2;
        vm.Slots[1].StartCommand.Execute(null);

        vm.ReleaseCamera();

        Assert.Equal(1, factory.CreatedServices[0].StopCallCount);
        Assert.Equal(1, factory.CreatedServices[1].StopCallCount);
    }

    [Fact]
    public void Dispose_ParaEDescartaTodosOsPaineis()
    {
        (CamerasViewModel vm, _, FakeCameraCaptureServiceFactory factory) = CriarViewModel(Camera1);
        vm.Slots[0].SelectedCamera = Camera1;
        vm.Slots[0].StartCommand.Execute(null);

        vm.Dispose();

        Assert.Equal(1, factory.CreatedServices[0].StopCallCount);
    }
}
