using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using RadarTorres.App.Configuration;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da aba "Configurações do Arduino": ambiente (localização do Arduino CLI),
/// compilação de sketches e monitor serial em tempo real (ver Docs/COMUNICACAO_ARDUINO.md).
/// </summary>
/// <remarks>
/// <para><b>Reuso da comunicação serial existente</b>: esta tela NÃO cria uma segunda conexão
/// serial concorrente. <see cref="ISerialCommunicationService"/> já é registrado como
/// Singleton no contêiner de DI (ver App.xaml.cs) e é o mesmo objeto usado pela tela de
/// Monitoramento (<see cref="MainViewModel"/>) — ele já é, portanto, o "coordenador central"
/// da porta serial pedido no enunciado. Esta ViewModel apenas se inscreve nos eventos desse
/// serviço compartilhado; se a porta já estiver aberta (por esta aba ou pela tela de
/// Monitoramento) com parâmetros diferentes dos solicitados, <see cref="ConnectAsync"/> pede
/// confirmação ao usuário antes de desconectar e reconectar — nunca derruba uma conexão em
/// uso silenciosamente.</para>
/// </remarks>
public sealed class ArduinoSettingsViewModel : ViewModelBase, INavigationAware, IDisposable
{
    private const int MaxCompileConsoleLines = 4000;
    private const int MaxSerialMonitorLines = 4000;

    private readonly IArduinoCliLocatorService _cliLocator;
    private readonly IArduinoCompilerService _compilerService;
    private readonly IArduinoSettingsRepository _settingsRepository;
    private readonly ISerialCommunicationService _serialService;
    private readonly ILoggingService _logger;
    private readonly Dispatcher _dispatcher;

    private CancellationTokenSource? _compileCts;

    public ArduinoSettingsViewModel(
        IArduinoCliLocatorService cliLocator,
        IArduinoCompilerService compilerService,
        IArduinoSettingsRepository settingsRepository,
        ISerialCommunicationService serialService,
        ILoggingService logger)
    {
        _cliLocator = cliLocator;
        _compilerService = compilerService;
        _settingsRepository = settingsRepository;
        _serialService = serialService;
        _logger = logger;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        foreach (ArduinoBoardOption board in ArduinoBoardCatalog.DefaultBoards)
        {
            AvailableBoards.Add(board);
        }

        AvailableBaudRates = SerialSettings.CommonBaudRates;

        BrowseCliPathCommand = new RelayCommand(_ => BrowseCliPathRequested?.Invoke(this, EventArgs.Empty));
        AutoDetectCommand = new RelayCommand(AutoDetectCli);
        RefreshBoardsAndPortsCommand = new RelayCommand(async () => await RefreshBoardsAndPortsAsync());
        BrowseSketchCommand = new RelayCommand(_ => BrowseSketchRequested?.Invoke(this, EventArgs.Empty));
        CompileCommand = new RelayCommand(async () => await CompileAsync(), () => !IsCompiling && !string.IsNullOrWhiteSpace(SketchPath) && SelectedBoard is not null);
        CancelCompileCommand = new RelayCommand(CancelCompile, _ => IsCompiling);
        ClearCompileConsoleCommand = new RelayCommand(() => CompileOutputLines.Clear());
        ConnectCommand = new RelayCommand(async () => await ConnectAsync(), () => !IsMonitorConnected);
        DisconnectCommand = new RelayCommand(() => _serialService.Disconnect(), () => IsMonitorConnected);
        ClearMonitorConsoleCommand = new RelayCommand(() => { MonitorLines.Clear(); MessageCount = 0; });
        RefreshPortsCommand = new RelayCommand(RefreshPorts);

        _serialService.ConnectionStateChanged += OnConnectionStateChanged;
        _serialService.MessageReceived += OnSerialMessageReceived;
        _serialService.CommunicationError += OnCommunicationError;

        LoadPersistedSettings();
        RefreshPorts();
        DetectCli(persistIfChanged: false);
    }

    public void OnNavigatedTo()
    {
        // Reflete estado atual da porta compartilhada sempre que o usuário volta a esta aba
        // (a conexão pode ter sido aberta/fechada pela tela de Monitoramento nesse meio-tempo).
        RunOnUi(() =>
        {
            MonitorConnectionState = _serialService.State;
            OnPropertyChanged(nameof(IsMonitorConnected));
        });
    }

    // =================================================================== Seção 1 — Ambiente Arduino

