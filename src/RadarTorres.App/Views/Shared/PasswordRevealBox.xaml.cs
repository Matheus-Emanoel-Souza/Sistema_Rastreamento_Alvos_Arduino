using System.Windows;
using System.Windows.Controls;
using RadarTorres.App.Services;

namespace RadarTorres.App.Views.Shared;

/// <summary>Ver comentário em PasswordRevealBox.xaml.</summary>
public partial class PasswordRevealBox : UserControl
{
    /// <summary>Segoe MDL2 Assets — glifo "View" (olho aberto): mostrar a senha.</summary>
    private const string GlyphShow = "";

    /// <summary>Segoe MDL2 Assets — glifo "Hide" (olho fechado): ocultar a senha.</summary>
    private const string GlyphHide = "";

    public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(
        nameof(Password),
        typeof(string),
        typeof(PasswordRevealBox),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPasswordChanged));

    /// <summary>Bindável normalmente (TwoWay por padrão) — diferente de PasswordBox.Password nativo.</summary>
    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    private bool _isSyncing;

    public PasswordRevealBox()
    {
        InitializeComponent();
    }

    private static void OnPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((PasswordRevealBox)d).SyncBoxesFromProperty();
    }

    private void SyncBoxesFromProperty()
    {
        if (_isSyncing) return;
        _isSyncing = true;
        if (HiddenBox.Password != Password) HiddenBox.Password = Password;
        if (VisibleBox.Text != Password) VisibleBox.Text = Password;
        _isSyncing = false;
    }

    private void HiddenBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSyncing) return;
        _isSyncing = true;
        Password = HiddenBox.Password;
        VisibleBox.Text = HiddenBox.Password;
        _isSyncing = false;
    }

    private void VisibleBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncing) return;
        _isSyncing = true;
        Password = VisibleBox.Text;
        HiddenBox.Password = VisibleBox.Text;
        _isSyncing = false;
    }

    private void RevealToggle_Checked(object sender, RoutedEventArgs e)
    {
        VisibleBox.Visibility = Visibility.Visible;
        HiddenBox.Visibility = Visibility.Collapsed;
        ToggleGlyph.Text = GlyphHide;
        RevealToggle.ToolTip = ResolveTooltip("Common.OcultarSenha", "Ocultar senha");

        VisibleBox.Focus();
        VisibleBox.CaretIndex = VisibleBox.Text.Length;
    }

    private void RevealToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        HiddenBox.Visibility = Visibility.Visible;
        VisibleBox.Visibility = Visibility.Collapsed;
        ToggleGlyph.Text = GlyphShow;
        RevealToggle.ToolTip = ResolveTooltip("Common.MostrarSenha", "Mostrar senha");

        HiddenBox.Focus();
    }

    private static string ResolveTooltip(string key, string fallback) =>
        LocalizationService.Current?[key] ?? fallback;
}
