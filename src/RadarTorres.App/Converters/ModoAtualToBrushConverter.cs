using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace RadarTorres.App.Converters;

/// <summary>Resolve o texto de <see cref="RadarTorres.App.ViewModels.PainelPrincipalViewModel.ModoAtual"/>
/// (sempre começa com "VERDE"/"AMARELO"/"VERMELHO" — ver <c>MainViewModel.DescribeMode</c>) na cor
/// semântica do modo, para colorir a etiqueta do card "Modo atual". "—" (nenhum modo ainda
/// registrado) resolve para <c>Border.Control</c>, usado na etiqueta neutra "Sem modo definido".</summary>
public sealed class ModoAtualToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string texto = value as string ?? string.Empty;
        string key = texto switch
        {
            _ when texto.StartsWith("VERDE", StringComparison.OrdinalIgnoreCase) => "Accent.Fill",
            _ when texto.StartsWith("AMARELO", StringComparison.OrdinalIgnoreCase) => "Target.Fill",
            _ when texto.StartsWith("VERMELHO", StringComparison.OrdinalIgnoreCase) => "Danger.Fill",
            _ => "Border.Control"
        };
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
