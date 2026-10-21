using System.IO;
using Microsoft.Data.Sqlite;

namespace RadarTorres.App.Data;

/// <summary>
/// Connection string única (<c>radartorres.db</c>) compartilhada por todos os repositórios
/// <c>Sqlite*Repository</c> — evita repetir a mesma resolução de caminho em cada um deles.
/// </summary>
public static class SqliteConnectionFactory
{
    private static string _connectionString;

    public static string ConnectionString => _connectionString;

    static SqliteConnectionFactory()
    {
        Directory.CreateDirectory(AppDataPaths.DataFolder);
        string dbPath = Path.Combine(AppDataPaths.DataFolder, "radartorres.db");
        _connectionString = $"Data Source={dbPath}";
    }

    /// <summary>
    /// Redireciona TODAS as conexões para um banco de teste. Existe exclusivamente para os
    /// testes automatizados (<c>RadarTorres.Tests</c>, ver <c>InternalsVisibleTo</c> no
    /// .csproj) — sem isto, exercitar um repositório num teste escreveria no banco real do
    /// usuário em <c>%AppData%</c>, misturando dados de teste com dados de verdade.
    /// </summary>
    internal static void UsarBancoDeTeste(string dbPath)
    {
        string? pasta = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

        _connectionString = $"Data Source={dbPath}";
    }

    /// <summary>
    /// Abre uma conexão já preparada para escrita concorrente.
    /// </summary>
    /// <remarks>
    /// As duas PRAGMAs existem por um problema concreto: a detecção por câmera passou a gravar
    /// de várias threads ao mesmo tempo (uma por câmera, ver
    /// <see cref="Services.OcorrenciaDeteccaoService"/>), e no modo padrão do SQLite a segunda
    /// escrita simultânea falha de imediato com "database is locked" — o que faria a ocorrência
    /// cair na fila offline (<see cref="PendingWriteQueue"/>) em vez de ir para a tabela, com a
    /// antiduplicidade por índice único deixando de valer. <c>journal_mode = WAL</c> permite um
    /// escritor junto de vários leitores, e <c>busy_timeout</c> faz quem chegou depois ESPERAR a
    /// vez em vez de desistir. Foi um teste de concorrência que expôs isso.
    /// </remarks>
    public static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using SqliteCommand pragmas = connection.CreateCommand();
        // journal_mode é propriedade do arquivo (aplicada uma vez e persistida); busy_timeout é
        // por conexão, por isso as duas são executadas a cada abertura.
        pragmas.CommandText = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 5000;";
        pragmas.ExecuteNonQuery();

        return connection;
    }
}
