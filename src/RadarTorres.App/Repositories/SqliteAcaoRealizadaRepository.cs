using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>acoes_realizadas</c> — nomes de
/// tabela/colunas espelham exatamente <c>Docs/Documentos_Entregaveis/Banco_de_Dados/schema.sql</c>)
/// de <see cref="IAcaoRealizadaRepository"/> — único armazenamento; sem fallback para CSV (todo
/// registro do sistema grava direto no banco, com fila de reenvio via
/// <see cref="PendingWriteQueue"/> se o banco estiver momentaneamente inacessível).
/// </summary>
public sealed class SqliteAcaoRealizadaRepository : IAcaoRealizadaRepository
{
    public IReadOnlyList<AcaoRealizada> GetAll()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var result = new List<AcaoRealizada>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT ID_ACAO, ID_TORRE, ID_OBJETO, DH_ACAO, OBSERVACAO " +
            "FROM acoes_realizadas ORDER BY ID_ACAO";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new AcaoRealizada
            {
                Id = reader.GetInt32(0),
                TorreId = reader.GetInt32(1),
                ObjetoDetectadoId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                DataHora = DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Observacao = reader.IsDBNull(4) ? null : reader.GetString(4),
            });
        }
        return result;
    }

    public AcaoRealizada Add(AcaoRealizada acao)
    {
        try
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
            EnsureTable(connection);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO acoes_realizadas (ID_TORRE, ID_OBJETO, DH_ACAO, OBSERVACAO) " +
                "VALUES ($idTorre, $idObjeto, $dhAcao, $observacao); " +
                "SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$idTorre", acao.TorreId);
            command.Parameters.AddWithValue("$idObjeto", (object?)acao.ObjetoDetectadoId ?? DBNull.Value);
            command.Parameters.AddWithValue("$dhAcao", acao.DataHora.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$observacao", (object?)acao.Observacao ?? DBNull.Value);

            acao.Id = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            return acao;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            acao.Id = PendingWriteQueue.NextOfflineId();
            PendingWriteQueue.Enqueue(
                "INSERT INTO acoes_realizadas (ID_TORRE, ID_OBJETO, DH_ACAO, OBSERVACAO) VALUES (" +
                $"{SqlLiteral.Number(acao.TorreId)}, {SqlLiteral.Number(acao.ObjetoDetectadoId)}, {SqlLiteral.DateTime(acao.DataHora)}, " +
                $"{SqlLiteral.Text(acao.Observacao)})");
            return acao;
        }
    }

    private static void EnsureTable(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS acoes_realizadas (" +
            "ID_ACAO INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "ID_TORRE INTEGER NOT NULL, " +
            "ID_OBJETO INTEGER NULL, " +
            "DH_ACAO TEXT NOT NULL, " +
            "OBSERVACAO TEXT NULL)";
        command.ExecuteNonQuery();
    }
}
