using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RadarTorres.App.Configuration;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel do card "Detecção por Câmera" (Monitoramento): mostra o vídeo em tempo real de
/// uma câmera e, enquanto o modo de operação é Amarelo (Acompanhar alvos) ou Vermelho
/// (Disparar), roda a detecção por visão (<see cref="IAlvoVisualService"/>) periodicamente —
/// quando uma detecção bate com o perfil de alvo ativo, dispara o alerta sonoro/por voz
/// (<see cref="IAlertaSonoroService"/>) com a distância estimada.
/// </summary>
/// <remarks>
/// Instanciada por DI e exposta como propriedade de <see cref="MainViewModel"/> (mesmo padrão
/// de composição de <see cref="CameraSlotViewModel"/> dentro de <see cref="CamerasViewModel"/>)
/// — <see cref="MainViewModel"/> empurra as mudanças de <see cref="MainViewModel.CurrentMode"/>
/// para aqui via <see cref="AtualizarModoAtual"/>, em vez desta classe ler o modo diretamente
/// (evita acoplamento circular entre as duas ViewModels).
/// </remarks>
public sealed class DeteccaoCameraViewModel : ViewModelBase, IDisposable
{
    private readonly ICameraDeviceEnumerator _cameraDeviceEnumerator;
    private readonly ICameraCaptureServiceFactory _cameraCaptureServiceFactory;
    private readonly IAlvoVisualService _alvoVisualService;
    private readonly IAlertaSonoroService _alertaSonoroService;
    private readonly IPerfilAlvoService _perfilAlvoService;
    private readonly ILoggingService _logger;

    private ICameraCaptureService? _captureService;
    private WriteableBitmap? _previewBitmap;
    private SystemMode _modoAtual = SystemMode.LigadoApenas;
    private DateTime _ultimoProcessamento = DateTime.MinValue;
    private DateTime _ultimoAlertaEm = DateTime.MinValue;
    private int _processando;

    public ObservableCollection<CameraDevice> AvailableCameras { get; } = new();

    public RelayCommand AtualizarCamerasCommand { get; }

    public DeteccaoCameraViewModel(
        ICameraDeviceEnumerator cameraDeviceEnumerator,
        ICameraCaptureServiceFactory cameraCaptureServiceFactory,
        IAlvoVisualService alvoVisualService,
        IAlertaSonoroService alertaSonoroService,
        IPerfilAlvoService perfilAlvoService,
        ILoggingService logger)
    {
        _cameraDeviceEnumerator = cameraDeviceEnumerator;
        _cameraCaptureServiceFactory = cameraCaptureServiceFactory;
        _alvoVisualService = alvoVisualService;
        _alertaSonoroService = alertaSonoroService;
        _perfilAlvoService = perfilAlvoService;
        _logger = logger;

        AtualizarCamerasCommand = new RelayCommand(_ => AtualizarCameras());

        StatusDeteccao = "Escolha uma câmera para iniciar a detecção.";
        AtualizarCameras();
    }

    /// <summary>Chamado por <see cref="MainViewModel"/> sempre que o modo de operação muda.</summary>
    public void AtualizarModoAtual(SystemMode modo) => _modoAtual = modo;

    /// <summary>
    /// Libera a câmera quando a tela de Monitoramento sai da árvore visual (troca de tela pela
    /// barra lateral) — sem isso, a câmera deste card ficaria ocupada para sempre, mesmo
    /// navegando para outra tela, igual ao cuidado já tomado em <see cref="CamerasView"/>.
    /// </summary>
    public void PararCamera() => PararPreview();

    /// <summary>Religa a câmera já escolhida ao voltar para a tela de Monitoramento (ver <see cref="MainViewModel.OnNavigatedTo"/>).</summary>
    public void RetomarCamera()
    {
        if (!IsStreaming) IniciarPreview(SelectedCamera);
    }

    private CameraDevice? _selectedCamera;
    public CameraDevice? SelectedCamera
    {
        get => _selectedCamera;
        set
        {
            if (SetProperty(ref _selectedCamera, value))
            {
                IniciarPreview(value);
            }
        }
    }

    private bool _isStreaming;
    public bool IsStreaming
    {
        get => _isStreaming;
        private set => SetProperty(ref _isStreaming, value);
    }

    public ImageSource? CurrentFrame => _previewBitmap;

