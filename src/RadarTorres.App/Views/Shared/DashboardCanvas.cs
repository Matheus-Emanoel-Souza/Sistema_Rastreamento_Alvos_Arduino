using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RadarTorres.App.Models;

namespace RadarTorres.App.Views.Shared;

/// <summary>
/// Canvas customizado que hospeda os <see cref="DashboardCard"/> do painel principal e é o
/// único responsável por decidir se um arraste/redimensionamento proposto por um card é válido:
/// não pode sair da área visível nem sobrepor outro card (Requisitos "arrastar e reposicionar
/// livremente" e "evitar sobreposição"). Também mantém o layout proporcional quando o tamanho
/// do canvas muda (Requisito "responsivo em diferentes tamanhos de tela"), reescalando
/// Canvas.Left/Top/Width/Height de todos os cards pela razão entre o tamanho novo e o anterior
/// — o que preserva exatamente a fração ocupada por cada card.
/// </summary>
public class DashboardCanvas : Canvas
{
    public const double MinCardWidth = 200;
    public const double MinCardHeight = 110;

    private const double DefaultCardWidth = 260;
    private const double DefaultCardHeight = 150;
    private const double DefaultGap = 12;
    private const int DefaultColumns = 3;

    private Size _lastSize = Size.Empty;

    /// <summary>Disparado depois que um arraste ou redimensionamento é concluído (Thumb solto)
    /// e a posição/tamanho final é válida — o assinante (a View) persiste o layout nesse ponto,
    /// em vez de a cada pixel de DragDelta.</summary>
    public event EventHandler? LayoutChanged;

    public DashboardCanvas()
    {
        SizeChanged += OnSizeChanged;
    }

    /// <summary>Aplica um deslocamento de arraste ao card, respeitando os limites do canvas e
    /// recusando o movimento (mantendo a posição anterior) se ele resultar em sobreposição com
    /// outro card.</summary>
    public void RequestMove(DashboardCard card, double deltaX, double deltaY)
    {
        double width = card.ActualWidth > 0 ? card.ActualWidth : card.Width;
        double height = card.ActualHeight > 0 ? card.ActualHeight : card.Height;

        double left = Clamp(GetLeft(card) + deltaX, 0, Math.Max(0, ActualWidth - width));
        double top = Clamp(GetTop(card) + deltaY, 0, Math.Max(0, ActualHeight - height));

        var proposed = new Rect(left, top, width, height);
        if (OverlapsAny(card, proposed)) return;

        SetLeft(card, left);
        SetTop(card, top);
    }

    /// <summary>Aplica um redimensionamento (a partir do canto inferior direito) ao card,
    /// respeitando o tamanho mínimo, os limites do canvas e recusando a mudança se ela resultar
    /// em sobreposição com outro card.</summary>
    public void RequestResize(DashboardCard card, double deltaWidth, double deltaHeight)
    {
        double left = GetLeft(card);
        double top = GetTop(card);
        double currentWidth = card.ActualWidth > 0 ? card.ActualWidth : card.Width;
        double currentHeight = card.ActualHeight > 0 ? card.ActualHeight : card.Height;

        double width = Clamp(currentWidth + deltaWidth, MinCardWidth, Math.Max(MinCardWidth, ActualWidth - left));
        double height = Clamp(currentHeight + deltaHeight, MinCardHeight, Math.Max(MinCardHeight, ActualHeight - top));

        var proposed = new Rect(left, top, width, height);
        if (OverlapsAny(card, proposed)) return;

        card.Width = width;
        card.Height = height;
    }

    /// <summary>Notifica que um gesto (arraste ou redimensionamento) terminou — a View escuta
    /// isso para gravar o layout em disco.</summary>
    public void NotifyLayoutChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Snapshot do layout atual, como frações do tamanho do canvas, pronto para
    /// persistir.</summary>
    public Dictionary<string, DashboardCardLayout> GetLayoutSnapshot()
    {
        var snapshot = new Dictionary<string, DashboardCardLayout>();
        if (ActualWidth <= 0 || ActualHeight <= 0) return snapshot;

        foreach (DashboardCard card in Children.OfType<DashboardCard>())
        {
            if (string.IsNullOrWhiteSpace(card.CardId)) continue;

            snapshot[card.CardId] = new DashboardCardLayout
            {
                RelX = GetLeft(card) / ActualWidth,
                RelY = GetTop(card) / ActualHeight,
                RelWidth = (card.ActualWidth > 0 ? card.ActualWidth : card.Width) / ActualWidth,
                RelHeight = (card.ActualHeight > 0 ? card.ActualHeight : card.Height) / ActualHeight,
            };
        }

        return snapshot;
    }

