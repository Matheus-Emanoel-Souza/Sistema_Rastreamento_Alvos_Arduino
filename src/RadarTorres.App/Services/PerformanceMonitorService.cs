using System;
using System.Collections.Generic;
using RadarTorres.App.Configuration;

namespace RadarTorres.App.Services;

/// <summary>Implementação de <see cref="IPerformanceMonitorService"/> baseada em janelas
/// deslizantes de tamanho fixo, protegidas por <c>lock</c>.</summary>
/// <remarks>
/// As chamadas chegam de threads diferentes: leituras seriais/simuladas disparam
/// <see cref="RecordDecisionTime"/>/<see cref="RecordCycleTime"/> a partir de threads de fundo
/// (mesmo padrão de concorrência documentado em <see cref="TargetTrackingService"/>), enquanto
/// <see cref="RegisterRadarTick"/> vem do <c>DispatcherTimer</c> de renderização do radar (thread
/// de UI). Um <c>lock</c> simples por janela é suficiente: a frequência de chamadas (no máximo
/// dezenas por segundo) não justifica estruturas lock-free mais elaboradas.
/// </remarks>
public sealed class PerformanceMonitorService : IPerformanceMonitorService
{
    /// <summary>Ponte estática para código instanciado via XAML que não recebe este serviço por
    /// injeção de dependência (<c>RadarControl</c>) — mesmo padrão já adotado por
    /// <see cref="LocalizationService.Current"/> neste projeto.</summary>
    public static IPerformanceMonitorService? Current { get; set; }

    private const int JanelaAmostras = 50;
    private const int JanelaTicksRadar = 20;

    private readonly object _decisaoLock = new();
    private readonly object _cicloLock = new();
    private readonly object _radarLock = new();

    private readonly Queue<double> _decisaoAmostras = new();
    private readonly Queue<double> _cicloAmostras = new();
    private readonly Queue<DateTime> _radarTicks = new();

    public double TaxaAtualizacaoConfiguradaHz => 1000.0 / Math.Max(1, AppConfig.Current.RadarSettings.RefreshRateMs);

    public double TaxaAtualizacaoMedidaHz
    {
        get
        {
            lock (_radarLock)
            {
                if (_radarTicks.Count < 2) return 0;

                DateTime primeiro = default;
                DateTime ultimo = default;
                int i = 0;
                foreach (DateTime tick in _radarTicks)
                {
                    if (i == 0) primeiro = tick;
                    ultimo = tick;
                    i++;
                }

                double segundos = (ultimo - primeiro).TotalSeconds;
                return segundos > 0 ? (_radarTicks.Count - 1) / segundos : 0;
            }
        }
    }

    public MetricaTempo TempoDecisao
    {
        get { lock (_decisaoLock) return Calcular(_decisaoAmostras); }
    }

    public MetricaTempo TempoCicloCompleto
    {
        get { lock (_cicloLock) return Calcular(_cicloAmostras); }
    }

    public void RegisterRadarTick()
    {
        lock (_radarLock)
        {
            _radarTicks.Enqueue(DateTime.Now);
            while (_radarTicks.Count > JanelaTicksRadar) _radarTicks.Dequeue();
        }
    }

    public void RecordDecisionTime(double milissegundos)
    {
        lock (_decisaoLock) Enfileirar(_decisaoAmostras, milissegundos);
    }

    public void RecordCycleTime(double milissegundos)
    {
        lock (_cicloLock) Enfileirar(_cicloAmostras, milissegundos);
    }

    public void Reset()
    {
        lock (_decisaoLock) _decisaoAmostras.Clear();
        lock (_cicloLock) _cicloAmostras.Clear();
        lock (_radarLock) _radarTicks.Clear();
    }

    private static void Enfileirar(Queue<double> amostras, double valor)
    {
        amostras.Enqueue(valor);
        while (amostras.Count > JanelaAmostras) amostras.Dequeue();
    }

    private static MetricaTempo Calcular(Queue<double> amostras)
    {
        if (amostras.Count == 0) return new MetricaTempo(0, 0, 0, 0, 0, 0);

        double ultimo = 0, soma = 0, minimo = double.MaxValue, maximo = double.MinValue;
        double somaDiferencas = 0;
        double? anterior = null;
        int n = 0;

        foreach (double valor in amostras)
        {
            ultimo = valor;
            soma += valor;
            if (valor < minimo) minimo = valor;
            if (valor > maximo) maximo = valor;
            if (anterior is not null) somaDiferencas += Math.Abs(valor - anterior.Value);
            anterior = valor;
            n++;
        }

        double jitter = n > 1 ? somaDiferencas / (n - 1) : 0;
        return new MetricaTempo(ultimo, soma / n, minimo, maximo, jitter, n);
    }
}
