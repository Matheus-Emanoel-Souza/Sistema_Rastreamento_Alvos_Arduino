using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RadarTorres.App.Converters;

/// <summary>Centraliza o conteúdo do botão de item da Sidebar quando a barra está recolhida (só
/// ícone, sem rótulo) e alinha à esquerda quando expandida (ícone + rótulo lado a lado).</summary>
public sealed class CollapsedToAlignmentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? HorizontalAlignment.Center : HorizontalAlignment.Left;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
