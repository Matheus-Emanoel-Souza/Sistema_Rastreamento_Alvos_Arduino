using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabelas <c>ObjetosDetectados</c> e
/// <c>OcorrenciaEstados</c>) de <see cref="IObjetoDetectadoRepository"/> — único armazenamento;
/// sem fallback para CSV (o CSV que a tela "Objetos Detectados" ainda gera é exportação manual,
/// ver <see cref="CsvObjetoDetectadoColumns"/>).
/// </summary>
/// <remarks>
/// As colunas de vínculo e ciclo de vida (perfil de alvo, câmera, rastreamento, estado) foram
/// acrescentadas depois que a tabela já existia na máquina dos usuários, então
/// <see cref="EnsureTable"/> cria o que falta via <see cref="SqliteSchema"/> em vez de depender
/// do <c>CREATE TABLE IF NOT EXISTS</c> — que não faz nada numa tabela existente. Nenhum dado é
/// descartado: linhas antigas ficam com as colunas novas nulas e estado <c>Detectado</c>.
/// </remarks>
public sealed class SqliteObjetoDetectadoRepository : IObjetoDetectadoRepository
{
    private const string ColunasSelecao =
        "Id, Tipo, X, Y, Z, Quadrante, DataHora, Dispositivo, NivelConfianca, Observacao, ReferenciaImagem, " +
        "PerfilAlvoId, CameraDispositivoId, CameraNome, RastreamentoId, Estado, DataHoraUltimaDeteccao";