    public ObservableCollection<ArduinoBoardOption> AvailableBoards { get; } = new();
    public ObservableCollection<string> AvailablePorts { get; } = new();
    public int[] AvailableBaudRates { get; }

    private string? _cliPath;
    public string? CliPath
    {
        get => _cliPath;
        set
        {
            if (SetProperty(ref _cliPath, value))
            {
                DetectCli(persistIfChanged: true);
            }
        }
    }

    private bool _isCliFound;
    public bool IsCliFound
    {
        get => _isCliFound;
        private set => SetProperty(ref _isCliFound, value);
    }

    private string _cliStatusText = string.Empty;
    public string CliStatusText
    {
        get => _cliStatusText;
        private set => SetProperty(ref _cliStatusText, value);
    }

    private string? _cliVersion;
    public string? CliVersion
    {
        get => _cliVersion;
        private set => SetProperty(ref _cliVersion, value);
    }

    private ArduinoBoardOption? _selectedBoard;
    public ArduinoBoardOption? SelectedBoard
    {
        get => _selectedBoard;
        set
        {
            if (SetProperty(ref _selectedBoard, value))
            {
                OnPropertyChanged(nameof(SelectedBoardName));
                PersistSettings();
                RelayCommand.RaiseCanExecuteChangedForAll();
            }
        }
    }

    public string SelectedBoardName => SelectedBoard?.DisplayName ?? "—";

    private string? _selectedPort;
    public string? SelectedPort
    {
        get => _selectedPort;
        set
        {
            if (SetProperty(ref _selectedPort, value)) PersistSettings();
        }
    }

    private int _selectedBaudRate = 9600;
    public int SelectedBaudRate
    {
        get => _selectedBaudRate;
        set
        {
            if (SetProperty(ref _selectedBaudRate, value)) PersistSettings();
        }
    }

    public RelayCommand BrowseCliPathCommand { get; }
    public RelayCommand AutoDetectCommand { get; }
    public RelayCommand RefreshBoardsAndPortsCommand { get; }
    public RelayCommand RefreshPortsCommand { get; }

    /// <summary>A View escuta este evento e abre um <c>Microsoft.Win32.OpenFileDialog</c> (mínimo code-behind, sem lógica de negócio).</summary>
    public event EventHandler? BrowseCliPathRequested;

    /// <summary>Chamado pela View após o usuário escolher um arquivo no diálogo "Procurar".</summary>
    public void SetCliPathFromDialog(string path) => CliPath = path;

    private void AutoDetectCli(object? _) => DetectCli(persistIfChanged: true);

    private void DetectCli(bool persistIfChanged)
    {
        ArduinoCliInfo info = _cliLocator.Locate(CliPath);
        IsCliFound = info.Found;

        if (!info.Found)
        {
            CliStatusText = "Arduino CLI não encontrado. É necessário para compilar — configure o caminho acima ou instale-o.";
            CliVersion = null;
            RelayCommand.RaiseCanExecuteChangedForAll();
            return;
        }

        if (!string.Equals(_cliPath, info.ExecutablePath, StringComparison.OrdinalIgnoreCase))
        {
            _cliPath = info.ExecutablePath;
            OnPropertyChanged(nameof(CliPath));
        }

        CliStatusText = $"Arduino CLI encontrado ({DescribeSource(info.Source)}).";

        if (persistIfChanged) PersistSettings();

        _ = LoadVersionAsync(info.ExecutablePath!);
        RelayCommand.RaiseCanExecuteChangedForAll();
    }

    private async Task LoadVersionAsync(string cliPath)
    {
        string? version = await _cliLocator.GetVersionAsync(cliPath).ConfigureAwait(false);
        RunOnUi(() => CliVersion = version ?? "—");
    }

    private async Task RefreshBoardsAndPortsAsync()
    {
        RefreshPorts();

        if (IsCliFound && !string.IsNullOrWhiteSpace(CliPath))
        {
            var installed = await _cliLocator.ListInstalledBoardsAsync(CliPath).ConfigureAwait(false);
            RunOnUi(() =>
            {
                ArduinoBoardOption? previouslySelected = SelectedBoard;
                foreach (ArduinoBoardOption board in installed)
                {
                    if (AvailableBoards.All(b => b.Fqbn != board.Fqbn))
                    {
                        AvailableBoards.Add(board);
                    }
                }
                SelectedBoard = previouslySelected ?? AvailableBoards.FirstOrDefault();
                _logger.Info($"{installed.Count} placa(s) adicional(is) do Arduino CLI carregada(s).");
            });
        }
    }

