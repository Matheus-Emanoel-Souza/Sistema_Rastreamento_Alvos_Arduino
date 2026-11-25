using System;
using System.Threading;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;

namespace RadarTorres.App.Services;

/// <summary>
/// Relógio do espelho: a cada <see cref="Intervalo"/>, manda
/// <see cref="IEspelhoSistemaRepository.Capturar"/> gravar uma linha em TABESP com o estado do
/// sistema naquele instante.
/// </summary>
/// <remarks>
/// <para>
/// Usa <see cref="Timer"/> (pool de threads) e não <c>DispatcherTimer</c> de propósito: a captura
/// é uma ida ao banco, não tem nada de interface, e na thread de UI atrasaria o radar a cada 30
/// segundos. <see cref="EspelhoCapturado"/> é disparado na thread do timer — quem assina a partir
/// de uma tela precisa voltar para o Dispatcher.
/// </para>
/// <para>
/// Uma falha de captura NUNCA derruba o aplicativo nem interrompe a amostragem: o espelho é
/// auditoria, não caminho crítico — o erro vai para o console de eventos e o próximo tique tenta
/// de novo. <see cref="_capturando"/> evita que um tique entre enquanto o anterior ainda roda
/// (banco lento ou travado), o que empilharia conexões sem nenhum ganho.
/// </para>
/// </remarks>
public sealed class EspelhoSistemaService : IEspelhoSistemaService, IDisposable
{
    /// <summary>
    /// Padrão de 30 s: fino o bastante para a linha do tempo mostrar o que aconteceu numa
    /// operação, e esparso o bastante para o buffer de <see cref="SqliteEspelhoSistemaRepository"/>
    /// cobrir mais de duas semanas de operação contínua.
    /// </summary>
    public static readonly TimeSpan IntervaloPadrao = TimeSpan.FromSeconds(30);

    private readonly IEspelhoSistemaRepository _repository;
    private readonly ILoggingService _logger;
    private readonly Timer _timer;

    /// <summary>1 enquanto uma captura está em andamento — ver observação sobre tiques sobrepostos.</summary>
    private int _capturando;

    private bool _descartado;

    public EspelhoSistemaService(IEspelhoSistemaRepository repository, ILoggingService logger)
        : this(repository, logger, IntervaloPadrao)
    {
    }

    public EspelhoSistemaService(IEspelhoSistemaRepository repository, ILoggingService logger, TimeSpan intervalo)
    {
        _repository = repository;
        _logger = logger;
        Intervalo = intervalo;

        // Criado parado (Timeout.InfiniteTimeSpan nos dois tempos); quem liga é Iniciar.
        _timer = new Timer(_ => CapturarAgora(), state: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public TimeSpan Intervalo { get; }

    public bool EmExecucao { get; private set; }

    public EspelhoSistema? UltimoEspelho { get; private set; }

    public event EventHandler<EspelhoSistema>? EspelhoCapturado;

    public void Iniciar()
    {
        if (_descartado || EmExecucao) return;

        EmExecucao = true;
        // Primeira amostra imediata: sem ela, fechar o aplicativo antes do primeiro tique não
        // deixaria espelho nenhum daquela execução.
        _timer.Change(TimeSpan.Zero, Intervalo);
    }

    public void Parar()
    {
        if (_descartado || !EmExecucao) return;

        EmExecucao = false;
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public EspelhoSistema? CapturarAgora()
    {
        if (_descartado) return null;

        // Tique que chega com o anterior ainda rodando é descartado, não enfileirado: espelho
        // atrasado não tem valor, o próximo já traz o estado mais novo.
        if (Interlocked.CompareExchange(ref _capturando, 1, 0) != 0) return null;

        try
        {
            EspelhoSistema espelho = _repository.Capturar();
            UltimoEspelho = espelho;
            EspelhoCapturado?.Invoke(this, espelho);
            return espelho;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Não foi possível gravar o espelho do sistema: {ex.Message}");
            return null;
        }
        finally
        {
            Volatile.Write(ref _capturando, 0);
        }
    }

    public void Dispose()
    {
        if (_descartado) return;

        _descartado = true;
        EmExecucao = false;
        _timer.Dispose();
    }
}
