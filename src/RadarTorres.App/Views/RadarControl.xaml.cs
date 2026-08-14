using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using RadarTorres.App.Configuration;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;

namespace RadarTorres.App.Views;

/// <summary>
/// Radar circular: desenha a base, os círculos de distância, as linhas de quadrante, as torres
/// e os alvos, convertendo coordenadas de mundo (metros) para pixels através de
/// <see cref="CoordinateConverter.WorldToScreen"/>.
/// </summary>
/// <remarks>
/// A atualização é feita por um <see cref="DispatcherTimer"/> próprio (não a cada PropertyChanged
/// individual), na frequência configurada em <c>RadarSettings.RefreshRateMs</c>. A cada tick os
/// elementos visuais já existentes (dicionários <c>_targetVisuals</c>/<c>_towerVisuals</c>) são
/// apenas reposicionados/recoloridos — novos elementos só são criados para alvos/torres novos,
/// e removidos apenas quando o alvo correspondente deixa de existir. Isso evita recriar toda a
/// árvore visual a cada atualização, conforme pedido no enunciado do TCC.
/// </remarks>
public partial class RadarControl : UserControl
{
    private sealed class TargetVisual
    {
        public required Ellipse Circle;
        public required TextBlock Label;
        public Line? TowerLink;
    }

    private sealed class TowerVisual
    {
        public required Polygon Shape;
        public required TextBlock Label;
    }

    public static readonly DependencyProperty TargetsProperty =
        DependencyProperty.Register(nameof(Targets), typeof(IEnumerable), typeof(RadarControl), new PropertyMetadata(null));

