using System.Collections.Generic;
using RadarTorres.App.Models;
using RadarTorres.App.Services;
using RadarTorres.App.ViewModels;
using RadarTorres.Tests.Fakes;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Testes de um único painel de câmera (<see cref="CameraSlotViewModel"/>) usando
/// <see cref="FakeCameraCaptureService"/> — nenhum destes testes toca uma webcam física, mesmo
/// racional de <c>ArduinoSettingsViewModelTests</c> para a porta serial. Os testes de
/// exclusividade ENTRE painéis (uma câmera reservada por um painel sumir da lista dos demais)
/// ficam em <see cref="CamerasViewModelTests"/>, que é quem orquestra os 4 painéis.
/// </summary>
public sealed class CameraSlotViewModelTests
{
    private static readonly CameraDevice Camera1 = new("\\device\\cam1", "Webcam Integrada");
    private static readonly CameraDevice Camera2 = new("\\device\\cam2", "Webcam USB");

    private static readonly HashSet<string> NadaReservado = [];

    [Fact]
    public void SemCamerasDisponiveis_IndicaClaramenteQueNenhumaFoiEncontrada()
    {
        var slot = new CameraSlotViewModel(1, new FakeCameraCaptureService(), new FakeLocalizationService());

        slot.UpdateAvailableCameras([], NadaReservado);

        Assert.False(slot.HasCameras);
        Assert.Empty(slot.AvailableCameras);
        Assert.Equal("Cameras.NenhumaEncontrada", slot.StatusMessage);
        Assert.False(slot.StartCommand.CanExecute(null));
    }

    [Fact]
    public void ComCamerasDisponiveis_SelecionaAPrimeiraEHabilitaIniciar()
    {
        var slot = new CameraSlotViewModel(1, new FakeCameraCaptureService(), new FakeLocalizationService());

        slot.UpdateAvailableCameras([Camera1, Camera2], NadaReservado);

        Assert.True(slot.HasCameras);
        Assert.Equal(2, slot.AvailableCameras.Count);
        Assert.Equal(Camera1, slot.SelectedCamera);
        Assert.Equal("Cameras.SelecioneParaIniciar", slot.StatusMessage);
        Assert.True(slot.StartCommand.CanExecute(null));
        Assert.False(slot.StopCommand.CanExecute(null));
    }

    [Fact]
    public void UpdateAvailableCameras_CameraAnteriormenteSelecionadaAindaPresente_MantemASelecao()
    {
        var slot = new CameraSlotViewModel(1, new FakeCameraCaptureService(), new FakeLocalizationService());
        slot.UpdateAvailableCameras([Camera1, Camera2], NadaReservado);
        slot.SelectedCamera = Camera2;

        slot.UpdateAvailableCameras([Camera1, Camera2], NadaReservado);

        Assert.Equal(Camera2, slot.SelectedCamera);
    }

    [Fact]
    public void UpdateAvailableCameras_CameraSelecionadaReservadaPorOutroPainel_LimpaASelecao()
    {
        var slot = new CameraSlotViewModel(1, new FakeCameraCaptureService(), new FakeLocalizationService());
        slot.UpdateAvailableCameras([Camera1, Camera2], NadaReservado);
        slot.SelectedCamera = Camera2;

        slot.UpdateAvailableCameras([Camera1, Camera2], new HashSet<string> { Camera2.Id });

        Assert.Null(slot.SelectedCamera);
        Assert.DoesNotContain(Camera2, slot.AvailableCameras);
    }

    [Fact]
    public void StartCommand_CameraSelecionada_IniciaCapturaNoServicoEReservaOPainel()
    {
        var service = new FakeCameraCaptureService();
        var slot = new CameraSlotViewModel(1, service, new FakeLocalizationService());
        slot.UpdateAvailableCameras([Camera1], NadaReservado);

        bool reservationChangedRaised = false;
        slot.ReservationChanged += (_, _) => reservationChangedRaised = true;

        slot.StartCommand.Execute(null);

        Assert.Equal(1, service.StartCallCount);
        Assert.Equal(Camera1, service.CurrentDevice);
        Assert.Equal("Cameras.Iniciando", slot.StatusMessage);
        Assert.True(slot.IsBusy);
        Assert.True(reservationChangedRaised);
        Assert.False(slot.StartCommand.CanExecute(null));
    }

