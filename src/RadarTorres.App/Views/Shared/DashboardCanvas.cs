using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using RadarTorres.App.Models;

namespace RadarTorres.App.Views.Shared;

/// <summary>
/// Canvas customizado que hospeda os <see cref="DashboardCard"/> de uma tela (painel principal
/// ou monitoramento) e é o único responsável por decidir se um arraste/redimensionamento
/// proposto por um card é válido: não pode sair da área visível nem sobrepor outro card visível
/// (Requisitos "arrastar e reposicionar livremente" e "evitar sobreposição"). Também:
/// * mantém o layout proporcional quando o tamanho do canvas muda (Requisito "responsivo em
///   diferentes tamanhos de tela"), reescalando Canvas.Left/Top/Width/Height de todos os cards
///   pela razão entre o tamanho novo e o anterior — o que preserva exatamente a fração ocupada
///   por cada card;
/// * oculta/reexibe cards individualmente (Requisitos "fechar/ocultar" e "reabrir sem
///   restaurar todo o layout") — um card oculto continua existindo como filho do canvas, só não
///   é desenhado nem participa da checagem de sobreposição;
/// * rastreia a ordem de empilhamento (Panel.ZIndex), trazendo um card para frente quando o
///   usuário começa a arrastá-lo/redimensioná-lo (Requisito "ordem dos componentes");
/// * "ímã" de alinhamento ao arrastar (Requisito "ajuda a deixar as janelas alinhadas"): ao
///   mover um card, se alguma de suas bordas/centro passa perto da borda/centro de outro card
///   visível (ou da borda/centro do próprio canvas), a posição é ajustada para encostar
///   exatamente nessa referência, com uma linha-guia tracejada mostrando onde alinhou —
///   mesmo princípio de "smart guides" de editores gráficos (PowerPoint, Figma).
/// </summary>
public class DashboardCanvas : Canvas
{
    public const double MinCardWidth = 200;
    public const double MinCardHeight = 110;

    private const double DefaultCardWidth = 260;
    private const double DefaultCardHeight = 150;
    private const double DefaultGap = 12;

    /// <summary>Distância máxima (em pixels) para uma borda/centro "grudar" em outra durante o
    /// arraste.</summary>
    private const double SnapThreshold = 8;

    private Size _lastSize = Size.Empty;
    private int _nextZIndex = 1;
    private Line? _verticalGuide;
    private Line? _horizontalGuide;

    /// <summary>Disparado depois de qualquer mudança concluída no layout — arraste/
    /// redimensionamento (Thumb solto), ocultar, reexibir, restaurar padrão ou aplicar um
    /// layout salvo. Quem hospeda o canvas usa isso para, por exemplo, atualizar o menu de
    /// "painéis ocultos"; não implica salvar em disco automaticamente (isso só acontece quando
    /// o usuário aciona "Definir layout como padrão" explicitamente).</summary>
    public event EventHandler? LayoutChanged;

    public DashboardCanvas()
    {
        SizeChanged += OnSizeChanged;
    }

