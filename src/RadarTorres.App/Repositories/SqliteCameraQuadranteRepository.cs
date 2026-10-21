using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Implementação em SQLite (<c>radartorres.db</c>, tabela <c>Cameras</c>) de
/// <see cref="ICameraQuadranteRepository"/>.
/// </summary>
/// <remarks>
/// <para>
/// Os dois índices únicos (<c>DispositivoId</c> e <c>Quadrante</c>) são a garantia de último
/// nível da regra "uma câmera, um quadrante; um quadrante, uma câmera": o serviço já valida
/// antes, mas duas janelas da aplicação (sessões independentes, ver <c>App.OpenNewSession</c>)
/// podem salvar ao mesmo tempo, e aí só o banco consegue decidir — a segunda gravação viola o
/// índice e é recusada, em vez de as duas aparentarem sucesso.
/// </para>
/// <para>
/// O quadrante de outra câmera NUNCA é liberado aqui: tomar o quadrante de quem já o ocupava
/// deixaria uma câmera sem responsabilidade sem que ninguém pedisse. A atribuição em quadrante
/// ocupado é recusada com mensagem (ver <see cref="Services.CameraQuadranteService.Atribuir"/>),
/// e cabe ao operador liberar a outra câmera primeiro.
/// </para>
/// </remarks>
public sealed class SqliteCameraQuadranteRepository : ICameraQuadranteRepository
{
    public IReadOnlyList<CameraQuadrante> GetAll()
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        var result = new List<CameraQuadrante>();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT Id, DispositivoId, Nome, Quadrante, AtualizadoEm FROM Cameras ORDER BY Quadrante";

        using SqliteDataReader reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new CameraQuadrante
            {
                Id = reader.GetInt32(0),
                DispositivoId = reader.GetString(1),
                Nome = reader.GetString(2),
                Quadrante = Enum.TryParse(reader.GetString(3), out Quadrant q) ? q : Quadrant.None,
                AtualizadoEm = DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            });
        }

        return result;
    }

    /// <summary>
    /// Grava a associação. Lança <see cref="SqliteException"/> quando o quadrante já pertence a
    /// outra câmera (violação do índice único) — quem chama traduz em mensagem para o usuário.
    /// </summary>
    public CameraQuadrante Salvar(CameraQuadrante camera)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteTransaction transaction = connection.BeginTransaction();

        using (SqliteCommand command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText =
                "INSERT INTO Cameras (DispositivoId, Nome, Quadrante, AtualizadoEm) " +
                "VALUES ($dispositivo, $nome, $quadrante, $atualizadoEm) " +
                "ON CONFLICT(DispositivoId) DO UPDATE SET Nome = excluded.Nome, " +
                "Quadrante = excluded.Quadrante, AtualizadoEm = excluded.AtualizadoEm; " +
                "SELECT Id FROM Cameras WHERE DispositivoId = $dispositivo";
            command.Parameters.AddWithValue("$dispositivo", camera.DispositivoId);
            command.Parameters.AddWithValue("$nome", camera.Nome);
            command.Parameters.AddWithValue("$quadrante", camera.Quadrante.ToString());
            command.Parameters.AddWithValue("$atualizadoEm", camera.AtualizadoEm.ToString("O", CultureInfo.InvariantCulture));

            camera.Id = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        transaction.Commit();
        return camera;
    }

    public void Remover(string dispositivoId)
    {
        using SqliteConnection connection = SqliteConnectionFactory.OpenConnection();
        EnsureTable(connection);

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Cameras WHERE DispositivoId = $dispositivo";
        command.Parameters.AddWithValue("$dispositivo", dispositivoId);
        command.ExecuteNonQuery();
    }

    private static void EnsureTable(SqliteConnection connection)
    {
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText =
                "CREATE TABLE IF NOT EXISTS Cameras (" +
                "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                "DispositivoId TEXT NOT NULL, " +
                "Nome TEXT NOT NULL, " +
                "Quadrante TEXT NOT NULL, " +
                "AtualizadoEm TEXT NOT NULL)";
            command.ExecuteNonQuery();
        }

        SqliteSchema.EnsureIndex(connection, "IX_Cameras_Dispositivo", "ON Cameras(DispositivoId)", unico: true);
        SqliteSchema.EnsureIndex(connection, "IX_Cameras_Quadrante", "ON Cameras(Quadrante)", unico: true);
    }
}
