using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RadarTorres.App.Converters;

/// <summary>
/// Mostra o elemento (<see cref="Visibility.Visible"/>) quando o valor de enum vinculado é
/// igual ao nome passado em <c>ConverterParameter</c>, e o oculta (<see cref="Visibility.Collapsed"/>)
/// caso contrário — usado para alternar entre os campos "quadrante" e "faixa de distância" do
/// formulário de nova zona morta conforme o tipo escolhido. Mesmo princípio de
/// <see cref="EnumToBooleanConverter"/>, só que produzindo <c>Visibility</c> em vez de <c>bool</c>.
/// </summary>
public sealed class EnumEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool matches = value is not null && parameter is not null
            && value.ToString()!.Equals(parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        return matches ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
