using System.Windows.Controls;
using RadarTorres.App.Services;

namespace RadarTorres.App.Views.Shell;

/// <summary>Ver comentário em PlaceholderView.xaml.</summary>
public partial class PlaceholderView : UserControl
{
    public PlaceholderView(string titleKey)
    {
        InitializeComponent();
        TituloTextBlock.Text = LocalizationService.Current?[titleKey] ?? titleKey;
    }
}
