using System;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>sessoes_usuario</c>) de
/// <see cref="ISessaoUsuarioRepository"/> — único armazenamento; sem fallback para CSV, com fila
/// de reenvio via <see cref="PendingWriteQueue"/> se o banco estiver momentaneamente inacessível
/// (mesmo padrão de <see cref="SqliteAcaoRealizadaRepository"/>).
/// </summary>
public sealed class SqliteSessaoUsuarioRepository : ISessaoUsuarioRepository
{
    public string Abrir(int usuarioId)
    {
        string id = Guid.NewGuid().ToString("N");
        DateTime agora = DateTime.Now;

        try
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
            EnsureTable(connection);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO sessoes_usuario (ID_SESSAO, UsuarioId, DataHoraInicio, DataHoraFim) " +
                "VALUES ($id, $usuarioId, $dataHoraInicio, NULL)";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$usuarioId", usuarioId);
            command.Parameters.AddWithValue("$dataHoraInicio", agora.ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            PendingWriteQueue.Enqueue(
                "INSERT INTO sessoes_usuario (ID_SESSAO, UsuarioId, DataHoraInicio, DataHoraFim) VALUES (" +
                $"{SqlLiteral.Text(id)}, {SqlLiteral.Number(usuarioId)}, {SqlLiteral.DateTime(agora)}, NULL)");
        }

        // O código é devolvido mesmo que a gravação tenha ido para a fila: quem chama (AuthService)
        // precisa do Id para carimbar as ações desta sessão independentemente de o banco estar
        // acessível agora — a linha correspondente aparece quando a fila for drenada.
        return id;
    }

    public void Encerrar(string sessaoId)
    {
        try
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
            EnsureTable(connection);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "UPDATE sessoes_usuario SET DataHoraFim = $dataHoraFim WHERE ID_SESSAO = $id AND DataHoraFim IS NULL";
            command.Parameters.AddWithValue("$dataHoraFim", DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$id", sessaoId);
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            PendingWriteQueue.Enqueue(
                "UPDATE sessoes_usuario SET DataHoraFim = " +
                $"{SqlLiteral.DateTime(DateTime.Now)} WHERE ID_SESSAO = {SqlLiteral.Text(sessaoId)} AND DataHoraFim IS NULL");
        }
    }

    public SessaoUsuario? GetById(string sessaoId)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT ID_SESSAO, UsuarioId, DataHoraInicio, DataHoraFim FROM sessoes_usuario WHERE ID_SESSAO = $id";
        command.Parameters.AddWithValue("$id", sessaoId);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        return new SessaoUsuario
        {
            Id = reader.GetString(0),
            UsuarioId = reader.GetInt32(1),
            DataHoraInicio = DateTime.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            DataHoraFim = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        };
    }

    private static void EnsureTable(SqliteConnection connection) =>
        SqliteSchema.GarantirUmaVez(connection, nameof(SqliteSessaoUsuarioRepository), CriarEsquema);

    /// <summary>Criação/migração do esquema desta tabela — roda uma única vez por banco e por execução.</summary>
    private static void CriarEsquema(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS sessoes_usuario (" +
            "ID_SESSAO TEXT PRIMARY KEY, " +
            "UsuarioId INTEGER NOT NULL REFERENCES Usuarios(Id), " +
            "DataHoraInicio TEXT NOT NULL, " +
            "DataHoraFim TEXT NULL); " +
            "CREATE INDEX IF NOT EXISTS idx_sessoes_usuario_usuario ON sessoes_usuario (UsuarioId);";
        command.ExecuteNonQuery();
    }
}
