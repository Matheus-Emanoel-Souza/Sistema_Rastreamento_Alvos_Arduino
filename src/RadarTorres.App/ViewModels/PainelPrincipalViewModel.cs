using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel do painel principal (Requisito 3): resumo com contagem de objetos/ações,
/// modo atual, última alteração de modo, estado da comunicação e data/hora da última
/// atualização. É uma tela puramente de leitura — consulta os repositórios de auditoria e o
/// serviço de comunicação, sem depender da <c>MainViewModel</c> de Monitoramento.
/// </summary>
public sealed class PainelPrincipalViewModel : ViewModelBase, INavigationAware, IDisposable
{
    private readonly IObjetoDetectadoRepository _objetoRepository;
    private readonly IAcaoRealizadaRepository _acaoRepository;
    private readonly IAlteracaoModoRepository _alteracaoModoRepository;
    private readonly ISerialCommunicationService _serialService;
    private readonly Dispatcher _dispatcher;

    public PainelPrincipalViewModel(
        IObjetoDetectadoRepository objetoRepository,
        IAcaoRealizadaRepository acaoRepository,
        IAlteracaoModoRepository alteracaoModoRepository,
        ISerialCommunicationService serialService)
    {
        _objetoRepository = objetoRepository;
        _acaoRepository = acaoRepository;
        _alteracaoModoRepository = alteracaoModoRepository;
        _serialService = serialService;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

        _serialService.ConnectionStateChanged += OnConnectionStateChanged;

        Refresh();
    }

    public int TotalObjetosDetectados { get; private set; }

    public int TotalAcoesRealizadas { get; private set; }

    public string ModoAtual { get; private set; } = "—";

    public string UltimaAlteracaoModoTexto { get; private set; } = "—";

    public string UsuarioResponsavelUltimaAlteracao { get; private set; } = "—";

    public ConnectionState EstadoComunicacao { get; private set; } = ConnectionState.Disconnected;

    public string UltimaAtualizacaoTexto { get; private set; } = "—";

    /// <summary>Chamado pelo NavigationService toda vez que o usuário volta a esta tela (view Singleton).</summary>
    public void OnNavigatedTo() => Refresh();

    private void OnConnectionStateChanged(object? sender, ConnectionState state)
    {
        RunOnUi(Refresh);
    }

    private void Refresh()
    {
        TotalObjetosDetectados = _objetoRepository.GetAll().Count;
        TotalAcoesRealizadas = _acaoRepository.GetAll().Count;

        AlteracaoModo? ultima = _alteracaoModoRepository.GetAll()
            .Where(a => a.Resultado == ResultadoAlteracaoModo.Sucesso)
            .OrderByDescending(a => a.DataHoraExecucao ?? a.DataHoraSolicitacao)
            .FirstOrDefault();

        if (ultima is not null)
        {
            ModoAtual = ultima.NovoModo;
            UltimaAlteracaoModoTexto = (ultima.DataHoraExecucao ?? ultima.DataHoraSolicitacao).ToString("dd/MM/yyyy HH:mm:ss");
            UsuarioResponsavelUltimaAlteracao = ultima.UsuarioSolicitante;
        }
        else
        {
            ModoAtual = "—";
            UltimaAlteracaoModoTexto = "—";
            UsuarioResponsavelUltimaAlteracao = "—";
        }

        EstadoComunicacao = _serialService.State;
        UltimaAtualizacaoTexto = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

        OnPropertyChanged(nameof(TotalObjetosDetectados));
        OnPropertyChanged(nameof(TotalAcoesRealizadas));
        OnPropertyChanged(nameof(ModoAtual));
        OnPropertyChanged(nameof(UltimaAlteracaoModoTexto));
        OnPropertyChanged(nameof(UsuarioResponsavelUltimaAlteracao));
        OnPropertyChanged(nameof(EstadoComunicacao));
        OnPropertyChanged(nameof(UltimaAtualizacaoTexto));
    }

    private void RunOnUi(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.BeginInvoke(action);
    }

    public void Dispose()
    {
        _serialService.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}
