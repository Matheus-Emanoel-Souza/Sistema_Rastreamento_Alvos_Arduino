using System;
using System.Globalization;

namespace RadarTorres.App.Data;

/// <summary>
/// Formata valores C# como literais SQL prontos para texto puro (usado por
/// <see cref="PendingWriteQueue"/> para montar INSERT/UPDATE completos, sem parâmetros — o
/// arquivo é reaplicado depois, fora do processo/comando que originou o valor).
/// </summary>
public static class SqlLiteral
{
    public static string Text(string? value) => value is null ? "NULL" : $"'{value.Replace("'", "''")}'";

    public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Number(int? value) => value.HasValue ? Number(value.Value) : "NULL";

    public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    public static string Number(double? value) => value.HasValue ? Number(value.Value) : "NULL";

    public static string Bool(bool value) => value ? "1" : "0";

    public static string DateTime(DateTime value) => Text(value.ToString("O", CultureInfo.InvariantCulture));

    public static string DateTime(DateTime? value) => value.HasValue ? DateTime(value.Value) : "NULL";
}
