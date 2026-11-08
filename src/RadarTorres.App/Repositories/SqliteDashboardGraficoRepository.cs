using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>dashboard_graficos</c>) de
/// <see cref="IDashboardGraficoRepository"/>. Mesmo padrão de conexão/criação de tabela dos demais
/// repositórios (ver <see cref="SqliteExperimentoHiperparametrosRepository"/>).
/// </summary>
public sealed class SqliteDashboardGraficoRepository : IDashboardGraficoRepository
{
    public DashboardGrafico Registrar(DashboardGrafico card)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand insert = connection.CreateCommand();
        insert.CommandText =
            "INSERT INTO dashboard_graficos (Titulo, FonteChave, CampoEixoXChave, CampoMetricaChave, TipoGrafico, Ordem, DataCriacao) " +
            "VALUES ($titulo, $fonte, $eixoX, $metrica, $tipo, $ordem, $dataCriacao); " +
            "SELECT last_insert_rowid();";
        insert.Parameters.AddWithValue("$titulo", card.Titulo);
        insert.Parameters.AddWithValue("$fonte", card.FonteChave);
        insert.Parameters.AddWithValue("$eixoX", card.CampoEixoXChave);
        insert.Parameters.AddWithValue("$metrica", card.CampoMetricaChave);
        insert.Parameters.AddWithValue("$tipo", card.TipoGrafico.ToString());
        insert.Parameters.AddWithValue("$ordem", card.Ordem);
        insert.Parameters.AddWithValue("$dataCriacao", card.DataCriacao.ToString("O", CultureInfo.InvariantCulture));

        card.Id = Convert.ToInt32(insert.ExecuteScalar());
        return card;
    }

    public IReadOnlyList<DashboardGrafico> ObterTodos()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var resultado = new List<DashboardGrafico>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, Titulo, FonteChave, CampoEixoXChave, CampoMetricaChave, TipoGrafico, Ordem, DataCriacao " +
            "FROM dashboard_graficos ORDER BY Ordem ASC, Id ASC";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            resultado.Add(new DashboardGrafico
            {
                Id = reader.GetInt32(0),
                Titulo = reader.GetString(1),
                FonteChave = reader.GetString(2),
                CampoEixoXChave = reader.GetString(3),
                CampoMetricaChave = reader.GetString(4),
                TipoGrafico = InterpretarTipo(reader.GetString(5)),
                Ordem = reader.GetInt32(6),
                DataCriacao = DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            });
        }
        return resultado;
    }

    public void Remover(int id)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM dashboard_graficos WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    /// <summary>Um tipo que o banco traga e o enum não reconheça vira <see cref="TipoGraficoDashboard.Linha"/> em vez de derrubar a leitura.</summary>
    private static TipoGraficoDashboard InterpretarTipo(string valor) =>
        Enum.TryParse(valor, ignoreCase: true, out TipoGraficoDashboard tipo) ? tipo : TipoGraficoDashboard.Linha;

    private static void EnsureTable(SqliteConnection connection) =>
        SqliteSchema.GarantirUmaVez(connection, nameof(SqliteDashboardGraficoRepository), CriarEsquema);

    private static void CriarEsquema(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS dashboard_graficos (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "Titulo TEXT NOT NULL, " +
            "FonteChave TEXT NOT NULL, " +
            "CampoEixoXChave TEXT NOT NULL, " +
            "CampoMetricaChave TEXT NOT NULL, " +
            "TipoGrafico TEXT NOT NULL, " +
            "Ordem INTEGER NOT NULL, " +
            "DataCriacao TEXT NOT NULL)";
        command.ExecuteNonQuery();
    }
}
