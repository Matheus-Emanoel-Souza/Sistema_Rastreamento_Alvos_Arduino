using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RadarTorres.App.Configuration;
using RadarTorres.App.Data;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
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
    private readonly IObjetoDetectadoRepository _objetoDetectadoRepository;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly ConfiguracoesAplicativo _configuracoes;
    private readonly ILoggingService _logger;

    /// <summary>
    /// Desde quando o alvo está sem ser reconhecido. Usado para medir a latência de aquisição —
    /// quanto o operador espera entre apontar o alvo para a câmera e o sistema travar nele.
    /// É a métrica que interessa de verdade; o tempo de uma rodada isolada só explica o porquê.
    /// </summary>
    private DateTime? _semAlvoDesde = DateTime.Now;

    private ICameraCaptureService? _captureService;
    private WriteableBitmap? _previewBitmap;
    private SystemMode _modoAtual = SystemMode.LigadoApenas;
    private DateTime _ultimoProcessamento = DateTime.MinValue;
    private DateTime _ultimoAlertaEm = DateTime.MinValue;
    private int _processando;

    /// <summary>
    /// Regiões da última rodada de detecção, redesenhadas sobre cada quadro novo. A detecção
    /// roda a cada ~120ms e a câmera entrega ~30 quadros/s, então sem guardar o último resultado
    /// as caixas piscariam: aparecem num quadro e sumiriam nos vários seguintes.
    /// </summary>
    private IReadOnlyList<AlvoVisualCandidato> _ultimosCandidatos = Array.Empty<AlvoVisualCandidato>();

    public ObservableCollection<CameraDevice> AvailableCameras { get; } = new();

    public RelayCommand AtualizarCamerasCommand { get; }

    public DeteccaoCameraViewModel(
        ICameraDeviceEnumerator cameraDeviceEnumerator,
        ICameraCaptureServiceFactory cameraCaptureServiceFactory,
        IAlvoVisualService alvoVisualService,
        IAlertaSonoroService alertaSonoroService,
        IPerfilAlvoService perfilAlvoService,
        IObjetoDetectadoRepository objetoDetectadoRepository,
        IPerformanceMonitor performanceMonitor,
        IConfiguracoesAplicativoRepository configuracoesRepository,
        ILoggingService logger)
    {
        _cameraDeviceEnumerator = cameraDeviceEnumerator;
        _cameraCaptureServiceFactory = cameraCaptureServiceFactory;
        _alvoVisualService = alvoVisualService;
        _alertaSonoroService = alertaSonoroService;
        _perfilAlvoService = perfilAlvoService;
        _objetoDetectadoRepository = objetoDetectadoRepository;
        _performanceMonitor = performanceMonitor;
        _configuracoes = configuracoesRepository.Atual;
        _logger = logger;

        _mostrarCaixasDeteccao = _configuracoes.MostrarCaixasDeteccao;

        AtualizarCamerasCommand = new RelayCommand(_ => AtualizarCameras());

        StatusDeteccao = _configuracoes.IniciarCameraAutomaticamente
            ? "Escolha uma câmera para iniciar a detecção."
            : "Câmera desligada por configuração. Escolha uma câmera na lista para ligá-la.";

        AtualizarCameras();
    }

    /// <summary>Chamado por <see cref="MainViewModel"/> sempre que o modo de operação muda.</summary>
    public void AtualizarModoAtual(SystemMode modo)
    {
        // Anotado no relatório porque o modo decide se o alerta dispara: sem essa marcação, um
        // trecho sem alertas no arquivo é ambíguo entre "não detectou" e "detectou em modo Verde".
        if (_modoAtual != modo) _performanceMonitor.RegistrarEvento($"Modo de operação alterado para {modo}.");

        _modoAtual = modo;
    }

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

    private bool _mostrarCaixasDeteccao;

    /// <summary>
    /// Se as regiões avaliadas são desenhadas sobre o vídeo. Ligado por padrão: é a única forma
    /// prática de o operador conferir que o sistema travou no objeto certo, e não em algo
    /// parecido no fundo da cena.
    /// </summary>
    public bool MostrarCaixasDeteccao
    {
        get => _mostrarCaixasDeteccao;
        set => SetProperty(ref _mostrarCaixasDeteccao, value);
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

        // Sem início automático, nenhuma câmera é pré-selecionada: selecionar já dispara o
        // preview (ver SelectedCamera), que é justamente o que a configuração pede para evitar
        // quando a webcam está sendo usada por outro programa. O usuário escolhe na lista
        // quando quiser ligar.
        CameraDevice? novaSelecao = atual is not null && AvailableCameras.Contains(atual)
            ? atual
            : AvailableCameras.Count > 0 && _configuracoes.IniciarCameraAutomaticamente ? AvailableCameras[0] : null;

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
        _performanceMonitor.RegistrarEvento($"Câmera iniciada: {device.Name}. Perfil ativo: {_perfilAlvoService.PerfilAtivo?.Nome ?? "(nenhum)"}.");
        _semAlvoDesde = DateTime.Now;

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

            if (MostrarCaixasDeteccao) DesenharCaixas(_previewBitmap);

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

            Application.Current?.Dispatcher.BeginInvoke(() => AplicarResultado(avaliacao, width, height));
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
    private void AplicarResultado(AlvoVisualAvaliacao avaliacao, int larguraQuadro, int alturaQuadro)
    {
        _ultimosCandidatos = avaliacao.Candidatos;

        PerfilAlvo? perfilAtivo = _perfilAlvoService.PerfilAtivo;

        RegistrarDesempenho(avaliacao, perfilAtivo, larguraQuadro, alturaQuadro);

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
                StatusDeteccao = $"Visualizando. Nenhuma região candidata neste quadro — nem mancha de {perfilAtivo.Cor}, nem objeto reconhecido pelo detector.";
            }
            else if (avaliacao.UsouFotoReferencia)
            {
                StatusDeteccao = $"Visualizando. {avaliacao.TotalDeteccoes} região(ões) avaliada(s), mas nenhuma bateu com a foto de referência de \"{perfilAtivo.Nome}\" (melhor similaridade: {avaliacao.MelhorPontuacao:P0}, precisa de {AppConfig.Current.VisionSettings.LimiarSimilaridadeFoto:P0}).";
            }
            else
            {
                StatusDeteccao = $"Visualizando. {avaliacao.TotalDeteccoes} região(ões) avaliada(s), mas nenhuma bateu com a cor/formato de \"{perfilAtivo.Nome}\" (melhor correspondência de cor: {avaliacao.MelhorPontuacao:P0}).";
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

            // O alerta sonoro pode ser desligado em Configurações, mas o REGISTRO da detecção
            // não: silenciar o aviso é preferência de ambiente (sala compartilhada, apresentação);
            // deixar de registrar o alvo seria perder o dado do histórico.
            if (_configuracoes.AlertaSonoroHabilitado)
            {
                _alertaSonoroService.Disparar(avaliacao.Resultado.DistanciaMetros);
            }

            _logger.Warning(
                $"Alvo visual identificado a {avaliacao.Resultado.DistanciaMetros:0.0} m " +
                $"(modo {_modoAtual}{(_configuracoes.AlertaSonoroHabilitado ? "" : ", alerta sonoro desligado nas configurações")}).");

            RegistrarObjetoDetectado(avaliacao.Resultado, avaliacao.MelhorPontuacao, larguraQuadro);
        }
    }

    /// <summary>
    /// Persiste o alvo confirmado no histórico de objetos detectados, para a detecção por
    /// câmera aparecer na tela "Objetos Detectados" junto com o que vem do Arduino — até então
    /// ela só mudava o status na tela e tocava o alerta, sem deixar registro nenhum.
    /// </summary>
    /// <remarks>
    /// Gravado dentro do debounce do alerta, e não a cada rodada de detecção, de propósito: com
    /// o alvo parado em frente à câmera a avaliação confirma a cada ~500ms, o que encheria a
    /// tabela com dezenas de linhas por minuto para o mesmo objeto. Uma linha por alerta mantém
    /// o histórico legível e espelha a granularidade do que o operador ouve.
    /// </remarks>
    private void RegistrarObjetoDetectado(AlvoVisualResultado resultado, double pontuacao, int larguraQuadro)
    {
        try
        {
            // A câmera só informa direção horizontal: o ângulo sai da posição do centro da caixa
            // dentro do campo de visão, e a distância vira X/Y no mesmo plano usado pelos alvos
            // do radar (Y para frente da câmera), para as duas origens conviverem na mesma tela.
            double centroXPx = resultado.Deteccao.BoxXPx + (resultado.Deteccao.BoxWidthPx / 2.0);
            double anguloGraus = ((centroXPx / Math.Max(1, larguraQuadro)) - 0.5) * AppConfig.Current.VisionSettings.CameraFovHorizontalGraus;
            double anguloRad = anguloGraus * Math.PI / 180.0;

            double x = resultado.DistanciaMetros * Math.Sin(anguloRad);
            double y = resultado.DistanciaMetros * Math.Cos(anguloRad);

            _objetoDetectadoRepository.Add(new ObjetoDetectado
            {
                Tipo = resultado.Perfil.Nome,
                X = x,
                Y = y,
                Z = null,
                Quadrante = QuadrantHelper.ToDisplayLabel(QuadrantHelper.Determine(x, y)),
                DataHora = DateTime.Now,
                Dispositivo = "Câmera",
                NivelConfianca = pontuacao,
                Observacao = $"Detecção por visão computacional — perfil \"{resultado.Perfil.Nome}\" ({resultado.Perfil.Cor}/{resultado.Perfil.Formato}), {anguloGraus:0.0}° do centro, modo {_modoAtual}.",
                ReferenciaImagem = resultado.Perfil.CaminhoFotoReferencia
            });
        }
        catch (Exception ex)
        {
            // Falhar ao gravar o histórico não pode interromper a detecção nem o alerta — o
            // alerta ao operador vale mais que a linha no banco.
            _logger.Error($"Alvo detectado, mas não foi possível gravar no histórico de objetos detectados: {ex.Message}");
        }
    }

    /// <summary>
    /// Grava a rodada no relatório de desempenho desta execução, fechando a medição da latência
    /// de aquisição quando o alvo volta a ser reconhecido depois de um período sem.
    /// </summary>
    private void RegistrarDesempenho(AlvoVisualAvaliacao avaliacao, PerfilAlvo? perfilAtivo, int larguraQuadro, int alturaQuadro)
    {
        bool identificado = avaliacao.Resultado is not null;
        double? tempoAteIdentificar = null;

        if (identificado)
        {
            // Só fecha a medição se havia de fato um período sem reconhecimento em aberto —
            // em alvo continuamente reconhecido, cada rodada registraria uma latência de ~0 e
            // afundaria a mediana do relatório, escondendo as aquisições reais.
            if (_semAlvoDesde is { } desde)
            {
                tempoAteIdentificar = (DateTime.Now - desde).TotalMilliseconds;
                _semAlvoDesde = null;
            }
        }
        else
        {
            _semAlvoDesde ??= DateTime.Now;
        }

        _performanceMonitor.RegistrarRodada(new RegistroRodada(
            perfilAtivo?.Nome ?? "(nenhum)",
            larguraQuadro,
            alturaQuadro,
            avaliacao.Tempos,
            avaliacao.Candidatos.Count(c => c.Origem == OrigemCandidato.Cor),
            avaliacao.Candidatos.Count(c => c.Origem == OrigemCandidato.Detector),
            identificado,
            avaliacao.MelhorPontuacao,
            avaliacao.Resultado?.DistanciaMetros,
            tempoAteIdentificar));
    }

    /// <summary>
    /// Desenha o contorno de cada região avaliada na última rodada direto sobre o quadro:
    /// <b>verde</b> = alvo aceito, <b>vermelho</b> = objeto que o detector ONNX propôs e foi
    /// recusado, <b>amarelo</b> = mancha da cor do perfil que foi recusada.
    /// </summary>
    /// <remarks>
    /// Desenhado no buffer do próprio <see cref="WriteableBitmap"/>, e não como retângulos num
    /// Canvas sobreposto no XAML, porque a <c>Image</c> usa <c>Stretch="Uniform"</c>: a sobreposição
    /// teria de reproduzir o letterboxing e o fator de escala do controle para alinhar, e
    /// sairia do lugar a cada redimensionamento do card. Pintando nos pixels, o contorno é
    /// parte da imagem e acompanha qualquer escala automaticamente.
    /// </remarks>
    private void DesenharCaixas(WriteableBitmap bitmap)
    {
        if (_ultimosCandidatos.Count == 0) return;

        bitmap.Lock();
        try
        {
            foreach (AlvoVisualCandidato candidato in _ultimosCandidatos)
            {
                (byte azul, byte verde, byte vermelho) = candidato switch
                {
                    { Aceito: true } => ((byte)0, (byte)255, (byte)0),
                    { Origem: OrigemCandidato.Detector } => ((byte)60, (byte)60, (byte)255),
                    _ => ((byte)0, (byte)210, (byte)255)
                };

                DesenharRetangulo(bitmap, candidato.Deteccao, azul, verde, vermelho, candidato.Aceito ? 4 : 2);
            }
        }
        finally
        {
            bitmap.Unlock();
        }
    }

    /// <summary>Pinta o contorno de uma caixa no buffer BGR24 do bitmap. Exige o bitmap já travado (<see cref="WriteableBitmap.Lock"/>).</summary>
    private static void DesenharRetangulo(WriteableBitmap bitmap, VisionDetection caixa, byte azul, byte verde, byte vermelho, int espessura)
    {
        int x0 = Math.Max(0, caixa.BoxXPx);
        int y0 = Math.Max(0, caixa.BoxYPx);
        int x1 = Math.Min(bitmap.PixelWidth, caixa.BoxXPx + caixa.BoxWidthPx);
        int y1 = Math.Min(bitmap.PixelHeight, caixa.BoxYPx + caixa.BoxHeightPx);

        int largura = x1 - x0;
        int altura = y1 - y0;
        if (largura <= 0 || altura <= 0) return;

        // Uma caixa fina não pode ter contorno mais grosso que a metade dela, senão as bordas
        // opostas se cruzariam e a escrita sairia dos limites da própria caixa.
        espessura = Math.Max(1, Math.Min(espessura, Math.Min(largura, altura) / 2));

        byte[] linhaHorizontal = CriarLinha(largura, azul, verde, vermelho);
        byte[] linhaVertical = CriarLinha(espessura, azul, verde, vermelho);

        IntPtr buffer = bitmap.BackBuffer;
        int stride = bitmap.BackBufferStride;
        const int bytesPorPixel = 3;

        for (int e = 0; e < espessura; e++)
        {
            Marshal.Copy(linhaHorizontal, 0, buffer + ((y0 + e) * stride) + (x0 * bytesPorPixel), linhaHorizontal.Length);
            Marshal.Copy(linhaHorizontal, 0, buffer + ((y1 - 1 - e) * stride) + (x0 * bytesPorPixel), linhaHorizontal.Length);
        }

        for (int y = y0; y < y1; y++)
        {
            Marshal.Copy(linhaVertical, 0, buffer + (y * stride) + (x0 * bytesPorPixel), linhaVertical.Length);
            Marshal.Copy(linhaVertical, 0, buffer + (y * stride) + ((x1 - espessura) * bytesPorPixel), linhaVertical.Length);
        }

        bitmap.AddDirtyRect(new Int32Rect(x0, y0, largura, altura));
    }

    private static byte[] CriarLinha(int pixels, byte azul, byte verde, byte vermelho)
    {
        var linha = new byte[pixels * 3];
        for (int i = 0; i < pixels; i++)
        {
            linha[i * 3] = azul;
            linha[(i * 3) + 1] = verde;
            linha[(i * 3) + 2] = vermelho;
        }
        return linha;
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
            _performanceMonitor.RegistrarErro("câmera", $"{e.Type} — {StatusDeteccao}");
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
        _ultimosCandidatos = Array.Empty<AlvoVisualCandidato>();
        OnPropertyChanged(nameof(CurrentFrame));
        UltimaDistanciaMetros = null;
    }

    public void Dispose() => PararPreview();
}
