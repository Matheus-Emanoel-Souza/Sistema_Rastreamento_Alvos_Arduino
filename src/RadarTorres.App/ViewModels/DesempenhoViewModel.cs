using System.Windows.Threading;
using RadarTorres.App.Helpers;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da tela "Desempenho": mostra, em tempo real, a taxa de atualização do radar e o
/// tempo do ciclo detecção → seleção de torre, coletados por <see cref="IPerformanceMonitorService"/>
/// a partir de <c>RadarControl</c>/<c>MainViewModel</c> (que continuam sem nenhuma referência a
/// esta tela — só reportam ao serviço, que é quem esta ViewModel consulta).
/// </summary>
public sealed class DesempenhoViewModel : ViewModelBase
{
    private readonly IPerformanceMonitorService _performanceMonitorService;
    private readonly DispatcherTimer _amostragemTimer;

    public RelayCommand ZerarEstatisticasCommand { get; }

    public DesempenhoViewModel(IPerformanceMonitorService performanceMonitorService)
    {
        _performanceMonitorService = performanceMonitorService;

        ZerarEstatisticasCommand = new RelayCommand(() =>
        {
            _performanceMonitorService.Reset();
            AtualizarValores();
        });

        // Só amostra enquanto a tela está visível (Start/Stop chamados pela View no
        // Loaded/Unloaded) — mesmo cuidado de custo do RadarControl (DispatcherPriority padrão
        // é suficiente aqui, não é uma animação).
        _amostragemTimer = new DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(300) };
        _amostragemTimer.Tick += (_, _) => AtualizarValores();

        AtualizarValores();
    }

    public void StartMonitoring() => _amostragemTimer.Start();

    public void StopMonitoring() => _amostragemTimer.Stop();

    // ---------------------------------------------------------------- Taxa de atualização do radar

    private double _taxaConfiguradaHz;
    public double TaxaConfiguradaHz
    {
        get => _taxaConfiguradaHz;
        private set => SetProperty(ref _taxaConfiguradaHz, value);
    }

    private double _taxaMedidaHz;
    public double TaxaMedidaHz
    {
        get => _taxaMedidaHz;
        private set => SetProperty(ref _taxaMedidaHz, value);
    }

    // ---------------------------------------------------------------- Tempo de decisão

    private double _decisaoUltimoMs;
    public double DecisaoUltimoMs { get => _decisaoUltimoMs; private set => SetProperty(ref _decisaoUltimoMs, value); }

    private double _decisaoMediaMs;
    public double DecisaoMediaMs { get => _decisaoMediaMs; private set => SetProperty(ref _decisaoMediaMs, value); }

    private double _decisaoMinimoMs;
    public double DecisaoMinimoMs { get => _decisaoMinimoMs; private set => SetProperty(ref _decisaoMinimoMs, value); }

    private double _decisaoMaximoMs;
    public double DecisaoMaximoMs { get => _decisaoMaximoMs; private set => SetProperty(ref _decisaoMaximoMs, value); }

    private double _decisaoJitterMs;
    public double DecisaoJitterMs { get => _decisaoJitterMs; private set => SetProperty(ref _decisaoJitterMs, value); }

    private int _decisaoAmostras;
    public int DecisaoAmostras { get => _decisaoAmostras; private set => SetProperty(ref _decisaoAmostras, value); }

    // ---------------------------------------------------------------- Tempo de ciclo completo

    private double _cicloUltimoMs;
    public double CicloUltimoMs { get => _cicloUltimoMs; private set => SetProperty(ref _cicloUltimoMs, value); }

    private double _cicloMediaMs;
    public double CicloMediaMs { get => _cicloMediaMs; private set => SetProperty(ref _cicloMediaMs, value); }

    private double _cicloMinimoMs;
    public double CicloMinimoMs { get => _cicloMinimoMs; private set => SetProperty(ref _cicloMinimoMs, value); }

    private double _cicloMaximoMs;
    public double CicloMaximoMs { get => _cicloMaximoMs; private set => SetProperty(ref _cicloMaximoMs, value); }

    private int _cicloAmostras;
    public int CicloAmostras { get => _cicloAmostras; private set => SetProperty(ref _cicloAmostras, value); }

    private void AtualizarValores()
    {
        TaxaConfiguradaHz = _performanceMonitorService.TaxaAtualizacaoConfiguradaHz;
        TaxaMedidaHz = _performanceMonitorService.TaxaAtualizacaoMedidaHz;

        MetricaTempo decisao = _performanceMonitorService.TempoDecisao;
        DecisaoUltimoMs = decisao.UltimoMs;
        DecisaoMediaMs = decisao.MediaMs;
        DecisaoMinimoMs = decisao.MinimoMs;
        DecisaoMaximoMs = decisao.MaximoMs;
        DecisaoJitterMs = decisao.JitterMs;
        DecisaoAmostras = decisao.Amostras;

        MetricaTempo ciclo = _performanceMonitorService.TempoCicloCompleto;
        CicloUltimoMs = ciclo.UltimoMs;
        CicloMediaMs = ciclo.MediaMs;
        CicloMinimoMs = ciclo.MinimoMs;
        CicloMaximoMs = ciclo.MaximoMs;
        CicloAmostras = ciclo.Amostras;
    }
}
