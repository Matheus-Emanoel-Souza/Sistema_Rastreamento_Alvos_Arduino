using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>historico_treinamento_geracoes</c>)
/// de <see cref="IHistoricoTreinamentoRepository"/>. Buffer circular por <c>ID</c> crescente: a
/// cada inserção, remove o excedente a partir do <c>ID</c> mais antigo — não é um TTL por data,
/// é estritamente "os <see cref="LimiteMaximoRegistros"/> registros mais recentes".
/// </summary>
public sealed class SqliteHistoricoTreinamentoRepository : IHistoricoTreinamentoRepository
{
    private const int LimiteMaximoRegistros = 300;

    public void Registrar(HistoricoGeracaoTreinamento registro)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.CommandText =
                "INSERT INTO historico_treinamento_geracoes " +
                "(GERACAO, MELHOR_FITNESS, FITNESS_MEDIO, ALVOS_ABATIDOS, ALVOS_ESCAPADOS, DH_REGISTRO) " +
                "VALUES ($geracao, $melhorFitness, $fitnessMedio, $alvosAbatidos, $alvosEscapados, $dh)";
            insert.Parameters.AddWithValue("$geracao", registro.Geracao);
            insert.Parameters.AddWithValue("$melhorFitness", registro.MelhorFitness);
            insert.Parameters.AddWithValue("$fitnessMedio", registro.FitnessMedio);
            insert.Parameters.AddWithValue("$alvosAbatidos", registro.AlvosAbatidos);
            insert.Parameters.AddWithValue("$alvosEscapados", registro.AlvosEscapados);
            insert.Parameters.AddWithValue("$dh", registro.DataHora.ToString("O", CultureInfo.InvariantCulture));
            insert.ExecuteNonQuery();
        }

        using SqliteCommand limitar = connection.CreateCommand();
        limitar.CommandText =
            "DELETE FROM historico_treinamento_geracoes WHERE ID NOT IN (" +
            "SELECT ID FROM historico_treinamento_geracoes ORDER BY ID DESC LIMIT $limite)";
        limitar.Parameters.AddWithValue("$limite", LimiteMaximoRegistros);
        limitar.ExecuteNonQuery();
    }

    public IReadOnlyList<HistoricoGeracaoTreinamento> ObterUltimos(int quantidade = LimiteMaximoRegistros)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var resultado = new List<HistoricoGeracaoTreinamento>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT ID, GERACAO, MELHOR_FITNESS, FITNESS_MEDIO, ALVOS_ABATIDOS, ALVOS_ESCAPADOS, DH_REGISTRO " +
            "FROM historico_treinamento_geracoes ORDER BY ID DESC LIMIT $quantidade";
        command.Parameters.AddWithValue("$quantidade", quantidade);

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            resultado.Add(new HistoricoGeracaoTreinamento
            {
                Id = reader.GetInt32(0),
                Geracao = reader.GetInt32(1),
                MelhorFitness = reader.GetDouble(2),
                FitnessMedio = reader.GetDouble(3),
                AlvosAbatidos = reader.GetInt32(4),
                AlvosEscapados = reader.GetInt32(5),
                DataHora = DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            });
        }

        resultado.Reverse(); // consulta veio do mais novo pro mais antigo; devolve em ordem cronológica
        return resultado;
    }

    private static void EnsureTable(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS historico_treinamento_geracoes (" +
            "ID INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "GERACAO INTEGER NOT NULL, " +
            "MELHOR_FITNESS REAL NOT NULL, " +
            "FITNESS_MEDIO REAL NOT NULL, " +
            "ALVOS_ABATIDOS INTEGER NOT NULL, " +
            "ALVOS_ESCAPADOS INTEGER NOT NULL, " +
            "DH_REGISTRO TEXT NOT NULL)";
        command.ExecuteNonQuery();

        using SqliteCommand indice = connection.CreateCommand();
        indice.CommandText = "CREATE INDEX IF NOT EXISTS idx_historico_treinamento_geracao ON historico_treinamento_geracoes (GERACAO)";
        indice.ExecuteNonQuery();
    }
}
