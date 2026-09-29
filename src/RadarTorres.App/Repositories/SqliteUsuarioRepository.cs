using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>Usuarios</c>) de
/// <see cref="IUsuarioRepository"/> — único armazenamento; sem fallback para CSV.
/// </summary>
public sealed class SqliteUsuarioRepository : IUsuarioRepository
{
    public IReadOnlyList<Usuario> GetAll()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectAllSql + " ORDER BY Id";
        using SqliteDataReader reader = command.ExecuteReader();

        var result = new List<Usuario>();
        while (reader.Read())
        {
            result.Add(ReadUsuario(reader));
        }
        return result;
    }

    public Usuario? GetByLogin(string login)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectAllSql + " WHERE Login = $login COLLATE NOCASE";
        command.Parameters.AddWithValue("$login", login);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadUsuario(reader) : null;
    }

    public Usuario? GetById(int id)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectAllSql + " WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? ReadUsuario(reader) : null;
    }

    public Usuario Add(Usuario usuario)
    {
        try
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
            EnsureTable(connection);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO Usuarios (Nome, Email, Idade, Login, SenhaHash, SenhaSalt, Perfil, Ativo, DataCriacao, UltimoAcesso) " +
                "VALUES ($nome, $email, $idade, $login, $senhaHash, $senhaSalt, $perfil, $ativo, $dataCriacao, $ultimoAcesso); " +
                "SELECT last_insert_rowid();";
            AddCommonParameters(command, usuario);

            usuario.Id = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            return usuario;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            usuario.Id = PendingWriteQueue.NextOfflineId();
            PendingWriteQueue.Enqueue(BuildInsertSql(usuario));
            return usuario;
        }
    }

    public void Update(Usuario usuario)
    {
        try
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
            EnsureTable(connection);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "UPDATE Usuarios SET Nome = $nome, Email = $email, Idade = $idade, Login = $login, " +
                "SenhaHash = $senhaHash, SenhaSalt = $senhaSalt, Perfil = $perfil, Ativo = $ativo, " +
                "DataCriacao = $dataCriacao, UltimoAcesso = $ultimoAcesso WHERE Id = $id";
            AddCommonParameters(command, usuario);
            command.Parameters.AddWithValue("$id", usuario.Id);
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            PendingWriteQueue.Enqueue(BuildUpdateSql(usuario));
        }
    }

    private static string BuildInsertSql(Usuario usuario) =>
        "INSERT INTO Usuarios (Nome, Email, Idade, Login, SenhaHash, SenhaSalt, Perfil, Ativo, DataCriacao, UltimoAcesso) VALUES (" +
        $"{SqlLiteral.Text(usuario.Nome)}, {SqlLiteral.Text(usuario.Email)}, {SqlLiteral.Number(usuario.Idade)}, {SqlLiteral.Text(usuario.Login)}, " +
        $"{SqlLiteral.Text(usuario.SenhaHash)}, {SqlLiteral.Text(usuario.SenhaSalt)}, {SqlLiteral.Text(usuario.Perfil.ToString())}, {SqlLiteral.Bool(usuario.Ativo)}, " +
        $"{SqlLiteral.DateTime(usuario.DataCriacao)}, {SqlLiteral.DateTime(usuario.UltimoAcesso)})";

    private static string BuildUpdateSql(Usuario usuario) =>
        "UPDATE Usuarios SET " +
        $"Nome = {SqlLiteral.Text(usuario.Nome)}, Email = {SqlLiteral.Text(usuario.Email)}, Idade = {SqlLiteral.Number(usuario.Idade)}, " +
        $"Login = {SqlLiteral.Text(usuario.Login)}, SenhaHash = {SqlLiteral.Text(usuario.SenhaHash)}, SenhaSalt = {SqlLiteral.Text(usuario.SenhaSalt)}, " +
        $"Perfil = {SqlLiteral.Text(usuario.Perfil.ToString())}, Ativo = {SqlLiteral.Bool(usuario.Ativo)}, " +
        $"DataCriacao = {SqlLiteral.DateTime(usuario.DataCriacao)}, UltimoAcesso = {SqlLiteral.DateTime(usuario.UltimoAcesso)} " +
        $"WHERE Id = {SqlLiteral.Number(usuario.Id)}";

    private static void AddCommonParameters(SqliteCommand command, Usuario usuario)
    {
        command.Parameters.AddWithValue("$nome", usuario.Nome);
        command.Parameters.AddWithValue("$email", usuario.Email);
        command.Parameters.AddWithValue("$idade", (object?)usuario.Idade ?? DBNull.Value);
        command.Parameters.AddWithValue("$login", usuario.Login);
        command.Parameters.AddWithValue("$senhaHash", usuario.SenhaHash);
        command.Parameters.AddWithValue("$senhaSalt", usuario.SenhaSalt);
        command.Parameters.AddWithValue("$perfil", usuario.Perfil.ToString());
        command.Parameters.AddWithValue("$ativo", usuario.Ativo ? 1 : 0);
        command.Parameters.AddWithValue("$dataCriacao", usuario.DataCriacao.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$ultimoAcesso", usuario.UltimoAcesso.HasValue ? usuario.UltimoAcesso.Value.ToString("O", CultureInfo.InvariantCulture) : DBNull.Value);
    }

    private const string SelectAllSql =
        "SELECT Id, Nome, Email, Idade, Login, SenhaHash, SenhaSalt, Perfil, Ativo, DataCriacao, UltimoAcesso FROM Usuarios";

    private static Usuario ReadUsuario(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Nome = reader.GetString(1),
        Email = reader.GetString(2),
        Idade = reader.IsDBNull(3) ? null : reader.GetInt32(3),
        Login = reader.GetString(4),
        SenhaHash = reader.GetString(5),
        SenhaSalt = reader.GetString(6),
        Perfil = Enum.Parse<PerfilUsuario>(reader.GetString(7)),
        Ativo = reader.GetInt32(8) != 0,
        DataCriacao = DateTime.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        UltimoAcesso = reader.IsDBNull(10) ? null : DateTime.Parse(reader.GetString(10), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
    };

    private static void EnsureTable(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS Usuarios (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "Nome TEXT NOT NULL, " +
            "Email TEXT NOT NULL, " +
            "Idade INTEGER NULL, " +
            "Login TEXT NOT NULL UNIQUE, " +
            "SenhaHash TEXT NOT NULL, " +
            "SenhaSalt TEXT NOT NULL, " +
            "Perfil TEXT NOT NULL, " +
            "Ativo INTEGER NOT NULL, " +
            "DataCriacao TEXT NOT NULL, " +
            "UltimoAcesso TEXT NULL)";
        command.ExecuteNonQuery();
    }
}
