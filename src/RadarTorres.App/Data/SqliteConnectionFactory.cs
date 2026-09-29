using System.IO;
using Microsoft.Data.Sqlite;

namespace RadarTorres.App.Data;

/// <summary>
/// Connection string única (<c>radartorres.db</c>) compartilhada por todos os repositórios
/// <c>Sqlite*Repository</c> — evita repetir a mesma resolução de caminho em cada um deles.
/// </summary>
public static class SqliteConnectionFactory
{
    public static string ConnectionString { get; }

    static SqliteConnectionFactory()
    {
        Directory.CreateDirectory(AppDataPaths.DataFolder);
        string dbPath = Path.Combine(AppDataPaths.DataFolder, "radartorres.db");
        ConnectionString = $"Data Source={dbPath}";
    }

    public static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        return connection;
    }
}
