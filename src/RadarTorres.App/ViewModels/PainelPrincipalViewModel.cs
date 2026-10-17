using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel do painel principal (Requisito 3): resumo com contagem de objetos/ações,
/// modo atual, última alteração de modo, estado da comunicação e data/hora da última
/// atualização. É uma tela puramente de leitura — consulta os repositórios de auditoria e o
/// serviço de comunicação, sem depender da <c>MainViewModel</c> de Monitoramento. O card
/// "Radar ao vivo (prévia)" é exceção parcial: lê os MESMOS serviços compartilhados que a
/// MainViewModel usa (ITargetTrackingService/ITowerSelectionService), não a MainViewModel em si
/// — mantém a independência entre as duas telas.
/// </summary>
public sealed class PainelPrincipalViewModel : ViewModelBase, INavigationAware, IDisposable
{
    private readonly IObjetoDetectadoRepository _objetoRepository;
    private readonly IAcaoRealizadaRepository _acaoRepository;
    private readonly IModoAtualTorreRepository _modoAtualTorreRepository;
    private readonly ISerialCommunicationService _serialService;
    private readonly ILocalizationService _localizationService;
    private readonly ITargetTrackingService _trackingService;
    private readonly INavigationService _navigationService;
    private readonly Dispatcher _dispatcher;

    public PainelPrincipalViewModel(
        IObjetoDetectadoRepository objetoRepository,
        IAcaoRealizadaRepository acaoRepository,
        IModoAtualTorreRepository modoAtualTorreRepository,
        ISerialCommunicationService serialService,
        ILocalizationService localizationService,
        ITargetTrackingService trackingService,
        ITowerSelectionService towerService,
        INavigationService navigationService)
    {
        _objetoRepository = objetoRepository;
        _acaoRepository = acaoRepository;
        _modoAtualTorreRepository = modoAtualTorreRepository;
        _serialService = serialService;
        _localizationService = localizationService;
        _trackingService = trackingService;
        _navigationService = navigationService;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        Towers = towerService.Towers;

        _serialService.ConnectionStateChanged += OnConnectionStateChanged;
        _localizationService.PropertyChanged += OnLocalizationChanged;
        Targets.CollectionChanged += OnTargetsCollectionChanged;

        ConectarCommand = new RelayCommand(() => _navigationService.NavigateTo(MenuItem.ConfiguracoesArduino));
        AbrirMonitoramentoCommand = new RelayCommand(() => _navigationService.NavigateTo(MenuItem.Monitoramento));

        Refresh();
    }

    // ---------------------------------------------------------------- Radar ao vivo (prévia)

    /// <summary>Mesma coleção ao vivo que o radar de Monitoramento usa — ver comentário da classe.</summary>
    public ObservableCollection<Target> Targets => _trackingService.Targets;

    public ObservableCollection<Tower> Towers { get; }

    public int AlvosNoRaio => Targets.Count(t => t.IsActive);

    public string AlvosNoRaioTexto => string.Format(_localizationService["Dashboard.AlvosNoRaio"], AlvosNoRaio);

    public RelayCommand ConectarCommand { get; }

    public RelayCommand AbrirMonitoramentoCommand { get; }

    private void OnTargetsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        RunOnUi(() =>
        {
            OnPropertyChanged(nameof(AlvosNoRaio));
            OnPropertyChanged(nameof(AlvosNoRaioTexto));
        });

    /// <summary>Disparado toda vez que o usuário navega para esta tela (view Singleton, pode
    /// ser revisitada várias vezes, inclusive por usuários diferentes após um logout/login). O
    /// code-behind da View usa isso para recarregar o layout do card do usuário atualmente
    /// logado — carregar/salvar layout é estado visual, não pertence à ViewModel (mesmo
    /// princípio de <c>ArduinoSettingsView</c> para diálogos de arquivo).</summary>
    public event EventHandler? ScreenActivated;

    public int TotalObjetosDetectados { get; private set; }

    public int TotalAcoesRealizadas { get; private set; }

    public string ModoAtual { get; private set; } = "—";

    public bool TemModoDefinido => ModoAtual != "—";

    public string UltimaModoAtualTorreTexto { get; private set; } = "—";

    public ConnectionState EstadoComunicacao { get; private set; } = ConnectionState.Disconnected;

    /// <summary>Texto traduzido de <see cref="EstadoComunicacao"/> (Requisito i18n — nunca o
    /// nome bruto do enum, que sempre sairia em inglês independente do idioma escolhido).</summary>
    public string EstadoComunicacaoTexto { get; private set; } = string.Empty;

    public string UltimaAtualizacaoTexto { get; private set; } = "—";

    /// <summary>Chamado pelo NavigationService toda vez que o usuário volta a esta tela (view Singleton).</summary>
    public void OnNavigatedTo()
    {
        Refresh();
        ScreenActivated?.Invoke(this, EventArgs.Empty);
    }

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        RunOnUi(Refresh);
    }

    private void Refresh()
    {
        TotalObjetosDetectados = _objetoRepository.GetAll().Count;
        TotalAcoesRealizadas = _acaoRepository.GetAll().Count;

        ModoAtualTorre? ultima = _modoAtualTorreRepository.GetAll()
            .OrderByDescending(a => a.DataHora)
            .FirstOrDefault();

        if (ultima is not null)
        {
            ModoAtual = ultima.NovoModo;
            UltimaModoAtualTorreTexto = ultima.DataHora.ToString("dd/MM/yyyy HH:mm:ss");
        }
        else
        {
            ModoAtual = "—";
            UltimaModoAtualTorreTexto = "—";
        }

        EstadoComunicacao = _serialService.State;
        EstadoComunicacaoTexto = _localizationService[$"ConnectionState.{EstadoComunicacao}"];
        UltimaAtualizacaoTexto = $"{DateTime.Now:dd/MM/yyyy}\n{DateTime.Now:HH:mm:ss}";

        OnPropertyChanged(nameof(TotalObjetosDetectados));
        OnPropertyChanged(nameof(TotalAcoesRealizadas));
        OnPropertyChanged(nameof(ModoAtual));
        OnPropertyChanged(nameof(TemModoDefinido));
        OnPropertyChanged(nameof(UltimaModoAtualTorreTexto));
        OnPropertyChanged(nameof(EstadoComunicacao));
        OnPropertyChanged(nameof(EstadoComunicacaoTexto));
        OnPropertyChanged(nameof(UltimaAtualizacaoTexto));
    }

    private void OnLocalizationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        RunOnUi(() =>
        {
            EstadoComunicacaoTexto = _localizationService[$"ConnectionState.{EstadoComunicacao}"];
            OnPropertyChanged(nameof(EstadoComunicacaoTexto));
            OnPropertyChanged(nameof(AlvosNoRaioTexto));
        });

    private void RunOnUi(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.BeginInvoke(action);
    }

    public void Dispose()
    {
        _serialService.ConnectionStateChanged -= OnConnectionStateChanged;
        _localizationService.PropertyChanged -= OnLocalizationChanged;
        Targets.CollectionChanged -= OnTargetsCollectionChanged;
    }
}
