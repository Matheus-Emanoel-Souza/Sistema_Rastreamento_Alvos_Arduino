using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>TABEXE</c>) de
/// <see cref="IExecucaoTreinamentoRepository"/>. Só inserção e um UPDATE de fechamento — nunca
/// descarta linhas, porque é ela que dá sentido aos <c>EXECUCAO_ID</c> gravados nas tabelas de
/// histórico, marcos, instantâneos e eventos.
/// </summary>
public sealed class SqliteExecucaoTreinamentoRepository : IExecucaoTreinamentoRepository
{
    /// <summary>
    /// Execução fictícia que recebe os registros migrados: as colunas <c>ExecucaoId</c> criadas
    /// por <see cref="SqliteSchema.EnsureColumn"/> nas tabelas filhas usam o padrão
    /// <c>'legada'</c>, e sem esta linha esses registros apontariam para uma execução inexistente.
    /// </summary>
    internal const string ExecucaoLegada = "legada";

    public void Abrir(ExecucaoTreinamento execucao)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand insert = connection.CreateCommand();
        insert.CommandText =
            "INSERT OR IGNORE INTO TABEXE (EXECUCAO_ID, DH_INICIO, TAMANHO_POPULACAO, QUANTIDADE_ESPECIES, " +
            "TETO_COMPLEXIDADE, META_GERACOES, CONFIGURACAO_FITNESS, GERACOES_CONCLUIDAS, ENCERRAMENTO) " +
            "VALUES ($id, $inicio, $populacao, $especies, $teto, $meta, $fitness, 0, $encerramento)";
        insert.Parameters.AddWithValue("$id", execucao.ExecucaoId);
        insert.Parameters.AddWithValue("$inicio", Texto(execucao.DataHoraInicio));
        insert.Parameters.AddWithValue("$populacao", execucao.TamanhoPopulacao);
        insert.Parameters.AddWithValue("$especies", execucao.QuantidadeEspecies);
        insert.Parameters.AddWithValue("$teto", execucao.TetoComplexidade);
        insert.Parameters.AddWithValue("$meta", execucao.MetaGeracoes);
        insert.Parameters.AddWithValue("$fitness", execucao.ConfiguracaoFitness);
        insert.Parameters.AddWithValue("$encerramento", EncerramentoExecucaoTreinamento.EmAndamento.ToString());
        insert.ExecuteNonQuery();
    }

    public void Fechar(string execucaoId, int geracoesConcluidas, double? melhorFitness, EncerramentoExecucaoTreinamento encerramento)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand update = connection.CreateCommand();
        update.CommandText =
            "UPDATE TABEXE SET DH_FIM = $fim, GERACOES_CONCLUIDAS = $geracoes, MELHOR_FITNESS = $melhor, " +
            "ENCERRAMENTO = $encerramento WHERE EXECUCAO_ID = $id";
        update.Parameters.AddWithValue("$fim", Texto(DateTime.Now));
        update.Parameters.AddWithValue("$geracoes", geracoesConcluidas);
        update.Parameters.AddWithValue("$melhor", (object?)melhorFitness ?? DBNull.Value);
        update.Parameters.AddWithValue("$encerramento", encerramento.ToString());
        update.Parameters.AddWithValue("$id", execucaoId);
        update.ExecuteNonQuery();
    }

    public ExecucaoTreinamento? ObterEmAndamento()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = ConsultaBase + " WHERE DH_FIM IS NULL ORDER BY DH_INICIO DESC LIMIT 1";

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? Ler(reader) : null;
    }

    public IReadOnlyList<ExecucaoTreinamento> ObterUltimas(int quantidade = 50)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var resultado = new List<ExecucaoTreinamento>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = ConsultaBase + " ORDER BY DH_INICIO DESC LIMIT $quantidade";
        command.Parameters.AddWithValue("$quantidade", quantidade);

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read()) resultado.Add(Ler(reader));

        return resultado;
    }

    public int FecharPendentesDeExecucaoAnterior()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        // O histórico por geração é a única fonte do "último sinal de vida" de uma execução
        // derrubada, mas num banco onde a tela de treinamento nunca foi aberta a tabela ainda não
        // existe — aí o fechamento se limita ao que TABEXE já sabe.
        bool temHistorico = SqliteSchema.TableExists(connection, "TABHTG");

        using SqliteCommand update = connection.CreateCommand();
        // DH_FIM recebe o último sinal de vida conhecido daquela execução (a geração mais recente
        // gravada, ou o próprio início quando nem isso houve) em vez de "agora": datar a queda de
        // ontem com a hora de hoje inventaria horas de treinamento que nunca aconteceram.
        update.CommandText = temHistorico
            ? "UPDATE TABEXE SET " +
              "DH_FIM = COALESCE((SELECT MAX(h.DH_REGISTRO) FROM TABHTG h WHERE h.EXECUCAO_ID = TABEXE.EXECUCAO_ID), DH_INICIO), " +
              "GERACOES_CONCLUIDAS = COALESCE((SELECT COUNT(*) FROM TABHTG h WHERE h.EXECUCAO_ID = TABEXE.EXECUCAO_ID), 0), " +
              "MELHOR_FITNESS = (SELECT MAX(h.MELHOR_FITNESS) FROM TABHTG h WHERE h.EXECUCAO_ID = TABEXE.EXECUCAO_ID), " +
              "ENCERRAMENTO = $interrompida " +
              "WHERE DH_FIM IS NULL AND EXECUCAO_ID <> $legada"
            : "UPDATE TABEXE SET DH_FIM = DH_INICIO, ENCERRAMENTO = $interrompida " +
              "WHERE DH_FIM IS NULL AND EXECUCAO_ID <> $legada";
        update.Parameters.AddWithValue("$interrompida", EncerramentoExecucaoTreinamento.Interrompida.ToString());
        update.Parameters.AddWithValue("$legada", ExecucaoLegada);
        return update.ExecuteNonQuery();
    }

    private const string ConsultaBase =
        "SELECT EXECUCAO_ID, DH_INICIO, DH_FIM, TAMANHO_POPULACAO, QUANTIDADE_ESPECIES, TETO_COMPLEXIDADE, " +
        "META_GERACOES, CONFIGURACAO_FITNESS, GERACOES_CONCLUIDAS, MELHOR_FITNESS, ENCERRAMENTO FROM TABEXE";

    private static ExecucaoTreinamento Ler(SqliteDataReader reader) => new()
    {
        ExecucaoId = reader.GetString(0),
        DataHoraInicio = Data(reader.GetString(1)),
        DataHoraFim = reader.IsDBNull(2) ? null : Data(reader.GetString(2)),
        TamanhoPopulacao = reader.GetInt32(3),
        QuantidadeEspecies = reader.GetInt32(4),
        TetoComplexidade = reader.GetInt32(5),
        MetaGeracoes = reader.GetInt32(6),
        ConfiguracaoFitness = reader.GetString(7),
        GeracoesConcluidas = reader.GetInt32(8),
        MelhorFitness = reader.IsDBNull(9) ? null : reader.GetDouble(9),
        Encerramento = InterpretarEncerramento(reader.GetString(10)),
    };

    /// <summary>
    /// Mesmo princípio de <c>SqliteEventoTreinamentoRepository.InterpretarTipo</c>: um valor que o
    /// enum não reconheça (gravado por versão mais nova) não derruba a leitura da lista inteira.
    /// </summary>
    private static EncerramentoExecucaoTreinamento InterpretarEncerramento(string valor) =>
        Enum.TryParse(valor, ignoreCase: true, out EncerramentoExecucaoTreinamento encerramento)
            ? encerramento
            : EncerramentoExecucaoTreinamento.Interrompida;

    private static string Texto(DateTime valor) => valor.ToString("O", CultureInfo.InvariantCulture);

    private static DateTime Data(string valor) =>
        DateTime.Parse(valor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    /// <summary>
    /// Internal porque <see cref="SqliteEspelhoSistemaRepository"/> precisa garantir TABEXE antes
    /// de criar TABESP: a chave estrangeira do espelho aponta para cá, e o Microsoft.Data.Sqlite
    /// abre as conexões com <c>PRAGMA foreign_keys</c> ligado — gravar numa tabela cuja tabela
    /// referenciada não existe falha com "no such table".
    /// </summary>
    internal static void EnsureTable(SqliteConnection connection) =>
        SqliteSchema.GarantirUmaVez(connection, nameof(SqliteExecucaoTreinamentoRepository), CriarEsquema);

    private static void CriarEsquema(SqliteConnection connection)
    {
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TABLE IF NOT EXISTS TABEXE (" +
                "EXECUCAO_ID TEXT PRIMARY KEY, " +
                "DH_INICIO TEXT NOT NULL, " +
                // NULL = em andamento. É esta coluna, e não ENCERRAMENTO, que ObterEmAndamento
                // consulta — uma só fonte de verdade para "ainda rodando".
                "DH_FIM TEXT NULL, " +
                "TAMANHO_POPULACAO INTEGER NOT NULL DEFAULT 0, " +
                "QUANTIDADE_ESPECIES INTEGER NOT NULL DEFAULT 0, " +
                "TETO_COMPLEXIDADE INTEGER NOT NULL DEFAULT 0, " +
                "META_GERACOES INTEGER NOT NULL DEFAULT 0, " +
                "CONFIGURACAO_FITNESS TEXT NOT NULL DEFAULT 'desconhecida', " +
                "GERACOES_CONCLUIDAS INTEGER NOT NULL DEFAULT 0, " +
                "MELHOR_FITNESS REAL NULL, " +
                "ENCERRAMENTO TEXT NOT NULL DEFAULT 'EmAndamento')";
            command.ExecuteNonQuery();
        }

        SqliteSchema.EnsureIndex(connection, "idx_execucoes_treinamento_inicio", "ON TABEXE (DH_INICIO)", unico: false);

        // A execução "legada": sem ela, todo registro migrado das tabelas filhas (que recebeu
        // EXECUCAO_ID = 'legada' por DEFAULT do ALTER TABLE) ficaria apontando para uma linha que
        // não existe. Já fechada, para nunca ser confundida com a execução em andamento.
        using SqliteCommand legada = connection.CreateCommand();
        legada.CommandText =
            "INSERT OR IGNORE INTO TABEXE (EXECUCAO_ID, DH_INICIO, DH_FIM, CONFIGURACAO_FITNESS, ENCERRAMENTO) " +
            "VALUES ($id, $inicio, $inicio, 'desconhecida', $encerramento)";
        legada.Parameters.AddWithValue("$id", ExecucaoLegada);
        legada.Parameters.AddWithValue("$inicio", Texto(DateTime.MinValue));
        legada.Parameters.AddWithValue("$encerramento", EncerramentoExecucaoTreinamento.Interrompida.ToString());
        legada.ExecuteNonQuery();
    }
}
