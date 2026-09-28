using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>AcoesRealizadas</c>) de
/// <see cref="IAcaoRealizadaRepository"/> — primeira tabela do projeto a gravar em banco
/// relacional de verdade (as demais continuam em CSV, ver <c>Docs/Tecnica/MODELO_DADOS.md</c>).
/// Cada operação abre e fecha sua própria conexão e deixa qualquer falha (arquivo bloqueado,
/// disco cheio, permissão negada etc.) propagar — quem decide o que fazer com essa falha é
/// <see cref="ResilientAcaoRealizadaRepository"/>, não esta classe.
/// </summary>
public sealed class SqliteAcaoRealizadaRepository : IAcaoRealizadaRepository
{
    private readonly string _connectionString;

    public SqliteAcaoRealizadaRepository()
    {
        Directory.CreateDirectory(AppDataPaths.DataFolder);
        string dbPath = Path.Combine(AppDataPaths.DataFolder, "radartorres.db");
        _connectionString = $"Data Source={dbPath}";
    }

    public IReadOnlyList<AcaoRealizada> GetAll()
    {
        using SqliteConnection connection = OpenConnection();
        EnsureTable(connection);

        var result = new List<AcaoRealizada>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, Dispositivo, TipoAcao, X, Y, Z, DataHora, UsuarioResponsavel, Origem, Resultado, Observacao " +
            "FROM AcoesRealizadas ORDER BY Id";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new AcaoRealizada
            {
                Id = reader.GetInt32(0),
                Dispositivo = reader.GetString(1),
                TipoAcao = reader.GetString(2),
                X = reader.GetDouble(3),
                Y = reader.GetDouble(4),
                Z = reader.IsDBNull(5) ? null : reader.GetDouble(5),
                DataHora = DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                UsuarioResponsavel = reader.IsDBNull(7) ? null : reader.GetString(7),
                Origem = Enum.Parse<OrigemAcao>(reader.GetString(8)),
                Resultado = Enum.Parse<ResultadoAcao>(reader.GetString(9)),
                Observacao = reader.IsDBNull(10) ? null : reader.GetString(10),
            });
        }
        return result;
    }

    public AcaoRealizada Add(AcaoRealizada acao)
    {
        using SqliteConnection connection = OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO AcoesRealizadas (Dispositivo, TipoAcao, X, Y, Z, DataHora, UsuarioResponsavel, Origem, Resultado, Observacao) " +
            "VALUES ($dispositivo, $tipoAcao, $x, $y, $z, $dataHora, $usuarioResponsavel, $origem, $resultado, $observacao); " +
            "SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$dispositivo", acao.Dispositivo);
        command.Parameters.AddWithValue("$tipoAcao", acao.TipoAcao);
        command.Parameters.AddWithValue("$x", acao.X);
        command.Parameters.AddWithValue("$y", acao.Y);
        command.Parameters.AddWithValue("$z", (object?)acao.Z ?? DBNull.Value);
        command.Parameters.AddWithValue("$dataHora", acao.DataHora.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$usuarioResponsavel", (object?)acao.UsuarioResponsavel ?? DBNull.Value);
        command.Parameters.AddWithValue("$origem", acao.Origem.ToString());
        command.Parameters.AddWithValue("$resultado", acao.Resultado.ToString());
        command.Parameters.AddWithValue("$observacao", (object?)acao.Observacao ?? DBNull.Value);

        acao.Id = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        return acao;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void EnsureTable(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS AcoesRealizadas (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "Dispositivo TEXT NOT NULL, " +
            "TipoAcao TEXT NOT NULL, " +
            "X REAL NOT NULL, " +
            "Y REAL NOT NULL, " +
            "Z REAL NULL, " +
            "DataHora TEXT NOT NULL, " +
            "UsuarioResponsavel TEXT NULL, " +
            "Origem TEXT NOT NULL, " +
            "Resultado TEXT NOT NULL, " +
            "Observacao TEXT NULL)";
        command.ExecuteNonQuery();
    }
}
