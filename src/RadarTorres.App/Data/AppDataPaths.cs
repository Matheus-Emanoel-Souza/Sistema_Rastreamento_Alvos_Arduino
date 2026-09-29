using System;
using System.IO;

namespace RadarTorres.App.Data;

/// <summary>
/// Resolve onde os dados persistidos do RadarTorres ficam gravados no disco do usuário.
/// Usa <c>%AppData%\RadarTorres</c> (por usuário do Windows, sem exigir permissão de
/// administrador) em vez da pasta de instalação (<c>C:\Program Files\...</c>, somente
/// leitura para usuários comuns) — assim o app funciona tanto instalado quanto em
/// desenvolvimento, e os dados sobrevivem a uma desinstalação/reinstalação do instalador.
/// </summary>
public static class AppDataPaths
{
    public static string RootFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "RadarTorres");

    /// <summary>
    /// Pasta com <c>radartorres.db</c> (SQLite, único armazenamento — ver
    /// <see cref="SqliteConnectionFactory"/> e <c>Docs/Tecnica/MODELO_DADOS.md</c>).
    /// </summary>
    public static string DataFolder { get; } = Path.Combine(RootFolder, "Data");

    public static string GetCsvPath(string tableName) => Path.Combine(DataFolder, $"{tableName}.csv");

    public static void EnsureDataFolderExists() => Directory.CreateDirectory(DataFolder);
}
