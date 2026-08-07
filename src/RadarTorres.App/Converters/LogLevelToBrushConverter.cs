using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using RadarTorres.App.Models;

namespace RadarTorres.App.Converters;

/// <summary>Colore cada linha do console de eventos de acordo com sua severidade.</summary>
public sealed class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string resourceKey = value switch
        {
            LogLevel.Success => "AccentBrush",
            LogLevel.Warning => "WarningBrush",
            LogLevel.Error => "DangerBrush",
            _ => "TextSecondaryBrush"
        };

        return Application.Current.TryFindResource(resourceKey) as SolidColorBrush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
