using System;
using System.IO;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>PreferenciasUsuario</c>) de
/// <see cref="IPreferenciasUsuarioRepository"/> — único armazenamento; sem fallback para CSV.
/// </summary>
public sealed class SqlitePreferenciasUsuarioRepository : IPreferenciasUsuarioRepository
{
    public PreferenciasUsuario? GetByUsuarioId(int usuarioId)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT UsuarioId, Idioma, Tema, SidebarRecolhida, TelaInicial, RegistrosPorPagina " +
            "FROM PreferenciasUsuario WHERE UsuarioId = $usuarioId";
        command.Parameters.AddWithValue("$usuarioId", usuarioId);

        using SqliteDataReader reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        return new PreferenciasUsuario
        {
            UsuarioId = reader.GetInt32(0),
            Idioma = reader.GetString(1),
            Tema = Enum.Parse<TemaPreferido>(reader.GetString(2)),
            SidebarRecolhida = reader.GetInt32(3) != 0,
            TelaInicial = reader.IsDBNull(4) ? null : reader.GetString(4),
            RegistrosPorPagina = reader.GetInt32(5),
        };
    }

    public void Salvar(PreferenciasUsuario preferencias)
    {
        try
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
            EnsureTable(connection);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO PreferenciasUsuario (UsuarioId, Idioma, Tema, SidebarRecolhida, TelaInicial, RegistrosPorPagina) " +
                "VALUES ($usuarioId, $idioma, $tema, $sidebarRecolhida, $telaInicial, $registrosPorPagina) " +
                "ON CONFLICT(UsuarioId) DO UPDATE SET " +
                "Idioma = excluded.Idioma, Tema = excluded.Tema, SidebarRecolhida = excluded.SidebarRecolhida, " +
                "TelaInicial = excluded.TelaInicial, RegistrosPorPagina = excluded.RegistrosPorPagina";
            command.Parameters.AddWithValue("$usuarioId", preferencias.UsuarioId);
            command.Parameters.AddWithValue("$idioma", preferencias.Idioma);
            command.Parameters.AddWithValue("$tema", preferencias.Tema.ToString());
            command.Parameters.AddWithValue("$sidebarRecolhida", preferencias.SidebarRecolhida ? 1 : 0);
            command.Parameters.AddWithValue("$telaInicial", (object?)preferencias.TelaInicial ?? DBNull.Value);
            command.Parameters.AddWithValue("$registrosPorPagina", preferencias.RegistrosPorPagina);
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            PendingWriteQueue.Enqueue(
                "INSERT INTO PreferenciasUsuario (UsuarioId, Idioma, Tema, SidebarRecolhida, TelaInicial, RegistrosPorPagina) VALUES (" +
                $"{SqlLiteral.Number(preferencias.UsuarioId)}, {SqlLiteral.Text(preferencias.Idioma)}, {SqlLiteral.Text(preferencias.Tema.ToString())}, " +
                $"{SqlLiteral.Bool(preferencias.SidebarRecolhida)}, {SqlLiteral.Text(preferencias.TelaInicial)}, {SqlLiteral.Number(preferencias.RegistrosPorPagina)}) " +
                "ON CONFLICT(UsuarioId) DO UPDATE SET " +
                "Idioma = excluded.Idioma, Tema = excluded.Tema, SidebarRecolhida = excluded.SidebarRecolhida, " +
                "TelaInicial = excluded.TelaInicial, RegistrosPorPagina = excluded.RegistrosPorPagina");
        }
    }

    private static void EnsureTable(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS PreferenciasUsuario (" +
            "UsuarioId INTEGER PRIMARY KEY, " +
            "Idioma TEXT NOT NULL, " +
            "Tema TEXT NOT NULL, " +
            "SidebarRecolhida INTEGER NOT NULL, " +
            "TelaInicial TEXT NULL, " +
            "RegistrosPorPagina INTEGER NOT NULL)";
        command.ExecuteNonQuery();
    }
}
