using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>rede_neural</c>) de
/// <see cref="IRedeNeuralRepository"/>. Mesmo padrão de conexão/criação de tabela dos demais
/// repositórios (ver <see cref="SqliteExperimentoHiperparametrosRepository"/>).
/// </summary>
public sealed class SqliteRedeNeuralRepository : IRedeNeuralRepository
{
    public void Registrar(RedeNeural rede)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand insert = connection.CreateCommand();
        insert.CommandText =
            "INSERT INTO rede_neural (Versao, Nome, Entradas, Saidas, TipoCamadaOculta, NeuroniosOcultos, Biblioteca, Observacoes, DataRegistro) " +
            "VALUES ($versao, $nome, $entradas, $saidas, $tipoCamada, $neuronios, $biblioteca, $observacoes, $dataRegistro)";
        insert.Parameters.AddWithValue("$versao", rede.Versao);
        insert.Parameters.AddWithValue("$nome", rede.Nome);
        insert.Parameters.AddWithValue("$entradas", (object?)rede.Entradas ?? DBNull.Value);
        insert.Parameters.AddWithValue("$saidas", (object?)rede.Saidas ?? DBNull.Value);
        insert.Parameters.AddWithValue("$tipoCamada", (object?)rede.TipoCamadaOculta ?? DBNull.Value);
        insert.Parameters.AddWithValue("$neuronios", (object?)rede.NeuroniosOcultos ?? DBNull.Value);
        insert.Parameters.AddWithValue("$biblioteca", (object?)rede.Biblioteca ?? DBNull.Value);
        insert.Parameters.AddWithValue("$observacoes", (object?)rede.Observacoes ?? DBNull.Value);
        insert.Parameters.AddWithValue("$dataRegistro", rede.DataRegistro.ToString("O", CultureInfo.InvariantCulture));
        insert.ExecuteNonQuery();
    }

    public IReadOnlyList<RedeNeural> ObterTodos()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var resultado = new List<RedeNeural>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, Versao, Nome, Entradas, Saidas, TipoCamadaOculta, NeuroniosOcultos, Biblioteca, Observacoes, DataRegistro " +
            "FROM rede_neural ORDER BY Id ASC";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            resultado.Add(Ler(reader));
        }
        return resultado;
    }

    public RedeNeural? ObterPorVersao(string versao)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, Versao, Nome, Entradas, Saidas, TipoCamadaOculta, NeuroniosOcultos, Biblioteca, Observacoes, DataRegistro " +
            "FROM rede_neural WHERE Versao = $versao LIMIT 1";
        command.Parameters.AddWithValue("$versao", versao);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? Ler(reader) : null;
    }

    private static RedeNeural Ler(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Versao = reader.GetString(1),
        Nome = reader.GetString(2),
        Entradas = reader.IsDBNull(3) ? null : reader.GetInt32(3),
        Saidas = reader.IsDBNull(4) ? null : reader.GetInt32(4),
        TipoCamadaOculta = reader.IsDBNull(5) ? null : reader.GetString(5),
        NeuroniosOcultos = reader.IsDBNull(6) ? null : reader.GetInt32(6),
        Biblioteca = reader.IsDBNull(7) ? null : reader.GetString(7),
        Observacoes = reader.IsDBNull(8) ? null : reader.GetString(8),
        DataRegistro = DateTime.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
    };

    private static void EnsureTable(SqliteConnection connection) =>
        SqliteSchema.GarantirUmaVez(connection, nameof(SqliteRedeNeuralRepository), CriarEsquema);

    private static void CriarEsquema(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS rede_neural (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "Versao TEXT NOT NULL, " +
            "Nome TEXT NOT NULL, " +
            "Entradas INTEGER NULL, " +
            "Saidas INTEGER NULL, " +
            "TipoCamadaOculta TEXT NULL, " +
            "NeuroniosOcultos INTEGER NULL, " +
            "Biblioteca TEXT NULL, " +
            "Observacoes TEXT NULL, " +
            "DataRegistro TEXT NOT NULL)";
        command.ExecuteNonQuery();

        SqliteSchema.EnsureIndex(connection, "idx_rede_neural_versao", "ON rede_neural (Versao)", unico: true);
    }
}