    public static readonly DependencyProperty TowersProperty =
        DependencyProperty.Register(nameof(Towers), typeof(IEnumerable), typeof(RadarControl), new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedTargetIdProperty =
        DependencyProperty.Register(nameof(SelectedTargetId), typeof(int?), typeof(RadarControl), new PropertyMetadata(null));

    public static readonly DependencyProperty DeadZonesProperty =
        DependencyProperty.Register(nameof(DeadZones), typeof(IEnumerable), typeof(RadarControl),
            new PropertyMetadata(null, OnDeadZonesChanged));

    /// <summary>
    /// <c>null</c> = clique no radar funciona só como seleção de alvo (comportamento normal).
    /// <see cref="DeadZoneType.Quadrant"/>/<see cref="DeadZoneType.DistanceRange"/> = clique
    /// (quadrante) ou arraste radial (faixa de distância) no radar define uma zona morta — ver
    /// <see cref="RadarCanvas_MouseLeftButtonDown"/>.
    /// </summary>
    public static readonly DependencyProperty DeadZoneEditModeProperty =
        DependencyProperty.Register(nameof(DeadZoneEditMode), typeof(DeadZoneType?), typeof(RadarControl),
            new PropertyMetadata(null, OnDeadZoneEditModeChanged));

    public IEnumerable? Targets
    {
        get => (IEnumerable?)GetValue(TargetsProperty);
        set => SetValue(TargetsProperty, value);
    }

    public IEnumerable? Towers
    {
        get => (IEnumerable?)GetValue(TowersProperty);
        set => SetValue(TowersProperty, value);
    }

    /// <summary>Zonas mortas ativas, desenhadas na camada estática (sombreamento translúcido
    /// sobre o quadrante ou a faixa de distância bloqueada). Opcional — se não vinculado, o
    /// radar simplesmente não desenha nenhum sombreamento.</summary>
    public IEnumerable? DeadZones
    {
        get => (IEnumerable?)GetValue(DeadZonesProperty);
        set => SetValue(DeadZonesProperty, value);
    }

    public DeadZoneType? DeadZoneEditMode
    {
        get => (DeadZoneType?)GetValue(DeadZoneEditModeProperty);
        set => SetValue(DeadZoneEditModeProperty, value);
    }

    public int? SelectedTargetId
    {
        get => (int?)GetValue(SelectedTargetIdProperty);
        set => SetValue(SelectedTargetIdProperty, value);
    }

    /// <summary>Disparado quando o usuário clica em um alvo desenhado no radar.</summary>
    public event EventHandler<int>? TargetClicked;

    /// <summary>Disparado ao clicar dentro de um quadrante do radar com <see cref="DeadZoneEditMode"/>
    /// igual a <see cref="DeadZoneType.Quadrant"/>.</summary>
    public event EventHandler<Quadrant>? DeadZoneQuadrantSelected;

    /// <summary>Disparado ao soltar o botão do mouse após arrastar radialmente no radar com
    /// <see cref="DeadZoneEditMode"/> igual a <see cref="DeadZoneType.DistanceRange"/> — os
    /// valores já vêm ordenados (mínima ≤ máxima) e limitados ao alcance atual do radar.</summary>
    public event EventHandler<(double MinDistance, double MaxDistance)>? DeadZoneRangeSelected;

    private const double MinZoom = 0.5;
    private const double MaxZoom = 3.0;
    private const double ZoomStep = 0.25;

    private const double MinRangeMultiplier = 0.5;
    private const double MaxRangeMultiplier = 5.0;
    private const double RangeStep = 0.5;

    private readonly DispatcherTimer _renderTimer;
    private readonly Dictionary<int, TargetVisual> _targetVisuals = new();
    private readonly Dictionary<int, TowerVisual> _towerVisuals = new();
    private bool _staticLayerDirty = true;
    private INotifyCollectionChanged? _watchedDeadZonesCollection;

    // ---- Estado da última renderização, guardado para converter coordenadas de mouse (que
    //      chegam entre um Render() e outro) de volta para metros — ver ScreenToWorldMeters.
    private double _lastRenderedSize;
    private double _lastRenderedMaxDistance = 1;

    // ---- Arraste radial em andamento (modo DistanceRange) — ver RadarCanvas_MouseLeftButtonDown/Move/Up.
    private bool _isDraggingRange;
    private double _dragStartMeters;
    private Ellipse? _rangePreviewStart;
    private Ellipse? _rangePreviewCurrent;

    /// <summary>Largura mínima (m) de uma faixa arrastada para valer como zona — evita que um
    /// clique simples (sem arraste de verdade) no modo Faixa crie uma zona de espessura ~0.</summary>
    private const double MinRangeDragMeters = 0.15;

    /// <summary>Fator de escala visual do radar, independente do tamanho do card que o hospeda
    /// (quão grande o círculo aparece na tela). 1.0 = preenche exatamente o espaço disponível;
    /// acima disso o radar fica maior que o espaço visível e pode ser rolado
    /// (<see cref="RadarScrollViewer"/>); abaixo disso fica menor, centralizado com margem ao
    /// redor. Não muda o que é mostrado, só o tamanho em pixels — para "enxergar mais longe"
    /// (mesmo espaço na tela representando uma distância maior), ver <see cref="_rangeMultiplier"/>.</summary>
    private double _zoom = 1.0;

    /// <summary>Multiplica <c>RadarSettings.MaxDetectionDistanceMeters</c> (Requisito "aumentar
    /// o raio do radar, como se quisesse ver coisas mais longe"): acima de 1.0, o círculo
    /// externo passa a representar uma distância maior — alvos/torres mais distantes da base
    /// ficam visíveis dentro do mesmo círculo (mais "encolhidos" em relação ao centro); abaixo
    /// de 1.0, o círculo mostra só as proximidades da base, em mais detalhe.</summary>
    private double _rangeMultiplier = 1.0;

    /// <summary>Distância (em metros) representada pela borda externa do radar agora —
    /// distância configurada em <c>appsettings.json</c> multiplicada pelo alcance atual.</summary>
    private double EffectiveMaxDistanceMeters => AppConfig.Current.RadarSettings.MaxDetectionDistanceMeters * _rangeMultiplier;

    public RadarControl()
    {
        InitializeComponent();

        RangeLabel.Text = $"{EffectiveMaxDistanceMeters:0.#} m";

        int refreshMs = Math.Max(30, AppConfig.Current.RadarSettings.RefreshRateMs);
        _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(refreshMs)
        };
        _renderTimer.Tick += (_, _) => Render();

        Loaded += (_, _) => { _renderTimer.Start(); Render(); };
        Unloaded += (_, _) => _renderTimer.Stop();
        SizeChanged += (_, _) => { _staticLayerDirty = true; Render(); };

        RadarCanvas.MouseLeftButtonDown += RadarCanvas_MouseLeftButtonDown;
        RadarCanvas.MouseMove += RadarCanvas_MouseMove;
        RadarCanvas.MouseLeftButtonUp += RadarCanvas_MouseLeftButtonUp;
        RadarCanvas.LostMouseCapture += (_, _) => CancelRangeDrag();
    }

