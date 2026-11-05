using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>eventos_treinamento</c>) de
/// <see cref="IEventoTreinamentoRepository"/>. Buffer circular por <c>ID</c> crescente, igual ao
/// de <see cref="SqliteHistoricoTreinamentoRepository"/>: a cada inserção o excedente é removido
/// a partir do <c>ID</c> mais antigo — não é um TTL por data.
/// </summary>
public sealed class SqliteEventoTreinamentoRepository : IEventoTreinamentoRepository
{
    /// <summary>
    /// Teto de eventos guardados. Maior que as 200 linhas que a tela exibe por sessão porque aqui
    /// cabem várias execuções: o objetivo é poder voltar à execução de ontem, não só à de agora.
    /// </summary>
    private const int LimiteMaximoRegistros = 1000;

    public void Registrar(EventoTreinamento evento)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.CommandText =
                "INSERT INTO eventos_treinamento (GERACAO, TIPO, MENSAGEM, DH_REGISTRO) " +
                "VALUES ($geracao, $tipo, $mensagem, $dh)";
            insert.Parameters.AddWithValue("$geracao", (object?)evento.Geracao ?? DBNull.Value);
            // Gravado pelo NOME do membro, não pelo número: assim reordenar o enum não reinterpreta
            // registros antigos (um "Falha" virando "Informacao" seria um erro silencioso e grave).
            insert.Parameters.AddWithValue("$tipo", evento.Tipo.ToString());
            insert.Parameters.AddWithValue("$mensagem", evento.Mensagem);
            insert.Parameters.AddWithValue("$dh", evento.DataHora.ToString("O", CultureInfo.InvariantCulture));
            insert.ExecuteNonQuery();
        }

        using SqliteCommand limitar = connection.CreateCommand();
        limitar.CommandText =
            "DELETE FROM eventos_treinamento WHERE ID NOT IN (" +
            "SELECT ID FROM eventos_treinamento ORDER BY ID DESC LIMIT $limite)";
        limitar.Parameters.AddWithValue("$limite", LimiteMaximoRegistros);
        limitar.ExecuteNonQuery();
    }

    public IReadOnlyList<EventoTreinamento> ObterUltimos(int quantidade = 200)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var resultado = new List<EventoTreinamento>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT ID, GERACAO, TIPO, MENSAGEM, DH_REGISTRO " +
            "FROM eventos_treinamento ORDER BY ID DESC LIMIT $quantidade";
        command.Parameters.AddWithValue("$quantidade", quantidade);

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            resultado.Add(new EventoTreinamento
            {
                Id = reader.GetInt32(0),
                Geracao = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                Tipo = InterpretarTipo(reader.GetString(2)),
                Mensagem = reader.GetString(3),
                DataHora = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            });
        }

        // Já vem do mais novo para o mais antigo — é a ordem em que a tela exibe, então não há o
        // que inverter aqui (diferente do histórico por geração, que é cronológico).
        return resultado;
    }

    /// <summary>
    /// Um tipo que o banco traga e o enum não reconheça (registro gravado por uma versão mais
    /// nova) vira <see cref="TipoEventoTreinamento.Informacao"/> em vez de derrubar a leitura do
    /// histórico inteiro — a mensagem, que é o conteúdo, continua legível.
    /// </summary>
    private static TipoEventoTreinamento InterpretarTipo(string valor) =>
        Enum.TryParse(valor, ignoreCase: true, out TipoEventoTreinamento tipo) ? tipo : TipoEventoTreinamento.Informacao;

    public void LimparTudo()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM eventos_treinamento";
        command.ExecuteNonQuery();
    }

    private static void EnsureTable(SqliteConnection connection) =>
        SqliteSchema.GarantirUmaVez(connection, nameof(SqliteEventoTreinamentoRepository), CriarEsquema);

    /// <summary>Criação do esquema desta tabela — roda uma única vez por banco e por execução.</summary>
    private static void CriarEsquema(SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "CREATE TABLE IF NOT EXISTS eventos_treinamento (" +
            "ID INTEGER PRIMARY KEY AUTOINCREMENT, " +
            // NULL de propósito: há eventos que não pertencem a geração nenhuma ("treinamento
            // iniciado"), e zero ali seria confundido com a geração 0 (a população inicial).
            "GERACAO INTEGER NULL, " +
            "TIPO TEXT NOT NULL, " +
            "MENSAGEM TEXT NOT NULL, " +
            "DH_REGISTRO TEXT NOT NULL)";
        command.ExecuteNonQuery();

        using SqliteCommand indice = connection.CreateCommand();
        indice.CommandText = "CREATE INDEX IF NOT EXISTS idx_eventos_treinamento_dh ON eventos_treinamento (DH_REGISTRO)";
        indice.ExecuteNonQuery();
    }
}
