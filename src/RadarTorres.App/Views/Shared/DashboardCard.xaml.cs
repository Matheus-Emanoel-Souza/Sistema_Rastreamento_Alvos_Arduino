using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace RadarTorres.App.Views.Shared;

/// <summary>
/// Card arrastável/redimensionável do painel principal — ver comentário em DashboardCard.xaml.
/// Sem lógica de posicionamento própria: cada Thumb apenas repassa o gesto ao
/// <see cref="DashboardCanvas"/> pai, que decide se o movimento é válido (bordas do canvas,
/// colisão com outros cards) antes de aplicá-lo.
/// </summary>
[ContentProperty(nameof(CardContent))]
public partial class DashboardCard : UserControl
{
    public static readonly DependencyProperty CardIdProperty = DependencyProperty.Register(
        nameof(CardId), typeof(string), typeof(DashboardCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(DashboardCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty CardContentProperty = DependencyProperty.Register(
        nameof(CardContent), typeof(object), typeof(DashboardCard), new PropertyMetadata(null));

    public DashboardCard()
    {
        InitializeComponent();
    }

    /// <summary>Identificador estável do card, usado como chave ao salvar/restaurar o layout.</summary>
    public string CardId
    {
        get => (string)GetValue(CardIdProperty);
        set => SetValue(CardIdProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public object? CardContent
    {
        get => GetValue(CardContentProperty);
        set => SetValue(CardContentProperty, value);
    }

    private void HeaderThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        (Parent as DashboardCanvas)?.RequestMove(this, e.HorizontalChange, e.VerticalChange);
    }

    private void HeaderThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        (Parent as DashboardCanvas)?.NotifyLayoutChanged();
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        (Parent as DashboardCanvas)?.RequestResize(this, e.HorizontalChange, e.VerticalChange);
    }

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        (Parent as DashboardCanvas)?.NotifyLayoutChanged();
    }
}
