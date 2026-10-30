using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using RadarTorres.App.Models;

namespace RadarTorres.App.Converters;

/// <summary>
/// Colore cada linha do histórico de execução da tela de treinamento conforme a natureza do
/// evento — mesmo papel que <see cref="LogLevelToBrushConverter"/> cumpre no console de eventos,
/// e pelo mesmo motivo: a cor é decisão de tema, não dado do modelo, então não pertence a
/// <see cref="EventoTreinamento"/>.
/// </summary>
public sealed class TipoEventoTreinamentoToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string resourceKey = value switch
        {
            TipoEventoTreinamento.Avanco => "Accent.Fill",
            TipoEventoTreinamento.Atencao => "Target.Fill",
            TipoEventoTreinamento.Falha => "Danger.Fill",
            _ => "Text.Secondary"
        };

        return Application.Current.TryFindResource(resourceKey) as SolidColorBrush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
