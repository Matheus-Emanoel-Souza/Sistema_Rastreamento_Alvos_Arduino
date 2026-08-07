using System;
using System.Globalization;
using System.Windows.Data;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;

namespace RadarTorres.App.Converters;

/// <summary>Converte <see cref="Quadrant"/> no rótulo textual exibido nos painéis (Q1, Q2, Q3, Q4, "—").</summary>
public sealed class QuadrantToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Quadrant quadrant ? QuadrantHelper.ToDisplayLabel(quadrant) : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
