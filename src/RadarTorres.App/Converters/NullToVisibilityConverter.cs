using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RadarTorres.App.Converters;

/// <summary>
/// Retorna <see cref="Visibility.Visible"/> quando o valor não é nulo (usado para mostrar o
/// painel de detalhes apenas quando há um alvo selecionado). Passe <c>Invert</c> como
/// parâmetro para inverter a lógica.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        bool isNull = value is null;
        bool visible = invert ? isNull : !isNull;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
