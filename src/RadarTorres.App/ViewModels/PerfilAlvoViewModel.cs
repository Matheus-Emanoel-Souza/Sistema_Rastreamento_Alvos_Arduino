using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da tela "Perfis de Alvo": cadastra perfis (cor + formato + tamanho) que a
/// detecção por visão computacional deve travar ao identificar um alvo, permite escolher qual
/// perfil está ativo e, opcionalmente, capturar uma foto de referência de uma das câmeras do
/// módulo "Câmeras" para tratar aquele objeto manualmente depois.
/// </summary>
/// <remarks>
/// Sem edição de campos após criado — igual ao padrão já usado em <see cref="ZonaMorta"/>/
/// <c>MonitoramentoView</c> (trocar cor/formato/tamanho de um perfil existente é removê-lo e
/// criar outro), o que mantém o formulário e a validação simples. A captura de foto usa sua
/// própria <see cref="ICameraCaptureService"/> (via <see cref="ICameraCaptureServiceFactory"/>),
/// independente das já abertas na tela "Câmeras" — se a câmera escolhida já estiver em uso em
/// outro lugar, o sistema operacional/driver recusa o acesso e isso aparece como erro normal de
/// captura, mesmo tratamento já usado em <see cref="CameraSlotViewModel"/>.
/// </remarks>
public sealed class PerfilAlvoViewModel : ViewModelBase, INavigationAware, IDisposable
{
    private readonly IPerfilAlvoService _perfilAlvoService;
    private readonly IPermissionService _permissionService;
    private readonly IAuthService _authService;
    private readonly ICameraDeviceEnumerator _cameraDeviceEnumerator;
    private readonly ICameraCaptureServiceFactory _cameraCaptureServiceFactory;
    private readonly ILoggingService _logger;

    private ICameraCaptureService? _captureService;

    public ObservableCollection<PerfilAlvo> Perfis => _perfilAlvoService.Perfis;

    public CorAlvo[] CoresDisponiveis { get; } = (CorAlvo[])Enum.GetValues(typeof(CorAlvo));

    public FormatoAlvo[] FormatosDisponiveis { get; } = (FormatoAlvo[])Enum.GetValues(typeof(FormatoAlvo));

    public bool PodeGerenciar => _permissionService.PodeGerenciarPerfisAlvo(_authService.CurrentUser?.Perfil ?? PerfilUsuario.Visualizador);

    public RelayCommand NovoCommand { get; }
    public RelayCommand SalvarCommand { get; }
    public RelayCommand SelecionarAtivoCommand { get; }
    public RelayCommand RemoverCommand { get; }
    public RelayCommand AtualizarCamerasCommand { get; }
    public RelayCommand TirarFotoCommand { get; }

    public PerfilAlvoViewModel(
        IPerfilAlvoService perfilAlvoService,
        IPermissionService permissionService,
        IAuthService authService,
        ICameraDeviceEnumerator cameraDeviceEnumerator,
        ICameraCaptureServiceFactory cameraCaptureServiceFactory,
        ILoggingService logger)
    {
        _perfilAlvoService = perfilAlvoService;
        _permissionService = permissionService;
        _authService = authService;
        _cameraDeviceEnumerator = cameraDeviceEnumerator;
        _cameraCaptureServiceFactory = cameraCaptureServiceFactory;
        _logger = logger;

        NovoCommand = new RelayCommand(_ => IniciarNovo(), _ => PodeGerenciar);
        SalvarCommand = new RelayCommand(_ => Salvar(), _ => PodeGerenciar);
        SelecionarAtivoCommand = new RelayCommand(_ => SelecionarAtivo(), _ => PodeGerenciar && SelectedPerfil is { Selecionado: false });
        RemoverCommand = new RelayCommand(_ => Remover(), _ => PodeGerenciar && SelectedPerfil is not null);
        AtualizarCamerasCommand = new RelayCommand(_ => AtualizarCameras());
        TirarFotoCommand = new RelayCommand(_ => TirarFoto(), _ => PodeGerenciar && SelectedPerfil is not null && IsStreaming);

        IniciarNovo();
        AtualizarCameras();
    }

    public void OnNavigatedTo()
    {
        AtualizarCameras();

        // AtualizarCameras só reinicia o preview se a câmera selecionada mudar de referência
        // (ver SelectedCamera.set) — ao voltar de outra tela (Dispose já parou a captura
        // anterior), a câmera escolhida continua a mesma, então é preciso religar aqui.
        if (!IsStreaming)
        {
            IniciarPreview(SelectedCamera);
        }
    }