    [Fact]
    public void FrameCaptured_PrimeiroQuadro_MarcaComoVisualizandoEExpoeCurrentFrame()
    {
        var service = new FakeCameraCaptureService();
        var slot = new CameraSlotViewModel(1, service, new FakeLocalizationService());
        slot.UpdateAvailableCameras([Camera1], NadaReservado);
        slot.StartCommand.Execute(null);

        service.RaiseFrameCaptured(width: 4, height: 4);

        Assert.True(slot.IsStreaming);
        Assert.False(slot.HasError);
        Assert.NotNull(slot.CurrentFrame);
        Assert.Equal("Cameras.Visualizando", slot.StatusMessage);
        Assert.True(slot.StopCommand.CanExecute(null));
    }

    [Fact]
    public void StopCommand_CapturaAtiva_ParaLiberaAReservaEDevolveEstadoOcioso()
    {
        var service = new FakeCameraCaptureService();
        var slot = new CameraSlotViewModel(1, service, new FakeLocalizationService());
        slot.UpdateAvailableCameras([Camera1], NadaReservado);
        slot.StartCommand.Execute(null);
        service.RaiseFrameCaptured(width: 4, height: 4);

        bool reservationChangedRaised = false;
        slot.ReservationChanged += (_, _) => reservationChangedRaised = true;
        slot.StopCommand.Execute(null);

        Assert.Equal(1, service.StopCallCount);
        Assert.False(slot.IsStreaming);
        Assert.False(slot.IsBusy);
        Assert.Null(slot.CurrentFrame);
        Assert.Equal("Cameras.SelecioneParaIniciar", slot.StatusMessage);
        Assert.True(reservationChangedRaised);
    }

    [Theory]
    [InlineData(CameraErrorType.DeviceBusy, "Cameras.Erro.Ocupada")]
    [InlineData(CameraErrorType.DeviceDisconnected, "Cameras.Erro.Desconectada")]
    [InlineData(CameraErrorType.AccessDenied, "Cameras.Erro.AcessoNegado")]
    [InlineData(CameraErrorType.Unknown, "Cameras.Erro.Desconhecido")]
    public void ErrorOccurred_QualquerTipoDeErro_TraduzParaAMensagemCorrespondente(CameraErrorType tipo, string chaveEsperada)
    {
        var service = new FakeCameraCaptureService();
        var slot = new CameraSlotViewModel(1, service, new FakeLocalizationService());
        slot.UpdateAvailableCameras([Camera1], NadaReservado);

        service.RaiseErrorOccurred(new CameraErrorEventArgs(tipo, "detalhe técnico irrelevante para a UI"));

        Assert.True(slot.HasError);
        Assert.Equal(chaveEsperada, slot.StatusMessage);
    }

    [Fact]
    public void ErrorOccurred_FalhaAoIniciar_LiberaAReservaImediatamente()
    {
        var service = new FakeCameraCaptureService
        {
            ErrorToRaiseOnStart = new CameraErrorEventArgs(CameraErrorType.DeviceBusy, "em uso")
        };
        var slot = new CameraSlotViewModel(1, service, new FakeLocalizationService());
        slot.UpdateAvailableCameras([Camera1], NadaReservado);

        slot.StartCommand.Execute(null);

        Assert.False(slot.IsBusy);
        Assert.True(slot.HasError);
        Assert.True(slot.StartCommand.CanExecute(null));
    }

    [Fact]
    public void ReleaseCamera_CapturaAtiva_ParaOServicoDeCaptura()
    {
        var service = new FakeCameraCaptureService();
        var slot = new CameraSlotViewModel(1, service, new FakeLocalizationService());
        slot.UpdateAvailableCameras([Camera1], NadaReservado);
        slot.StartCommand.Execute(null);

        slot.ReleaseCamera();

        Assert.Equal(1, service.StopCallCount);
        Assert.False(service.IsRunning);
    }

    [Fact]
    public void Dispose_PareCapturaEDeixaDeReagirAEventosDoServico()
    {
        var service = new FakeCameraCaptureService();
        var slot = new CameraSlotViewModel(1, service, new FakeLocalizationService());
        slot.UpdateAvailableCameras([Camera1], NadaReservado);
        slot.StartCommand.Execute(null);

        slot.Dispose();

        Assert.Equal(1, service.StopCallCount);

        // Depois de Dispose, novos eventos do serviço não devem mais alterar o painel.
        service.RaiseErrorOccurred(new CameraErrorEventArgs(CameraErrorType.Unknown, "erro pós-dispose"));
        Assert.False(slot.HasError);
    }
}
