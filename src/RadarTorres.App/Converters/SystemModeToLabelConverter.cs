using System;
using System.Globalization;
using System.Windows.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Converters;

/// <summary>Converte <see cref="SystemMode"/> no rótulo exibido no painel de status.</summary>
public sealed class SystemModeToLabelConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        SystemMode.LigadoApenas => "VERDE — LIGADO APENAS",
        SystemMode.AcompanharAlvos => "AMARELO — ACOMPANHAR ALVOS",
        SystemMode.Disparar => "VERMELHO — DISPARAR",
        _ => "—"
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
