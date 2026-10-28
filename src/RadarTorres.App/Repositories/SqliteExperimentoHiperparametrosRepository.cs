using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>experimentos_hiperparametros</c>)
/// de <see cref="IExperimentoHiperparametrosRepository"/>.
/// </summary>
public sealed class SqliteExperimentoHiperparametrosRepository : IExperimentoHiperparametrosRepository
{
    public void Registrar(ExperimentoHiperparametros experimento)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand insert = connection.CreateCommand();
        insert.CommandText =
            "INSERT INTO experimentos_hiperparametros " +
            "(TAMANHO_POPULACAO, GERACOES, NEURONIOS_OCULTOS, FITNESS_FINAL, TEMPO_SEGUNDOS, DH_REGISTRO) " +
            "VALUES ($populacao, $geracoes, $neuronios, $fitness, $tempo, $dh)";
        insert.Parameters.AddWithValue("$populacao", experimento.TamanhoPopulacao);
        insert.Parameters.AddWithValue("$geracoes", experimento.Geracoes);
        insert.Parameters.AddWithValue("$neuronios", experimento.NeuroniosOcultos);
        insert.Parameters.AddWithValue("$fitness", experimento.FitnessFinal);
        insert.Parameters.AddWithValue("$tempo", (object?)experimento.TempoSegundos ?? DBNull.Value);
        insert.Parameters.AddWithValue("$dh", experimento.DataHora.ToString("O", CultureInfo.InvariantCulture));
        insert.ExecuteNonQuery();
    }

    public IReadOnlyList<ExperimentoHiperparametros> ObterTodos()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var resultado = new List<ExperimentoHiperparametros>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT ID, TAMANHO_POPULACAO, GERACOES, NEURONIOS_OCULTOS, FITNESS_FINAL, TEMPO_SEGUNDOS, DH_REGISTRO " +
            "FROM experimentos_hiperparametros ORDER BY ID ASC";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            resultado.Add(new ExperimentoHiperparametros
            {
                Id = reader.GetInt32(0),
                TamanhoPopulacao = reader.GetInt32(1),
                Geracoes = reader.GetInt32(2),
                NeuroniosOcultos = reader.GetInt32(3),
                FitnessFinal = reader.GetDouble(4),
                TempoSegundos = reader.IsDBNull(5) ? null : reader.GetDouble(5),
                DataHora = DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            });
        }

        return resultado;
    }

    public void Remover(int id)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM experimentos_hiperparametros WHERE ID = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    private static void EnsureTable(SqliteConnection connection)
    {
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TABLE IF NOT EXISTS experimentos_hiperparametros (" +
                "ID INTEGER PRIMARY KEY AUTOINCREMENT, " +
                "TAMANHO_POPULACAO INTEGER NOT NULL, " +
                "GERACOES INTEGER NOT NULL, " +
                "NEURONIOS_OCULTOS INTEGER NOT NULL, " +
                "FITNESS_FINAL REAL NOT NULL, " +
                "TEMPO_SEGUNDOS REAL NULL, " +
                "DH_REGISTRO TEXT NOT NULL)";
            command.ExecuteNonQuery();
        }

        // Migração das bases criadas antes de a medição de tempo existir: a coluna é adicionada
        // como NULL, preservando as execuções reais já registradas (a tela mostra "—" nelas).
        if (!ColunaExiste(connection, "TEMPO_SEGUNDOS"))
        {
            using SqliteCommand alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE experimentos_hiperparametros ADD COLUMN TEMPO_SEGUNDOS REAL NULL";
            alter.ExecuteNonQuery();
        }
    }

    private static bool ColunaExiste(SqliteConnection connection, string coluna)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM pragma_table_info('experimentos_hiperparametros') WHERE name = $coluna";
        command.Parameters.AddWithValue("$coluna", coluna);
        return command.ExecuteScalar() is not null;
    }
}
