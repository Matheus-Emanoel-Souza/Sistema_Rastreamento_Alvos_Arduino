namespace RadarTorres.App.Services;

/// <summary>
/// Alerta sonoro do card "Detecção por Câmera" (Monitoramento): toca um som alto e anuncia por
/// voz a distância do alvo identificado, quando o modo de operação está em Amarelo/Vermelho.
/// </summary>
public interface IAlertaSonoroService
{
    /// <summary>
    /// Dispara o alerta (som + voz) para a distância informada, em metros. Roda em segundo
    /// plano — nunca bloqueia quem chamou nem lança exceção (melhor esforço, ver
    /// <see cref="AlertaSonoroService"/>: TTS depende de voz instalada no Windows).
    /// </summary>
    void Disparar(double distanciaMetros);
}
