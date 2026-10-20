using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace RadarTorres.App.Converters;

/// <summary>Resolve uma chave de recurso (ex.: "Icon.Monitoramento", vinda de
/// <c>SidebarMenuEntry.IconKey</c>) na <see cref="Geometry"/> cadastrada em
/// Resources/Icons.xaml — permite que o ícone de cada item da Sidebar seja escolhido por
/// dados (binding) em vez de um DataTemplate fixo por tipo de item.</summary>
public sealed class ResourceKeyToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string key ? Application.Current.TryFindResource(key) as Geometry : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
