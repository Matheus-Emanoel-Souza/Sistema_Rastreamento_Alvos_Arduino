using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>classificacoes_alvo</c> — nomes
/// espelham <c>Docs/Documentos_Entregaveis/Banco_de_Dados/schema.sql</c>) de
/// <see cref="IClassificacaoPerigoRepository"/>. Mesmo padrão de <see cref="SqliteAcaoRealizadaRepository"/>:
/// só inserção, com fila de reenvio via <see cref="PendingWriteQueue"/> se o banco estiver
/// momentaneamente inacessível.
/// </summary>
public sealed class SqliteClassificacaoPerigoRepository : IClassificacaoPerigoRepository
{
    public IReadOnlyList<ClassificacaoPerigoAlvo> GetAll()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var result = new List<ClassificacaoPerigoAlvo>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, ObjetoDetectadoId, VelocidadeMetrosPorSegundo, Grau, DataHora " +
            "FROM classificacoes_alvo ORDER BY Id";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new ClassificacaoPerigoAlvo
            {
                Id = reader.GetInt32(0),
                ObjetoDetectadoId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                VelocidadeMetrosPorSegundo = reader.GetDouble(2),
                Grau = Enum.Parse<GrauPerigoAlvo>(reader.GetString(3)),
                DataHora = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            });
        }
        return result;
    }

    public ClassificacaoPerigoAlvo Add(ClassificacaoPerigoAlvo classificacao)
    {
        try
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
            EnsureTable(connection);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO classificacoes_alvo (ObjetoDetectadoId, VelocidadeMetrosPorSegundo, Grau, DataHora) " +
                "VALUES ($idObjeto, $velocidade, $grau, $dataHora); " +
                "SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$idObjeto", (object?)classificacao.ObjetoDetectadoId ?? DBNull.Value);
            command.Parameters.AddWithValue("$velocidade", classificacao.VelocidadeMetrosPorSegundo);
            command.Parameters.AddWithValue("$grau", classificacao.Grau.ToString());
            command.Parameters.AddWithValue("$dataHora", classificacao.DataHora.ToString("O", CultureInfo.InvariantCulture));

            classificacao.Id = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            return classificacao;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            classificacao.Id = PendingWriteQueue.NextOfflineId();
            PendingWriteQueue.Enqueue(
                "INSERT INTO classificacoes_alvo (ObjetoDetectadoId, VelocidadeMetrosPorSegundo, Grau, DataHora) VALUES (" +
                $"{SqlLiteral.Number(classificacao.ObjetoDetectadoId)}, {SqlLiteral.Number(classificacao.VelocidadeMetrosPorSegundo)}, " +
                $"{SqlLiteral.Text(classificacao.Grau.ToString())}, {SqlLiteral.DateTime(classificacao.DataHora)})");
            return classificacao;
        }
    }

    private static void EnsureTable(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS classificacoes_alvo (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "ObjetoDetectadoId INTEGER NULL REFERENCES objetos_detectados (Id), " +
            "VelocidadeMetrosPorSegundo REAL NOT NULL, " +
            "Grau TEXT NOT NULL, " +
            "DataHora TEXT NOT NULL); " +
            "CREATE INDEX IF NOT EXISTS idx_classificacoes_alvo_objeto ON classificacoes_alvo (ObjetoDetectadoId);";
        command.ExecuteNonQuery();
    }
}