    private void RefreshPorts(object? _ = null)
    {
        string? previous = SelectedPort;
        AvailablePorts.Clear();
        foreach (string port in _serialService.GetAvailablePorts())
        {
            AvailablePorts.Add(port);
        }

        SelectedPort = previous is not null && AvailablePorts.Contains(previous)
            ? previous
            : AvailablePorts.FirstOrDefault();
    }

    private static string DescribeSource(ArduinoCliSource source) => source switch
    {
        ArduinoCliSource.ConfiguracaoSalva => "caminho salvo",
        ArduinoCliSource.PastaDoAplicativo => "pasta do aplicativo",
        ArduinoCliSource.VariavelPath => "PATH do Windows",
        ArduinoCliSource.LocalComumDeInstalacao => "local comum de instalação",
        _ => "origem desconhecida"
    };

    // =================================================================== Seção 2 — Compilação

    public ObservableCollection<ArduinoCliOutputLine> CompileOutputLines { get; } = new();

    private string? _sketchPath;
    public string? SketchPath
    {
        get => _sketchPath;
        set
        {
            if (SetProperty(ref _sketchPath, value))
            {
                PersistSettings();
                RelayCommand.RaiseCanExecuteChangedForAll();
            }
        }
    }

    private bool _isCompiling;
    public bool IsCompiling
    {
        get => _isCompiling;
        private set
        {
            if (SetProperty(ref _isCompiling, value))
            {
                RelayCommand.RaiseCanExecuteChangedForAll();
            }
        }
    }

    private ArduinoCompileStatus? _lastCompileStatus;
    public ArduinoCompileStatus? LastCompileStatus
    {
        get => _lastCompileStatus;
        private set => SetProperty(ref _lastCompileStatus, value);
    }

    private string _compileStatusText = "Aguardando compilação";
    public string CompileStatusText
    {
        get => _compileStatusText;
        private set => SetProperty(ref _compileStatusText, value);
    }

    public RelayCommand BrowseSketchCommand { get; }
    public RelayCommand CompileCommand { get; }
    public RelayCommand CancelCompileCommand { get; }
    public RelayCommand ClearCompileConsoleCommand { get; }

    /// <summary>A View escuta este evento e abre um <c>Microsoft.Win32.OpenFileDialog</c> filtrado para .ino.</summary>
    public event EventHandler? BrowseSketchRequested;

    public void SetSketchPathFromDialog(string path) => SketchPath = path;

    /// <summary>Preenche o sketch inicial com <c>Arduino/ArduinoSimulation.ino</c> quando disponível e nada foi persistido ainda.</summary>
    private string? TryFindDefaultSketch()
    {
        // Procura a partir da pasta do executável subindo diretórios (execução via `dotnet run`
        // fica bin/Debug/net9.0-windows/ dentro do repo; instalado, o .ino não acompanha o
        // instalador — a busca simplesmente não encontra nada, o que é esperado).
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "Arduino", "ArduinoSimulation.ino");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private async Task CompileAsync()
    {
        if (string.IsNullOrWhiteSpace(SketchPath))
        {
            _logger.Warning("Selecione um sketch (.ino) antes de compilar.");
            return;
        }
        if (!File.Exists(SketchPath) && !Directory.Exists(SketchPath))
        {
            SetCompileStatus(ArduinoCompileStatus.Failed, "Sketch não encontrado no caminho informado.");
            return;
        }
        if (SelectedBoard is null)
        {
            _logger.Warning("Selecione uma placa/FQBN antes de compilar.");
            return;
        }
        if (!IsCliFound || string.IsNullOrWhiteSpace(CliPath))
        {
            SetCompileStatus(ArduinoCompileStatus.Failed, "Arduino CLI não encontrado — configure o caminho na seção Ambiente Arduino.");
            return;
        }

        _compileCts = new CancellationTokenSource();
        IsCompiling = true;
        CompileStatusText = "Compilando…";
        LastCompileStatus = null;

        var progress = new Progress<ArduinoCliOutputLine>(line => RunOnUi(() => AppendCompileLine(line)));

        try
        {
            var request = new ArduinoCompileRequest
            {
                CliExecutablePath = CliPath!,
                SketchPath = SketchPath!,
                Fqbn = SelectedBoard.Fqbn,
            };

            ArduinoCompileResult result = await _compilerService.CompileAsync(request, progress, _compileCts.Token).ConfigureAwait(false);

            RunOnUi(() =>
            {
                switch (result.Status)
                {
                    case ArduinoCompileStatus.Success:
                        SetCompileStatus(ArduinoCompileStatus.Success, "Compilação concluída com sucesso.");
                        _logger.Success($"Compilação do sketch '{Path.GetFileName(SketchPath)}' concluída com sucesso.");
                        break;
                    case ArduinoCompileStatus.Cancelled:
                        SetCompileStatus(ArduinoCompileStatus.Cancelled, "Compilação cancelada pelo usuário.");
                        _logger.Warning("Compilação cancelada pelo usuário.");
                        break;
                    default:
                        SetCompileStatus(ArduinoCompileStatus.Failed, $"Falha na compilação (código de saída {result.ExitCode?.ToString() ?? "?"}).");
                        _logger.Error($"Falha ao compilar o sketch '{Path.GetFileName(SketchPath)}' (código de saída {result.ExitCode?.ToString() ?? "?"}).");
                        break;
                }
            });
        }
        catch (Exception ex)
        {
            RunOnUi(() =>
            {
                AppendCompileLine(new ArduinoCliOutputLine { Stream = ArduinoCliOutputStream.Info, Text = $"Erro inesperado ao compilar: {ex.Message}" });
                SetCompileStatus(ArduinoCompileStatus.Failed, "Erro inesperado ao compilar — ver console.");
            });
            _logger.Error($"Erro inesperado ao compilar: {ex.Message}");
        }
        finally
        {
            _compileCts?.Dispose();
            _compileCts = null;
            RunOnUi(() => IsCompiling = false);
        }
    }