    private void ZoomInButton_Click(object sender, RoutedEventArgs e) => ChangeZoom(ZoomStep);

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e) => ChangeZoom(-ZoomStep);

    private void ZoomResetButton_Click(object sender, RoutedEventArgs e) => SetZoom(1.0);

    /// <summary>Reage à troca da coleção inteira vinculada em <see cref="DeadZones"/> (o binding
    /// só dispara isso uma vez, quando resolve — a coleção em si nunca é trocada em tempo de
    /// execução, é sempre a mesma <c>ObservableCollection</c> do serviço). Passa a observar
    /// tanto a coleção (zonas adicionadas/removidas) quanto cada zona individualmente (o
    /// campo <see cref="DeadZone.Enabled"/> muda sem a coleção em si mudar).</summary>
    private static void OnDeadZonesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (RadarControl)d;
        control.DetachDeadZoneWatchers(e.OldValue as IEnumerable);
        control.AttachDeadZoneWatchers(e.NewValue as IEnumerable);
        control._staticLayerDirty = true;
        control.Render();
    }

    private void AttachDeadZoneWatchers(IEnumerable? source)
    {
        if (source is INotifyCollectionChanged incc)
        {
            _watchedDeadZonesCollection = incc;
            incc.CollectionChanged += DeadZones_CollectionChanged;
        }

        if (source is null) return;
        foreach (DeadZone zone in source.Cast<DeadZone>())
        {
            zone.PropertyChanged += DeadZone_PropertyChanged;
        }
    }

    private void DetachDeadZoneWatchers(IEnumerable? source)
    {
        if (_watchedDeadZonesCollection is not null)
        {
            _watchedDeadZonesCollection.CollectionChanged -= DeadZones_CollectionChanged;
            _watchedDeadZonesCollection = null;
        }

        if (source is null) return;
        foreach (DeadZone zone in source.Cast<DeadZone>())
        {
            zone.PropertyChanged -= DeadZone_PropertyChanged;
        }
    }

    private void DeadZones_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (DeadZone zone in e.OldItems) zone.PropertyChanged -= DeadZone_PropertyChanged;
        }
        if (e.NewItems is not null)
        {
            foreach (DeadZone zone in e.NewItems) zone.PropertyChanged += DeadZone_PropertyChanged;
        }

        _staticLayerDirty = true;
        Render();
    }

    private void DeadZone_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DeadZone.Enabled)) return;

        _staticLayerDirty = true;
        Render();
    }

    /// <summary>Só troca o cursor (cruz = "clique/arraste aqui define uma zona morta") — nenhuma
    /// outra reação necessária, o modo em si é lido diretamente de <see cref="DeadZoneEditMode"/>
    /// a cada evento de mouse.</summary>
    private static void OnDeadZoneEditModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (RadarControl)d;
        control.RadarCanvas.Cursor = e.NewValue is null ? Cursors.Arrow : Cursors.Cross;
        if (e.NewValue is null) control.CancelRangeDrag();
    }

    /// <summary>
    /// Início da interação de zona morta no radar (Requisito "definir zona morta com o mouse").
    /// Ignorado quando <see cref="DeadZoneEditMode"/> é <c>null</c> (comportamento normal —
    /// clique só seleciona alvo, tratado pelo handler do próprio <see cref="Ellipse"/> do alvo,
    /// que marca <c>e.Handled</c> e por isso nunca chega aqui) ou quando o clique caiu em cima
    /// de um alvo/torre (mesmo motivo).
    /// </summary>
    private void RadarCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DeadZoneEditMode is null) return;

        (double worldX, double worldY) = ScreenToWorldMeters(e.GetPosition(RadarCanvas));

        if (DeadZoneEditMode == DeadZoneType.Quadrant)
        {
            Quadrant quadrant = QuadrantHelper.Determine(worldX, worldY);
            if (quadrant != Quadrant.None)
            {
                DeadZoneQuadrantSelected?.Invoke(this, quadrant);
            }
            e.Handled = true;
            return;
        }

        // DeadZoneType.DistanceRange: começa o arraste radial — o raio (metros) do ponto de
        // clique vira uma das duas bordas da faixa, a outra borda é onde o botão for solto.
        _isDraggingRange = true;
        _dragStartMeters = DistanceFromCenter(worldX, worldY);
        RadarCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void RadarCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingRange) return;

        (double worldX, double worldY) = ScreenToWorldMeters(e.GetPosition(RadarCanvas));
        UpdateRangePreview(_dragStartMeters, DistanceFromCenter(worldX, worldY));
    }

    private void RadarCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingRange) return;

        (double worldX, double worldY) = ScreenToWorldMeters(e.GetPosition(RadarCanvas));
        double endMeters = DistanceFromCenter(worldX, worldY);
        double startMeters = _dragStartMeters;

        CancelRangeDrag(); // já solta a captura e remove a prévia antes de notificar o evento

        double min = Math.Min(startMeters, endMeters);
        double max = Math.Max(startMeters, endMeters);
        if (max - min < MinRangeDragMeters) return; // clique sem arraste de verdade — ignora

        DeadZoneRangeSelected?.Invoke(this, (min, Math.Min(max, _lastRenderedMaxDistance)));
    }

    /// <summary>Interrompe um arraste de faixa em andamento sem disparar
    /// <see cref="DeadZoneRangeSelected"/> — usado ao desligar o modo de edição no meio de um
    /// arraste e como limpeza comum antes de notificar um arraste concluído.</summary>
    private void CancelRangeDrag()
    {
        if (!_isDraggingRange) return;

        _isDraggingRange = false;
        if (RadarCanvas.IsMouseCaptured) RadarCanvas.ReleaseMouseCapture();
        RemoveRangePreview();
    }

    private (double X, double Y) ScreenToWorldMeters(Point screenPoint) =>
        CoordinateConverter.ScreenToWorld(screenPoint, _lastRenderedSize, _lastRenderedMaxDistance);

    private static double DistanceFromCenter(double worldX, double worldY) => Math.Sqrt(worldX * worldX + worldY * worldY);

    /// <summary>Dois círculos tracejados (raio inicial do arraste + raio atual do mouse) dando
    /// feedback visual de onde a faixa ficaria se o botão fosse solto agora.</summary>
    private void UpdateRangePreview(double startMeters, double currentMeters)
    {
        double radius = _lastRenderedSize / 2.0;
        var center = new Point(radius, radius);

        _rangePreviewStart ??= CreateRangePreviewCircle();
        _rangePreviewCurrent ??= CreateRangePreviewCircle();

        PositionRangePreviewCircle(_rangePreviewStart, center, MetersToPixels(startMeters, radius, _lastRenderedMaxDistance));
        PositionRangePreviewCircle(_rangePreviewCurrent, center, MetersToPixels(currentMeters, radius, _lastRenderedMaxDistance));
    }

    private Ellipse CreateRangePreviewCircle()
    {
        var circle = new Ellipse
        {
            Stroke = TryFindBrush("DangerBrush", Brushes.Red),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        DynamicLayer.Children.Add(circle);
        return circle;
    }

    private static void PositionRangePreviewCircle(Ellipse circle, Point center, double radiusPx)
    {
        circle.Width = circle.Height = Math.Max(0, radiusPx * 2);
        Canvas.SetLeft(circle, center.X - radiusPx);
        Canvas.SetTop(circle, center.Y - radiusPx);
    }

    private void RemoveRangePreview()
    {
        if (_rangePreviewStart is not null)
        {
            DynamicLayer.Children.Remove(_rangePreviewStart);
            _rangePreviewStart = null;
        }
        if (_rangePreviewCurrent is not null)
        {
            DynamicLayer.Children.Remove(_rangePreviewCurrent);
            _rangePreviewCurrent = null;
        }
    }

    private void RangeInButton_Click(object sender, RoutedEventArgs e) => ChangeRange(RangeStep);

    private void RangeOutButton_Click(object sender, RoutedEventArgs e) => ChangeRange(-RangeStep);

    private void RangeResetButton_Click(object sender, RoutedEventArgs e) => SetRange(1.0);

    /// <summary>Ctrl+roda do mouse controla o zoom visual; Shift+roda controla o alcance (raio)
    /// do radar — mesmo princípio de mapas/editores gráficos. Sem modificador, a roda continua
    /// rolando o conteúdo normalmente pelo ScrollViewer.</summary>
    private void RadarScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        switch (Keyboard.Modifiers)
        {
            case ModifierKeys.Control:
                ChangeZoom(e.Delta > 0 ? ZoomStep : -ZoomStep);
                e.Handled = true;
                break;
            case ModifierKeys.Shift:
                ChangeRange(e.Delta > 0 ? RangeStep : -RangeStep);
                e.Handled = true;
                break;
        }
    }

    private void ChangeZoom(double delta) => SetZoom(_zoom + delta);

    private void SetZoom(double value)
    {
        double clamped = Math.Clamp(value, MinZoom, MaxZoom);
        if (Math.Abs(clamped - _zoom) < 0.001) return;

        _zoom = clamped;
        ZoomLabel.Text = $"{_zoom:0%}";
        _staticLayerDirty = true;
        Render();
    }

    private void ChangeRange(double delta) => SetRange(_rangeMultiplier + delta);

    private void SetRange(double multiplier)
    {
        double clamped = Math.Clamp(multiplier, MinRangeMultiplier, MaxRangeMultiplier);
        if (Math.Abs(clamped - _rangeMultiplier) < 0.001) return;

        _rangeMultiplier = clamped;
        RangeLabel.Text = $"{EffectiveMaxDistanceMeters:0.#} m";
        _staticLayerDirty = true; // os rótulos de distância dos anéis mudam com o alcance
        Render();
    }

    private void Render()
    {
        // Mede o espaço disponível pelo próprio controle (RootGrid), nunca por RadarCanvas:
        // DrawStaticLayer fixa RadarCanvas.Width/Height a cada redesenho para manter o radar
        // quadrado/centralizado dentro de uma área que pode não ser quadrada — se a medição
        // partisse do próprio RadarCanvas, ela ficaria "presa" no último tamanho que nós mesmos
        // fixamos, e o radar nunca acompanharia o card encolhendo ou crescendo depois da
        // primeira renderização. O fator de zoom multiplica esse espaço disponível: acima de
        // 100% o radar fica maior que a área visível (rolável pelo ScrollViewer), abaixo fica
        // menor.
        double availableSize = Math.Min(RootGrid.ActualWidth, RootGrid.ActualHeight);
        if (availableSize <= 1) return;

        double size = availableSize * _zoom;
        double maxDistance = EffectiveMaxDistanceMeters;

        // Guardado para converter posições de mouse (ScreenToWorldMeters) fora do fluxo normal
        // de renderização — eventos de clique/arraste chegam a qualquer momento entre um tick
        // do _renderTimer e outro.
        _lastRenderedSize = size;
        _lastRenderedMaxDistance = maxDistance;

        if (_staticLayerDirty)
        {
            DrawStaticLayer(size, maxDistance);
            _staticLayerDirty = false;
        }

        DrawTowers(size, maxDistance);
        DrawTargets(size, maxDistance);
    }

    // ---------------------------------------------------------------- Camada estática

    private void DrawStaticLayer(double size, double maxDistance)
    {
        StaticLayer.Children.Clear();
        RadarCanvas.Width = size;
        RadarCanvas.Height = size;

        var background = TryFindBrush("RadarBackgroundBrush", Brushes.Black);
        var gridBrush = TryFindBrush("RadarGridBrush", Brushes.DarkGreen);
        var textBrush = TryFindBrush("TextSecondaryBrush", Brushes.Gray);

        var baseCircle = new Ellipse
        {
            Width = size,
            Height = size,
            Fill = background,
            Stroke = gridBrush,
            StrokeThickness = 1.5
        };
        StaticLayer.Children.Add(baseCircle);

        int ringCount = Math.Max(1, AppConfig.Current.RadarSettings.DistanceRingCount);
        double radius = size / 2.0;

        // Sombreamento das zonas mortas ativas, desenhado antes dos anéis/rótulos de
        // distância para que eles continuem legíveis por cima do preenchimento translúcido.
        DrawDeadZones(size, radius, maxDistance);

        for (int i = 1; i <= ringCount; i++)
        {
            double ringRadius = radius * i / ringCount;
            var ring = new Ellipse
            {
                Width = ringRadius * 2,
                Height = ringRadius * 2,
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 3 }
            };
            Canvas.SetLeft(ring, radius - ringRadius);
            Canvas.SetTop(ring, radius - ringRadius);
            StaticLayer.Children.Add(ring);

            double ringDistanceMeters = maxDistance * i / ringCount;
            var label = new TextBlock
            {
                Text = $"{ringDistanceMeters:0.0} m",
                Foreground = textBrush,
                FontSize = 10
            };
            Canvas.SetLeft(label, radius + 2);
            Canvas.SetTop(label, radius - ringRadius);
            StaticLayer.Children.Add(label);
        }

        // Linhas dos quadrantes (eixos X/Y passando pelo centro).
        StaticLayer.Children.Add(new Line { X1 = 0, Y1 = radius, X2 = size, Y2 = radius, Stroke = gridBrush, StrokeThickness = 1 });
        StaticLayer.Children.Add(new Line { X1 = radius, Y1 = 0, X2 = radius, Y2 = size, Stroke = gridBrush, StrokeThickness = 1 });

        // Base (origem) ao centro.
        var baseMarker = new Ellipse { Width = 8, Height = 8, Fill = TryFindBrush("AccentBrush", Brushes.LimeGreen) };
        Canvas.SetLeft(baseMarker, radius - 4);
        Canvas.SetTop(baseMarker, radius - 4);
        StaticLayer.Children.Add(baseMarker);

        // Indicação de Norte / 0°.
        var northLabel = new TextBlock { Text = "N / 0°", Foreground = textBrush, FontSize = 12, FontWeight = FontWeights.Bold };
        Canvas.SetLeft(northLabel, radius - 18);
        Canvas.SetTop(northLabel, 4);
        StaticLayer.Children.Add(northLabel);

        // Rótulos de quadrante.
        AddQuadrantLabel("Q1", radius + radius * 0.45, radius - radius * 0.45, textBrush);
        AddQuadrantLabel("Q2", radius - radius * 0.55, radius - radius * 0.45, textBrush);
        AddQuadrantLabel("Q3", radius - radius * 0.55, radius + radius * 0.35, textBrush);
        AddQuadrantLabel("Q4", radius + radius * 0.45, radius + radius * 0.35, textBrush);
    }

    private void AddQuadrantLabel(string text, double x, double y, Brush brush)
    {
        var label = new TextBlock { Text = text, Foreground = brush, FontSize = 13, FontWeight = FontWeights.SemiBold, Opacity = 0.6 };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, y);
        StaticLayer.Children.Add(label);
    }

    /// <summary>
    /// Sombreia, em vermelho translúcido, cada zona morta ativa: um quarto de círculo inteiro
    /// para <see cref="DeadZoneType.Quadrant"/>, ou um anel entre <see cref="DeadZone.MinDistance"/>
    /// e <see cref="DeadZone.MaxDistance"/> para <see cref="DeadZoneType.DistanceRange"/>
    /// (mesma conversão metros→pixel de <see cref="CoordinateConverter.WorldToScreen"/>, só que
    /// aplicada a um raio em vez de a um ponto).
    /// </summary>
    private void DrawDeadZones(double size, double radius, double maxDistance)
    {
        var zones = (DeadZones?.Cast<DeadZone>() ?? Enumerable.Empty<DeadZone>()).Where(z => z.Enabled);
        Brush brush = TryFindBrush("DangerBrush", Brushes.Red);
        var center = new Point(radius, radius);

        // Pontos onde cada semieixo cruza a borda do radar, na ordem Leste->Norte->Oeste->Sul —
        // percorrida sempre no mesmo sentido (anti-horário em tela), cada par consecutivo
        // delimita exatamente um quadrante (Q1=Leste->Norte, Q2=Norte->Oeste, Q3=Oeste->Sul,
        // Q4=Sul->Leste), na mesma convenção de QuadrantHelper.
        Point east = new(size, radius);
        Point north = new(radius, 0);
        Point west = new(0, radius);
        Point south = new(radius, size);

        foreach (DeadZone zone in zones)
        {
            Geometry? geometry = zone.Type == DeadZoneType.Quadrant
                ? QuadrantWedge(zone.Quadrant, center, radius, east, north, west, south)
                : DistanceRing(zone, center, radius, maxDistance);

            if (geometry is null) continue;

            StaticLayer.Children.Add(new Path { Data = geometry, Fill = brush, Opacity = 0.18 });
        }
    }

    private static Geometry? QuadrantWedge(Quadrant quadrant, Point center, double radius, Point east, Point north, Point west, Point south)
    {
        (Point from, Point to) = quadrant switch
        {
            Quadrant.Q1 => (east, north),
            Quadrant.Q2 => (north, west),
            Quadrant.Q3 => (west, south),
            Quadrant.Q4 => (south, east),
            _ => (east, east) // Quadrant.None nunca é usado por uma zona morta — nada a desenhar.
        };

        if (from == to) return null;

        var figure = new PathFigure { StartPoint = center, IsClosed = true };
        figure.Segments.Add(new LineSegment(from, true));
        figure.Segments.Add(new ArcSegment(to, new Size(radius, radius), 0, false, SweepDirection.Counterclockwise, true));
        return new PathGeometry(new[] { figure });
    }

    private static Geometry? DistanceRing(DeadZone zone, Point center, double radius, double maxDistance)
    {
        double outerPx = MetersToPixels(zone.MaxDistance, radius, maxDistance);
        if (outerPx <= 0) return null;

        double innerPx = MetersToPixels(zone.MinDistance, radius, maxDistance);
        var outer = new EllipseGeometry(center, outerPx, outerPx);
        if (innerPx <= 0) return outer;

        var inner = new EllipseGeometry(center, innerPx, innerPx);
        return new CombinedGeometry(GeometryCombineMode.Exclude, outer, inner);
    }

    private static double MetersToPixels(double meters, double radius, double maxDistance) =>
        Math.Clamp(meters, 0, maxDistance) / maxDistance * radius;

    // ---------------------------------------------------------------- Torres (camada dinâmica)

    private void DrawTowers(double size, double maxDistance)
    {
        var towers = (Towers?.Cast<Tower>() ?? Enumerable.Empty<Tower>()).ToList();
        var currentIds = towers.Select(t => t.Id).ToHashSet();

        foreach (int staleId in _towerVisuals.Keys.Where(id => !currentIds.Contains(id)).ToList())
        {
            RemoveTowerVisual(staleId);
        }

        foreach (Tower tower in towers)
        {
            Point p = CoordinateConverter.WorldToScreen(tower.X, tower.Y, size, maxDistance);

            if (!_towerVisuals.TryGetValue(tower.Id, out TowerVisual? visual))
            {
                var shape = new Polygon
                {
                    Points = new PointCollection { new(0, -8), new(7, 7), new(-7, 7) }, // triângulo
                    StrokeThickness = 1,
                    Stroke = Brushes.Black
                };
                var label = new TextBlock { FontSize = 10, FontWeight = FontWeights.SemiBold };

                DynamicLayer.Children.Add(shape);
                DynamicLayer.Children.Add(label);

                visual = new TowerVisual { Shape = shape, Label = label };
                _towerVisuals[tower.Id] = visual;
            }

            Canvas.SetLeft(visual.Shape, p.X - 8);
            Canvas.SetTop(visual.Shape, p.Y - 8);
            visual.Shape.Fill = TowerBrushFor(tower.State);

            visual.Label.Text = tower.Name;
            Canvas.SetLeft(visual.Label, p.X + 9);
            Canvas.SetTop(visual.Label, p.Y - 7);
        }
    }

    private void RemoveTowerVisual(int id)
    {
        if (!_towerVisuals.Remove(id, out TowerVisual? visual)) return;
        DynamicLayer.Children.Remove(visual.Shape);
        DynamicLayer.Children.Remove(visual.Label);
    }

    private Brush TowerBrushFor(TowerState state) => state switch
    {
        TowerState.Selected => TryFindBrush("TowerSelectedBrush", Brushes.LimeGreen),
        TowerState.Firing => TryFindBrush("DangerBrush", Brushes.Red),
        TowerState.Unavailable or TowerState.Offline => TryFindBrush("TextSecondaryBrush", Brushes.Gray),
        _ => TryFindBrush("TowerBrush", Brushes.Purple)
    };

    // ---------------------------------------------------------------- Alvos (camada dinâmica)

    private void DrawTargets(double size, double maxDistance)
    {
        var targets = (Targets?.Cast<Target>() ?? Enumerable.Empty<Target>()).Where(t => t.IsActive).ToList();
        var currentIds = targets.Select(t => t.Id).ToHashSet();

        foreach (int staleId in _targetVisuals.Keys.Where(id => !currentIds.Contains(id)).ToList())
        {
            RemoveTargetVisual(staleId);
        }

        foreach (Target target in targets)
        {
            Point p = CoordinateConverter.WorldToScreen(target.X, target.Y, size, maxDistance);
            bool isSelected = target.Id == SelectedTargetId;

            if (!_targetVisuals.TryGetValue(target.Id, out TargetVisual? visual))
            {
                var circle = new Ellipse { Width = 14, Height = 14, StrokeThickness = 2, Stroke = Brushes.White, Cursor = System.Windows.Input.Cursors.Hand };
                // e.Handled = true impede que o clique "vaze" para RadarCanvas_MouseLeftButtonDown
                // por baixo — clicar num alvo sempre seleciona o alvo, nunca também conta como
                // clique de zona morta, mesmo com DeadZoneEditMode ativo.
                circle.MouseLeftButtonDown += (_, e) => { TargetClicked?.Invoke(this, target.Id); e.Handled = true; };

                var label = new TextBlock { FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White };

                DynamicLayer.Children.Add(circle);
                DynamicLayer.Children.Add(label);

                visual = new TargetVisual { Circle = circle, Label = label };
                _targetVisuals[target.Id] = visual;
            }

            Canvas.SetLeft(visual.Circle, p.X - 7);
            Canvas.SetTop(visual.Circle, p.Y - 7);
            visual.Circle.Fill = isSelected ? TryFindBrush("TargetSelectedBrush", Brushes.Gold) : TryFindBrush("TargetBrush", Brushes.DodgerBlue);
            visual.Circle.Width = visual.Circle.Height = isSelected ? 18 : 14;
            Canvas.SetLeft(visual.Circle, p.X - visual.Circle.Width / 2);
            Canvas.SetTop(visual.Circle, p.Y - visual.Circle.Height / 2);

            visual.Label.Text = $"#{target.Id:D2}";
            Canvas.SetLeft(visual.Label, p.X + 10);
            Canvas.SetTop(visual.Label, p.Y - 8);

            UpdateTowerLink(visual, target, p, size, maxDistance);
        }
    }

    private void UpdateTowerLink(TargetVisual visual, Target target, Point targetScreenPos, double size, double maxDistance)
    {
        if (target.SelectedTower is null)
        {
            if (visual.TowerLink is not null)
            {
                DynamicLayer.Children.Remove(visual.TowerLink);
                visual.TowerLink = null;
            }
            return;
        }

        Point towerPos = CoordinateConverter.WorldToScreen(target.SelectedTower.X, target.SelectedTower.Y, size, maxDistance);

        if (visual.TowerLink is null)
        {
            visual.TowerLink = new Line
            {
                Stroke = TryFindBrush("TowerSelectedBrush", Brushes.LimeGreen),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Opacity = 0.8
            };
            DynamicLayer.Children.Insert(0, visual.TowerLink); // atrás dos marcadores
        }

        visual.TowerLink.X1 = targetScreenPos.X;
        visual.TowerLink.Y1 = targetScreenPos.Y;
        visual.TowerLink.X2 = towerPos.X;
        visual.TowerLink.Y2 = towerPos.Y;
    }

    private void RemoveTargetVisual(int id)
    {
        if (!_targetVisuals.Remove(id, out TargetVisual? visual)) return;
        DynamicLayer.Children.Remove(visual.Circle);
        DynamicLayer.Children.Remove(visual.Label);
        if (visual.TowerLink is not null) DynamicLayer.Children.Remove(visual.TowerLink);
    }

    private static Brush TryFindBrush(string key, Brush fallback) =>
        Application.Current.TryFindResource(key) as Brush ?? fallback;
}