    /// <summary>Aplica um deslocamento de arraste ao card, respeitando os limites do canvas e
    /// recusando o movimento (mantendo a posição anterior) se ele resultar em sobreposição com
    /// outro card visível. Antes de aplicar, tenta "imantar" cada eixo (independentemente) à
    /// borda/centro mais próximo de outro card visível ou do próprio canvas, dentro de
    /// <see cref="SnapThreshold"/> pixels — ver <see cref="SnapAxis"/>.</summary>
    public void RequestMove(DashboardCard card, double deltaX, double deltaY)
    {
        double width = card.ActualWidth > 0 ? card.ActualWidth : card.Width;
        double height = card.ActualHeight > 0 ? card.ActualHeight : card.Height;

        double left = Clamp(GetLeft(card) + deltaX, 0, Math.Max(0, ActualWidth - width));
        double top = Clamp(GetTop(card) + deltaY, 0, Math.Max(0, ActualHeight - height));

        var otherEdgesX = CollectSnapEdges(card, horizontal: true);
        var otherEdgesY = CollectSnapEdges(card, horizontal: false);
        (double snappedLeft, double? guideX) = SnapAxis(left, width, otherEdgesX);
        (double snappedTop, double? guideY) = SnapAxis(top, height, otherEdgesY);

        snappedLeft = Clamp(snappedLeft, 0, Math.Max(0, ActualWidth - width));
        snappedTop = Clamp(snappedTop, 0, Math.Max(0, ActualHeight - height));

        var snappedProposed = new Rect(snappedLeft, snappedTop, width, height);
        if (!OverlapsAnyVisible(card, snappedProposed))
        {
            SetLeft(card, snappedLeft);
            SetTop(card, snappedTop);
            UpdateSnapGuides(guideX, guideY);
            return;
        }

        // O ajuste do imã encostaria em outro card — tenta a posição "crua" (sem imã) antes de
        // desistir do movimento inteiro.
        var proposed = new Rect(left, top, width, height);
        if (OverlapsAnyVisible(card, proposed))
        {
            HideSnapGuides();
            return;
        }

        SetLeft(card, left);
        SetTop(card, top);
        HideSnapGuides();
    }

    /// <summary>Aplica um redimensionamento (a partir do canto inferior direito) ao card,
    /// respeitando o tamanho mínimo, os limites do canvas e recusando a mudança se ela resultar
    /// em sobreposição com outro card visível.</summary>
    public void RequestResize(DashboardCard card, double deltaWidth, double deltaHeight)
    {
        double left = GetLeft(card);
        double top = GetTop(card);
        double currentWidth = card.ActualWidth > 0 ? card.ActualWidth : card.Width;
        double currentHeight = card.ActualHeight > 0 ? card.ActualHeight : card.Height;

        double width = Clamp(currentWidth + deltaWidth, MinCardWidth, Math.Max(MinCardWidth, ActualWidth - left));
        double height = Clamp(currentHeight + deltaHeight, MinCardHeight, Math.Max(MinCardHeight, ActualHeight - top));

        var proposed = new Rect(left, top, width, height);
        if (OverlapsAnyVisible(card, proposed)) return;

        card.Width = width;
        card.Height = height;
    }

    /// <summary>Traz o card para frente dos demais (novo maior ZIndex) — chamado ao começar a
    /// arrastar/redimensionar.</summary>
    public void BringToFront(DashboardCard card) => SetZIndex(card, _nextZIndex++);

    /// <summary>Oculta o card (Requisito 1: fechar/ocultar). Ele continua no canvas — só some
    /// da tela e da checagem de sobreposição — pronto para ser reexibido por
    /// <see cref="ShowCard"/> sem afetar a posição de nenhum outro card.</summary>
    public void HideCard(DashboardCard card)
    {
        if (card.Visibility != Visibility.Visible) return;

        card.Visibility = Visibility.Collapsed;
        NotifyLayoutChanged();
    }

    /// <summary>Reexibe um card oculto (Requisito 2). Tenta primeiro a última posição/tamanho
    /// conhecidos; se isso agora sobrepuser outro card visível (por exemplo, algo foi movido
    /// para lá enquanto estava oculto), procura o próximo espaço livre no canvas.</summary>
    public void ShowCard(DashboardCard card)
    {
        if (card.Visibility == Visibility.Visible) return;

        double width = card.ActualWidth > 0 ? card.ActualWidth : (card.Width > 0 ? card.Width : card.DefaultWidth);
        double height = card.ActualHeight > 0 ? card.ActualHeight : (card.Height > 0 ? card.Height : card.DefaultHeight);
        var lastKnown = new Rect(GetLeft(card), GetTop(card), width, height);

        var target = OverlapsAnyVisible(card, lastKnown) ? FindFreeSlot(width, height) : lastKnown;

        SetLeft(card, target.X);
        SetTop(card, target.Y);
        card.Width = target.Width;
        card.Height = target.Height;
        card.Visibility = Visibility.Visible;
        BringToFront(card);
        NotifyLayoutChanged();
    }

