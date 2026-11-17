using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RadarTorres.App.Converters;

/// <summary>
/// Retorna <see cref="Visibility.Visible"/> quando o valor é <c>false</c> e
/// <see cref="Visibility.Collapsed"/> quando é <c>true</c> — o inverso de
/// <see cref="BooleanToVisibilityConverter"/> nativo do WPF, que não tem opção de inverter.
/// Usado para mostrar algo só quando uma condição NÃO se aplica (ex.: marcar um evento como "de
/// sessão anterior" só quando ele não é da sessão atual — ver EventoTreinamento.EhSessaoAtual).
/// </summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
