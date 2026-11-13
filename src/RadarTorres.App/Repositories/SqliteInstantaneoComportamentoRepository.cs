using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>instantaneos_comportamento_melhor_gene</c>)
/// de <see cref="IInstantaneoComportamentoRepository"/>. Mesmo padrão de conexão/criação de
/// tabela das demais tabelas de auditoria do projeto — só inserção, sem limite de linhas.
/// </summary>
public sealed class SqliteInstantaneoComportamentoRepository : IInstantaneoComportamentoRepository
{
    public void Registrar(InstantaneoComportamentoMelhorGene instantaneo)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO instantaneos_comportamento_melhor_gene " +
            "(GeracaoMarco, Fitness, AlvosAbatidos, AlvosEscapados, TirosErrados, TaxaDeAcerto, TaxaDeCobertura, GenomaJson, DataHora, ExecucaoId, ConfiguracaoFitness) " +
            "VALUES ($geracaoMarco, $fitness, $abatidos, $escapados, $errados, $taxaAcerto, $taxaCobertura, $genoma, $dataHora, $execucaoId, $configuracaoFitness)";
        command.Parameters.AddWithValue("$geracaoMarco", instantaneo.GeracaoMarco);
        command.Parameters.AddWithValue("$fitness", instantaneo.Fitness);
        command.Parameters.AddWithValue("$abatidos", instantaneo.AlvosAbatidos);
        command.Parameters.AddWithValue("$escapados", instantaneo.AlvosEscapados);
        command.Parameters.AddWithValue("$errados", instantaneo.TirosErrados);
        command.Parameters.AddWithValue("$taxaAcerto", (object?)instantaneo.TaxaDeAcerto ?? DBNull.Value);
        command.Parameters.AddWithValue("$taxaCobertura", (object?)instantaneo.TaxaDeCobertura ?? DBNull.Value);
        command.Parameters.AddWithValue("$genoma", instantaneo.GenomaJson);
        command.Parameters.AddWithValue("$dataHora", instantaneo.DataHora.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$execucaoId", instantaneo.ExecucaoId);
        command.Parameters.AddWithValue("$configuracaoFitness", instantaneo.ConfiguracaoFitness);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<InstantaneoComportamentoMelhorGene> ObterTodos()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var resultado = new List<InstantaneoComportamentoMelhorGene>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, GeracaoMarco, Fitness, AlvosAbatidos, AlvosEscapados, TirosErrados, TaxaDeAcerto, TaxaDeCobertura, GenomaJson, DataHora, ExecucaoId, ConfiguracaoFitness " +
            "FROM instantaneos_comportamento_melhor_gene ORDER BY GeracaoMarco";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            resultado.Add(new InstantaneoComportamentoMelhorGene
            {
                Id = reader.GetInt32(0),
                GeracaoMarco = reader.GetInt32(1),
                Fitness = reader.GetDouble(2),
                AlvosAbatidos = reader.GetInt32(3),
                AlvosEscapados = reader.GetInt32(4),
                TirosErrados = reader.GetInt32(5),
                TaxaDeAcerto = reader.IsDBNull(6) ? null : reader.GetDouble(6),
                TaxaDeCobertura = reader.IsDBNull(7) ? null : reader.GetDouble(7),
                GenomaJson = reader.GetString(8),
                DataHora = DateTime.Parse(reader.GetString(9), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                ExecucaoId = reader.GetString(10),
                ConfiguracaoFitness = reader.GetString(11),
            });
        }
        return resultado;
    }

    private static void EnsureTable(SqliteConnection connection) =>
        SqliteSchema.GarantirUmaVez(connection, nameof(SqliteInstantaneoComportamentoRepository), CriarEsquema);

    /// <summary>
    /// Criação/migração do esquema desta tabela — mesma correção e mesmo motivo de
    /// <see cref="SqliteMarcoFitnessTreinamentoRepository.CriarEsquema"/>: o índice único
    /// trocou de <c>GeracaoMarco</c> sozinho para <c>(ExecucaoId, GeracaoMarco)</c>, porque a
    /// geração reinicia em 0 a cada sessão de treinamento e duas sessões podiam colidir no mesmo
    /// marco de 1.000 gerações ("UNIQUE constraint failed"). Nenhum registro existente é
    /// apagado — os antigos recebem <c>ExecucaoId = 'legada'</c>.
    /// </summary>
    private static void CriarEsquema(SqliteConnection connection)
    {
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TABLE IF NOT EXISTS instantaneos_comportamento_melhor_gene (" +
                "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                "GeracaoMarco INTEGER NOT NULL, " +
                "Fitness REAL NOT NULL, " +
                "AlvosAbatidos INTEGER NOT NULL, " +
                "AlvosEscapados INTEGER NOT NULL, " +
                "TirosErrados INTEGER NOT NULL, " +
                "TaxaDeAcerto REAL NULL, " +
                "TaxaDeCobertura REAL NULL, " +
                "GenomaJson TEXT NOT NULL, " +
                "DataHora TEXT NOT NULL);";
            command.ExecuteNonQuery();
        }

        SqliteSchema.EnsureColumn(connection, "instantaneos_comportamento_melhor_gene", "ExecucaoId", "TEXT NOT NULL DEFAULT 'legada'");

        // Mesmo princípio da migração de ExecucaoId acima: nenhum registro existente é apagado,
        // os gravados antes desta coluna existir recebem 'desconhecida' (ver
        // ParametrosTreinamento.DescricaoConfiguracaoFitness).
        SqliteSchema.EnsureColumn(connection, "instantaneos_comportamento_melhor_gene", "ConfiguracaoFitness", "TEXT NOT NULL DEFAULT 'desconhecida'");

        using (SqliteCommand dropIndex = connection.CreateCommand())
        {
            dropIndex.CommandText = "DROP INDEX IF EXISTS idx_instantaneos_comportamento_geracao";
            dropIndex.ExecuteNonQuery();
        }

        SqliteSchema.EnsureIndex(connection, "idx_instantaneos_comportamento_execucao_geracao", "ON instantaneos_comportamento_melhor_gene (ExecucaoId, GeracaoMarco)", unico: true);
    }
}