    // ---------------------------------------------------------------- Seleção / formulário

    private PerfilAlvo? _selectedPerfil;
    public PerfilAlvo? SelectedPerfil
    {
        get => _selectedPerfil;
        set
        {
            if (SetProperty(ref _selectedPerfil, value))
            {
                AtualizarPreviewFoto();
                RelayCommand.RaiseCanExecuteChangedForAll();
            }
        }
    }

    private string _formNome = string.Empty;
    public string FormNome
    {
        get => _formNome;
        set => SetProperty(ref _formNome, value);
    }

    private CorAlvo _formCor = CorAlvo.Vermelho;
    public CorAlvo FormCor
    {
        get => _formCor;
        set => SetProperty(ref _formCor, value);
    }

    private FormatoAlvo _formFormato = FormatoAlvo.Quadrado;
    public FormatoAlvo FormFormato
    {
        get => _formFormato;
        set => SetProperty(ref _formFormato, value);
    }

    private string _formTamanhoCm = string.Empty;
    public string FormTamanhoCm
    {
        get => _formTamanhoCm;
        set => SetProperty(ref _formTamanhoCm, value);
    }

    private string? _mensagemFormulario;
    public string? MensagemFormulario
    {
        get => _mensagemFormulario;
        set => SetProperty(ref _mensagemFormulario, value);
    }

    private bool _sucessoFormulario;
    public bool SucessoFormulario
    {
        get => _sucessoFormulario;
        set => SetProperty(ref _sucessoFormulario, value);
    }

    private void IniciarNovo()
    {
        SelectedPerfil = null;
        FormNome = string.Empty;
        FormCor = CorAlvo.Vermelho;
        FormFormato = FormatoAlvo.Quadrado;
        FormTamanhoCm = string.Empty;
        MensagemFormulario = null;
    }

    private void Salvar()
    {
        SucessoFormulario = false;

        if (string.IsNullOrWhiteSpace(FormNome))
        {
            MensagemFormulario = "Informe um nome para o perfil.";
            return;
        }

        if (!double.TryParse(FormTamanhoCm, NumberStyles.Float, CultureInfo.InvariantCulture, out double tamanhoCm) || tamanhoCm <= 0)
        {
            MensagemFormulario = "Informe um tamanho válido, em centímetros (maior dimensão do objeto).";
            return;
        }

        PerfilAlvo novo = _perfilAlvoService.Add(FormNome.Trim(), FormCor, FormFormato, tamanhoCm);

        MensagemFormulario = "Perfil criado com sucesso.";
        SucessoFormulario = true;
        IniciarNovo();
        SelectedPerfil = novo;
    }

    private void SelecionarAtivo()
    {
        if (SelectedPerfil is null) return;
        _perfilAlvoService.SelecionarAtivo(SelectedPerfil);
        RelayCommand.RaiseCanExecuteChangedForAll();
    }

    private void Remover()
    {
        if (SelectedPerfil is null) return;
        _perfilAlvoService.Remove(SelectedPerfil);
        IniciarNovo();
    }

    // ---------------------------------------------------------------- Foto de referência

    public ObservableCollection<CameraDevice> AvailableCameras { get; } = new();

    private CameraDevice? _selectedCamera;
    public CameraDevice? SelectedCamera
    {
        get => _selectedCamera;
        set
        {
            if (SetProperty(ref _selectedCamera, value))
            {
                RelayCommand.RaiseCanExecuteChangedForAll();
                IniciarPreview(value);
            }
        }
    }

    private bool _isStreaming;

    /// <summary>Se a pré-visualização em tempo real está ativa (câmera já ligada, pronta para centralizar o objeto).</summary>
    public bool IsStreaming
    {
        get => _isStreaming;
        private set
        {
            if (SetProperty(ref _isStreaming, value))
            {
                RelayCommand.RaiseCanExecuteChangedForAll();
            }
        }
    }

    private string? _statusFoto;
    public string? StatusFoto
    {
        get => _statusFoto;
        set => SetProperty(ref _statusFoto, value);
    }

    private WriteableBitmap? _previewBitmap;

    /// <summary>Quadro atual da pré-visualização em tempo real, para o usuário centralizar o objeto antes de clicar em "Tirar foto".</summary>
    public ImageSource? CurrentFrame => _previewBitmap;

    private ImageSource? _fotoReferenciaPreview;

