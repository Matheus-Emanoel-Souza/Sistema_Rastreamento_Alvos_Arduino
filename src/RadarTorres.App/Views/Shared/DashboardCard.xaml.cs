using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace RadarTorres.App.Views.Shared;

/// <summary>
/// Card arrastável/redimensionável/ocultável de um DashboardCanvas — ver comentário em
/// DashboardCard.xaml. Sem lógica de posicionamento própria: cada Thumb e o botão de fechar
/// apenas repassam o gesto ao <see cref="DashboardCanvas"/> pai, que decide se o movimento é
/// válido (bordas do canvas, colisão com outros cards) antes de aplicá-lo.
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

    /// <summary>Largura/altura usadas por DashboardCanvas ao posicionar este card no arranjo
    /// padrão (ex.: o Radar precisa de mais espaço que um card de status). Se não definido,
    /// DashboardCanvas usa um tamanho padrão genérico.</summary>
    public static readonly DependencyProperty DefaultWidthProperty = DependencyProperty.Register(
        nameof(DefaultWidth), typeof(double), typeof(DashboardCard), new PropertyMetadata(260d));

    public static readonly DependencyProperty DefaultHeightProperty = DependencyProperty.Register(
        nameof(DefaultHeight), typeof(double), typeof(DashboardCard), new PropertyMetadata(150d));

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

    public double DefaultWidth
    {
        get => (double)GetValue(DefaultWidthProperty);
        set => SetValue(DefaultWidthProperty, value);
    }

    public double DefaultHeight
    {
        get => (double)GetValue(DefaultHeightProperty);
        set => SetValue(DefaultHeightProperty, value);
    }

    private void HeaderThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        (Parent as DashboardCanvas)?.BringToFront(this);
    }

    private void HeaderThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        (Parent as DashboardCanvas)?.RequestMove(this, e.HorizontalChange, e.VerticalChange);
    }

    private void HeaderThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        (Parent as DashboardCanvas)?.NotifyLayoutChanged();
    }

    private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        (Parent as DashboardCanvas)?.BringToFront(this);
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        (Parent as DashboardCanvas)?.RequestResize(this, e.HorizontalChange, e.VerticalChange);
    }

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        (Parent as DashboardCanvas)?.NotifyLayoutChanged();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        (Parent as DashboardCanvas)?.HideCard(this);
    }
}
