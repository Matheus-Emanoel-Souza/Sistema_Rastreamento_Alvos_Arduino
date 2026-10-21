using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using RadarTorres.App.Models;

namespace RadarTorres.App.Converters;

/// <summary>Colore cada linha do console de compilação conforme sua origem (info/stdout/stderr).</summary>
public sealed class ArduinoOutputStreamToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string resourceKey = value switch
        {
            ArduinoCliOutputStream.StdErr => "WarningBrush",
            ArduinoCliOutputStream.Info => "TextSecondaryBrush",
            _ => "TextPrimaryBrush"
        };

        return Application.Current.TryFindResource(resourceKey) as SolidColorBrush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
