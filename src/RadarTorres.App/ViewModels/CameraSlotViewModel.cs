using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RadarTorres.App.Configuration;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// Um dos até 4 painéis de câmera do módulo "Câmeras" (<see cref="CamerasViewModel"/>): detecta
/// as câmeras disponíveis para ESTE painel, permite selecionar uma e visualizar em tempo real o
/// que ela está captando. Cada instância possui sua própria <see cref="ICameraCaptureService"/>
/// (criada pelo <see cref="ICameraCaptureServiceFactory"/> do painel-pai), então iniciar, parar
/// ou uma falha neste painel nunca afeta os demais.
/// </summary>
/// <remarks>
/// <para>
/// Além da visualização, cada painel roda a DETECÇÃO por visão na sua própria câmera (um
/// <see cref="AnalisadorQuadros"/> por painel, portanto em paralelo com os outros) e registra as
/// ocorrências no quadrante atribuído a essa câmera — é o que permite ao mesmo alvo cadastrado
/// originar uma ocorrência por quadrante. A detecção só roda quando o painel está transmitindo e
/// a câmera tem quadrante atribuído; a atribuição é feita aqui mesmo, no seletor de quadrante.
/// </para>
/// <para>
/// Não decide sozinho quais câmeras aparecem na sua lista: <see cref="CamerasViewModel"/> chama
/// <see cref="UpdateAvailableCameras"/> sempre que a lista de dispositivos do Windows muda ou que
/// algum painel (deste ou de outro) começa/termina de usar uma câmera — é assim que a mesma
/// câmera física fica indisponível para os demais painéis enquanto estiver em uso (Requisito de
/// exclusividade do módulo "Câmeras").
/// </para>
/// </remarks>
public sealed class CameraSlotViewModel : ViewModelBase, IDisposable
{
    private readonly ICameraCaptureService _cameraService;
    private readonly ILocalizationService _localizationService;
    private readonly ICameraQuadranteService _cameraQuadranteService;
    private readonly IOcorrenciaDeteccaoService _ocorrenciaService;
    private readonly IPerfilAlvoService _perfilAlvoService;
    private readonly AnalisadorQuadros _analisador;
    private readonly Dispatcher _dispatcher;

    private WriteableBitmap? _frameBitmap;

    public CameraSlotViewModel(
        int slotNumber,
        ICameraCaptureService cameraService,
        ILocalizationService localizationService,
        ICameraQuadranteService cameraQuadranteService,
        IOcorrenciaDeteccaoService ocorrenciaService,
        IPerfilAlvoService perfilAlvoService,
        IAlvoVisualService alvoVisualService,
        ILoggingService logger)
    {
        SlotNumber = slotNumber;
        _cameraService = cameraService;
        _localizationService = localizationService;
        _cameraQuadranteService = cameraQuadranteService;
        _ocorrenciaService = ocorrenciaService;
        _perfilAlvoService = perfilAlvoService;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _analisador = new AnalisadorQuadros(alvoVisualService, logger);
        _analisador.QuadroAvaliado += OnQuadroAvaliado;

        StartCommand = new RelayCommand(StartSelected, () => SelectedCamera is not null && !IsBusy);
        StopCommand = new RelayCommand(StopStreaming, () => IsBusy);
        DefinirQuadranteCommand = new RelayCommand(DefinirQuadrante, () => SelectedCamera is not null);

        _cameraService.FrameCaptured += OnFrameCaptured;
        _cameraService.ErrorOccurred += OnErrorOccurred;
        _cameraService.Stopped += OnStopped;

        StatusMessage = _localizationService["Cameras.NenhumaEncontrada"];
    }

    /// <summary>Posição do painel na grade 2x2 (1 a 4) — usada apenas para exibição ("Câmera 1", "Câmera 2", ...).</summary>
    public int SlotNumber { get; }

    /// <summary>Câmeras que este painel pode selecionar agora — já excluindo as em uso por outros painéis (ver <see cref="UpdateAvailableCameras"/>).</summary>
    public IReadOnlyList<CameraDevice> AvailableCameras { get; private set; } = Array.Empty<CameraDevice>();