    /// <summary>Última foto de referência já salva para o perfil selecionado (diferente de <see cref="CurrentFrame"/>, que é o quadro em tempo real).</summary>
    public ImageSource? FotoReferenciaPreview
    {
        get => _fotoReferenciaPreview;
        private set => SetProperty(ref _fotoReferenciaPreview, value);
    }

    private void AtualizarCameras()
    {
        CameraDevice? atual = SelectedCamera;

        AvailableCameras.Clear();
        foreach (CameraDevice device in _cameraDeviceEnumerator.GetAvailableCameras())
        {
            AvailableCameras.Add(device);
        }

        SelectedCamera = atual is not null && AvailableCameras.Contains(atual)
            ? atual
            : AvailableCameras.Count > 0 ? AvailableCameras[0] : null;
    }

    private void AtualizarPreviewFoto()
    {
        string? caminho = SelectedPerfil?.CaminhoFotoReferencia;

        if (string.IsNullOrEmpty(caminho) || !File.Exists(caminho))
        {
            FotoReferenciaPreview = null;
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(caminho, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        FotoReferenciaPreview = bitmap;
    }

    /// <summary>
    /// Liga a pré-visualização em tempo real assim que uma câmera é escolhida (troca de câmera
    /// encerra a anterior primeiro) — o usuário já vê o quadro ao vivo para centralizar o
    /// objeto antes de clicar em "Tirar foto", em vez de só descobrir o enquadramento depois
    /// de capturar.
    /// </summary>
    private void IniciarPreview(CameraDevice? device)
    {
        PararPreview();

        if (device is null) return;

        StatusFoto = "Iniciando a câmera…";

        _captureService = _cameraCaptureServiceFactory.Create();
        _captureService.FrameCaptured += OnPreviewFrameCaptured;
        _captureService.ErrorOccurred += OnPreviewErroCaptura;
        _captureService.Start(device);
    }

    private void OnPreviewFrameCaptured(object? sender, CameraFrameCapturedEventArgs e)
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
                StatusFoto = "Centralize o objeto e clique em \"Tirar foto\".";
            }
        });
    }

    private void OnPreviewErroCaptura(object? sender, CameraErrorEventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            StatusFoto = e.Type switch
            {
                CameraErrorType.DeviceDisconnected => "A câmera foi desconectada.",
                CameraErrorType.DeviceBusy => "A câmera está em uso por outra tela ou aplicativo.",
                CameraErrorType.AccessDenied => "O Windows negou o acesso à câmera.",
                _ => "Não foi possível exibir a câmera selecionada."
            };
            PararPreview();
        });
    }

    private void TirarFoto()
    {
        if (SelectedPerfil is null || _previewBitmap is null) return;

        PerfilAlvo perfil = SelectedPerfil;

        try
        {
            string caminho = SalvarFotoReferencia(perfil.Id, _previewBitmap);
            _perfilAlvoService.DefinirFotoReferencia(perfil, caminho);
            AtualizarPreviewFoto();

            StatusFoto = "Foto capturada com sucesso.";
            _logger.Success($"Foto de referência capturada para o perfil \"{perfil.Nome}\" ({caminho}).");
        }
        catch (Exception ex)
        {
            StatusFoto = $"Não foi possível salvar a foto: {ex.Message}";
            _logger.Error($"Falha ao salvar foto de referência do perfil \"{perfil.Nome}\": {ex.Message}");
        }
    }

    private static string SalvarFotoReferencia(int perfilId, BitmapSource quadroAtual)
    {
        string pasta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RadarTorres", "PerfisAlvo", "Fotos");
        Directory.CreateDirectory(pasta);

        string caminho = Path.Combine(pasta, $"perfil-{perfilId}-{DateTime.Now:yyyyMMdd-HHmmss}.png");

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(quadroAtual));
        using FileStream stream = File.Create(caminho);
        encoder.Save(stream);

        return caminho;
    }

    private void PararPreview()
    {
        if (_captureService is null)
        {
            IsStreaming = false;
            _previewBitmap = null;
            OnPropertyChanged(nameof(CurrentFrame));
            return;
        }

        _captureService.FrameCaptured -= OnPreviewFrameCaptured;
        _captureService.ErrorOccurred -= OnPreviewErroCaptura;
        _captureService.Stop();
        _captureService.Dispose();
        _captureService = null;

        IsStreaming = false;
        _previewBitmap = null;
        OnPropertyChanged(nameof(CurrentFrame));
    }

    public void Dispose() => PararPreview();
}
