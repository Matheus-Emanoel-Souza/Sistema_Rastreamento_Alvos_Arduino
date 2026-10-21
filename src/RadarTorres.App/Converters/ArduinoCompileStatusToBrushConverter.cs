using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using RadarTorres.App.Models;

namespace RadarTorres.App.Converters;

/// <summary>Colore o texto de status final da compilação (sucesso/falha/cancelada).</summary>
public sealed class ArduinoCompileStatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string resourceKey = value switch
        {
            ArduinoCompileStatus.Success => "AccentBrush",
            ArduinoCompileStatus.Cancelled => "WarningBrush",
            ArduinoCompileStatus.Failed => "DangerBrush",
            _ => "TextSecondaryBrush"
        };

        return Application.Current.TryFindResource(resourceKey) as SolidColorBrush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
