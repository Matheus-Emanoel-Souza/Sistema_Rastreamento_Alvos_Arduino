using System;
using System.Globalization;

namespace RadarTorres.App.Data;

/// <summary>Conversões de tipo &lt;-&gt; texto usadas pelos repositórios CSV, centralizadas para evitar duplicação.</summary>
public static class CsvConvert
{
    public static string From(DateTime value) => value.ToString("O", CultureInfo.InvariantCulture);

    public static DateTime ToDateTime(string value) =>
        DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static string From(DateTime? value) => value.HasValue ? From(value.Value) : string.Empty;

    public static DateTime? ToNullableDateTime(string value) =>
        string.IsNullOrEmpty(value) ? null : ToDateTime(value);

    public static string From(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    public static double ToDouble(string value) =>
        string.IsNullOrEmpty(value) ? 0 : double.Parse(value, CultureInfo.InvariantCulture);

    public static string From(double? value) => value.HasValue ? From(value.Value) : string.Empty;

    public static double? ToNullableDouble(string value) =>
        string.IsNullOrEmpty(value) ? null : double.Parse(value, CultureInfo.InvariantCulture);

    public static string From(bool value) => value ? "true" : "false";

    public static bool ToBool(string value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    public static string From<TEnum>(TEnum value) where TEnum : struct, Enum => value.ToString();

    public static TEnum ToEnum<TEnum>(string value, TEnum fallback = default) where TEnum : struct, Enum =>
        Enum.TryParse(value, true, out TEnum result) ? result : fallback;

    public static int ToInt(string value) => string.IsNullOrEmpty(value) ? 0 : int.Parse(value, CultureInfo.InvariantCulture);
}
