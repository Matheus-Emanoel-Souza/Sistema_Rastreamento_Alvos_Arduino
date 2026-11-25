using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>TABESP</c>) de
/// <see cref="IEspelhoSistemaRepository"/>: grava, de tempos em tempos, uma linha com o estado
/// agregado do sistema inteiro.
/// </summary>
/// <remarks>
/// <para>
/// A captura é UMA instrução <c>INSERT ... SELECT</c> com subconsultas sobre as tabelas
/// existentes — não lê serviços em memória nem faz várias idas ao banco. Além de barato, isso faz
/// o espelho refletir exatamente o que está persistido (que é o que alguém audita depois) e não
/// o que a interface estava exibindo.
/// </para>
/// <para>
/// Cada tabela é consultada só se existir (ver <see cref="SqliteSchema.TableExists"/>): os
/// <c>Sqlite*Repository</c> criam a própria tabela sob demanda, então numa instalação onde a tela
/// de treinamento nunca foi aberta TABHTG simplesmente não existe, e um join direto quebraria a
/// captura inteira. O que falta vira 0/NULL, que é a leitura correta: não havia nada ali.
/// </para>
/// </remarks>
public sealed class SqliteEspelhoSistemaRepository : IEspelhoSistemaRepository
{
    /// <summary>
    /// Teto de espelhos guardados, em buffer circular por <c>ID</c> (mesma mecânica de
    /// <see cref="SqliteHistoricoTreinamentoRepository"/>). No padrão de 30 s do
    /// <c>EspelhoSistemaService</c> isso cobre cerca de 17 dias de operação contínua.
    /// </summary>
    private const int LimiteMaximoRegistros = 50_000;

    public EspelhoSistema Capturar()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        string agora = Texto(DateTime.Now);

        using (SqliteCommand insert = connection.CreateCommand())
        {
            insert.CommandText = MontarInsertDeCaptura(connection);
            insert.Parameters.AddWithValue("$agora", agora);
            insert.ExecuteNonQuery();
        }

        using (SqliteCommand limitar = connection.CreateCommand())
        {
            limitar.CommandText =
                "DELETE FROM TABESP WHERE ID NOT IN (SELECT ID FROM TABESP ORDER BY ID DESC LIMIT $limite)";
            limitar.Parameters.AddWithValue("$limite", LimiteMaximoRegistros);
            limitar.ExecuteNonQuery();
        }

        using SqliteCommand lido = connection.CreateCommand();
        lido.CommandText = ConsultaBase + " WHERE ID = last_insert_rowid()";
        using SqliteDataReader reader = lido.ExecuteReader();

