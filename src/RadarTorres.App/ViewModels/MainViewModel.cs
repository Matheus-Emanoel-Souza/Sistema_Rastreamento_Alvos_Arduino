using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using RadarTorres.App.Configuration;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel principal da aplicação. Orquestra todos os serviços (comunicação serial,
/// rastreamento de alvos, seleção de torres, acionamento e simulação) e expõe apenas
/// propriedades e comandos simples para o <see cref="Views.MainWindow"/> consumir via binding.
/// Nenhuma regra de negócio mora aqui — esta classe apenas decide "quando chamar o quê"
/// em resposta a eventos da UI ou dos serviços.
/// </summary>
public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly ILoggingService _logger;
    private readonly ISerialCommunicationService _serialService;
    private readonly ITargetTrackingService _trackingService;
    private readonly ITowerSelectionService _towerService;
    private readonly IFireControlService _fireControlService;
    private readonly ISimulationService _simulationService;
    private readonly Dispatcher _dispatcher;

    public MainViewModel(
        ILoggingService logger,
        ISerialCommunicationService serialService,
        ITargetTrackingService trackingService,
        ITowerSelectionService towerService,
        IFireControlService fireControlService,
        ISimulationService simulationService)
    {
        _logger = logger;
        _serialService = serialService;
        _trackingService = trackingService;
        _towerService = towerService;
        _fireControlService = fireControlService;
        _simulationService = simulationService;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _minDistance = AppConfig.Current.RadarSettings.MinSafetyDistanceMeters;
        _maxDistance = AppConfig.Current.RadarSettings.MaxDetectionDistanceMeters;
        SelectedBaudRate = AppConfig.Current.SerialSettings.DefaultBaudRate;

        Targets.CollectionChanged += OnTargetsCollectionChanged;

        _serialService.ConnectionStateChanged += OnConnectionStateChanged;
        _serialService.MessageReceived += OnSerialMessageReceived;
        _serialService.CommunicationError += OnCommunicationError;

        _trackingService.TargetCreated += OnTargetCreatedOrUpdated;
        _trackingService.TargetUpdated += OnTargetCreatedOrUpdated;
        _trackingService.TargetRemoved += OnTargetRemoved;

        _simulationService.ReadingGenerated += OnSimulationReadingGenerated;

        RefreshPortsCommand = new RelayCommand(RefreshPorts);
        ConnectCommand = new RelayCommand(async () => await ConnectAsync(), () => !IsConnected);
        DisconnectCommand = new RelayCommand(() => _serialService.Disconnect(), () => IsConnected);
        ManualFireCommand = new RelayCommand(async () => await ManualFireAsync(), () => SelectedTarget is not null && CurrentMode != SystemMode.Off);
        ClearRadarCommand = new RelayCommand(ClearRadar);
        TogglePauseCommand = new RelayCommand(() => IsPaused = !IsPaused);

        RefreshPorts();
        _logger.Success("Sistema iniciado");
    }

    // ---------------------------------------------------------------- Coleções expostas

    public ObservableCollection<Target> Targets => _trackingService.Targets;
    public ObservableCollection<Tower> Towers => _towerService.Towers;
    public ObservableCollection<LogEntry> LogEntries => _logger.Entries;
    public ObservableCollection<string> AvailablePorts { get; } = new();
    public int[] AvailableBaudRates => SerialSettings.CommonBaudRates;

    // ---------------------------------------------------------------- Conexão serial

    private string? _selectedPort;
    public string? SelectedPort
    {
        get => _selectedPort;
        set => SetProperty(ref _selectedPort, value);
    }

    private int _selectedBaudRate;
    public int SelectedBaudRate
    {
        get => _selectedBaudRate;
        set => SetProperty(ref _selectedBaudRate, value);
    }

    private ConnectionState _connectionStatus = ConnectionState.Disconnected;
    public ConnectionState ConnectionStatus
    {
        get => _connectionStatus;
        private set
        {
            if (SetProperty(ref _connectionStatus, value))
            {
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(SystemStatusText));
            }
        }
    }

    public bool IsConnected => ConnectionStatus == ConnectionState.Connected;

    // ---------------------------------------------------------------- Modo / simulação / pausa

    private SystemMode _currentMode = SystemMode.Off;
    public SystemMode CurrentMode
    {
        get => _currentMode;
        set
        {
            SystemMode old = _currentMode;
            if (SetProperty(ref _currentMode, value))
            {
                OnPropertyChanged(nameof(SystemStatusText));
                OnModeChanged(old, value);
            }
        }
    }

    private bool _isSimulationMode;
    public bool IsSimulationMode
    {
        get => _isSimulationMode;
        set
        {
            if (value == _isSimulationMode) return;

            if (value && IsConnected)
            {
                _logger.Warning("Desconecte do Arduino antes de ativar o modo de simulação.");
                OnPropertyChanged(nameof(IsSimulationMode));
                return;
            }

            _isSimulationMode = value;
            OnPropertyChanged(nameof(IsSimulationMode));

            if (value) _simulationService.Start();
            else _simulationService.Stop();
        }
    }

    private bool _isPaused;
    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (SetProperty(ref _isPaused, value))
            {
                _logger.Info(value ? "Monitoramento pausado" : "Monitoramento retomado");
            }
        }
    }

    // ---------------------------------------------------------------- Distâncias configuráveis

    private double _minDistance;
    public double MinDistance
    {
        get => _minDistance;
        set
        {
            double clamped = Math.Max(0, value);
            if (SetProperty(ref _minDistance, clamped))
            {
                _logger.Info($"Distância mínima de segurança definida: {clamped:0.00} m");
                _ = SendCommandSafeAsync(SerialProtocolParser.BuildSetMinDistance(clamped));
                OnPropertyChanged(nameof(SelectedTargetSafetyStatus));
            }
        }
    }

    private double _maxDistance;
    public double MaxDistance
    {
        get => _maxDistance;
        set
        {
            double clamped = Math.Max(MinDistance + 0.1, value);
            if (SetProperty(ref _maxDistance, clamped))
            {
                _logger.Info($"Distância máxima de detecção definida: {clamped:0.00} m");
                _ = SendCommandSafeAsync(SerialProtocolParser.BuildSetMaxDistance(clamped));
            }
        }
    }

    // ---------------------------------------------------------------- Alvo selecionado / resumo

    private Target? _selectedTarget;
    public Target? SelectedTarget
    {
        get => _selectedTarget;
        set
        {
            if (_selectedTarget == value) return;

            if (_selectedTarget is not null)
            {
                _selectedTarget.IsSelected = false;
                _selectedTarget.PropertyChanged -= OnSelectedTargetPropertyChanged;
            }

            SetProperty(ref _selectedTarget, value);

            if (_selectedTarget is not null)
            {
                _selectedTarget.IsSelected = true;
                _selectedTarget.PropertyChanged += OnSelectedTargetPropertyChanged;
            }

            OnPropertyChanged(nameof(SelectedTargetSafetyStatus));
        }
    }

    private void OnSelectedTargetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Target.Distance) or nameof(Target.SelectedTower))
        {
            RunOnUi(() => OnPropertyChanged(nameof(SelectedTargetSafetyStatus)));
        }
    }

    /// <summary>
    /// Texto de status exibido no painel de resumo ("PRONTO" / "BLOQUEADO — ..." / "—").
    /// Reflete em tempo real a regra de segurança de distância mínima para o alvo selecionado.
    /// </summary>
    public string SelectedTargetSafetyStatus
    {
        get
        {
            if (SelectedTarget is null) return "—";
            if (!SelectedTarget.IsActive) return "ALVO PERDIDO";
            if (SelectedTarget.Distance < MinDistance) return "BLOQUEADO (DISTÂNCIA MÍNIMA)";
            if (SelectedTarget.SelectedTower is null) return "SEM TORRE";
            return "PRONTO";
        }
    }

    public int DetectedTargetsCount => Targets.Count(t => t.IsActive);

    public string SystemStatusText
    {
        get
        {
            if (CurrentMode == SystemMode.Off) return "OFFLINE";
            return IsConnected || IsSimulationMode ? "ONLINE" : "AGUARDANDO CONEXÃO";
        }
    }

    // ---------------------------------------------------------------- Comandos

    public RelayCommand RefreshPortsCommand { get; }
    public RelayCommand ConnectCommand { get; }
    public RelayCommand DisconnectCommand { get; }
    public RelayCommand ManualFireCommand { get; }
    public RelayCommand ClearRadarCommand { get; }
    public RelayCommand TogglePauseCommand { get; }

    public void SelectTargetById(int id) => SelectedTarget = Targets.FirstOrDefault(t => t.Id == id);

    // ---------------------------------------------------------------- Implementação dos comandos

    private void RefreshPorts()
    {
        AvailablePorts.Clear();
        foreach (string port in _serialService.GetAvailablePorts())
        {
            AvailablePorts.Add(port);
        }

        if (SelectedPort is null || !AvailablePorts.Contains(SelectedPort))
        {
            SelectedPort = AvailablePorts.Contains(AppConfig.Current.SerialSettings.DefaultPort)
                ? AppConfig.Current.SerialSettings.DefaultPort
                : AvailablePorts.FirstOrDefault();
        }
    }

    private async Task ConnectAsync()
    {
        if (IsSimulationMode)
        {
            _logger.Warning("Desative o modo de simulação antes de conectar ao Arduino.");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedPort))
        {
            _logger.Warning("Nenhuma porta COM selecionada.");
            return;
        }

        bool ok = await _serialService.ConnectAsync(SelectedPort, SelectedBaudRate);
        if (ok)
        {
            await SendCommandSafeAsync(SerialProtocolParser.BuildSetMinDistance(MinDistance));
            await SendCommandSafeAsync(SerialProtocolParser.BuildSetMaxDistance(MaxDistance));
        }
    }

    private async Task ManualFireAsync()
    {
        if (SelectedTarget is null) return;
        await _fireControlService.TryFireAsync(SelectedTarget, IsSimulationMode ? null : _serialService, IsSimulationMode, MinDistance);
    }

    private void ClearRadar()
    {
        _trackingService.ClearAll();
        SelectedTarget = null;
        _logger.Info("Radar limpo pelo usuário.");
    }

    // ---------------------------------------------------------------- Reações a eventos de serviços

    private void OnModeChanged(SystemMode oldMode, SystemMode newMode)
    {
        if (newMode == SystemMode.Off)
        {
            _logger.Warning("Sistema desligado");
            _ = SendCommandSafeAsync(SerialProtocolParser.BuildSystemOff());
            return;
        }

        if (oldMode == SystemMode.Off)
        {
            _ = SendCommandSafeAsync(SerialProtocolParser.BuildSystemOn());
        }

        string modeCommand = newMode == SystemMode.LocationOnly
            ? SerialProtocolParser.BuildModeDetection()
            : SerialProtocolParser.BuildModeAuto();

        _ = SendCommandSafeAsync(modeCommand);
        _logger.Info($"Modo alterado para: {DescribeMode(newMode)}");
    }

    private static string DescribeMode(SystemMode mode) => mode switch
    {
        SystemMode.Off => "SISTEMA DESLIGADO",
        SystemMode.LocationOnly => "SOMENTE LOCALIZAÇÃO",
        SystemMode.LocationAutoTower => "LOCALIZAÇÃO + SELEÇÃO AUTOMÁTICA DE TORRE",
        SystemMode.LocationAutoFire => "LOCALIZAÇÃO + ACIONAMENTO DEMONSTRATIVO AUTOMÁTICO",
        _ => mode.ToString()
    };

    private async Task SendCommandSafeAsync(string command)
    {
        if (IsSimulationMode)
        {
            _logger.Info($"[SIMULAÇÃO] Comando: {command}");
            return;
        }

        if (!IsConnected) return;

        await _serialService.SendCommandAsync(command);
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        RunOnUi(() => ConnectionStatus = state);
    }

    private void OnCommunicationError(object? sender, string message)
    {
        // O próprio serviço já registra o erro no console de eventos; aqui apenas garantimos
        // que qualquer propriedade dependente da UI seja reavaliada na thread correta.
        RunOnUi(() => OnPropertyChanged(nameof(SystemStatusText)));
    }

    private void OnSerialMessageReceived(object? sender, SerialMessage message)
    {
        switch (message)
        {
            case TargetMessage targetMessage:
                if (IsPaused) return;
                _trackingService.ProcessReading(new SensorReading
                {
                    TargetId = targetMessage.Id,
                    Angle = targetMessage.Angle,
                    Distance = targetMessage.Distance,
                    Source = DataSource.Serial
                });
                break;

            case StatusMessage statusMessage:
                _logger.Info($"Status do Arduino: {statusMessage.SystemStatus}");
                break;

            case AckMessage ackMessage:
                _logger.Success($"Arduino confirmou comando: {ackMessage.Command}");
                break;

            case ErrorMessage errorMessage:
                _logger.Error($"Erro reportado pelo Arduino: {errorMessage.Reason}");
                break;

            case UnknownMessage unknownMessage:
                _logger.Warning($"Mensagem serial não reconhecida: '{unknownMessage.RawLine}'");
                break;
        }
    }

    private void OnSimulationReadingGenerated(object? sender, SensorReading reading)
    {
        if (IsPaused) return;
        _trackingService.ProcessReading(reading);
    }

    private void OnTargetCreatedOrUpdated(object? sender, Target target)
    {
        if (CurrentMode == SystemMode.LocationAutoTower || CurrentMode == SystemMode.LocationAutoFire)
        {
            TowerSelectionResult result = _towerService.SelectTowerFor(target);
            _towerService.RecomputeTowerStates(Targets.Where(t => t.IsActive));

            if (result.Success)
            {
                _logger.Info($"Torre {result.SelectedTower!.Name} selecionada para o alvo #{target.Id:D2}");
            }

            if (CurrentMode == SystemMode.LocationAutoFire && result.Success)
            {
                FireAuthorizationResult auth = _fireControlService.Authorize(target, MinDistance);
                if (auth.Authorized)
                {
                    _ = _fireControlService.TryFireAsync(target, IsSimulationMode ? null : _serialService, IsSimulationMode, MinDistance);
                }
                else
                {
                    _logger.Warning(auth.Reason);
                }
            }
        }

        SelectedTarget ??= target;
    }

    private void OnTargetRemoved(object? sender, Target target)
    {
        _towerService.RecomputeTowerStates(Targets.Where(t => t.IsActive));
        if (SelectedTarget == target) SelectedTarget = null;
    }

    private void OnTargetsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        OnPropertyChanged(nameof(DetectedTargetsCount));

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
        _trackingService.TargetCreated -= OnTargetCreatedOrUpdated;
        _trackingService.TargetUpdated -= OnTargetCreatedOrUpdated;
        _trackingService.TargetRemoved -= OnTargetRemoved;
        _simulationService.ReadingGenerated -= OnSimulationReadingGenerated;
        _serialService.Dispose();
    }
}
