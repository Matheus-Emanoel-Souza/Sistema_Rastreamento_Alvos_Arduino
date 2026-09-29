using System;
using System.IO;
using System.Threading;
using Microsoft.Data.Sqlite;

namespace RadarTorres.App.Data;

/// <summary>
/// Fila de escrita offline do banco (radartorres.db). Quando um <c>Sqlite*Repository</c> não
/// consegue gravar (banco inacessível — arquivo bloqueado, removido, sem permissão etc.), ele
/// registra aqui o INSERT/UPDATE equivalente em vez de perder o dado ou travar a operação para
/// o usuário: o cadastro "continua acontecendo" do ponto de vista do app, só que fica pendente
/// de ser realmente gravado no banco.
///
/// Guardado como texto SQL puro (não criptografado — o próprio radartorres.db também não é
/// criptografado hoje, então cifrar só este arquivo seria proteção inconsistente) em
/// <c>pending-writes.sql</c>, na mesma pasta do banco. Cada escrita perdida é acrescentada
/// (append) imediatamente ao arquivo — nunca só em memória — para sobreviver a um fechamento
/// forçado/crash do app antes do próximo <see cref="TryReplayPendingWrites"/>.
/// </summary>
public static class PendingWriteQueue
{
    private static readonly object FileLock = new();
    private static int _offlineIdCounter;

    public static string FilePath { get; } = Path.Combine(AppDataPaths.DataFolder, "pending-writes.sql");

    /// <summary>
    /// Id temporário e negativo para uso imediato (ex.: exibir na tela) enquanto o cadastro real
    /// não é reaplicado no banco. Não corresponde ao Id definitivo que a linha vai receber quando
    /// o INSERT for realmente executado (autoincrement do SQLite) — limitação aceita deste modo
    /// offline, não há reconciliação retroativa dos objetos já em uso na sessão.
    /// </summary>
    public static int NextOfflineId() => Interlocked.Decrement(ref _offlineIdCounter);

    /// <summary>Registra uma instrução SQL completa (INSERT ou UPDATE, valores já embutidos como
    /// literais) como pendente — grava no arquivo na hora, não enfileira só em memória.</summary>
    public static void Enqueue(string sqlStatement)
    {
        lock (FileLock)
        {
            Directory.CreateDirectory(AppDataPaths.DataFolder);
            string linha = sqlStatement.TrimEnd().TrimEnd(';') + ";" + Environment.NewLine;
            File.AppendAllText(FilePath, linha);
        }
    }

    /// <summary>
    /// Chamado uma vez no início do app (ver App.xaml.cs), antes de qualquer outra leitura/
    /// escrita no banco. Sem arquivo pendente, não faz nada. Com arquivo pendente: se o banco
    /// estiver acessível, executa todo o conteúdo em uma única transação (o parser do próprio
    /// SQLite separa as instruções pelos ";", respeitando corretamente ";" dentro de valores de
    /// texto) e só apaga o arquivo se tudo for aplicado com sucesso. Se o banco continuar
    /// inacessível, ou alguma instrução falhar, o arquivo é mantido intacto para a próxima
    /// tentativa — novas escritas perdidas nessa mesma sessão são acrescentadas a ele.
    /// </summary>
    public static void TryReplayPendingWrites()
    {
        lock (FileLock)
        {
            if (!File.Exists(FilePath)) return;

            string conteudo = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(conteudo))
            {
                File.Delete(FilePath);
                return;
            }

            SqliteConnection connection;
            try
            {
                connection = SqliteConnectionFactory.OpenConnection();
            }
            catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
            {
                return; // banco ainda inacessível — arquivo fica como está, tenta de novo no próximo run.
            }

            using (connection)
            {
                using SqliteTransaction transaction = connection.BeginTransaction();
                try
                {
                    using SqliteCommand command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = conteudo;
                    command.ExecuteNonQuery();
                    transaction.Commit();
                }
                catch (SqliteException)
                {
                    transaction.Rollback();
                    return; // alguma instrução falhou (ex.: schema divergente) — mantém o arquivo, não descarta os dados.
                }
            }

            File.Delete(FilePath);
        }
    }
}
