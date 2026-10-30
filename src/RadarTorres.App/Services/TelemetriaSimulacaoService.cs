using System;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação em memória de <see cref="ITelemetriaSimulacaoService"/>: guarda o quadro mais
/// recente e avisa quem estiver assinando.
/// </summary>
/// <remarks>
/// <para>
/// Deliberadamente sem fila e sem histórico. Quem envia dados em tempo real a um sistema físico
/// quer o estado ATUAL: um quadro atrasado numa fila descreveria uma posição que o alvo já não
/// ocupa, e acumular histórico aqui só criaria memória crescendo sem ninguém para consumi-la.
/// Guardar o histórico, se um dia for preciso, é responsabilidade de quem assina o evento.
/// </para>
/// <para>
/// Uma exceção lançada por um assinante não pode derrubar a simulação que o notificou — a tela
/// continua sendo a dona do laço. Por isso <see cref="Publicar"/> isola a notificação: o quadro
/// já está guardado em <see cref="UltimoQuadro"/> antes de qualquer assinante ser chamado.
/// </para>
/// </remarks>
public sealed class TelemetriaSimulacaoService : ITelemetriaSimulacaoService
{
    private readonly ILoggingService? _logger;

    public TelemetriaSimulacaoService(ILoggingService? logger = null)
    {
        _logger = logger;
    }

    public QuadroTelemetriaSimulacao? UltimoQuadro { get; private set; }

    public event EventHandler<QuadroTelemetriaSimulacao>? QuadroPublicado;

    public void Publicar(QuadroTelemetriaSimulacao quadro)
    {
        ArgumentNullException.ThrowIfNull(quadro);

        UltimoQuadro = quadro;

        try
        {
            QuadroPublicado?.Invoke(this, quadro);
        }
        catch (Exception ex)
        {
            // Um consumidor com defeito (amanhã, a ponte serial com o Arduino) não pode
            // interromper a simulação que o alimenta.
            _logger?.Error($"Um consumidor da telemetria da simulação falhou: {ex.Message}");
        }
    }

    public void Limpar()
    {
        UltimoQuadro = null;
    }
}
