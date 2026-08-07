using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

    public int? SelectedTargetId
    {
        get => (int?)GetValue(SelectedTargetIdProperty);
        set => SetValue(SelectedTargetIdProperty, value);
    }

    /// <summary>Disparado quando o usuário clica em um alvo desenhado no radar.</summary>
    public event EventHandler<int>? TargetClicked;

    private readonly DispatcherTimer _renderTimer;
    private readonly Dictionary<int, TargetVisual> _targetVisuals = new();
    private readonly Dictionary<int, TowerVisual> _towerVisuals = new();
    private bool _staticLayerDirty = true;

    public RadarControl()
    {
        InitializeComponent();

        int refreshMs = Math.Max(30, AppConfig.Current.RadarSettings.RefreshRateMs);
        _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(refreshMs)
        };
        _renderTimer.Tick += (_, _) => Render();

        Loaded += (_, _) => { _renderTimer.Start(); Render(); };
        Unloaded += (_, _) => _renderTimer.Stop();
        SizeChanged += (_, _) => { _staticLayerDirty = true; Render(); };
    }

    private void Render()
    {
        double size = Math.Min(RadarCanvas.ActualWidth, RadarCanvas.ActualHeight);
        if (size <= 1) return;

        if (_staticLayerDirty)
        {
            DrawStaticLayer(size);
            _staticLayerDirty = false;
        }

        double maxDistance = AppConfig.Current.RadarSettings.MaxDetectionDistanceMeters;
        DrawTowers(size, maxDistance);
        DrawTargets(size, maxDistance);
    }

    // ---------------------------------------------------------------- Camada estática

    private void DrawStaticLayer(double size)
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
        double maxDistance = AppConfig.Current.RadarSettings.MaxDetectionDistanceMeters;
        double radius = size / 2.0;

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
                circle.MouseLeftButtonDown += (_, _) => TargetClicked?.Invoke(this, target.Id);

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
