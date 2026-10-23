using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>marcos_fitness_treinamento</c>) de
/// <see cref="IMarcoFitnessTreinamentoRepository"/>. Mesmo padrão de conexão/criação de tabela de
/// <see cref="SqliteHistoricoTreinamentoRepository"/>, mas sem o descarte por limite — um marco
/// por 500 gerações nunca acumula volume relevante.
/// </summary>
public sealed class SqliteMarcoFitnessTreinamentoRepository : IMarcoFitnessTreinamentoRepository
{
    public void Registrar(MarcoFitnessTreinamento marco)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO marcos_fitness_treinamento (GeracaoMarco, FitnessMedioDoBloco, MelhorFitnessDoBloco, DataHora) " +
            "VALUES ($geracaoMarco, $fitnessMedio, $melhorFitness, $dataHora)";
        command.Parameters.AddWithValue("$geracaoMarco", marco.GeracaoMarco);
        command.Parameters.AddWithValue("$fitnessMedio", marco.FitnessMedioDoBloco);
        command.Parameters.AddWithValue("$melhorFitness", marco.MelhorFitnessDoBloco);
        command.Parameters.AddWithValue("$dataHora", marco.DataHora.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<MarcoFitnessTreinamento> ObterTodos()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var resultado = new List<MarcoFitnessTreinamento>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, GeracaoMarco, FitnessMedioDoBloco, MelhorFitnessDoBloco, DataHora " +
            "FROM marcos_fitness_treinamento ORDER BY GeracaoMarco";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            resultado.Add(new MarcoFitnessTreinamento
            {
                Id = reader.GetInt32(0),
                GeracaoMarco = reader.GetInt32(1),
                FitnessMedioDoBloco = reader.GetDouble(2),
                MelhorFitnessDoBloco = reader.GetDouble(3),
                DataHora = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            });
        }
        return resultado;
    }

    private static void EnsureTable(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS marcos_fitness_treinamento (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "GeracaoMarco INTEGER NOT NULL, " +
            "FitnessMedioDoBloco REAL NOT NULL, " +
            "MelhorFitnessDoBloco REAL NOT NULL, " +
            "DataHora TEXT NOT NULL); " +
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_marcos_fitness_geracao ON marcos_fitness_treinamento (GeracaoMarco);";
        command.ExecuteNonQuery();
    }
}