    private void CancelCompile(object? _)
    {
        _compileCts?.Cancel();
    }

    private void SetCompileStatus(ArduinoCompileStatus status, string text)
    {
        LastCompileStatus = status;
        CompileStatusText = text;
    }

    private void AppendCompileLine(ArduinoCliOutputLine line)
    {
        CompileOutputLines.Add(line);
        while (CompileOutputLines.Count > MaxCompileConsoleLines)
        {
            CompileOutputLines.RemoveAt(0);
        }
    }

    // =================================================================== Seção 3 — Monitor serial

    public ObservableCollection<string> MonitorLines { get; } = new();

    private ConnectionState _monitorConnectionState = ConnectionState.Disconnected;
    public ConnectionState MonitorConnectionState
    {
        get => _monitorConnectionState;
        private set
        {
            if (SetProperty(ref _monitorConnectionState, value))
            {
                OnPropertyChanged(nameof(IsMonitorConnected));
                RelayCommand.RaiseCanExecuteChangedForAll();
            }
        }
    }

    public bool IsMonitorConnected => MonitorConnectionState == ConnectionState.Connected;

    private bool _autoScroll = true;
    public bool AutoScroll
    {
        get => _autoScroll;
        set { if (SetProperty(ref _autoScroll, value)) PersistSettings(); }
    }

    private bool _showTimestamps = true;
    public bool ShowTimestamps
    {
        get => _showTimestamps;
        set { if (SetProperty(ref _showTimestamps, value)) PersistSettings(); }
    }

    private int _messageCount;
    public int MessageCount
    {
        get => _messageCount;
        private set => SetProperty(ref _messageCount, value);
    }