    private CameraDevice? _selectedCamera;
    public CameraDevice? SelectedCamera
    {
        get => _selectedCamera;
        set
        {
            CameraDevice? anterior = _selectedCamera;

            if (SetProperty(ref _selectedCamera, value))
            {
                if (anterior is not null) _ocorrenciaService.EncerrarCamera(anterior.Id, $"câmera trocada no painel {SlotNumber}");

                // Mostra o quadrante JÁ salvo para a câmera recém-escolhida, em vez de manter na
                // tela a escolha feita para a câmera anterior.
                _quadranteSelecionado = value is null ? Quadrant.None : _cameraQuadranteService.ObterQuadrante(value.Id);
                OnPropertyChanged(nameof(QuadranteSelecionado));
                OnPropertyChanged(nameof(TemQuadrante));
                OnPropertyChanged(nameof(QuadranteDescricao));

                RelayCommand.RaiseCanExecuteChangedForAll();
            }
        }
    }

    public bool HasCameras => AvailableCameras.Count > 0;

    /// <summary>
    /// <c>true</c> desde o instante em que <see cref="StartCommand"/> é acionado até a captura
    /// realmente parar (<see cref="OnStopped"/>) ou falhar ao iniciar (<see cref="OnErrorOccurred"/>).
    /// É este estado — não apenas <see cref="IsStreaming"/> — que reserva a câmera para este
    /// painel: sem isso, dois painéis poderiam tentar iniciar a mesma câmera na janela de tempo
    /// entre clicar "Iniciar" e chegar o primeiro quadro.
    /// </summary>
    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RelayCommand.RaiseCanExecuteChangedForAll();
            }
        }
    }

    private bool _isStreaming;
    public bool IsStreaming
    {
        get => _isStreaming;
        private set => SetProperty(ref _isStreaming, value);
    }

    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Quadro atual pronto para binding em um <c>Image.Source</c>; <c>null</c> enquanto não há captura ativa.</summary>
    public ImageSource? CurrentFrame => _frameBitmap;

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }

    /// <summary>Salva o quadrante escolhido para a câmera selecionada neste painel.</summary>
    public RelayCommand DefinirQuadranteCommand { get; }

    // ------------------------------------------------------------- Quadrante monitorado

    /// <summary>Opções do seletor de quadrante — Q1..Q4 mais "nenhum" (<see cref="Quadrant.None"/>, remove a atribuição).</summary>
    public IReadOnlyList<Quadrant> QuadrantesDisponiveis { get; } =
        new[] { Quadrant.None, Quadrant.Q1, Quadrant.Q2, Quadrant.Q3, Quadrant.Q4 };

    private Quadrant _quadranteSelecionado = Quadrant.None;

    /// <summary>Quadrante escolhido no seletor (só vale depois de <see cref="DefinirQuadranteCommand"/>).</summary>
    public Quadrant QuadranteSelecionado
    {
        get => _quadranteSelecionado;
        set => SetProperty(ref _quadranteSelecionado, value);
    }

    /// <summary>Quadrante efetivamente salvo para a câmera selecionada (<see cref="Quadrant.None"/> se nenhum).</summary>
    public Quadrant QuadranteSalvo => SelectedCamera is null
        ? Quadrant.None
        : _cameraQuadranteService.ObterQuadrante(SelectedCamera.Id);

    public bool TemQuadrante => QuadranteSalvo != Quadrant.None;

    /// <summary>Texto de apoio: quadrante salvo e o que isso implica para a detecção neste painel.</summary>
    public string QuadranteDescricao => SelectedCamera is null
        ? "Escolha uma câmera para atribuir um quadrante."
        : TemQuadrante
            ? $"Monitorando {QuadrantHelper.ToDisplayLabel(QuadranteSalvo)} — as ocorrências detectadas aqui são registradas nesse quadrante."
            : "Sem quadrante atribuído: a detecção deste painel fica desligada até você definir um quadrante.";

    private string? _statusDeteccao;

    /// <summary>Última informação da detecção por visão deste painel.</summary>
    public string? StatusDeteccao
    {
        get => _statusDeteccao;
        private set => SetProperty(ref _statusDeteccao, value);
    }

    /// <summary>
    /// Disparado sempre que este painel começa ou termina de reservar uma câmera (<see cref="IsBusy"/>
    /// muda) — o <see cref="CamerasViewModel"/> escuta este evento em todos os painéis para
    /// recalcular, para cada um, quais câmeras continuam disponíveis.
    /// </summary>
    public event EventHandler? ReservationChanged;

    /// <summary>
    /// Recebe a lista completa de câmeras do Windows e o conjunto de identificadores reservados
    /// pelos OUTROS painéis, e recalcula <see cref="AvailableCameras"/> para este painel. Se a
    /// câmera atualmente selecionada (mas ainda não em uso) deixar de estar disponível — foi
    /// desconectada ou passou a ser usada por outro painel — a seleção é limpa.
    /// </summary>
    /// <param name="refreshIdleStatus">
    /// Quando <c>true</c> (atualização de lista de dispositivos — refresh manual, navegação ou
    /// construção), também recalcula <see cref="StatusMessage"/>/<see cref="HasError"/> deste
    /// painel se ele estiver ocioso. Quando <c>false</c> (recálculo disparado apenas porque OUTRO
    /// painel começou/terminou de reservar uma câmera), preserva o status atual — do contrário,
    /// uma falha reportada por este painel (<see cref="OnErrorOccurred"/>) seria apagada assim
    /// que outro painel qualquer parasse de usar sua própria câmera.
    /// </param>
    internal void UpdateAvailableCameras(IReadOnlyList<CameraDevice> allDevices, IReadOnlySet<string> reservedByOtherSlots, bool refreshIdleStatus = true)
    {
        List<CameraDevice> filtered = allDevices.Where(d => !reservedByOtherSlots.Contains(d.Id)).ToList();

        AvailableCameras = filtered;
        OnPropertyChanged(nameof(AvailableCameras));
        OnPropertyChanged(nameof(HasCameras));

        if (SelectedCamera is not null)
        {
            CameraDevice? stillAvailable = filtered.FirstOrDefault(d => d.Id == SelectedCamera.Id);
            if (stillAvailable is null)
            {
                if (!IsBusy) SelectedCamera = null; // idle e sumiu da lista: limpa a seleção
            }
            else
            {
                SelectedCamera = stillAvailable; // mesma câmera, mas mantém a referência mais recente
            }
        }
        else if (!IsBusy)
        {
            SelectedCamera = filtered.FirstOrDefault();
        }

        if (refreshIdleStatus && !IsBusy)
        {
            HasError = false;
            StatusMessage = HasCameras
                ? _localizationService["Cameras.SelecioneParaIniciar"]
                : _localizationService["Cameras.NenhumaEncontrada"];
        }

        RelayCommand.RaiseCanExecuteChangedForAll();
    }

    private void StartSelected()
    {
        if (SelectedCamera is null || IsBusy) return;

        HasError = false;
        IsBusy = true;
        StatusMessage = _localizationService["Cameras.Iniciando"];
        ReservationChanged?.Invoke(this, EventArgs.Empty);

        _analisador.Reiniciar();
        _cameraService.Start(SelectedCamera);
    }

    private void DefinirQuadrante()
    {
        if (SelectedCamera is null) return;

        ResultadoAtribuicaoQuadrante resultado = _cameraQuadranteService.Atribuir(SelectedCamera, QuadranteSelecionado);

        StatusDeteccao = resultado.Sucesso
            ? QuadranteSelecionado == Quadrant.None
                ? "Quadrante removido desta câmera."
                : $"Câmera responsável pelo quadrante {QuadrantHelper.ToDisplayLabel(QuadranteSelecionado)}."
            : resultado.Erro;

        OnPropertyChanged(nameof(QuadranteSalvo));
        OnPropertyChanged(nameof(TemQuadrante));
        OnPropertyChanged(nameof(QuadranteDescricao));
    }

    private void StopStreaming() => _cameraService.Stop();

    /// <summary>Libera a câmera deste painel (chamado ao sair da tela ou fechar o aplicativo) sem esperar o usuário clicar em "Parar".</summary>
    public void ReleaseCamera() => _cameraService.Stop();

    private void OnFrameCaptured(object? sender, CameraFrameCapturedEventArgs e)
    {
        RunOnUi(() =>
        {
            if (_frameBitmap is null || _frameBitmap.PixelWidth != e.Width || _frameBitmap.PixelHeight != e.Height)
            {
                _frameBitmap = new WriteableBitmap(e.Width, e.Height, 96, 96, PixelFormats.Bgr24, null);
                OnPropertyChanged(nameof(CurrentFrame));
            }

            _frameBitmap.WritePixels(new Int32Rect(0, 0, e.Width, e.Height), e.PixelsBgr24, e.Stride, 0);

            if (!IsStreaming)
            {
                IsStreaming = true;
                HasError = false;
                StatusMessage = _localizationService["Cameras.Visualizando"];
            }
        });

        // Detecção só com quadrante atribuído: sem ele não há onde registrar a ocorrência, e
        // rodar a inferência à toa tiraria tempo de CPU dos painéis que estão configurados.
        if (TemQuadrante) _analisador.Submeter(e);
    }

    /// <summary>
    /// Fim de uma rodada de detecção deste painel (thread de trabalho). Registra a ocorrência no
    /// quadrante da câmera, deixando ao rastreamento a decisão de abrir registro novo ou apenas
    /// continuar o acompanhamento já aberto.
    /// </summary>
    private void OnQuadroAvaliado(object? sender, AvaliacaoQuadroEventArgs e)
    {
        CameraDevice? camera = SelectedCamera;
        if (camera is null) return;

        if (e.Avaliacao.Resultado is null)
        {
            _ocorrenciaService.RegistrarRodadaSemDeteccao(DateTime.Now);

            string semAlvo = _perfilAlvoService.PerfilAtivo is null
                ? "Nenhum perfil de alvo ativo."
                : e.Avaliacao.TotalDeteccoes == 0
                    ? "Nenhuma região candidata neste quadro."
                    : $"{e.Avaliacao.TotalDeteccoes} região(ões) avaliada(s), nenhuma bateu com o perfil ativo (melhor: {e.Avaliacao.MelhorPontuacao:P0}).";

            RunOnUi(() => StatusDeteccao = semAlvo);
            return;
        }

        ResultadoRegistroOcorrencia registro = _ocorrenciaService.RegistrarDeteccao(new ContextoDeteccao(
            camera,
            e.Avaliacao.Resultado.Perfil,
            e.Avaliacao.Resultado,
            e.Avaliacao.MelhorPontuacao,
            e.Largura,
            // Este painel não acompanha o modo de operação (ele é estado da tela de
            // Monitoramento): a ocorrência fica sem essa anotação em vez de receber um valor
            // presumido.
            Modo: null));

        string texto = registro switch
        {
            { Criada: true, Ocorrencia: { } nova } =>
                $"Alvo \"{e.Avaliacao.Resultado.Perfil.Nome}\" a {e.Avaliacao.Resultado.DistanciaMetros:0.0} m — ocorrência #{nova.Id} aberta em {QuadrantHelper.ToDisplayLabel(registro.Quadrante)}.",
            { AguardandoConfirmacao: true } =>
                $"Alvo \"{e.Avaliacao.Resultado.Perfil.Nome}\" identificado — confirmando antes de registrar…",
            _ =>
                $"Alvo \"{e.Avaliacao.Resultado.Perfil.Nome}\" em acompanhamento ({QuadrantHelper.ToDisplayLabel(registro.Quadrante)}) — sem novo registro."
        };

        RunOnUi(() => StatusDeteccao = texto);
    }



    private void OnErrorOccurred(object? sender, CameraErrorEventArgs e)
    {
        RunOnUi(() =>
        {
            HasError = true;
            StatusMessage = e.Type switch
            {
                CameraErrorType.DeviceDisconnected => _localizationService["Cameras.Erro.Desconectada"],
                CameraErrorType.DeviceBusy => _localizationService["Cameras.Erro.Ocupada"],
                CameraErrorType.AccessDenied => _localizationService["Cameras.Erro.AcessoNegado"],
                _ => _localizationService["Cameras.Erro.Desconhecido"]
            };

            // Falha ao iniciar (sem chegar a rodar) não dispara Stopped — libera a reserva aqui
            // para que este e outros painéis possam tentar novamente/usar a câmera imediatamente.
            if (IsBusy && !_cameraService.IsRunning)
            {
                IsBusy = false;
                ReservationChanged?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    private void OnStopped(object? sender, EventArgs e)
    {
        if (SelectedCamera is not null)
        {
            _ocorrenciaService.EncerrarCamera(SelectedCamera.Id, $"câmera parada no painel {SlotNumber}");
        }

        RunOnUi(() =>
        {
            IsBusy = false;
            IsStreaming = false;
            _frameBitmap = null;
            OnPropertyChanged(nameof(CurrentFrame));

            if (!HasError)
            {
                StatusMessage = HasCameras
                    ? _localizationService["Cameras.SelecioneParaIniciar"]
                    : _localizationService["Cameras.NenhumaEncontrada"];
            }

            ReservationChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private void RunOnUi(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.BeginInvoke(action);
    }

    public void Dispose()
    {
        _cameraService.FrameCaptured -= OnFrameCaptured;
        _cameraService.ErrorOccurred -= OnErrorOccurred;
        _cameraService.Stopped -= OnStopped;
        _cameraService.Stop();
        _cameraService.Dispose();

        _analisador.QuadroAvaliado -= OnQuadroAvaliado;
        _analisador.Dispose();
    }
}
