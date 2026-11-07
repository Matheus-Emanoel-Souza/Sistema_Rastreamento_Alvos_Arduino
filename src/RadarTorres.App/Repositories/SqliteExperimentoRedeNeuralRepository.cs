using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>experimento_rede_neural</c>) de
/// <see cref="IExperimentoRedeNeuralRepository"/>.
/// </summary>
public sealed class SqliteExperimentoRedeNeuralRepository : IExperimentoRedeNeuralRepository
{
    public void Registrar(ExperimentoRedeNeural e)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand insert = connection.CreateCommand();
        insert.CommandText =
            "INSERT INTO experimento_rede_neural " +
            "(RedeNeuralId, Variante, Repeticao, Populacao, Geracoes, TetoComplexidade, RedeRecorrente, FitnessV2, " +
            " FitnessTreinoPico, GeracaoDoPico, SegundosTreino, ValFitnessMedio, ValAlvosNascidosMedio, ValAbatidosMedio, " +
            " ValEscapadosMedio, ValTirosErradosMedio, ValTempoRespostaMedioSegundos, ValTrocasPenalizadasMedia, " +
            " ValCooperacoesConcedidasMedia, ValCooperacoesNegadasMedia, FonteDados, DataRegistro) " +
            "VALUES " +
            "($redeNeuralId, $variante, $repeticao, $populacao, $geracoes, $teto, $recorrente, $fitnessV2, " +
            " $fitnessPico, $geracaoPico, $segundosTreino, $valFitness, $valNascidos, $valAbatidos, " +
            " $valEscapados, $valErrados, $valTempoResposta, $valTrocas, " +
            " $valCoopConcedida, $valCoopNegada, $fonte, $dataRegistro)";
        insert.Parameters.AddWithValue("$redeNeuralId", e.RedeNeuralId);
        insert.Parameters.AddWithValue("$variante", e.Variante);
        insert.Parameters.AddWithValue("$repeticao", e.Repeticao);
        insert.Parameters.AddWithValue("$populacao", e.Populacao);
        insert.Parameters.AddWithValue("$geracoes", e.Geracoes);
        insert.Parameters.AddWithValue("$teto", e.TetoComplexidade);
        insert.Parameters.AddWithValue("$recorrente", e.RedeRecorrente ? 1 : 0);
        insert.Parameters.AddWithValue("$fitnessV2", e.FitnessV2 ? 1 : 0);
        insert.Parameters.AddWithValue("$fitnessPico", e.FitnessTreinoPico);
        insert.Parameters.AddWithValue("$geracaoPico", e.GeracaoDoPico);
        insert.Parameters.AddWithValue("$segundosTreino", e.SegundosTreino);
        insert.Parameters.AddWithValue("$valFitness", e.ValFitnessMedio);
        insert.Parameters.AddWithValue("$valNascidos", e.ValAlvosNascidosMedio);
        insert.Parameters.AddWithValue("$valAbatidos", e.ValAbatidosMedio);
        insert.Parameters.AddWithValue("$valEscapados", e.ValEscapadosMedio);
        insert.Parameters.AddWithValue("$valErrados", e.ValTirosErradosMedio);
        insert.Parameters.AddWithValue("$valTempoResposta", e.ValTempoRespostaMedioSegundos);
        insert.Parameters.AddWithValue("$valTrocas", e.ValTrocasPenalizadasMedia);
        insert.Parameters.AddWithValue("$valCoopConcedida", e.ValCooperacoesConcedidasMedia);
        insert.Parameters.AddWithValue("$valCoopNegada", e.ValCooperacoesNegadasMedia);
        insert.Parameters.AddWithValue("$fonte", e.FonteDados);
        insert.Parameters.AddWithValue("$dataRegistro", e.DataRegistro.ToString("O", CultureInfo.InvariantCulture));
        insert.ExecuteNonQuery();
    }

    public IReadOnlyList<ExperimentoRedeNeural> ObterTodos()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectBase + "ORDER BY Id ASC";
        return Ler(command);
    }

    public IReadOnlyList<ExperimentoRedeNeural> ObterPorRedeNeural(int redeNeuralId)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = SelectBase + "WHERE RedeNeuralId = $redeNeuralId ORDER BY Id ASC";
        command.Parameters.AddWithValue("$redeNeuralId", redeNeuralId);
        return Ler(command);
    }

    private const string SelectBase =
        "SELECT Id, RedeNeuralId, Variante, Repeticao, Populacao, Geracoes, TetoComplexidade, RedeRecorrente, FitnessV2, " +
        "       FitnessTreinoPico, GeracaoDoPico, SegundosTreino, ValFitnessMedio, ValAlvosNascidosMedio, ValAbatidosMedio, " +
        "       ValEscapadosMedio, ValTirosErradosMedio, ValTempoRespostaMedioSegundos, ValTrocasPenalizadasMedia, " +
        "       ValCooperacoesConcedidasMedia, ValCooperacoesNegadasMedia, FonteDados, DataRegistro " +
        "FROM experimento_rede_neural ";

    private static List<ExperimentoRedeNeural> Ler(SqliteCommand command)
    {
        var resultado = new List<ExperimentoRedeNeural>();
        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            resultado.Add(new ExperimentoRedeNeural
            {
                Id = reader.GetInt32(0),
                RedeNeuralId = reader.GetInt32(1),
                Variante = reader.GetString(2),
                Repeticao = reader.GetInt32(3),
                Populacao = reader.GetInt32(4),
                Geracoes = reader.GetInt32(5),
                TetoComplexidade = reader.GetInt32(6),
                RedeRecorrente = reader.GetInt32(7) != 0,
                FitnessV2 = reader.GetInt32(8) != 0,
                FitnessTreinoPico = reader.GetDouble(9),
                GeracaoDoPico = reader.GetInt32(10),
                SegundosTreino = reader.GetDouble(11),
                ValFitnessMedio = reader.GetDouble(12),
                ValAlvosNascidosMedio = reader.GetDouble(13),
                ValAbatidosMedio = reader.GetDouble(14),
                ValEscapadosMedio = reader.GetDouble(15),
                ValTirosErradosMedio = reader.GetDouble(16),
                ValTempoRespostaMedioSegundos = reader.GetDouble(17),
                ValTrocasPenalizadasMedia = reader.GetDouble(18),
                ValCooperacoesConcedidasMedia = reader.GetDouble(19),
                ValCooperacoesNegadasMedia = reader.GetDouble(20),
                FonteDados = reader.GetString(21),
                DataRegistro = DateTime.Parse(reader.GetString(22), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            });
        }
        return resultado;
    }

    private static void EnsureTable(SqliteConnection connection) =>
        SqliteSchema.GarantirUmaVez(connection, nameof(SqliteExperimentoRedeNeuralRepository), CriarEsquema);

    private static void CriarEsquema(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS experimento_rede_neural (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "RedeNeuralId INTEGER NOT NULL REFERENCES rede_neural(Id), " +
            "Variante TEXT NOT NULL, " +
            "Repeticao INTEGER NOT NULL, " +
            "Populacao INTEGER NOT NULL, " +
            "Geracoes INTEGER NOT NULL, " +
            "TetoComplexidade INTEGER NOT NULL, " +
            "RedeRecorrente INTEGER NOT NULL, " +
            "FitnessV2 INTEGER NOT NULL, " +
            "FitnessTreinoPico REAL NOT NULL, " +
            "GeracaoDoPico INTEGER NOT NULL, " +
            "SegundosTreino REAL NOT NULL, " +
            "ValFitnessMedio REAL NOT NULL, " +
            "ValAlvosNascidosMedio REAL NOT NULL, " +
            "ValAbatidosMedio REAL NOT NULL, " +
            "ValEscapadosMedio REAL NOT NULL, " +
            "ValTirosErradosMedio REAL NOT NULL, " +
            "ValTempoRespostaMedioSegundos REAL NOT NULL, " +
            "ValTrocasPenalizadasMedia REAL NOT NULL, " +
            "ValCooperacoesConcedidasMedia REAL NOT NULL, " +
            "ValCooperacoesNegadasMedia REAL NOT NULL, " +
            "FonteDados TEXT NOT NULL, " +
            "DataRegistro TEXT NOT NULL)";
        command.ExecuteNonQuery();

        SqliteSchema.EnsureIndex(connection, "idx_experimento_rede_neural_rede", "ON experimento_rede_neural (RedeNeuralId)", unico: false);
    }
}
