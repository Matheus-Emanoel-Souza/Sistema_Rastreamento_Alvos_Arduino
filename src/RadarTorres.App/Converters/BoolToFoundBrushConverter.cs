using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace RadarTorres.App.Converters;

/// <summary>Colore o indicador "Arduino CLI encontrado/não encontrado" da seção Ambiente Arduino.</summary>
public sealed class BoolToFoundBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string resourceKey = value is true ? "AccentBrush" : "DangerBrush";
        return Application.Current.TryFindResource(resourceKey) as SolidColorBrush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