    /// <summary>Aplica um layout salvo anteriormente. Cards sem entrada no dicionário (ex.:
    /// versão nova com um card a mais) recebem o layout padrão individualmente.</summary>
    public void ApplyLayoutSnapshot(Dictionary<string, DashboardCardLayout> layout)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var cards = Children.OfType<DashboardCard>().ToList();
        var missing = new List<DashboardCard>();

        foreach (DashboardCard card in cards)
        {
            if (!string.IsNullOrWhiteSpace(card.CardId) && layout.TryGetValue(card.CardId, out DashboardCardLayout? rect))
            {
                double width = Clamp(rect.RelWidth * ActualWidth, MinCardWidth, ActualWidth);
                double height = Clamp(rect.RelHeight * ActualHeight, MinCardHeight, ActualHeight);
                SetLeft(card, Clamp(rect.RelX * ActualWidth, 0, Math.Max(0, ActualWidth - width)));
                SetTop(card, Clamp(rect.RelY * ActualHeight, 0, Math.Max(0, ActualHeight - height)));
                card.Width = width;
                card.Height = height;
            }
            else
            {
                missing.Add(card);
            }
        }

        if (missing.Count > 0)
        {
            ArrangeDefaultGrid(missing);
        }

        _lastSize = new Size(ActualWidth, ActualHeight);
    }

    /// <summary>Rearranja todos os cards em uma grade padrão (Requisito "restaurar layout
    /// padrão"), na ordem em que aparecem no XAML.</summary>
    public void ResetToDefaultLayout()
    {
        ArrangeDefaultGrid(Children.OfType<DashboardCard>().ToList());
        _lastSize = new Size(ActualWidth, ActualHeight);
    }

    private void ArrangeDefaultGrid(IReadOnlyList<DashboardCard> cards)
    {
        double canvasWidth = ActualWidth > 0 ? ActualWidth : DefaultCardWidth * DefaultColumns + DefaultGap * (DefaultColumns + 1);
        double columnWidth = Math.Max(MinCardWidth, (canvasWidth - DefaultGap * (DefaultColumns + 1)) / DefaultColumns);

        for (int i = 0; i < cards.Count; i++)
        {
            int row = i / DefaultColumns;
            int column = i % DefaultColumns;

            SetLeft(cards[i], DefaultGap + column * (columnWidth + DefaultGap));
            SetTop(cards[i], DefaultGap + row * (DefaultCardHeight + DefaultGap));
            cards[i].Width = columnWidth;
            cards[i].Height = DefaultCardHeight;
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_lastSize == Size.Empty || _lastSize.Width <= 0 || _lastSize.Height <= 0)
        {
            _lastSize = e.NewSize;
            return;
        }

        double scaleX = e.NewSize.Width / _lastSize.Width;
        double scaleY = e.NewSize.Height / _lastSize.Height;

        foreach (DashboardCard card in Children.OfType<DashboardCard>())
        {
            double width = Clamp((card.ActualWidth > 0 ? card.ActualWidth : card.Width) * scaleX, MinCardWidth, e.NewSize.Width);
            double height = Clamp((card.ActualHeight > 0 ? card.ActualHeight : card.Height) * scaleY, MinCardHeight, e.NewSize.Height);
            SetLeft(card, Clamp(GetLeft(card) * scaleX, 0, Math.Max(0, e.NewSize.Width - width)));
            SetTop(card, Clamp(GetTop(card) * scaleY, 0, Math.Max(0, e.NewSize.Height - height)));
            card.Width = width;
            card.Height = height;
        }

        _lastSize = e.NewSize;
    }

    private bool OverlapsAny(DashboardCard card, Rect proposed)
    {
        foreach (DashboardCard other in Children.OfType<DashboardCard>())
        {
            if (ReferenceEquals(other, card)) continue;

            double otherWidth = other.ActualWidth > 0 ? other.ActualWidth : other.Width;
            double otherHeight = other.ActualHeight > 0 ? other.ActualHeight : other.Height;
            var otherRect = new Rect(GetLeft(other), GetTop(other), otherWidth, otherHeight);

            if (proposed.IntersectsWith(otherRect)) return true;
        }

        return false;
    }

    private static double Clamp(double value, double min, double max) =>
        max < min ? min : Math.Min(Math.Max(value, min), max);
}
