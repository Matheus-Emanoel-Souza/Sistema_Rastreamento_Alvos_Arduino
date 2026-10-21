using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>modo_atual_torre</c> — nomes de
/// tabela/colunas espelham exatamente <c>Docs/Documentos_Entregaveis/Banco_de_Dados/schema.sql</c>)
/// de <see cref="IModoAtualTorreRepository"/> — único armazenamento; sem fallback para CSV, com
/// fila de reenvio via <see cref="PendingWriteQueue"/> se o banco estiver momentaneamente
/// inacessível.
/// </summary>
public sealed class SqliteModoAtualTorreRepository : IModoAtualTorreRepository
{
    public IReadOnlyList<ModoAtualTorre> GetAll()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var result = new List<ModoAtualTorre>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT ID_MODO, MODOATUAL, MODOANTERIOR, ID_USER, DHALTERACAO " +
            "FROM modo_atual_torre ORDER BY ID_MODO";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new ModoAtualTorre
            {
                Id = reader.GetInt32(0),
                NovoModo = reader.GetString(1),
                ModoAnterior = reader.GetString(2),
                UsuarioId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                DataHora = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            });
        }
        return result;
    }

    public ModoAtualTorre Add(ModoAtualTorre alteracao)
    {
        try
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
            EnsureTable(connection);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO modo_atual_torre (MODOATUAL, MODOANTERIOR, ID_USER, DHALTERACAO) " +
                "VALUES ($modoAtual, $modoAnterior, $idUser, $dhAlteracao); " +
                "SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$modoAtual", alteracao.NovoModo);
            command.Parameters.AddWithValue("$modoAnterior", alteracao.ModoAnterior);
            command.Parameters.AddWithValue("$idUser", (object?)alteracao.UsuarioId ?? DBNull.Value);
            command.Parameters.AddWithValue("$dhAlteracao", alteracao.DataHora.ToString("O", CultureInfo.InvariantCulture));

            alteracao.Id = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            return alteracao;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            alteracao.Id = PendingWriteQueue.NextOfflineId();
            PendingWriteQueue.Enqueue(
                "INSERT INTO modo_atual_torre (MODOATUAL, MODOANTERIOR, ID_USER, DHALTERACAO) VALUES (" +
                $"{SqlLiteral.Text(alteracao.NovoModo)}, {SqlLiteral.Text(alteracao.ModoAnterior)}, {SqlLiteral.Number(alteracao.UsuarioId)}, " +
                $"{SqlLiteral.DateTime(alteracao.DataHora)})");
            return alteracao;
        }
    }

    private static void EnsureTable(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS modo_atual_torre (" +
            "ID_MODO INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "MODOATUAL TEXT NOT NULL, " +
            "MODOANTERIOR TEXT NOT NULL, " +
            "ID_USER INTEGER NULL, " +
            "DHALTERACAO TEXT NOT NULL)";
        command.ExecuteNonQuery();
    }
}