    private string? _statusDeteccao;
    public string? StatusDeteccao
    {
        get => _statusDeteccao;
        private set => SetProperty(ref _statusDeteccao, value);
    }

    private double? _ultimaDistanciaMetros;
    public double? UltimaDistanciaMetros
    {
        get => _ultimaDistanciaMetros;
        private set => SetProperty(ref _ultimaDistanciaMetros, value);
    }

    private void AtualizarCameras()
    {
        CameraDevice? atual = SelectedCamera;

        AvailableCameras.Clear();
        foreach (CameraDevice device in _cameraDeviceEnumerator.GetAvailableCameras())
        {
            AvailableCameras.Add(device);
        }

        CameraDevice? novaSelecao = atual is not null && AvailableCameras.Contains(atual)
            ? atual
            : AvailableCameras.Count > 0 ? AvailableCameras[0] : null;

        if (Equals(novaSelecao, SelectedCamera))
        {
            // AtualizarCameras pode não mudar a referência (mesma câmera já selecionada) —
            // garante que o preview religue mesmo assim ao reabrir a tela (ver OnNavigatedTo
            // no mesmo racional em PerfilAlvoViewModel).
            if (!IsStreaming) IniciarPreview(novaSelecao);
        }
        else
        {
            SelectedCamera = novaSelecao;
        }
    }

    private void IniciarPreview(CameraDevice? device)
    {
        PararPreview();

        if (device is null) return;

        StatusDeteccao = "Iniciando a câmera…";

        _captureService = _cameraCaptureServiceFactory.Create();
        _captureService.FrameCaptured += OnFrameCaptured;
        _captureService.ErrorOccurred += OnErroCaptura;
        _captureService.Start(device);
    }

    private void OnFrameCaptured(object? sender, CameraFrameCapturedEventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_captureService is null) return;

            if (_previewBitmap is null || _previewBitmap.PixelWidth != e.Width || _previewBitmap.PixelHeight != e.Height)
            {
                _previewBitmap = new WriteableBitmap(e.Width, e.Height, 96, 96, PixelFormats.Bgr24, null);
                OnPropertyChanged(nameof(CurrentFrame));
            }

            _previewBitmap.WritePixels(new Int32Rect(0, 0, e.Width, e.Height), e.PixelsBgr24, e.Stride, 0);

