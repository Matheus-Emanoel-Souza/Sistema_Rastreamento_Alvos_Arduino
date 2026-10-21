using System;
using System.Globalization;
using System.Windows.Data;

namespace RadarTorres.App.Converters;

/// <summary>
/// Converte um valor de enum para <c>bool</c> (e vice-versa), permitindo usar um grupo de
/// <c>RadioButton</c> para representar uma única propriedade de enum na ViewModel — usado
/// pelos quatro modos de operação do sistema (Off / Localização / Localização+Torre / Localização+Auto).
/// </summary>
public sealed class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return value.ToString()!.Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool isChecked || !isChecked || parameter is null) return Binding.DoNothing;
        return Enum.Parse(targetType, parameter.ToString()!);
    }
}
