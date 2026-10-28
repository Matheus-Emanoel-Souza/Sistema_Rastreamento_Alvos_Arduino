using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Data.Sqlite;

namespace RadarTorres.App.Data;

/// <summary>
/// Evolução de esquema das tabelas SQLite já existentes no banco de um usuário. Os
/// <c>Sqlite*Repository</c> criam suas tabelas com <c>CREATE TABLE IF NOT EXISTS</c>, que não faz
/// nada quando a tabela já existe — ou seja, coluna nova só aparece em instalação limpa. Este
/// helper fecha essa lacuna sem introduzir um framework de migração: cada repositório declara as
/// colunas e os índices que passou a precisar, e aqui se verifica o que falta e se aplica o
/// <c>ALTER TABLE</c>/<c>CREATE INDEX</c> correspondente.
/// </summary>
/// <remarks>
/// Idempotente de propósito (roda a cada abertura de conexão, igual ao <c>CREATE TABLE IF NOT
/// EXISTS</c> que já existia): o custo é uma consulta ao <c>PRAGMA table_info</c>, e em troca não
/// há estado de versão de banco para manter em sincronia com o código. SQLite só aceita adicionar
/// coluna ao fim da tabela e com valor padrão constante — é exatamente o caso de todas as colunas
/// acrescentadas aqui, e é o motivo de nenhuma delas ser <c>NOT NULL</c> sem <c>DEFAULT</c>.
/// </remarks>
public static class SqliteSchema
{
    /// <summary>
    /// Chaves "(connection string)||(nome do repositório)" cujo esquema já foi criado/migrado
    /// nesta execução do processo.
    /// </summary>
    private static readonly ConcurrentDictionary<string, byte> EsquemasAplicados = new();

    private static int _execucoesDeEsquema;

    /// <summary>Quantas vezes um bloco de criação/migração de esquema foi REALMENTE executado (testes).</summary>
    internal static int ExecucoesDeEsquema => Volatile.Read(ref _execucoesDeEsquema);

    /// <summary>Esquece o que já foi aplicado — só para testes que trocam o banco no meio do caminho.</summary>
    internal static void EsquecerEsquemasAplicados() => EsquemasAplicados.Clear();

    /// <summary>
    /// Executa <paramref name="criarEsquema"/> apenas na PRIMEIRA vez que este banco
    /// (identificado pela connection string da conexão aberta) e esta <paramref name="chave"/>
    /// aparecem no processo.
    /// </summary>
    /// <remarks>
    /// Motivo: cada operação de repositório abria a conexão e reexecutava o bloco inteiro de
    /// <c>CREATE TABLE IF NOT EXISTS</c> + <c>PRAGMA table_info</c> (um por coluna migrada) +
    /// <c>CREATE INDEX IF NOT EXISTS</c>. Em caminho frio isso é irrelevante; no caminho quente
    /// da detecção por câmera (uma gravação por quadro, por câmera) e no laço de treinamento
    /// (uma gravação por geração) são mais de dez instruções desperdiçadas por chamada. O
    /// esquema não muda enquanto o aplicativo roda, então aplicar uma vez por banco basta.
    /// Se <paramref name="criarEsquema"/> lançar, nada é marcado e a próxima chamada tenta de novo.
    /// </remarks>
    public static void GarantirUmaVez(SqliteConnection connection, string chave, Action<SqliteConnection> criarEsquema)
    {
        string marca = connection.ConnectionString + "||" + chave;
        if (EsquemasAplicados.ContainsKey(marca)) return;

        criarEsquema(connection);

        Interlocked.Increment(ref _execucoesDeEsquema);
        EsquemasAplicados[marca] = 0;
    }

    /// <summary>
    /// Garante que <paramref name="coluna"/> exista em <paramref name="tabela"/>, criando-a com
    /// <paramref name="definicao"/> (ex.: <c>"TEXT NULL"</c>, <c>"INTEGER NOT NULL DEFAULT 0"</c>)
    /// quando faltar. Não altera coluna que já existe.
    /// </summary>
    public static void EnsureColumn(SqliteConnection connection, string tabela, string coluna, string definicao)
    {
        if (ColumnExists(connection, tabela, coluna)) return;

        using SqliteCommand command = connection.CreateCommand();
        // Nomes de tabela/coluna não podem ser parametrizados em DDL; são constantes do próprio
        // código (nunca entrada do usuário), então não há superfície de injeção aqui.
        command.CommandText = $"ALTER TABLE {tabela} ADD COLUMN {coluna} {definicao}";
        command.ExecuteNonQuery();
    }

    /// <summary>Cria um índice (único ou não) se ele ainda não existir.</summary>
    /// <param name="corpo">
    /// O índice a partir do nome: <c>"ON Tabela(Col1, Col2) WHERE Col1 IS NOT NULL"</c>.
    /// </param>
    public static void EnsureIndex(SqliteConnection connection, string nome, string corpo, bool unico)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"CREATE {(unico ? "UNIQUE " : "")}INDEX IF NOT EXISTS {nome} {corpo}";
        command.ExecuteNonQuery();
    }

    public static bool ColumnExists(SqliteConnection connection, string tabela, string coluna)
    {
        foreach (string existente in GetColumns(connection, tabela))
        {
            if (string.Equals(existente, coluna, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static IReadOnlyList<string> GetColumns(SqliteConnection connection, string tabela)
    {
        var colunas = new List<string>();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tabela})";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            colunas.Add(reader.GetString(1));
        }

        return colunas;
    }
}
