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
    /// <summary>
    /// Colunas acrescentadas depois que a tabela já existia em bases de usuário. Todas NULL, para
    /// as execuções reais já registradas continuarem válidas (a tela mostra "—" no que não foi
    /// medido) em vez de serem apagadas ou preenchidas com valor inventado.
    /// </summary>
    private static readonly (string Nome, string Tipo)[] ColunasEvolutivas =
    [
        ("TEMPO_SEGUNDOS", "REAL"),
        ("TAXA_MUTACAO", "REAL"),
        ("GERACAO_DO_PICO", "INTEGER"),
        ("FITNESS_G25", "REAL"),
        ("FITNESS_G50", "REAL"),
        ("FITNESS_G100", "REAL"),
        ("FITNESS_G200", "REAL"),
    ];

    public void Registrar(ExperimentoHiperparametros experimento)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand insert = connection.CreateCommand();
        insert.CommandText =
            "INSERT INTO experimentos_hiperparametros " +
            "(TAMANHO_POPULACAO, GERACOES, NEURONIOS_OCULTOS, FITNESS_FINAL, TEMPO_SEGUNDOS, TAXA_MUTACAO, " +
            " GERACAO_DO_PICO, FITNESS_G25, FITNESS_G50, FITNESS_G100, FITNESS_G200, DH_REGISTRO) " +
            "VALUES ($populacao, $geracoes, $neuronios, $fitness, $tempo, $mutacao, " +
            " $pico, $g25, $g50, $g100, $g200, $dh)";
        insert.Parameters.AddWithValue("$populacao", experimento.TamanhoPopulacao);
        insert.Parameters.AddWithValue("$geracoes", experimento.Geracoes);
        insert.Parameters.AddWithValue("$neuronios", experimento.NeuroniosOcultos);
        insert.Parameters.AddWithValue("$fitness", experimento.FitnessFinal);
        insert.Parameters.AddWithValue("$tempo", (object?)experimento.TempoSegundos ?? DBNull.Value);
        insert.Parameters.AddWithValue("$mutacao", (object?)experimento.TaxaMutacao ?? DBNull.Value);
        insert.Parameters.AddWithValue("$pico", (object?)experimento.GeracaoDoPico ?? DBNull.Value);
        insert.Parameters.AddWithValue("$g25", (object?)experimento.FitnessGeracao25 ?? DBNull.Value);
        insert.Parameters.AddWithValue("$g50", (object?)experimento.FitnessGeracao50 ?? DBNull.Value);
        insert.Parameters.AddWithValue("$g100", (object?)experimento.FitnessGeracao100 ?? DBNull.Value);
        insert.Parameters.AddWithValue("$g200", (object?)experimento.FitnessGeracao200 ?? DBNull.Value);
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
            "SELECT ID, TAMANHO_POPULACAO, GERACOES, NEURONIOS_OCULTOS, FITNESS_FINAL, TEMPO_SEGUNDOS, " +
            "       TAXA_MUTACAO, GERACAO_DO_PICO, FITNESS_G25, FITNESS_G50, FITNESS_G100, FITNESS_G200, DH_REGISTRO " +
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
                TaxaMutacao = reader.IsDBNull(6) ? null : reader.GetDouble(6),
                GeracaoDoPico = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                FitnessGeracao25 = reader.IsDBNull(8) ? null : reader.GetDouble(8),
                FitnessGeracao50 = reader.IsDBNull(9) ? null : reader.GetDouble(9),
                FitnessGeracao100 = reader.IsDBNull(10) ? null : reader.GetDouble(10),
                FitnessGeracao200 = reader.IsDBNull(11) ? null : reader.GetDouble(11),
                DataHora = DateTime.Parse(reader.GetString(12), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
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
                "DH_REGISTRO TEXT NOT NULL)";
            command.ExecuteNonQuery();
        }

        foreach ((string nome, string tipo) in ColunasEvolutivas)
        {
            if (ColunaExiste(connection, nome)) continue;

            using SqliteCommand alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE experimentos_hiperparametros ADD COLUMN {nome} {tipo} NULL";
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