    /// <summary>Cards atualmente ocultos, na ordem em que aparecem no canvas — usado para
    /// popular o menu "Painéis ocultos".</summary>
    public IReadOnlyList<DashboardCard> GetHiddenCards() =>
        Children.OfType<DashboardCard>().Where(c => c.Visibility != Visibility.Visible).ToList();

    /// <summary>Notifica que algo no layout mudou (arraste/redimensionamento concluído, card
    /// ocultado/reexibido, layout restaurado ou aplicado). Também esconde as linhas-guia do
    /// imã, que só fazem sentido durante o arraste em si.</summary>
    public void NotifyLayoutChanged()
    {
        HideSnapGuides();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Snapshot do layout atual (posição, tamanho, visibilidade e ordem de
    /// empilhamento), como frações do tamanho do canvas, pronto para persistir.</summary>
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
                IsVisible = card.Visibility == Visibility.Visible,
                ZIndex = GetZIndex(card),
            };
        }

        return snapshot;
    }

    /// <summary>Aplica um layout salvo anteriormente. Cards sem entrada no dicionário (ex.:
    /// versão nova com um card a mais) recebem o arranjo padrão individualmente, visíveis.</summary>
    public void ApplyLayoutSnapshot(Dictionary<string, DashboardCardLayout> layout)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        var cards = Children.OfType<DashboardCard>().ToList();
        var missing = new List<DashboardCard>();
        int maxZIndex = 0;

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
                card.Visibility = rect.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                SetZIndex(card, rect.ZIndex);
                maxZIndex = Math.Max(maxZIndex, rect.ZIndex);
            }
            else
            {
                missing.Add(card);
            }
        }

        if (missing.Count > 0)
        {
            ArrangeDefaultFlow(missing);
        }

        _nextZIndex = maxZIndex + 1;
        _lastSize = new Size(ActualWidth, ActualHeight);
        NotifyLayoutChanged();
    }

    /// <summary>Rearranja todos os cards no arranjo padrão do sistema (Requisito "retornar ao
    /// padrão do sistema"): todos visíveis, em fluxo da esquerda para a direita / cima para
    /// baixo na ordem em que aparecem no XAML, usando o tamanho preferido de cada um
    /// (<see cref="DashboardCard.DefaultWidth"/>/<see cref="DashboardCard.DefaultHeight"/>).
    /// Não mexe em nenhum arquivo salvo em disco — só no estado em memória/tela.</summary>
    public void ResetToDefaultLayout()
    {
        var cards = Children.OfType<DashboardCard>().ToList();
        ArrangeDefaultFlow(cards);
        _nextZIndex = 1;
        _lastSize = new Size(ActualWidth, ActualHeight);
        NotifyLayoutChanged();
    }

    private void ArrangeDefaultFlow(IReadOnlyList<DashboardCard> cards)
    {
        double canvasWidth = ActualWidth > 0 ? ActualWidth : DefaultCardWidth * 3 + DefaultGap * 4;

        double x = DefaultGap;
        double y = DefaultGap;
        double rowHeight = 0;

        foreach (DashboardCard card in cards)
        {
            double preferredWidth = card.DefaultWidth > 0 ? card.DefaultWidth : DefaultCardWidth;
            double preferredHeight = card.DefaultHeight > 0 ? card.DefaultHeight : DefaultCardHeight;
            double width = Clamp(preferredWidth, MinCardWidth, Math.Max(MinCardWidth, canvasWidth - DefaultGap * 2));
            double height = Math.Max(MinCardHeight, preferredHeight);

            if (x > DefaultGap && x + width + DefaultGap > canvasWidth)
            {
                x = DefaultGap;
                y += rowHeight + DefaultGap;
                rowHeight = 0;
            }

            SetLeft(card, x);
            SetTop(card, y);
            card.Width = width;
            card.Height = height;
            card.Visibility = Visibility.Visible;
            SetZIndex(card, 0);

            x += width + DefaultGap;
            rowHeight = Math.Max(rowHeight, height);
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

    /// <summary>Procura o primeiro espaço livre no canvas (varrendo em linhas) que comporte um
    /// card do tamanho informado sem sobrepor nenhum card visível — usado por
    /// <see cref="ShowCard"/> quando a última posição conhecida do card não está mais livre.</summary>
    private Rect FindFreeSlot(double width, double height)
    {
        double canvasWidth = ActualWidth > 0 ? ActualWidth : width + DefaultGap * 2;
        double canvasHeight = ActualHeight > 0 ? ActualHeight : height + DefaultGap * 2;
        width = Clamp(width, MinCardWidth, Math.Max(MinCardWidth, canvasWidth - DefaultGap * 2));
        height = Clamp(height, MinCardHeight, Math.Max(MinCardHeight, canvasHeight - DefaultGap * 2));

        int maxCols = Math.Max(1, (int)((canvasWidth - DefaultGap) / (width + DefaultGap)));
        int maxRows = Math.Max(1, (int)((canvasHeight - DefaultGap) / (height + DefaultGap)) + 1);

        for (int row = 0; row < maxRows; row++)
        {
            for (int col = 0; col < maxCols; col++)
            {
                double x = DefaultGap + col * (width + DefaultGap);
                double y = DefaultGap + row * (height + DefaultGap);
                var candidate = new Rect(x, y, width, height);
                if (!OverlapsAnyVisible(null, candidate)) return candidate;
            }
        }

        // Canvas sem espaço "limpo" (pequeno demais ou cheio) — reabre mesmo assim; o usuário
        // pode reposicionar manualmente depois. Melhor reaparecer do que ficar preso oculto.
        return new Rect(0, 0, width, height);
    }

    /// <summary>Testa se <paramref name="proposed"/> colide com algum card visível diferente de
    /// <paramref name="exclude"/> — cards ocultos nunca contam para colisão.</summary>
    private bool OverlapsAnyVisible(DashboardCard? exclude, Rect proposed)
    {
        foreach (DashboardCard other in Children.OfType<DashboardCard>())
        {
            if (ReferenceEquals(other, exclude)) continue;
            if (other.Visibility != Visibility.Visible) continue;

            double otherWidth = other.ActualWidth > 0 ? other.ActualWidth : other.Width;
            double otherHeight = other.ActualHeight > 0 ? other.ActualHeight : other.Height;
            var otherRect = new Rect(GetLeft(other), GetTop(other), otherWidth, otherHeight);

            if (proposed.IntersectsWith(otherRect)) return true;
        }

        return false;
    }

    private static double Clamp(double value, double min, double max) =>
        max < min ? min : Math.Min(Math.Max(value, min), max);

    // ---------------------------------------------------------------- Imã de alinhamento

    /// <summary>Posições (início, fim, centro) de referência no eixo pedido, vindas de todos os
    /// cards visíveis diferentes de <paramref name="exclude"/> e das bordas/centro do próprio
    /// canvas — candidatas a "imantar" o card que está sendo arrastado.</summary>
    private List<double> CollectSnapEdges(DashboardCard exclude, bool horizontal)
    {
        var edges = new List<double>();

        double canvasExtent = horizontal ? ActualWidth : ActualHeight;
        if (canvasExtent > 0)
        {
            edges.Add(0);
            edges.Add(canvasExtent);
            edges.Add(canvasExtent / 2);
        }

        foreach (DashboardCard other in Children.OfType<DashboardCard>())
        {
            if (ReferenceEquals(other, exclude) || other.Visibility != Visibility.Visible) continue;

            double start = horizontal ? GetLeft(other) : GetTop(other);
            double extent = horizontal
                ? (other.ActualWidth > 0 ? other.ActualWidth : other.Width)
                : (other.ActualHeight > 0 ? other.ActualHeight : other.Height);

            edges.Add(start);
            edges.Add(start + extent);
            edges.Add(start + extent / 2);
        }

        return edges;
    }

    /// <summary>Tenta ajustar <paramref name="start"/> (posição bruta, já dentro dos limites do
    /// canvas) para que o início, o fim ou o centro do card coincida exatamente com alguma
    /// referência em <paramref name="candidates"/>, se a diferença for menor que
    /// <see cref="SnapThreshold"/>. Entre várias referências dentro do limite, usa a mais
    /// próxima. Retorna a posição ajustada (ou a original, se nada "imantou") e a coordenada da
    /// linha-guia a desenhar, se houve ajuste.</summary>
    private static (double Value, double? Guide) SnapAxis(double start, double extent, IReadOnlyList<double> candidates)
    {
        double end = start + extent;
        double center = start + extent / 2;

        double bestDistance = SnapThreshold;
        double? bestValue = null;
        double? bestGuide = null;

        foreach (double candidate in candidates)
        {
            TryBetter(candidate - start, start, candidate);
            TryBetter(candidate - end, start + (candidate - end), candidate);
            TryBetter(candidate - center, start + (candidate - center), candidate);
        }

        return (bestValue ?? start, bestGuide);

        void TryBetter(double delta, double snappedStart, double guide)
        {
            double distance = Math.Abs(delta);
            if (distance >= bestDistance) return;

            bestDistance = distance;
            bestValue = snappedStart;
            bestGuide = guide;
        }
    }

    /// <summary>Cria (uma única vez) as duas linhas-guia usadas pelo imã e as adiciona ao
    /// canvas, sempre por cima dos cards.</summary>
    private void EnsureSnapGuides()
    {
        if (_verticalGuide is not null) return;

        var guideBrush = TryFindBrush("AccentBrush", Brushes.DeepSkyBlue);

        _verticalGuide = new Line { Stroke = guideBrush, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 3 }, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        _horizontalGuide = new Line { Stroke = guideBrush, StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 3 }, Visibility = Visibility.Collapsed, IsHitTestVisible = false };

        Children.Add(_verticalGuide);
        Children.Add(_horizontalGuide);
        SetZIndex(_verticalGuide, int.MaxValue);
        SetZIndex(_horizontalGuide, int.MaxValue);
    }

    /// <summary>Mostra as linhas-guia nas coordenadas onde o imã "grudou" (ou esconde o eixo
    /// correspondente, se não houve ajuste naquele eixo).</summary>
    private void UpdateSnapGuides(double? guideX, double? guideY)
    {
        EnsureSnapGuides();

        if (guideX.HasValue)
        {
            _verticalGuide!.X1 = _verticalGuide.X2 = guideX.Value;
            _verticalGuide.Y1 = 0;
            _verticalGuide.Y2 = Math.Max(ActualHeight, 1);
            _verticalGuide.Visibility = Visibility.Visible;
        }
        else
        {
            _verticalGuide!.Visibility = Visibility.Collapsed;
        }

        if (guideY.HasValue)
        {
            _horizontalGuide!.Y1 = _horizontalGuide.Y2 = guideY.Value;
            _horizontalGuide.X1 = 0;
            _horizontalGuide.X2 = Math.Max(ActualWidth, 1);
            _horizontalGuide.Visibility = Visibility.Visible;
        }
        else
        {
            _horizontalGuide!.Visibility = Visibility.Collapsed;
        }
    }

    private void HideSnapGuides()
    {
        if (_verticalGuide is not null) _verticalGuide.Visibility = Visibility.Collapsed;
        if (_horizontalGuide is not null) _horizontalGuide.Visibility = Visibility.Collapsed;
    }

    private static Brush TryFindBrush(string key, Brush fallback) =>
        Application.Current.TryFindResource(key) as Brush ?? fallback;
}
