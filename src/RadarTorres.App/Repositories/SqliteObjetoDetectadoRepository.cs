using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>ObjetosDetectados</c>) de
/// <see cref="IObjetoDetectadoRepository"/> — único armazenamento; sem fallback para CSV (o
/// CSV que a tela "Objetos Detectados" ainda gera é exportação manual, ver
/// <see cref="CsvObjetoDetectadoColumns"/>).
/// </summary>
public sealed class SqliteObjetoDetectadoRepository : IObjetoDetectadoRepository
{
    public IReadOnlyList<ObjetoDetectado> GetAll()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var result = new List<ObjetoDetectado>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, Tipo, X, Y, Z, Quadrante, DataHora, Dispositivo, NivelConfianca, Observacao, ReferenciaImagem " +
            "FROM ObjetosDetectados ORDER BY Id";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new ObjetoDetectado
            {
                Id = reader.GetInt32(0),
                Tipo = reader.GetString(1),
                X = reader.GetDouble(2),
                Y = reader.GetDouble(3),
                Z = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                Quadrante = reader.GetString(5),
                DataHora = DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Dispositivo = reader.GetString(7),
                NivelConfianca = reader.IsDBNull(8) ? null : reader.GetDouble(8),
                Observacao = reader.IsDBNull(9) ? null : reader.GetString(9),
                ReferenciaImagem = reader.IsDBNull(10) ? null : reader.GetString(10),
            });
        }
        return result;
    }

    public ObjetoDetectado Add(ObjetoDetectado objeto)
    {
        try
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
            EnsureTable(connection);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO ObjetosDetectados (Tipo, X, Y, Z, Quadrante, DataHora, Dispositivo, NivelConfianca, Observacao, ReferenciaImagem) " +
                "VALUES ($tipo, $x, $y, $z, $quadrante, $dataHora, $dispositivo, $nivelConfianca, $observacao, $referenciaImagem); " +
                "SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$tipo", objeto.Tipo);
            command.Parameters.AddWithValue("$x", objeto.X);
            command.Parameters.AddWithValue("$y", objeto.Y);
            command.Parameters.AddWithValue("$z", (object?)objeto.Z ?? DBNull.Value);
            command.Parameters.AddWithValue("$quadrante", objeto.Quadrante);
            command.Parameters.AddWithValue("$dataHora", objeto.DataHora.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$dispositivo", objeto.Dispositivo);
            command.Parameters.AddWithValue("$nivelConfianca", (object?)objeto.NivelConfianca ?? DBNull.Value);
            command.Parameters.AddWithValue("$observacao", (object?)objeto.Observacao ?? DBNull.Value);
            command.Parameters.AddWithValue("$referenciaImagem", (object?)objeto.ReferenciaImagem ?? DBNull.Value);

            objeto.Id = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            return objeto;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            objeto.Id = PendingWriteQueue.NextOfflineId();
            PendingWriteQueue.Enqueue(
                "INSERT INTO ObjetosDetectados (Tipo, X, Y, Z, Quadrante, DataHora, Dispositivo, NivelConfianca, Observacao, ReferenciaImagem) VALUES (" +
                $"{SqlLiteral.Text(objeto.Tipo)}, {SqlLiteral.Number(objeto.X)}, {SqlLiteral.Number(objeto.Y)}, {SqlLiteral.Number(objeto.Z)}, " +
                $"{SqlLiteral.Text(objeto.Quadrante)}, {SqlLiteral.DateTime(objeto.DataHora)}, {SqlLiteral.Text(objeto.Dispositivo)}, " +
                $"{SqlLiteral.Number(objeto.NivelConfianca)}, {SqlLiteral.Text(objeto.Observacao)}, {SqlLiteral.Text(objeto.ReferenciaImagem)})");
            return objeto;
        }
    }

    private static void EnsureTable(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS ObjetosDetectados (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "Tipo TEXT NOT NULL, " +
            "X REAL NOT NULL, " +
            "Y REAL NOT NULL, " +
            "Z REAL NULL, " +
            "Quadrante TEXT NOT NULL, " +
            "DataHora TEXT NOT NULL, " +
            "Dispositivo TEXT NOT NULL, " +
            "NivelConfianca REAL NULL, " +
            "Observacao TEXT NULL, " +
            "ReferenciaImagem TEXT NULL)";
        command.ExecuteNonQuery();
    }
}
