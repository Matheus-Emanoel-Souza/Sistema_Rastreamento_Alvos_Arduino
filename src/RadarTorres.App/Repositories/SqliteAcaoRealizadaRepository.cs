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
            "SELECT Id, TorreAcao, ObjetoDetectadoId, DataHora, UsuarioResponsavel, Origem, Resultado, Observacao " +
            "FROM AcoesRealizadas ORDER BY Id";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new AcaoRealizada
            {
                Id = reader.GetInt32(0),
                TorreAcao = reader.GetString(1),
                ObjetoDetectadoId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                DataHora = DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                UsuarioResponsavel = reader.IsDBNull(4) ? null : reader.GetString(4),
                Origem = Enum.Parse<OrigemAcao>(reader.GetString(5)),
                Resultado = Enum.Parse<ResultadoAcao>(reader.GetString(6)),
                Observacao = reader.IsDBNull(7) ? null : reader.GetString(7),
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
            "INSERT INTO AcoesRealizadas (TorreAcao, ObjetoDetectadoId, DataHora, UsuarioResponsavel, Origem, Resultado, Observacao) " +
            "VALUES ($torreAcao, $objetoDetectadoId, $dataHora, $usuarioResponsavel, $origem, $resultado, $observacao); " +
            "SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$torreAcao", acao.TorreAcao);
        command.Parameters.AddWithValue("$objetoDetectadoId", (object?)acao.ObjetoDetectadoId ?? DBNull.Value);
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
            "TorreAcao TEXT NOT NULL, " +
            "ObjetoDetectadoId INTEGER NULL, " +
            "DataHora TEXT NOT NULL, " +
            "UsuarioResponsavel TEXT NULL, " +
            "Origem TEXT NOT NULL, " +
            "Resultado TEXT NOT NULL, " +
            "Observacao TEXT NULL)";
        command.ExecuteNonQuery();
    }
}
