using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>acoes_realizadas</c> — nomes de
/// tabela/colunas espelham exatamente <c>Software/Banco_de_Dados/schema.sql</c>)
/// de <see cref="IAcaoRealizadaRepository"/> — único armazenamento; sem fallback para CSV (todo
/// registro do sistema grava direto no banco, com fila de reenvio via
/// <see cref="PendingWriteQueue"/> se o banco estiver momentaneamente inacessível).
/// </summary>
public sealed class SqliteAcaoRealizadaRepository : IAcaoRealizadaRepository
{
    public IReadOnlyList<AcaoRealizada> GetAll()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var result = new List<AcaoRealizada>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT ID_ACAO, ID_TORRE, ID_OBJETO, DH_ACAO, OBSERVACAO, CameraDispositivoId, SessaoId " +
            "FROM acoes_realizadas ORDER BY ID_ACAO";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new AcaoRealizada
            {
                Id = reader.GetInt32(0),
                TorreId = reader.GetInt32(1),
                ObjetoDetectadoId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                DataHora = DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Observacao = reader.IsDBNull(4) ? null : reader.GetString(4),
                CameraDispositivoId = reader.IsDBNull(5) ? null : reader.GetString(5),
                SessaoId = reader.IsDBNull(6) ? null : reader.GetString(6),
            });
        }
        return result;
    }

    public AcaoRealizada Add(AcaoRealizada acao)
    {
        try
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
            EnsureTable(connection);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO acoes_realizadas (ID_TORRE, ID_OBJETO, DH_ACAO, OBSERVACAO, CameraDispositivoId, SessaoId) " +
                "VALUES ($idTorre, $idObjeto, $dhAcao, $observacao, $cameraDispositivoId, $sessaoId); " +
                "SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$idTorre", acao.TorreId);
            command.Parameters.AddWithValue("$idObjeto", (object?)acao.ObjetoDetectadoId ?? DBNull.Value);
            command.Parameters.AddWithValue("$dhAcao", acao.DataHora.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$observacao", (object?)acao.Observacao ?? DBNull.Value);
            command.Parameters.AddWithValue("$cameraDispositivoId", (object?)acao.CameraDispositivoId ?? DBNull.Value);
            command.Parameters.AddWithValue("$sessaoId", (object?)acao.SessaoId ?? DBNull.Value);

            acao.Id = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            return acao;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            acao.Id = PendingWriteQueue.NextOfflineId();
            PendingWriteQueue.Enqueue(
                "INSERT INTO acoes_realizadas (ID_TORRE, ID_OBJETO, DH_ACAO, OBSERVACAO, CameraDispositivoId, SessaoId) VALUES (" +
                $"{SqlLiteral.Number(acao.TorreId)}, {SqlLiteral.Number(acao.ObjetoDetectadoId)}, {SqlLiteral.DateTime(acao.DataHora)}, " +
                $"{SqlLiteral.Text(acao.Observacao)}, {SqlLiteral.Text(acao.CameraDispositivoId)}, {SqlLiteral.Text(acao.SessaoId)})");
            return acao;
        }
    }

    private static void EnsureTable(SqliteConnection connection) =>
        SqliteSchema.GarantirUmaVez(connection, nameof(SqliteAcaoRealizadaRepository), CriarEsquema);

    /// <summary>Criação/migração do esquema desta tabela — roda uma única vez por banco e por execução.</summary>
    private static void CriarEsquema(SqliteConnection connection)
    {
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TABLE IF NOT EXISTS acoes_realizadas (" +
                "ID_ACAO INTEGER PRIMARY KEY AUTOINCREMENT, " +
                "ID_TORRE INTEGER NOT NULL, " +
                "ID_OBJETO INTEGER NULL, " +
                "DH_ACAO TEXT NOT NULL, " +
                "OBSERVACAO TEXT NULL)";
            command.ExecuteNonQuery();
        }

        // Câmera que originou a ocorrência por trás da ação (ver ObjetosDetectados.CameraDispositivoId,
        // mesma convenção de coluna documentária sem FK imposta pelo SQLite — PRAGMA foreign_keys
        // nunca é ligado nesta base, ver SqliteConnectionFactory).
        SqliteSchema.EnsureColumn(connection, "acoes_realizadas", "CameraDispositivoId", "TEXT NULL");

        // Código de sessão (sessoes_usuario.ID_SESSAO) de quem desencadeou o disparo — mesma
        // convenção documentária sem FK imposta pelo SQLite (ver nota acima).
        SqliteSchema.EnsureColumn(connection, "acoes_realizadas", "SessaoId", "TEXT NULL");

        // "AcoesRealizadas" (PascalCase) é tabela órfã de uma versão anterior do schema — nenhum
        // código atual cria, lê ou escreve nela; "acoes_realizadas" (snake_case) é a única em uso
        // real (ver Software/Banco_de_Dados/schema.sql). Removida aqui para não deixar duas
        // tabelas de ações realizadas convivendo no mesmo banco.
        using (SqliteCommand drop = connection.CreateCommand())
        {
            drop.CommandText = "DROP TABLE IF EXISTS AcoesRealizadas";
            drop.ExecuteNonQuery();
        }
    }
}