    public IReadOnlyList<ObjetoDetectado> GetAll()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var result = new List<ObjetoDetectado>();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {ColunasSelecao} FROM ObjetosDetectados ORDER BY Id";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(Ler(reader));
        }
        return result;
    }

    public ObjetoDetectado? GetById(int id)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT {ColunasSelecao} FROM ObjetosDetectados WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);

        using SqliteDataReader reader = command.ExecuteReader();
        return reader.Read() ? Ler(reader) : null;
    }

    public ObjetoDetectado Add(ObjetoDetectado objeto)
    {
        try
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
            EnsureTable(connection);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = Insert + "; SELECT last_insert_rowid();";
            Vincular(command, objeto);

            objeto.Id = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            RegistrarTransicao(connection, objeto.Id, objeto.Estado, usuario: null, observacao: "Ocorrência registrada.");
            return objeto;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            objeto.Id = PendingWriteQueue.NextOfflineId();
            PendingWriteQueue.Enqueue(InsertLiteral(objeto));
            return objeto;
        }
    }

    /// <remarks>
    /// <c>ON CONFLICT DO NOTHING</c> contra o índice único (RastreamentoId, Quadrante) e, em
    /// seguida, leitura da linha vencedora: a corrida entre duas câmeras termina com uma única
    /// ocorrência, e as duas threads recebem a MESMA (quem perdeu passa a trabalhar sobre a linha
    /// de quem ganhou, em vez de criar uma duplicata ou falhar).
    /// </remarks>
    public ObjetoDetectado AdicionarSeNaoExistir(ObjetoDetectado objeto)
    {
        if (string.IsNullOrEmpty(objeto.RastreamentoId)) return Add(objeto);

        try
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
            EnsureTable(connection);

            bool inseridoAgora;
            using (SqliteCommand insert = connection.CreateCommand())
            {
                // O "WHERE RastreamentoId IS NOT NULL" repete o predicado do índice: SQLite só
                // reconhece um índice PARCIAL como alvo de ON CONFLICT quando o predicado é
                // informado aqui também — sem ele a instrução falha por "no unique index
                // matching the conflict target".
                insert.CommandText = Insert +
                    " ON CONFLICT(RastreamentoId, Quadrante) WHERE RastreamentoId IS NOT NULL DO NOTHING";
                Vincular(insert, objeto);

                // 1 = inseriu; 0 = outra thread/janela já havia criado esta ocorrência.
                inseridoAgora = insert.ExecuteNonQuery() > 0;
            }

            using SqliteCommand select = connection.CreateCommand();
            select.CommandText =
                $"SELECT {ColunasSelecao} FROM ObjetosDetectados " +
                "WHERE RastreamentoId = $rastreamentoId AND Quadrante = $quadrante";
            select.Parameters.AddWithValue("$rastreamentoId", objeto.RastreamentoId);
            select.Parameters.AddWithValue("$quadrante", objeto.Quadrante);

            using SqliteDataReader reader = select.ExecuteReader();
            if (!reader.Read()) return objeto;

            ObjetoDetectado gravado = Ler(reader);

            if (inseridoAgora)
            {
                reader.Close();
                RegistrarTransicao(connection, gravado.Id, gravado.Estado, usuario: null, observacao: "Ocorrência registrada.");
            }

            return gravado;
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            objeto.Id = PendingWriteQueue.NextOfflineId();
            PendingWriteQueue.Enqueue(InsertLiteral(objeto));
            return objeto;
        }
    }

    public void RegistrarDeteccaoContinuada(int id, DateTime quando)
    {
        try
        {
            using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
            EnsureTable(connection);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "UPDATE ObjetosDetectados SET DataHoraUltimaDeteccao = $quando WHERE Id = $id";
            command.Parameters.AddWithValue("$quando", quando.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException)
        {
            // Perder o "visto por último" de um acompanhamento em curso não corrompe nada: a
            // ocorrência já está gravada e o rastreamento em memória segue mandando. Enfileirar
            // esta atualização seria pior — ela é reescrita a cada rodada.
        }
    }

    public bool AtualizarEstado(int id, EstadoOcorrencia estado, string? usuario, string? observacao)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE ObjetosDetectados SET Estado = $estado WHERE Id = $id AND Estado <> $estado";
            command.Parameters.AddWithValue("$estado", estado.ToString());
            command.Parameters.AddWithValue("$id", id);

            if (command.ExecuteNonQuery() == 0) return false;
        }

        RegistrarTransicao(connection, id, estado, usuario, observacao);
        return true;
    }

    public IReadOnlyList<OcorrenciaEstadoTransicao> ObterHistoricoEstados(int objetoDetectadoId)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var result = new List<OcorrenciaEstadoTransicao>();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT Id, ObjetoDetectadoId, Estado, DataHora, Usuario, Observacao FROM OcorrenciaEstados " +
            "WHERE ObjetoDetectadoId = $id ORDER BY Id";
        command.Parameters.AddWithValue("$id", objetoDetectadoId);

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new OcorrenciaEstadoTransicao
            {
                Id = reader.GetInt32(0),
                ObjetoDetectadoId = reader.GetInt32(1),
                Estado = Enum.TryParse(reader.GetString(2), out EstadoOcorrencia e) ? e : EstadoOcorrencia.Detectado,
                DataHora = DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Usuario = reader.IsDBNull(4) ? null : reader.GetString(4),
                Observacao = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }

        return result;
    }

    // ---------------------------------------------------------------- Infraestrutura

    /// <summary>Corpo do INSERT, SEM ponto e vírgula final — quem chama acrescenta o que precisa depois (<c>ON CONFLICT…</c> ou <c>; SELECT last_insert_rowid()</c>).</summary>
    private const string Insert =
        "INSERT INTO ObjetosDetectados (Tipo, X, Y, Z, Quadrante, DataHora, Dispositivo, NivelConfianca, Observacao, " +
        "ReferenciaImagem, PerfilAlvoId, CameraDispositivoId, CameraNome, RastreamentoId, Estado, DataHoraUltimaDeteccao) " +
        "VALUES ($tipo, $x, $y, $z, $quadrante, $dataHora, $dispositivo, $nivelConfianca, $observacao, " +
        "$referenciaImagem, $perfilAlvoId, $cameraDispositivoId, $cameraNome, $rastreamentoId, $estado, NULL)";

    private static void Vincular(SqliteCommand command, ObjetoDetectado objeto)
    {
        command.Parameters.AddWithValue("$tipo", objeto.Tipo);
        command.Parameters.AddWithValue("$x", objeto.X);
        command.Parameters.AddWithValue("$y", objeto.Y);
        command.Parameters.AddWithValue("$z", (object?)objeto.Z ?? DBNull.Value);
        command.Parameters.AddWithValue("$quadrante", objeto.Quadrante);
        command.Parameters.AddWithValue("$dataHora", objeto.DataHora.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$dispositivo", objeto.Dispositivo);
        command.Parameters.AddWithValue("$nivelConfianca", (object?)objeto.NivelConfianca ?? DBNull.Value);
        command.Parameters.AddWithValue("$observacao", (object?)objeto.Observacao ?? DBNull.Value);
        command.Parameters.AddWithValue("$referenciaImagem", (object?)objeto.ReferenciaImagem ?? DBNull.Value);
        command.Parameters.AddWithValue("$perfilAlvoId", (object?)objeto.PerfilAlvoId ?? DBNull.Value);
        command.Parameters.AddWithValue("$cameraDispositivoId", (object?)objeto.CameraDispositivoId ?? DBNull.Value);
        command.Parameters.AddWithValue("$cameraNome", (object?)objeto.CameraNome ?? DBNull.Value);
        command.Parameters.AddWithValue("$rastreamentoId", (object?)objeto.RastreamentoId ?? DBNull.Value);
        command.Parameters.AddWithValue("$estado", objeto.Estado.ToString());
    }

    private static string InsertLiteral(ObjetoDetectado o) =>
        "INSERT INTO ObjetosDetectados (Tipo, X, Y, Z, Quadrante, DataHora, Dispositivo, NivelConfianca, Observacao, " +
        "ReferenciaImagem, PerfilAlvoId, CameraDispositivoId, CameraNome, RastreamentoId, Estado) VALUES (" +
        $"{SqlLiteral.Text(o.Tipo)}, {SqlLiteral.Number(o.X)}, {SqlLiteral.Number(o.Y)}, {SqlLiteral.Number(o.Z)}, " +
        $"{SqlLiteral.Text(o.Quadrante)}, {SqlLiteral.DateTime(o.DataHora)}, {SqlLiteral.Text(o.Dispositivo)}, " +
        $"{SqlLiteral.Number(o.NivelConfianca)}, {SqlLiteral.Text(o.Observacao)}, {SqlLiteral.Text(o.ReferenciaImagem)}, " +
        $"{SqlLiteral.Number(o.PerfilAlvoId)}, {SqlLiteral.Text(o.CameraDispositivoId)}, {SqlLiteral.Text(o.CameraNome)}, " +
        $"{SqlLiteral.Text(o.RastreamentoId)}, {SqlLiteral.Text(o.Estado.ToString())})";

    private static ObjetoDetectado Ler(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Tipo = reader.GetString(1),
        X = reader.GetDouble(2),
        Y = reader.GetDouble(3),
        Z = reader.IsDBNull(4) ? null : reader.GetDouble(4),
        Quadrante = reader.GetString(5),
        DataHora = DateTime.Parse(reader.GetString(6), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        Dispositivo = reader.GetString(7),
        NivelConfianca = reader.IsDBNull(8) ? null : reader.GetDouble(8),
        Observacao = reader.IsDBNull(9) ? null : reader.GetString(9),
        ReferenciaImagem = reader.IsDBNull(10) ? null : reader.GetString(10),
        PerfilAlvoId = reader.IsDBNull(11) ? null : reader.GetInt32(11),
        CameraDispositivoId = reader.IsDBNull(12) ? null : reader.GetString(12),
        CameraNome = reader.IsDBNull(13) ? null : reader.GetString(13),
        RastreamentoId = reader.IsDBNull(14) ? null : reader.GetString(14),
        Estado = reader.IsDBNull(15) || !Enum.TryParse(reader.GetString(15), out EstadoOcorrencia estado)
            ? EstadoOcorrencia.Detectado
            : estado,
        DataHoraUltimaDeteccao = reader.IsDBNull(16)
            ? null
            : DateTime.Parse(reader.GetString(16), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
    };

    private static void RegistrarTransicao(SqliteConnection connection, int objetoDetectadoId, EstadoOcorrencia estado, string? usuario, string? observacao)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO OcorrenciaEstados (ObjetoDetectadoId, Estado, DataHora, Usuario, Observacao) " +
            "VALUES ($objetoId, $estado, $dataHora, $usuario, $observacao)";
        command.Parameters.AddWithValue("$objetoId", objetoDetectadoId);
        command.Parameters.AddWithValue("$estado", estado.ToString());
        command.Parameters.AddWithValue("$dataHora", DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$usuario", (object?)usuario ?? DBNull.Value);
        command.Parameters.AddWithValue("$observacao", (object?)observacao ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    private static void EnsureTable(SqliteConnection connection)
    {
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TABLE IF NOT EXISTS ObjetosDetectados (" +
                "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                "Tipo TEXT NOT NULL, " +
                "X REAL NOT NULL, " +
                "Y REAL NOT NULL, " +
                "Z REAL NULL, " +
                "Quadrante TEXT NOT NULL, " +
                "DataHora TEXT NOT NULL, " +
                "Dispositivo TEXT NOT NULL, " +
                "NivelConfianca REAL NULL, " +
                "Observacao TEXT NULL, " +
                "ReferenciaImagem TEXT NULL)";
            command.ExecuteNonQuery();
        }

        // Colunas acrescentadas depois da primeira versão da tabela (ver observações da classe).
        SqliteSchema.EnsureColumn(connection, "ObjetosDetectados", "PerfilAlvoId", "INTEGER NULL");
        SqliteSchema.EnsureColumn(connection, "ObjetosDetectados", "CameraDispositivoId", "TEXT NULL");
        SqliteSchema.EnsureColumn(connection, "ObjetosDetectados", "CameraNome", "TEXT NULL");
        SqliteSchema.EnsureColumn(connection, "ObjetosDetectados", "RastreamentoId", "TEXT NULL");
        SqliteSchema.EnsureColumn(connection, "ObjetosDetectados", "Estado", "TEXT NOT NULL DEFAULT 'Detectado'");
        SqliteSchema.EnsureColumn(connection, "ObjetosDetectados", "DataHoraUltimaDeteccao", "TEXT NULL");

        // Unicidade por (rastreamento, quadrante): é a restrição que torna impossível duplicar a
        // ocorrência do mesmo acompanhamento num mesmo quadrante, inclusive sob concorrência de
        // várias câmeras. Parcial (WHERE NOT NULL) para não afetar os registros do radar, que
        // não têm rastreamento visual e podem repetir quadrante livremente.
        SqliteSchema.EnsureIndex(
            connection,
            "IX_ObjetosDetectados_Rastreamento",
            "ON ObjetosDetectados(RastreamentoId, Quadrante) WHERE RastreamentoId IS NOT NULL",
            unico: true);

        SqliteSchema.EnsureIndex(connection, "IX_ObjetosDetectados_PerfilAlvo", "ON ObjetosDetectados(PerfilAlvoId)", unico: false);

        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TABLE IF NOT EXISTS OcorrenciaEstados (" +
                "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                "ObjetoDetectadoId INTEGER NOT NULL REFERENCES ObjetosDetectados(Id), " +
                "Estado TEXT NOT NULL, " +
                "DataHora TEXT NOT NULL, " +
                "Usuario TEXT NULL, " +
                "Observacao TEXT NULL)";
            command.ExecuteNonQuery();
        }

        SqliteSchema.EnsureIndex(connection, "IX_OcorrenciaEstados_Objeto", "ON OcorrenciaEstados(ObjetoDetectadoId)", unico: false);
    }
}