        // O SELECT acima só não traz linha se outra conexão tiver apagado o espelho recém-gravado
        // entre o INSERT e a leitura — impossível na prática (o buffer só descarta os mais
        // antigos), mas o contrato do método é devolver o que foi gravado, não um nulo.
        return reader.Read() ? Ler(reader) : new EspelhoSistema { DataHora = Data(agora) };
    }

    public IReadOnlyList<EspelhoSistema> ObterUltimos(int quantidade = 200)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var resultado = new List<EspelhoSistema>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = ConsultaBase + " ORDER BY ID DESC LIMIT $quantidade";
        command.Parameters.AddWithValue("$quantidade", quantidade);

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read()) resultado.Add(Ler(reader));

        resultado.Reverse(); // consulta veio do mais novo pro mais antigo; devolve em ordem cronológica
        return resultado;
    }

    public IReadOnlyList<EspelhoSistema> ObterPorPeriodo(DateTime inicio, DateTime fim)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var resultado = new List<EspelhoSistema>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = ConsultaBase + " WHERE DH_ESPELHO BETWEEN $inicio AND $fim ORDER BY DH_ESPELHO";
        command.Parameters.AddWithValue("$inicio", Texto(inicio));
        command.Parameters.AddWithValue("$fim", Texto(fim));

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read()) resultado.Add(Ler(reader));

        return resultado;
    }

    // ------------------------------------------------------------------ Captura

    /// <summary>
    /// Monta o <c>INSERT ... SELECT</c> da captura conforme as tabelas presentes neste banco.
    /// Nenhum valor de usuário entra na string: as partes variáveis são constantes do código e o
    /// único dado externo (<c>$agora</c>) vai parametrizado.
    /// </summary>
    private static string MontarInsertDeCaptura(SqliteConnection connection)
    {
        bool temObjetos = SqliteSchema.TableExists(connection, "TABOBJ");
        bool temModo = SqliteSchema.TableExists(connection, "TABMOD");
        bool temCameras = SqliteSchema.TableExists(connection, "TABCAM");
        bool temSessoes = SqliteSchema.TableExists(connection, "TABSES");
        bool temAcoes = SqliteSchema.TableExists(connection, "TABACA");
        bool temClassificacoes = SqliteSchema.TableExists(connection, "TABCLA");
        bool temExecucoes = SqliteSchema.TableExists(connection, "TABEXE");
        bool temHistorico = SqliteSchema.TableExists(connection, "TABHTG");

        // Instante do espelho anterior: delimita as colunas "no intervalo". Na primeira captura
        // não há anterior, e o COALESCE com o início dos tempos faz a primeira linha contabilizar
        // tudo que já existia — de propósito, para o espelho inicial descrever o banco por inteiro.
        const string Anterior = "COALESCE((SELECT MAX(DH_ESPELHO) FROM TABESP), '')";

        string modoId = temModo
            ? "(SELECT ID_MODO FROM TABMOD ORDER BY ID_MODO DESC LIMIT 1)"
            : "NULL";
        string modoAtual = temModo
            ? "(SELECT MODOATUAL FROM TABMOD ORDER BY ID_MODO DESC LIMIT 1)"
            : "NULL";

        string PorEstado(string estado) => temObjetos
            ? $"(SELECT COUNT(*) FROM TABOBJ WHERE Estado = '{estado}')"
            : "0";

        string PorQuadrante(string quadrante) => temObjetos
            ? $"(SELECT COUNT(*) FROM TABOBJ WHERE Quadrante = '{quadrante}' AND Estado <> 'Abatido')"
            : "0";

        string cameras = temCameras ? "(SELECT COUNT(*) FROM TABCAM)" : "0";
        string sessoes = temSessoes ? "(SELECT COUNT(*) FROM TABSES WHERE DataHoraFim IS NULL)" : "0";
        string acoes = temAcoes ? $"(SELECT COUNT(*) FROM TABACA WHERE DH_ACAO > {Anterior})" : "0";
        string criticas = temClassificacoes
            ? $"(SELECT COUNT(*) FROM TABCLA WHERE Grau = 'Critico' AND DataHora > {Anterior})"
            : "0";

        // A execução de treinamento ATIVA no instante da amostra — é ela que amarra o espelho às
        // tabelas de neuroevolução. NULL quando ninguém está treinando agora.
        string execucao = temExecucoes
            ? "(SELECT EXECUCAO_ID FROM TABEXE WHERE DH_FIM IS NULL ORDER BY DH_INICIO DESC LIMIT 1)"
            : "NULL";

        // Última geração JÁ GRAVADA dessa execução ativa. Depende das duas tabelas: sem TABEXE
        // não há de quem pegar as gerações, e sem TABHTG não há geração nenhuma.
        string UltimaGeracao(string coluna) => temExecucoes && temHistorico
            ? "(SELECT h." + coluna + " FROM TABHTG h " +
              "WHERE h.EXECUCAO_ID = (SELECT EXECUCAO_ID FROM TABEXE WHERE DH_FIM IS NULL ORDER BY DH_INICIO DESC LIMIT 1) " +
              "ORDER BY h.ID DESC LIMIT 1)"
            : "NULL";

        return
            "INSERT INTO TABESP (DH_ESPELHO, ID_MODO, MODO_ATUAL, ALVOS_DETECTADOS, ALVOS_ABATE_PENDENTE, " +
            "ALVOS_ABATIDOS, ALVOS_Q1, ALVOS_Q2, ALVOS_Q3, ALVOS_Q4, CAMERAS_CADASTRADAS, SESSOES_ABERTAS, " +
            "ACOES_NO_INTERVALO, CLASSIFICACOES_CRITICAS, EXECUCAO_ID, GERACAO_ATUAL, MELHOR_FITNESS, FITNESS_MEDIO) " +
            "SELECT $agora, " +
            modoId + ", " +
            modoAtual + ", " +
            PorEstado("Detectado") + ", " +
            PorEstado("AbatePendente") + ", " +
            PorEstado("Abatido") + ", " +
            PorQuadrante("Q1") + ", " +
            PorQuadrante("Q2") + ", " +
            PorQuadrante("Q3") + ", " +
            PorQuadrante("Q4") + ", " +
            cameras + ", " +
            sessoes + ", " +
            acoes + ", " +
            criticas + ", " +
            execucao + ", " +
            UltimaGeracao("GERACAO") + ", " +
            UltimaGeracao("MELHOR_FITNESS") + ", " +
            UltimaGeracao("FITNESS_MEDIO");
    }

    // ------------------------------------------------------------------ Leitura

    private const string ConsultaBase =
        "SELECT ID, DH_ESPELHO, ID_MODO, MODO_ATUAL, ALVOS_DETECTADOS, ALVOS_ABATE_PENDENTE, ALVOS_ABATIDOS, " +
        "ALVOS_Q1, ALVOS_Q2, ALVOS_Q3, ALVOS_Q4, CAMERAS_CADASTRADAS, SESSOES_ABERTAS, ACOES_NO_INTERVALO, " +
        "CLASSIFICACOES_CRITICAS, EXECUCAO_ID, GERACAO_ATUAL, MELHOR_FITNESS, FITNESS_MEDIO FROM TABESP";

    private static EspelhoSistema Ler(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        DataHora = Data(reader.GetString(1)),
        ModoId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
        ModoAtual = reader.IsDBNull(3) ? null : reader.GetString(3),
        AlvosDetectados = reader.GetInt32(4),
        AlvosAbatePendente = reader.GetInt32(5),
        AlvosAbatidos = reader.GetInt32(6),
        AlvosQ1 = reader.GetInt32(7),
        AlvosQ2 = reader.GetInt32(8),
        AlvosQ3 = reader.GetInt32(9),
        AlvosQ4 = reader.GetInt32(10),
        CamerasCadastradas = reader.GetInt32(11),
        SessoesAbertas = reader.GetInt32(12),
        AcoesNoIntervalo = reader.GetInt32(13),
        ClassificacoesCriticasNoIntervalo = reader.GetInt32(14),
        ExecucaoId = reader.IsDBNull(15) ? null : reader.GetString(15),
        GeracaoAtual = reader.IsDBNull(16) ? null : reader.GetInt32(16),
        MelhorFitness = reader.IsDBNull(17) ? null : reader.GetDouble(17),
        FitnessMedio = reader.IsDBNull(18) ? null : reader.GetDouble(18),
    };

    private static string Texto(DateTime valor) => valor.ToString("O", CultureInfo.InvariantCulture);

    private static DateTime Data(string valor) =>
        DateTime.Parse(valor, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static void EnsureTable(SqliteConnection connection) =>
        SqliteSchema.GarantirUmaVez(connection, nameof(SqliteEspelhoSistemaRepository), CriarEsquema);

    private static void CriarEsquema(SqliteConnection connection)
    {
        // As duas tabelas referenciadas pelas chaves estrangeiras de TABESP precisam existir
        // ANTES dela: as conexões do Microsoft.Data.Sqlite vêm com foreign_keys ligado, e gravar
        // numa tabela cuja referenciada não existe falha com "no such table". Ambas as chamadas
        // são deduplicadas por GarantirUmaVez, então não custam nada quando já rodaram.
        SqliteModoAtualTorreRepository.EnsureTable(connection);
        SqliteExecucaoTreinamentoRepository.EnsureTable(connection);

        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TABLE IF NOT EXISTS TABESP (" +
                "ID INTEGER PRIMARY KEY AUTOINCREMENT, " +
                "DH_ESPELHO TEXT NOT NULL, " +
                "ID_MODO INTEGER NULL REFERENCES TABMOD (ID_MODO), " +
                "MODO_ATUAL TEXT NULL, " +
                "ALVOS_DETECTADOS INTEGER NOT NULL DEFAULT 0, " +
                "ALVOS_ABATE_PENDENTE INTEGER NOT NULL DEFAULT 0, " +
                "ALVOS_ABATIDOS INTEGER NOT NULL DEFAULT 0, " +
                "ALVOS_Q1 INTEGER NOT NULL DEFAULT 0, " +
                "ALVOS_Q2 INTEGER NOT NULL DEFAULT 0, " +
                "ALVOS_Q3 INTEGER NOT NULL DEFAULT 0, " +
                "ALVOS_Q4 INTEGER NOT NULL DEFAULT 0, " +
                "CAMERAS_CADASTRADAS INTEGER NOT NULL DEFAULT 0, " +
                "SESSOES_ABERTAS INTEGER NOT NULL DEFAULT 0, " +
                "ACOES_NO_INTERVALO INTEGER NOT NULL DEFAULT 0, " +
                "CLASSIFICACOES_CRITICAS INTEGER NOT NULL DEFAULT 0, " +
                "EXECUCAO_ID TEXT NULL REFERENCES TABEXE (EXECUCAO_ID), " +
                "GERACAO_ATUAL INTEGER NULL, " +
                "MELHOR_FITNESS REAL NULL, " +
                "FITNESS_MEDIO REAL NULL)";
            command.ExecuteNonQuery();
        }

        SqliteSchema.EnsureIndex(connection, "idx_espelhos_sistema_dh", "ON TABESP (DH_ESPELHO)", unico: false);
        SqliteSchema.EnsureIndex(connection, "idx_espelhos_sistema_execucao", "ON TABESP (EXECUCAO_ID)", unico: false);
    }
}
