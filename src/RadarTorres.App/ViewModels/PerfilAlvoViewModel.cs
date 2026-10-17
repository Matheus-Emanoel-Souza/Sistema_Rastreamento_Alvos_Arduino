using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da tela "Perfis de Alvo": cadastra, edita e remove os perfis (cor + formato +
/// dimensões + fotografias) que a detecção por visão computacional deve travar, e escolhe qual
/// está ativo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Formulário único para criar e editar.</b> O painel direito trabalha sempre sobre um
/// rascunho (nome, cor, formato, dimensões e a lista <see cref="FormFotos"/>), que é uma CÓPIA
/// das fotos do perfil quando se está editando — nada é gravado até <see cref="SalvarCommand"/>.
/// É o que permite trocar fotos e marcações e desistir no meio sem deixar o perfil num estado
/// intermediário. Editar nunca cria registro novo: o serviço atualiza o perfil pelo
/// <see cref="PerfilAlvo.Id"/>, preservando o vínculo com o histórico de detecções.
/// </para>
/// <para>
/// <b>Mínimo de fotografias.</b> O botão de cadastrar fica desabilitado enquanto não houver
/// <see cref="PerfilAlvo.MinimoFotos"/> fotos válidas, e <see cref="MensagemFotos"/> diz quantas
/// faltam. A checagem de verdade é no serviço (<see cref="IPerfilAlvoService.Add"/>) — aqui é
/// conveniência para o usuário, não a regra.
/// </para>
/// <para>
/// A câmera usada para as fotos é a mesma infraestrutura do módulo "Câmeras"
/// (<see cref="ICameraCaptureServiceFactory"/>), em sessão própria: se o dispositivo já estiver
/// em uso em outro lugar, o sistema operacional recusa o acesso e isso aparece como erro normal
/// de captura, mesmo tratamento já usado em <see cref="CameraSlotViewModel"/>.
/// </para>
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

    public bool PodeGerenciar => _permissionService.PodeGerenciarPerfisAlvo(_authService.CurrentUser?.Perfil ?? PerfilUsuario.Visualizador);

    public RelayCommand NovoCommand { get; }
    public RelayCommand SalvarCommand { get; }
    public RelayCommand EditarCommand { get; }
    public RelayCommand CancelarEdicaoCommand { get; }
    public RelayCommand SelecionarAtivoCommand { get; }
    public RelayCommand RemoverCommand { get; }
    public RelayCommand AtualizarCamerasCommand { get; }
    public RelayCommand TirarFotoCommand { get; }
    public RelayCommand RemoverFotoCommand { get; }
    public RelayCommand LimparRecorteCommand { get; }

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
        SalvarCommand = new RelayCommand(_ => Salvar(), _ => PodeGerenciar && PodeSalvar);
        EditarCommand = new RelayCommand(_ => IniciarEdicao(), _ => PodeGerenciar && SelectedPerfil is not null);
        CancelarEdicaoCommand = new RelayCommand(_ => IniciarNovo(), _ => PodeGerenciar && EmEdicao);
        SelecionarAtivoCommand = new RelayCommand(_ => SelecionarAtivo(), _ => PodeGerenciar && SelectedPerfil is { Selecionado: false });
        RemoverCommand = new RelayCommand(_ => Remover(), _ => PodeGerenciar && SelectedPerfil is not null);
        AtualizarCamerasCommand = new RelayCommand(_ => AtualizarCameras());
        TirarFotoCommand = new RelayCommand(_ => TirarFoto(), _ => PodeGerenciar && IsStreaming);
        RemoverFotoCommand = new RelayCommand(foto => RemoverFoto(foto as FotoReferenciaAlvo), foto => PodeGerenciar && foto is FotoReferenciaAlvo);
        LimparRecorteCommand = new RelayCommand(_ => LimparRecorte(), _ => PodeGerenciar && TemRecorte);

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

    // ---------------------------------------------------------------- Seleção na lista

    private PerfilAlvo? _selectedPerfil;
    public PerfilAlvo? SelectedPerfil
    {
        get => _selectedPerfil;
        set
        {
            if (SetProperty(ref _selectedPerfil, value))
            {
                RelayCommand.RaiseCanExecuteChangedForAll();
            }
        }
    }

    // ---------------------------------------------------------------- Formulário (criar/editar)

    private int? _editandoId;

    /// <summary><c>true</c> quando o formulário está alterando um perfil já cadastrado, não criando outro.</summary>
    public bool EmEdicao => _editandoId is not null;

    public string TituloFormulario => EmEdicao
        ? $"Editando o alvo #{_editandoId} — as alterações preservam o histórico de detecções"
        : "Novo alvo";

    public string TextoBotaoSalvar => EmEdicao ? "Salvar alterações" : "Cadastrar alvo";

    private string _formNome = string.Empty;
    public string FormNome
    {
        get => _formNome;
        set { if (SetProperty(ref _formNome, value)) AtualizarValidacao(); }
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
        set
        {
            if (!SetProperty(ref _formFormato, value)) return;

            OnPropertyChanged(nameof(EhRetangular));
            OnPropertyChanged(nameof(RotuloDimensaoUnica));
            AtualizarValidacao();
        }
    }

    /// <summary>Retangular pede duas dimensões independentes; os outros formatos, uma só.</summary>
    public bool EhRetangular => FormFormato == FormatoAlvo.Retangular;

    /// <summary>Rótulo da dimensão única conforme o formato — lado (quadrado) ou diâmetro (redondo).</summary>
    public string RotuloDimensaoUnica => FormFormato == FormatoAlvo.Redondo
        ? "Diâmetro do objeto (cm)"
        : "Lado do objeto (cm)";

    private string _formTamanhoCm = string.Empty;
    public string FormTamanhoCm
    {
        get => _formTamanhoCm;
        set { if (SetProperty(ref _formTamanhoCm, value)) AtualizarValidacao(); }
    }

    private string _formLarguraCm = string.Empty;

    /// <summary>Largura (dimensão horizontal) em centímetros — só usada quando <see cref="EhRetangular"/>.</summary>
    public string FormLarguraCm
    {
        get => _formLarguraCm;
        set { if (SetProperty(ref _formLarguraCm, value)) AtualizarValidacao(); }
    }

    private string _formAlturaCm = string.Empty;

    /// <summary>Altura (dimensão vertical) em centímetros — só usada quando <see cref="EhRetangular"/>.</summary>
    public string FormAlturaCm
    {
        get => _formAlturaCm;
        set { if (SetProperty(ref _formAlturaCm, value)) AtualizarValidacao(); }
    }

    /// <summary>Fotografias do rascunho atual — cópia de trabalho, gravada só ao salvar.</summary>
    public ObservableCollection<FotoReferenciaAlvo> FormFotos { get; } = new();

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

    /// <summary>Quantas fotografias do rascunho ainda faltam para atingir o mínimo (0 se já atingiu).</summary>
    public int FotosFaltantes => Math.Max(0, PerfilAlvo.MinimoFotos - FormFotos.Count);

    /// <summary>Texto que diz onde o rascunho está em relação à exigência de fotografias.</summary>
    public string MensagemFotos => EmEdicao
        ? FormFotos.Count == 0
            ? "Este alvo está sem fotografias — o reconhecimento cai para cor/formato."
            : $"{FormFotos.Count} fotografia(s). Na edição, o mínimo de {PerfilAlvo.MinimoFotos} não é exigido."
        : FotosFaltantes == 0
            ? $"{FormFotos.Count} fotografias — mínimo atingido."
            : $"{FormFotos.Count} de {PerfilAlvo.MinimoFotos} fotografias — faltam {FotosFaltantes} para cadastrar.";

    /// <summary>
    /// Se o botão de salvar pode ser acionado. Espelha as regras de
    /// <see cref="IPerfilAlvoService"/> para o usuário não descobrir o problema só depois de
    /// clicar; a recusa definitiva continua sendo do serviço.
    /// </summary>
    public bool PodeSalvar
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FormNome)) return false;
            if (!EmEdicao && FotosFaltantes > 0) return false;

            return EhRetangular
                ? TentarLer(FormLarguraCm, out _) && TentarLer(FormAlturaCm, out _)
                : TentarLer(FormTamanhoCm, out _);
        }
    }

    private void AtualizarValidacao()
    {
        OnPropertyChanged(nameof(PodeSalvar));
        OnPropertyChanged(nameof(FotosFaltantes));
        OnPropertyChanged(nameof(MensagemFotos));
        RelayCommand.RaiseCanExecuteChangedForAll();
    }

    private void IniciarNovo()
    {
        _editandoId = null;
        FormNome = string.Empty;
        FormCor = CorAlvo.Vermelho;
        FormFormato = FormatoAlvo.Quadrado;
        FormTamanhoCm = string.Empty;
        FormLarguraCm = string.Empty;
        FormAlturaCm = string.Empty;
        MensagemFormulario = null;
        SucessoFormulario = false;

        FormFotos.Clear();
        FotoSelecionada = null;

        OnPropertyChanged(nameof(EmEdicao));
        OnPropertyChanged(nameof(TituloFormulario));
        OnPropertyChanged(nameof(TextoBotaoSalvar));
        AtualizarValidacao();
    }

    /// <summary>Carrega o perfil selecionado no formulário para alteração (ver <see cref="EmEdicao"/>).</summary>
    private void IniciarEdicao()
    {
        if (SelectedPerfil is not { } perfil) return;

        _editandoId = perfil.Id;
        FormNome = perfil.Nome;
        FormCor = perfil.Cor;
        FormFormato = perfil.Formato;

        FormTamanhoCm = perfil.Formato == FormatoAlvo.Retangular
            ? string.Empty
            : Formatar(perfil.TamanhoCm);

        // Perfil retangular antigo (só TamanhoCm) chega aqui sem largura/altura: os campos ficam
        // vazios e a validação exige que o usuário informe as duas — é o momento natural de
        // detalhar a dimensão, e o valor antigo continua gravado enquanto ele não salvar.
        FormLarguraCm = perfil.LarguraCm is { } largura ? Formatar(largura) : string.Empty;
        FormAlturaCm = perfil.AlturaCm is { } altura ? Formatar(altura) : string.Empty;

        FormFotos.Clear();
        foreach (FotoReferenciaAlvo foto in perfil.ClonarFotos())
        {
            FormFotos.Add(foto);
        }
        FotoSelecionada = FormFotos.FirstOrDefault();

        MensagemFormulario = perfil.Formato == FormatoAlvo.Retangular && (perfil.LarguraCm is null || perfil.AlturaCm is null)
            ? $"Este alvo retangular foi cadastrado com uma dimensão única ({Formatar(perfil.TamanhoCm)} cm). Informe largura e altura para concluir."
            : null;
        SucessoFormulario = false;

        OnPropertyChanged(nameof(EmEdicao));
        OnPropertyChanged(nameof(TituloFormulario));
        OnPropertyChanged(nameof(TextoBotaoSalvar));
        AtualizarValidacao();
    }

    private void Salvar()
    {
        SucessoFormulario = false;

        if (!MontarDimensoes(out DimensoesAlvo dimensoes, out string? erroDimensao))
        {
            MensagemFormulario = erroDimensao;
            return;
        }

        List<FotoReferenciaAlvo> fotos = FormFotos.ToList();

        bool eraEdicao = EmEdicao;

        ResultadoPerfilAlvo resultado = eraEdicao
            ? AtualizarPerfilEmEdicao(dimensoes, fotos)
            : _perfilAlvoService.Add(FormNome, FormCor, FormFormato, dimensoes, fotos);

        if (!resultado.Sucesso)
        {
            MensagemFormulario = resultado.Erro;
            return;
        }

        PerfilAlvo? salvo = resultado.Perfil;

        IniciarNovo();
        SelectedPerfil = salvo;

        MensagemFormulario = eraEdicao ? "Alterações salvas." : "Alvo cadastrado com sucesso.";
        SucessoFormulario = true;
    }

    private ResultadoPerfilAlvo AtualizarPerfilEmEdicao(DimensoesAlvo dimensoes, List<FotoReferenciaAlvo> fotos)
    {
        PerfilAlvo? perfil = Perfis.FirstOrDefault(p => p.Id == _editandoId);

        return perfil is null
            ? ResultadoPerfilAlvo.Falha("O alvo que estava sendo editado não está mais cadastrado.")
            : _perfilAlvoService.Atualizar(perfil, FormNome, FormCor, FormFormato, dimensoes, fotos);
    }

    private bool MontarDimensoes(out DimensoesAlvo dimensoes, out string? erro)
    {
        dimensoes = new DimensoesAlvo(null, null, null);

        if (EhRetangular)
        {
            if (!TentarLer(FormLarguraCm, out double largura) || !TentarLer(FormAlturaCm, out double altura))
            {
                erro = "Informe largura e altura em centímetros (maiores que zero) para um objeto retangular.";
                return false;
            }

            dimensoes = DimensoesAlvo.Retangulo(largura, altura);
        }
        else
        {
            if (!TentarLer(FormTamanhoCm, out double tamanho))
            {
                erro = $"Informe {(FormFormato == FormatoAlvo.Redondo ? "o diâmetro" : "o lado")} do objeto em centímetros (maior que zero).";
                return false;
            }

            dimensoes = DimensoesAlvo.Unica(tamanho);
        }

        erro = null;
        return true;
    }

    /// <summary>
    /// Aceita vírgula e ponto como separador decimal: o usuário digita no teclado em pt-BR, mas
    /// o campo é texto livre e colar um valor com ponto é comum.
    /// </summary>
    private static bool TentarLer(string? texto, out double valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        string normalizado = texto.Trim().Replace(',', '.');
        return double.TryParse(normalizado, NumberStyles.Float, CultureInfo.InvariantCulture, out valor) && valor > 0;
    }

    private static string Formatar(double valor) => valor.ToString("0.##", CultureInfo.InvariantCulture);

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

    // ---------------------------------------------------------------- Fotografias

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

    private FotoReferenciaAlvo? _fotoSelecionada;

    /// <summary>Foto do rascunho exibida na área de marcação do objeto.</summary>
    public FotoReferenciaAlvo? FotoSelecionada
    {
        get => _fotoSelecionada;
        set
        {
            if (!SetProperty(ref _fotoSelecionada, value)) return;

            CarregarFotoSelecionada();
            RelayCommand.RaiseCanExecuteChangedForAll();
        }
    }

    private ImageSource? _fotoSelecionadaPreview;

    /// <summary>Imagem da <see cref="FotoSelecionada"/> (diferente de <see cref="CurrentFrame"/>, que é o quadro ao vivo).</summary>
    public ImageSource? FotoSelecionadaPreview
    {
        get => _fotoSelecionadaPreview;
        private set => SetProperty(ref _fotoSelecionadaPreview, value);
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

    /// <summary>
    /// Captura o quadro atual como uma nova fotografia do rascunho. O arquivo vai para o disco na
    /// hora (a validação e a prévia precisam de um arquivo real), mas só passa a pertencer ao
    /// perfil quando o formulário é salvo.
    /// </summary>
    private void TirarFoto()
    {
        if (_previewBitmap is null) return;

        try
        {
            string caminho = SalvarArquivoFoto(_editandoId, _previewBitmap);

            if (!ValidacaoFotoAlvo.EhValida(caminho, out string motivo))
            {
                StatusFoto = $"A foto capturada não é válida: {motivo}";
                _logger.Warning($"Fotografia descartada no cadastro de alvo: {motivo}");
                return;
            }

            var foto = new FotoReferenciaAlvo { Caminho = caminho, CapturadaEm = DateTime.Now };
            FormFotos.Add(foto);
            FotoSelecionada = foto;

            AtualizarValidacao();

            StatusFoto = !EmEdicao && FotosFaltantes > 0
                ? $"Foto {FormFotos.Count} de {PerfilAlvo.MinimoFotos}. Arraste sobre a foto para marcar o objeto; faltam {FotosFaltantes}."
                : $"Foto {FormFotos.Count} adicionada. Arraste sobre a foto para marcar o objeto a ser observado.";

            _logger.Success($"Fotografia capturada para o cadastro de alvo ({caminho}).");
        }
        catch (Exception ex)
        {
            StatusFoto = $"Não foi possível salvar a foto: {ex.Message}";
            _logger.Error($"Falha ao salvar fotografia de alvo: {ex.Message}");
        }
    }

    /// <remarks>
    /// O arquivo NÃO é apagado do disco: ocorrências já registradas guardam o caminho da foto de
    /// referência (<see cref="ObjetoDetectado.ReferenciaImagem"/>), e apagá-lo deixaria o
    /// histórico apontando para um arquivo inexistente. Remover aqui significa desvincular.
    /// </remarks>
    private void RemoverFoto(FotoReferenciaAlvo? foto)
    {
        if (foto is null) return;
        if (!FormFotos.Remove(foto)) return;

        if (ReferenceEquals(FotoSelecionada, foto)) FotoSelecionada = FormFotos.FirstOrDefault();

        AtualizarValidacao();

        StatusFoto = !EmEdicao && FotosFaltantes > 0
            ? $"Fotografia removida do cadastro — faltam {FotosFaltantes} para o mínimo de {PerfilAlvo.MinimoFotos}."
            : "Fotografia removida do cadastro (o arquivo continua no disco, preservando o histórico).";

        _logger.Info($"Fotografia desvinculada do rascunho de alvo: {Path.GetFileName(foto.Caminho)}");
    }

    private static string SalvarArquivoFoto(int? perfilId, BitmapSource quadroAtual)
    {
        string pasta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RadarTorres", "PerfisAlvo", "Fotos");
        Directory.CreateDirectory(pasta);

        // Nome inclui o perfil quando já existe (edição) e "novo" durante o cadastro, que ainda
        // não tem Id; o sufixo aleatório evita colisão entre duas capturas no mesmo segundo.
        string identificacao = perfilId?.ToString(CultureInfo.InvariantCulture) ?? "novo";
        string caminho = Path.Combine(
            pasta,
            $"perfil-{identificacao}-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}.png");

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(quadroAtual));
        using FileStream stream = File.Create(caminho);
        encoder.Save(stream);

        return caminho;
    }

    // ------------------------------------------------- Marcação do objeto dentro da foto
    //
    // A seleção é guardada em PIXELS DA FOTO, não em unidades da tela: a view desenha a foto
    // dentro de um Canvas do tamanho exato da imagem (ver PerfilAlvoView.xaml), então as
    // coordenadas do mouse já chegam aqui nessa mesma escala e nada precisa ser convertido —
    // o que também mantém a marcação válida se a janela for redimensionada depois.

    private int _fotoLargura;

    /// <summary>Largura em pixels da foto exibida para marcação (0 se não há foto).</summary>
    public int FotoLargura
    {
        get => _fotoLargura;
        private set => SetProperty(ref _fotoLargura, value);
    }

    private int _fotoAltura;

    /// <summary>Altura em pixels da foto exibida para marcação (0 se não há foto).</summary>
    public int FotoAltura
    {
        get => _fotoAltura;
        private set => SetProperty(ref _fotoAltura, value);
    }

    public bool TemRecorte => FotoSelecionada?.Recorte is { EhValido: true };

    public double RecorteX => FotoSelecionada?.Recorte?.X ?? 0;
    public double RecorteY => FotoSelecionada?.Recorte?.Y ?? 0;
    public double RecorteLargura => FotoSelecionada?.Recorte?.Largura ?? 0;
    public double RecorteAltura => FotoSelecionada?.Recorte?.Altura ?? 0;

    /// <summary>
    /// Se a marcação do objeto está disponível agora (há foto carregada e o usuário pode
    /// gerenciar perfis). A view usa isto para só então aceitar o arrasto sobre a foto.
    /// </summary>
    public bool PodeMarcarObjeto => PodeGerenciar && FotoSelecionada is not null && FotoLargura > 0 && FotoAltura > 0;

    /// <summary>
    /// Marca, na foto selecionada, a região que contém o objeto a observar. Recebe as coordenadas
    /// já em pixels da foto; valores fora dela são aparados, e um arrasto sem área (clique solto)
    /// é ignorado para não apagar a marcação anterior por acidente.
    /// </summary>
    public void MarcarObjeto(int x, int y, int largura, int altura)
    {
        if (!PodeMarcarObjeto || FotoSelecionada is not { } foto) return;

        int x1 = Math.Clamp(x, 0, FotoLargura);
        int y1 = Math.Clamp(y, 0, FotoAltura);
        int x2 = Math.Clamp(x + largura, 0, FotoLargura);
        int y2 = Math.Clamp(y + altura, 0, FotoAltura);

        var recorte = new RecorteAlvo(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));

        if (!recorte.EhValido)
        {
            StatusFoto = "Marcação muito pequena — arraste sobre o objeto na foto.";
            return;
        }

        foto.Recorte = recorte;
        NotificarRecorte();

        StatusFoto = EmEdicao
            ? $"Objeto marcado ({recorte.Largura}×{recorte.Altura} px). Clique em \"Salvar alterações\" para gravar."
            : $"Objeto marcado ({recorte.Largura}×{recorte.Altura} px) nesta foto.";
    }

    private void LimparRecorte()
    {
        if (FotoSelecionada is not { } foto) return;

        foto.Recorte = null;
        NotificarRecorte();
        StatusFoto = "Marcação removida desta foto — o sistema vai localizar o objeto nela pela cor cadastrada.";
    }

    private void NotificarRecorte()
    {
        OnPropertyChanged(nameof(TemRecorte));
        OnPropertyChanged(nameof(RecorteX));
        OnPropertyChanged(nameof(RecorteY));
        OnPropertyChanged(nameof(RecorteLargura));
        OnPropertyChanged(nameof(RecorteAltura));
        RelayCommand.RaiseCanExecuteChangedForAll();
    }

    private void CarregarFotoSelecionada()
    {
        string? caminho = FotoSelecionada?.Caminho;

        if (string.IsNullOrEmpty(caminho) || !File.Exists(caminho))
        {
            FotoSelecionadaPreview = null;
            FotoLargura = 0;
            FotoAltura = 0;
            NotificarRecorte();
            OnPropertyChanged(nameof(PodeMarcarObjeto));
            return;
        }

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(caminho, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        FotoSelecionadaPreview = bitmap;
        FotoLargura = bitmap.PixelWidth;
        FotoAltura = bitmap.PixelHeight;

        NotificarRecorte();
        OnPropertyChanged(nameof(PodeMarcarObjeto));
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
