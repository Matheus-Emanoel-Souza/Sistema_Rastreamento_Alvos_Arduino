using System;
using System.Collections.Generic;
using System.Linq;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel do módulo "Câmeras": orquestra até 4 painéis independentes
/// (<see cref="CameraSlotViewModel"/>), cada um com sua própria câmera/webcam selecionável,
/// permitindo visualizar em tempo real até 4 câmeras simultaneamente (numa grade 2x2 na View).
/// Escopo desta etapa continua sendo exclusivamente visualização — sem gravação, captura de
/// foto, processamento de imagem ou qualquer integração com o Arduino/torres (ver
/// Docs/Projeto/LOG_SOLICITACOES.md).
/// </summary>
/// <remarks>
/// Esta classe não sabe nada sobre captura de vídeo em si — isso é responsabilidade de cada
/// <see cref="CameraSlotViewModel"/> (e da <see cref="ICameraCaptureService"/> própria dele). O
/// papel desta classe é: (1) consultar a lista de câmeras do Windows uma única vez por atualização
/// via <see cref="ICameraDeviceEnumerator"/> e repassá-la aos 4 painéis; e (2) garantir a regra de
/// exclusividade — uma mesma câmera física nunca pode estar reservada (<see cref="CameraSlotViewModel.IsBusy"/>)
/// em mais de um painel ao mesmo tempo — recalculando a lista disponível de cada painel sempre que
/// qualquer um deles começa ou termina de usar uma câmera.
/// </remarks>
public sealed class CamerasViewModel : ViewModelBase, INavigationAware, IDisposable
{
    /// <summary>Quantidade fixa de painéis exibidos na grade 2x2.</summary>
    public const int SlotCount = 4;

    private readonly ICameraDeviceEnumerator _deviceEnumerator;

    private IReadOnlyList<CameraDevice> _allCameras = Array.Empty<CameraDevice>();

    public CamerasViewModel(
        ICameraDeviceEnumerator deviceEnumerator,
        ICameraCaptureServiceFactory captureServiceFactory,
        ILocalizationService localizationService)
    {
        _deviceEnumerator = deviceEnumerator;

        Slots = Enumerable.Range(1, SlotCount)
            .Select(slotNumber => new CameraSlotViewModel(slotNumber, captureServiceFactory.Create(), localizationService))
            .ToList();

        foreach (CameraSlotViewModel slot in Slots)
        {
            slot.ReservationChanged += OnSlotReservationChanged;
        }

        RefreshCommand = new RelayCommand(RefreshCameras);

        RefreshCameras();
    }

    /// <summary>Os até 4 painéis de câmera exibidos pela View (um por posição da grade 2x2).</summary>
    public IReadOnlyList<CameraSlotViewModel> Slots { get; }

    public RelayCommand RefreshCommand { get; }

    /// <summary>Chamado pela ShellWindow/NavigationService ao entrar nesta tela — atualiza a lista de câmeras de todos os painéis, já que dispositivos podem ter sido conectados/removidos enquanto o usuário estava em outra tela.</summary>
    public void OnNavigatedTo() => RefreshCameras();

    /// <summary>Chamado pela View quando ela sai da árvore visual (navegação para outra tela) — libera todas as câmeras em uso imediatamente, em vez de deixá-las ocupadas até o app inteiro fechar.</summary>
    public void ReleaseCamera()
    {
        foreach (CameraSlotViewModel slot in Slots)
        {
            slot.ReleaseCamera();
        }
    }

    private void RefreshCameras()
    {
        _allCameras = _deviceEnumerator.GetAvailableCameras();
        RecomputeAvailabilityForAllSlots(refreshIdleStatus: true);
    }

    /// <summary>
    /// Só recalcula quais câmeras cada painel pode selecionar — nunca mexe em status/erro dos
    /// painéis (<paramref name="refreshIdleStatus"/> = false), pois essa reação é apenas ao
    /// OUTRO painel ter começado/terminado de usar uma câmera, não a uma atualização de lista.
    /// </summary>
    private void OnSlotReservationChanged(object? sender, EventArgs e) => RecomputeAvailabilityForAllSlots(refreshIdleStatus: false);

    /// <summary>
    /// Para cada painel, exclui da lista as câmeras atualmente reservadas (<see cref="CameraSlotViewModel.IsBusy"/>)
    /// pelos OUTROS painéis — é isto que impede a mesma câmera física de ser selecionada
    /// simultaneamente em dois painéis, e que a libera de volta assim que o painel que a usava
    /// parar ou desconectar.
    /// </summary>
    private void RecomputeAvailabilityForAllSlots(bool refreshIdleStatus)
    {
        foreach (CameraSlotViewModel slot in Slots)
        {
            HashSet<string> reservedByOthers = Slots
                .Where(other => other != slot && other.IsBusy && other.SelectedCamera is not null)
                .Select(other => other.SelectedCamera!.Id)
                .ToHashSet();

            slot.UpdateAvailableCameras(_allCameras, reservedByOthers, refreshIdleStatus);
        }
    }

    public void Dispose()
    {
        foreach (CameraSlotViewModel slot in Slots)
        {
            slot.ReservationChanged -= OnSlotReservationChanged;
            slot.Dispose();
        }
    }
}
