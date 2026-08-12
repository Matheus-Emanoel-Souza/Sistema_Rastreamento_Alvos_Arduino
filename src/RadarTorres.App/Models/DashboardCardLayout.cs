namespace RadarTorres.App.Models;

/// <summary>
/// Posição e tamanho de um card do painel principal, salvos como frações (0..1) do tamanho
/// do <c>DashboardCanvas</c> no momento em que o usuário soltou o arraste/redimensionamento —
/// e não em pixels absolutos — para que o layout continue proporcional em qualquer resolução
/// de tela (Requisito "manter o layout responsivo em diferentes tamanhos de tela").
/// </summary>
public sealed class DashboardCardLayout
{
    public double RelX { get; set; }

    public double RelY { get; set; }

    public double RelWidth { get; set; }

    public double RelHeight { get; set; }
}