            if (!IsStreaming)
            {
                IsStreaming = true;
                StatusDeteccao = "Visualizando. Aguardando detecção…";
            }
        });

        TentarProcessarQuadro(e);
    }

    /// <summary>
    /// Processa no máximo uma rodada de detecção por vez (<see cref="_processando"/>) e no
    /// máximo a cada <see cref="VisionSettings.IntervaloProcessamentoMs"/> — a inferência do
    /// YOLO é bem mais lenta que a taxa de quadros da câmera, então não roda em todo quadro.
    /// </summary>
    private void TentarProcessarQuadro(CameraFrameCapturedEventArgs e)
    {
        DateTime agora = DateTime.Now;
        int intervaloMs = AppConfig.Current.VisionSettings.IntervaloProcessamentoMs;
        if ((agora - _ultimoProcessamento).TotalMilliseconds < intervaloMs) return;

        if (Interlocked.CompareExchange(ref _processando, 1, 0) != 0) return;
        _ultimoProcessamento = agora;

        byte[] pixelsCopia = (byte[])e.PixelsBgr24.Clone();
        int width = e.Width, height = e.Height, stride = e.Stride;

        System.Threading.Tasks.Task.Run(() => ProcessarQuadro(pixelsCopia, width, height, stride))
            .ContinueWith(_ => Interlocked.Exchange(ref _processando, 0));
    }

    private void ProcessarQuadro(byte[] pixels, int width, int height, int stride)
    {
        IntPtr ptr = Marshal.AllocHGlobal(stride * height);
        try
        {
            Marshal.Copy(pixels, 0, ptr, stride * height);
            using var bmp = new Bitmap(width, height, stride, System.Drawing.Imaging.PixelFormat.Format24bppRgb, ptr);

            AlvoVisualAvaliacao avaliacao = _alvoVisualService.AvaliarFrame(bmp);

            Application.Current?.Dispatcher.BeginInvoke(() => AplicarResultado(avaliacao));
        }
        catch (Exception ex)
        {
            _logger.Warning($"Falha ao processar quadro para detecção por visão: {ex.Message}");
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    /// <summary>
    /// Traduz a avaliação do quadro em status legível — deliberadamente granular (diferencia
    /// "sem perfil ativo" de "YOLO não achou nada" de "achou mas cor/formato não bateu" de
    /// "bateu mas o modo não libera o alarme") para dar pra diagnosticar pela própria tela, sem
    /// precisar de acesso à câmera/depurador quando o alarme "não funciona".
    /// </summary>
    private void AplicarResultado(AlvoVisualAvaliacao avaliacao)
    {
        PerfilAlvo? perfilAtivo = _perfilAlvoService.PerfilAtivo;

        if (perfilAtivo is null)
        {
            UltimaDistanciaMetros = null;
            StatusDeteccao = "Nenhum perfil de alvo ativo (ver tela \"Perfis de Alvo\" → \"Selecionar como ativo\").";
            return;
        }

        if (avaliacao.Resultado is null)
        {
            UltimaDistanciaMetros = null;

            if (avaliacao.TotalDeteccoes == 0)
            {
                StatusDeteccao = "Visualizando. O YOLO não encontrou nenhum objeto candidato neste quadro.";
            }
            else if (avaliacao.UsouFotoReferencia)
            {
                StatusDeteccao = $"Visualizando. {avaliacao.TotalDeteccoes} objeto(s) candidato(s), mas nenhum bateu com a foto de referência de \"{perfilAtivo.Nome}\" (melhor similaridade: {avaliacao.MelhorPontuacao:P0}).";
            }
            else
            {
                StatusDeteccao = $"Visualizando. {avaliacao.TotalDeteccoes} objeto(s) candidato(s), mas nenhum bateu com a cor/formato de \"{perfilAtivo.Nome}\" (melhor correspondência de cor: {avaliacao.MelhorPontuacao:P0}).";
            }

            return;
        }

        UltimaDistanciaMetros = avaliacao.Resultado.DistanciaMetros;

        bool modoAlertaAtivo = _modoAtual is SystemMode.AcompanharAlvos or SystemMode.Disparar;
        if (!modoAlertaAtivo)
        {
            StatusDeteccao = $"Alvo \"{avaliacao.Resultado.Perfil.Nome}\" identificado a {avaliacao.Resultado.DistanciaMetros:0.0} m — alarme NÃO disparado: modo atual é Verde (alarme só dispara em Amarelo/Vermelho).";
            return;
        }

        StatusDeteccao = $"Alvo \"{avaliacao.Resultado.Perfil.Nome}\" identificado — {avaliacao.Resultado.DistanciaMetros:0.0} m.";

        bool debounceLiberado = (DateTime.Now - _ultimoAlertaEm).TotalSeconds >= AppConfig.Current.VisionSettings.DebounceAlertaSegundos;
        if (debounceLiberado)
        {
            _ultimoAlertaEm = DateTime.Now;
            _alertaSonoroService.Disparar(avaliacao.Resultado.DistanciaMetros);
            _logger.Warning($"Alvo visual identificado a {avaliacao.Resultado.DistanciaMetros:0.0} m — alerta disparado (modo {_modoAtual}).");
        }
    }

    private void OnErroCaptura(object? sender, CameraErrorEventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            StatusDeteccao = e.Type switch
            {
                CameraErrorType.DeviceDisconnected => "A câmera foi desconectada.",
                CameraErrorType.DeviceBusy => "A câmera está em uso por outra tela ou aplicativo.",
                CameraErrorType.AccessDenied => "O Windows negou o acesso à câmera.",
                _ => "Não foi possível exibir a câmera selecionada."
            };
            PararPreview();
        });
    }

    private void PararPreview()
    {
        if (_captureService is null)
        {
            IsStreaming = false;
            _previewBitmap = null;
            OnPropertyChanged(nameof(CurrentFrame));
            UltimaDistanciaMetros = null;
            return;
        }

        _captureService.FrameCaptured -= OnFrameCaptured;
        _captureService.ErrorOccurred -= OnErroCaptura;
        _captureService.Stop();
        _captureService.Dispose();
        _captureService = null;

        IsStreaming = false;
        _previewBitmap = null;
        OnPropertyChanged(nameof(CurrentFrame));
        UltimaDistanciaMetros = null;
    }

    public void Dispose() => PararPreview();
}
