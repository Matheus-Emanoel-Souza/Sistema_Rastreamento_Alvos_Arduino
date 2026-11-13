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
            "INSERT INTO marcos_fitness_treinamento (GeracaoMarco, FitnessMedioDoBloco, MelhorFitnessDoBloco, DataHora, ExecucaoId, ConfiguracaoFitness) " +
            "VALUES ($geracaoMarco, $fitnessMedio, $melhorFitness, $dataHora, $execucaoId, $configuracaoFitness)";
        command.Parameters.AddWithValue("$geracaoMarco", marco.GeracaoMarco);
        command.Parameters.AddWithValue("$fitnessMedio", marco.FitnessMedioDoBloco);
        command.Parameters.AddWithValue("$melhorFitness", marco.MelhorFitnessDoBloco);
        command.Parameters.AddWithValue("$dataHora", marco.DataHora.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$execucaoId", marco.ExecucaoId);
        command.Parameters.AddWithValue("$configuracaoFitness", marco.ConfiguracaoFitness);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<MarcoFitnessTreinamento> ObterTodos()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var resultado = new List<MarcoFitnessTreinamento>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, GeracaoMarco, FitnessMedioDoBloco, MelhorFitnessDoBloco, DataHora, ExecucaoId, ConfiguracaoFitness " +
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
                ExecucaoId = reader.GetString(5),
                ConfiguracaoFitness = reader.GetString(6),
            });
        }
        return resultado;
    }

    public void LimparTudo()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM marcos_fitness_treinamento";
        command.ExecuteNonQuery();
    }

    private static void EnsureTable(SqliteConnection connection) =>
        SqliteSchema.GarantirUmaVez(connection, nameof(SqliteMarcoFitnessTreinamentoRepository), CriarEsquema);

    /// <summary>
    /// Criação/migração do esquema desta tabela — roda uma única vez por banco e por execução.
    /// </summary>
    /// <remarks>
    /// Até esta migração, o índice único era só em <c>GeracaoMarco</c>: como a numeração de
    /// gerações do NEAT reinicia em 0 a cada "Iniciar" (ver TreinadorNeat.EvoluirGeracao), uma
    /// segunda sessão de treinamento que também passasse pela geração 500 colidia com o marco já
    /// gravado na primeira e falhava com "UNIQUE constraint failed" — o INSERT era descartado
    /// silenciosamente (capturado pelo try/catch do chamador), perdendo o marco da sessão nova.
    /// A correção troca a unicidade para (ExecucaoId, GeracaoMarco): cada sessão tem seu próprio
    /// <see cref="MarcoFitnessTreinamento.ExecucaoId"/> (um Guid novo por "Iniciar"), então a
    /// mesma geração em sessões diferentes não colide mais. Nenhum registro existente é apagado
    /// — os antigos (de antes desta coluna existir) recebem <c>'legada'</c> via
    /// <c>ALTER TABLE ... DEFAULT</c> (ver <see cref="SqliteSchema.EnsureColumn"/>).
    /// </remarks>
    private static void CriarEsquema(SqliteConnection connection)
    {
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TABLE IF NOT EXISTS marcos_fitness_treinamento (" +
                "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                "GeracaoMarco INTEGER NOT NULL, " +
                "FitnessMedioDoBloco REAL NOT NULL, " +
                "MelhorFitnessDoBloco REAL NOT NULL, " +
                "DataHora TEXT NOT NULL);";
            command.ExecuteNonQuery();
        }

        SqliteSchema.EnsureColumn(connection, "marcos_fitness_treinamento", "ExecucaoId", "TEXT NOT NULL DEFAULT 'legada'");

        // Mesmo princípio da migração de ExecucaoId acima: nenhum registro existente é apagado,
        // os gravados antes desta coluna existir recebem 'desconhecida' (ver
        // ParametrosTreinamento.DescricaoConfiguracaoFitness).
        SqliteSchema.EnsureColumn(connection, "marcos_fitness_treinamento", "ConfiguracaoFitness", "TEXT NOT NULL DEFAULT 'desconhecida'");

        using (SqliteCommand dropIndex = connection.CreateCommand())
        {
            // Índice antigo (só GeracaoMarco) — a causa raiz do bug; precisa sair antes de criar
            // o composto, senão os dois coexistiriam e o antigo continuaria barrando o INSERT.
            dropIndex.CommandText = "DROP INDEX IF EXISTS idx_marcos_fitness_geracao";
            dropIndex.ExecuteNonQuery();
        }

        SqliteSchema.EnsureIndex(connection, "idx_marcos_fitness_execucao_geracao", "ON marcos_fitness_treinamento (ExecucaoId, GeracaoMarco)", unico: true);
    }
}