    public RelayCommand ConnectCommand { get; }
    public RelayCommand DisconnectCommand { get; }
    public RelayCommand ClearMonitorConsoleCommand { get; }

    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            _logger.Warning("Nenhuma porta COM selecionada para o monitor serial.");
            return;
        }
        if (SelectedBaudRate <= 0)
        {
            _logger.Warning("Baud Rate inválido.");
            return;
        }

        // A porta serial é compartilhada com a tela de Monitoramento (mesmo serviço Singleton
        // — ver comentário de classe). Se já houver uma conexão ativa com parâmetros
        // diferentes dos solicitados aqui, pedimos confirmação antes de derrubá-la.
        if (_serialService.State == ConnectionState.Connected)
        {
            bool mesmosParametros = string.Equals(_serialService.CurrentPortName, SelectedPort, StringComparison.OrdinalIgnoreCase)
                                     && _serialService.CurrentBaudRate == SelectedBaudRate;
            if (mesmosParametros)
            {
                // Já conectado exatamente na porta/baud desejados (possivelmente pela tela de
                // Monitoramento) — reaproveita o fluxo existente sem reabrir a porta.
                _logger.Info($"Monitor serial reutilizando a conexão já ativa em {SelectedPort}.");
                return;
            }

            string mensagem = $"A porta serial já está em uso em '{_serialService.CurrentPortName}' @ {_serialService.CurrentBaudRate} bps " +
                               "(possivelmente pela tela de Monitoramento). Deseja desconectar e reconectar com os parâmetros selecionados aqui?";
            bool confirmar = MessageBox.Show(mensagem, "Porta serial em uso", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            if (!confirmar) return;

            _serialService.Disconnect();
        }

        await _serialService.ConnectAsync(SelectedPort, SelectedBaudRate).ConfigureAwait(false);
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        RunOnUi(() => MonitorConnectionState = state);
    }

    private void OnCommunicationError(object? sender, string message)
    {
        RunOnUi(() => AppendMonitorLine($"[ERRO] {message}"));
    }

    private void OnSerialMessageReceived(object? sender, SerialMessage message)
    {
        RunOnUi(() =>
        {
            MessageCount++;
            AppendMonitorLine(message switch
            {
                TargetMessage t => $"TARGET;ID={t.Id};ANGLE={t.Angle:0.0};DIST={t.Distance:0.00}",
                StatusMessage s => $"STATUS;SYSTEM={s.SystemStatus}",
                AckMessage a => $"ACK;CMD={a.Command}",
                ErrorMessage e => $"ERROR;REASON={e.Reason}",
                UnknownMessage u => u.RawLine,
                _ => message.ToString() ?? string.Empty
            });
        });
    }

    private void AppendMonitorLine(string text)
    {
        string line = ShowTimestamps ? $"[{DateTime.Now:HH:mm:ss}] {text}" : text;
        MonitorLines.Add(line);
        while (MonitorLines.Count > MaxSerialMonitorLines)
        {
            MonitorLines.RemoveAt(0);
        }
    }

    // =================================================================== Persistência

    private void LoadPersistedSettings()
    {
        ArduinoCliSettings settings = _settingsRepository.Load();

        _cliPath = settings.CliPath;
        _sketchPath = settings.LastSketchPath ?? TryFindDefaultSketch();
        _selectedPort = settings.LastPort;
        _selectedBaudRate = settings.BaudRate > 0 ? settings.BaudRate : 9600;
        _autoScroll = settings.ConsoleAutoScroll;
        _showTimestamps = settings.ConsoleShowTimestamps;

        if (!string.IsNullOrWhiteSpace(settings.SelectedFqbn))
        {
            _selectedBoard = AvailableBoards.FirstOrDefault(b => b.Fqbn == settings.SelectedFqbn)
                              ?? new ArduinoBoardOption(settings.SelectedFqbn, settings.SelectedFqbn);
            if (!AvailableBoards.Contains(_selectedBoard)) AvailableBoards.Add(_selectedBoard);
        }
        else
        {
            _selectedBoard = AvailableBoards.FirstOrDefault();
        }

        OnPropertyChanged(nameof(CliPath));
        OnPropertyChanged(nameof(SketchPath));
        OnPropertyChanged(nameof(SelectedPort));
        OnPropertyChanged(nameof(SelectedBaudRate));
        OnPropertyChanged(nameof(AutoScroll));
        OnPropertyChanged(nameof(ShowTimestamps));
        OnPropertyChanged(nameof(SelectedBoard));
        OnPropertyChanged(nameof(SelectedBoardName));
    }

    private void PersistSettings()
    {
        _settingsRepository.Save(new ArduinoCliSettings
        {
            CliPath = CliPath,
            LastSketchPath = SketchPath,
            SelectedFqbn = SelectedBoard?.Fqbn,
            LastPort = SelectedPort,
            BaudRate = SelectedBaudRate,
            ConsoleAutoScroll = AutoScroll,
            ConsoleShowTimestamps = ShowTimestamps,
        });
    }

    private void RunOnUi(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.BeginInvoke(action);
    }

    public void Dispose()
    {
        _serialService.ConnectionStateChanged -= OnConnectionStateChanged;
        _serialService.MessageReceived -= OnSerialMessageReceived;
        _serialService.CommunicationError -= OnCommunicationError;
        _compileCts?.Cancel();
        _compileCts?.Dispose();
    }
}
