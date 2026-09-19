namespace RadarTorres.App.Services;

/// <summary>
/// Estatísticas de uma métrica de tempo (em milissegundos), calculadas sobre uma janela
/// deslizante das últimas amostras — não desde o início da sessão, para refletir o
/// comportamento recente do sistema em vez de ser "diluída" por uma sessão longa.
/// </summary>
/// <param name="UltimoMs">Valor da amostra mais recente.</param>
/// <param name="MediaMs">Média das amostras na janela.</param>
/// <param name="MinimoMs">Menor valor observado na janela.</param>
/// <param name="MaximoMs">Maior valor observado na janela.</param>
/// <param name="JitterMs">
/// Variação do tempo entre amostras consecutivas: média das diferenças absolutas entre cada
/// amostra e a anterior (mesma definição usada para jitter de rede, RFC 3550) — quanto mais
/// perto de zero, mais estável/previsível é o tempo medido.
/// </param>
/// <param name="Amostras">Quantidade de amostras atualmente na janela.</param>
public readonly record struct MetricaTempo(double UltimoMs, double MediaMs, double MinimoMs, double MaximoMs, double JitterMs, int Amostras);

/// <summary>
/// Monitor de desempenho em tempo de execução (Requisito de monitoramento operacional):
/// acompanha a taxa de atualização real do radar e o tempo gasto no ciclo
/// detecção → rastreamento → seleção de torre, para permitir observar a responsividade do
/// sistema durante uma demonstração/operação sem precisar instrumentar código externamente.
/// </summary>
public interface IPerformanceMonitorService
{
    /// <summary>Taxa de atualização configurada do radar, em Hz (<c>RadarSettings.RefreshRateMs</c>).</summary>
    double TaxaAtualizacaoConfiguradaHz { get; }

    /// <summary>Taxa de atualização medida do radar (renderizações por segundo, real), em Hz.</summary>
    double TaxaAtualizacaoMedidaHz { get; }

    /// <summary>Tempo de decisão: da chegada da leitura já processada em <see cref="ITargetTrackingService"/>
    /// (alvo criado/atualizado) até a conclusão de <see cref="ITowerSelectionService.SelectTowerFor"/>.</summary>
    MetricaTempo TempoDecisao { get; }

    /// <summary>Tempo de ciclo completo: da chegada de uma leitura (serial ou simulada) até o fim
    /// de todo o processamento reativo que ela dispara (rastreamento + seleção de torre +
    /// atualização do estado das torres).</summary>
    MetricaTempo TempoCicloCompleto { get; }

    /// <summary>Chamado a cada renderização do radar (ver <c>RadarControl</c>) para medir a taxa real.</summary>
    void RegisterRadarTick();

    /// <summary>Chamado ao concluir a seleção de torre para um alvo recém-detectado/atualizado.</summary>
    void RecordDecisionTime(double milissegundos);

    /// <summary>Chamado ao concluir o processamento completo de uma leitura recebida.</summary>
    void RecordCycleTime(double milissegundos);

    /// <summary>Zera todas as janelas de amostras (usado pelo botão "Zerar estatísticas" da tela de Desempenho).</summary>
    void Reset();
}
