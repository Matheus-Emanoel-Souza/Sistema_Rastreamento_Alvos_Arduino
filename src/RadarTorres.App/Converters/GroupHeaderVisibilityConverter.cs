using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RadarTorres.App.Converters;

/// <summary>Mostra o cabeçalho de grupo da Sidebar (ex.: "OPERAÇÃO") apenas quando o item tem um
/// <c>GroupLabel</c> (é o primeiro do seu grupo — ver <c>ShellViewModel.RebuildMenu</c>) E a
/// barra está expandida (a barra compacta só mostra ícones, sem nenhum texto).</summary>
public sealed class GroupHeaderVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        bool temGrupo = values.Length > 0 && values[0] is string { Length: > 0 };
        bool sidebarExpandida = values.Length > 1 && values[1] is false;
        return temGrupo && sidebarExpandida ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
