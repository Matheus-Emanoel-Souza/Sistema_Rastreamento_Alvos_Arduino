using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using RadarTorres.App.Models;

namespace RadarTorres.App.Converters;

/// <summary>Mapeia o estado de conexão para a cor do indicador visual "conectado/desconectado".</summary>
public sealed class ConnectionStateToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string resourceKey = value switch
        {
            ConnectionState.Connected => "AccentBrush",
            ConnectionState.Connecting or ConnectionState.Reconnecting => "WarningBrush",
            ConnectionState.Error => "DangerBrush",
            _ => "TextSecondaryBrush"
        };

        return Application.Current.TryFindResource(resourceKey) as SolidColorBrush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
