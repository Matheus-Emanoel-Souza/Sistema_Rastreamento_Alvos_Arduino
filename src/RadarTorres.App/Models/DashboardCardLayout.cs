namespace RadarTorres.App.Models;

/// <summary>
/// Posição, tamanho, visibilidade e ordem de empilhamento de um card de um
/// <c>DashboardCanvas</c> (painel principal ou monitoramento). Posição/tamanho são salvos como
/// frações (0..1) do tamanho do canvas no momento em que o usuário soltou o arraste/
/// redimensionamento — e não em pixels absolutos — para que o layout continue proporcional em
/// qualquer resolução de tela (Requisito "manter o layout responsivo em diferentes tamanhos de
/// tela").
/// </summary>
public sealed class DashboardCardLayout
{
    public double RelX { get; set; }

    public double RelY { get; set; }

    public double RelWidth { get; set; }

    public double RelHeight { get; set; }

    /// <summary>Falso quando o usuário ocultou o card (Requisito "fechar/ocultar cada
    /// painel"). O card continua existindo no canvas, só não é exibido nem participa da
    /// checagem de sobreposição.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Ordem de empilhamento (Panel.ZIndex) — o card trazido mais recentemente para
    /// frente durante um arraste tem o maior valor. Faz parte do que é persistido (Requisito
    /// "ordem dos componentes").</summary>
    public int ZIndex { get; set; }

    /// <summary>
    /// Só usado hoje pelo card "Console de Eventos" da tela de Monitoramento (Requisito "fixar
    /// o console de logs na aba lateral direita"): quando verdadeiro, o card sai do
    /// <c>DashboardCanvas</c> arrastável e vira um painel encaixado na borda direita da tela —
    /// ver <c>MonitoramentoView.SetLogPinned</c>. Falso (padrão) para qualquer outro card, que
    /// nunca lê nem grava este campo.
    /// </summary>
    public bool IsPinnedRight { get; set; }
}
