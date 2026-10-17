using System.IO;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Esquema e integridade no banco de verdade (critérios 9, 10 e 11 na camada de dados): migração
/// das colunas novas numa tabela antiga, unicidade por (rastreamento, quadrante), histórico de
/// estados e a regra "uma câmera, um quadrante".
/// </summary>
/// <remarks>
/// Usa um arquivo SQLite temporário via <c>SqliteConnectionFactory.UsarBancoDeTeste</c> — sem
/// isso, os testes escreveriam no banco real do usuário em <c>%AppData%</c>.
/// </remarks>
public sealed class PersistenciaSqliteTests : IDisposable
{
    private readonly string _pasta;

    public PersistenciaSqliteTests()
    {
        _pasta = FotosDeTeste.CriarPasta();
        SqliteConnectionFactory.UsarBancoDeTeste(Path.Combine(_pasta, "teste.db"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        FotosDeTeste.Limpar(_pasta);
    }

    private static ObjetoDetectado Ocorrencia(string rastreamento, string quadrante, int? perfilId = 1) => new()
    {
        Tipo = "Bola vermelha",
        X = 1.5,
        Y = 2.5,
        Quadrante = quadrante,
        DataHora = DateTime.Now,
        Dispositivo = "Câmera",
        NivelConfianca = 0.8,
        PerfilAlvoId = perfilId,
        CameraDispositivoId = $"cam-{quadrante}",
        CameraNome = $"Webcam {quadrante}",
        RastreamentoId = rastreamento,
        Estado = EstadoOcorrencia.Detectado
    };

    [Fact]
    public void Ocorrencia_persiste_e_le_de_volta_todos_os_vinculos()
    {
        var repositorio = new SqliteObjetoDetectadoRepository();

        ObjetoDetectado gravada = repositorio.AdicionarSeNaoExistir(Ocorrencia("rastreio-1", "Q1"));
        ObjetoDetectado lida = repositorio.GetById(gravada.Id)!;

        Assert.Equal("Q1", lida.Quadrante);
        Assert.Equal("cam-Q1", lida.CameraDispositivoId);
        Assert.Equal("Webcam Q1", lida.CameraNome);
        Assert.Equal(1, lida.PerfilAlvoId);
        Assert.Equal("rastreio-1", lida.RastreamentoId);
        Assert.Equal(EstadoOcorrencia.Detectado, lida.Estado);
        Assert.Null(lida.DataHoraUltimaDeteccao);
    }

    [Fact]
    public void Mesmo_rastreamento_e_quadrante_nao_duplica_ocorrencia()
    {
        var repositorio = new SqliteObjetoDetectadoRepository();

        ObjetoDetectado primeira = repositorio.AdicionarSeNaoExistir(Ocorrencia("rastreio-1", "Q1"));
        ObjetoDetectado segunda = repositorio.AdicionarSeNaoExistir(Ocorrencia("rastreio-1", "Q1"));

        Assert.Equal(primeira.Id, segunda.Id);
        Assert.Single(repositorio.GetAll());
    }

    [Fact]
    public void Mesmo_alvo_em_quadrantes_diferentes_gera_ocorrencias_separadas()
    {
        var repositorio = new SqliteObjetoDetectadoRepository();

        repositorio.AdicionarSeNaoExistir(Ocorrencia("rastreio-q1", "Q1"));
        repositorio.AdicionarSeNaoExistir(Ocorrencia("rastreio-q2", "Q2"));
        repositorio.AdicionarSeNaoExistir(Ocorrencia("rastreio-q3", "Q3"));
        repositorio.AdicionarSeNaoExistir(Ocorrencia("rastreio-q4", "Q4"));

        List<ObjetoDetectado> todas = repositorio.GetAll().ToList();

        Assert.Equal(4, todas.Count);
        Assert.Single(todas.Select(o => o.PerfilAlvoId).Distinct());
    }

    [Fact]
    public void Insercoes_concorrentes_do_mesmo_rastreamento_resultam_numa_ocorrencia()
    {
        var repositorio = new SqliteObjetoDetectadoRepository();

        var ids = new System.Collections.Concurrent.ConcurrentBag<int>();
        Parallel.For(0, 8, _ => ids.Add(repositorio.AdicionarSeNaoExistir(Ocorrencia("rastreio-concorrente", "Q1")).Id));

        Assert.Single(repositorio.GetAll());
        Assert.Single(ids.Distinct());
    }

    [Fact]
    public void Deteccao_continuada_atualiza_apenas_o_visto_por_ultimo()
    {
        var repositorio = new SqliteObjetoDetectadoRepository();
        ObjetoDetectado gravada = repositorio.AdicionarSeNaoExistir(Ocorrencia("rastreio-1", "Q1"));

        var quando = new DateTime(2026, 10, 16, 10, 30, 0);
        repositorio.RegistrarDeteccaoContinuada(gravada.Id, quando);

        ObjetoDetectado lida = repositorio.GetById(gravada.Id)!;

        Assert.Equal(quando, lida.DataHoraUltimaDeteccao);
        Assert.Single(repositorio.GetAll());
    }

    [Fact]
    public void Transicoes_de_estado_ficam_registradas_em_ordem()
    {
        var repositorio = new SqliteObjetoDetectadoRepository();
        ObjetoDetectado gravada = repositorio.AdicionarSeNaoExistir(Ocorrencia("rastreio-1", "Q1"));

        Assert.True(repositorio.AtualizarEstado(gravada.Id, EstadoOcorrencia.AbatePendente, "operador", "acionamento"));
        Assert.True(repositorio.AtualizarEstado(gravada.Id, EstadoOcorrencia.Abatido, "supervisor", "confirmado"));

        // Repetir o mesmo estado não gera transição nova.
        Assert.False(repositorio.AtualizarEstado(gravada.Id, EstadoOcorrencia.Abatido, "supervisor", "confirmado"));

        IReadOnlyList<OcorrenciaEstadoTransicao> historico = repositorio.ObterHistoricoEstados(gravada.Id);

        Assert.Equal(
            new[] { EstadoOcorrencia.Detectado, EstadoOcorrencia.AbatePendente, EstadoOcorrencia.Abatido },
            historico.Select(h => h.Estado).ToArray());
        Assert.Equal("supervisor", historico[^1].Usuario);
        Assert.Equal(EstadoOcorrencia.Abatido, repositorio.GetById(gravada.Id)!.Estado);
    }

    /// <summary>
    /// Banco que já existia com o esquema antigo (sem as colunas de vínculo/estado): abrir o
    /// repositório precisa acrescentar as colunas e preservar as linhas gravadas.
    /// </summary>
    [Fact]
    public void Tabela_antiga_recebe_as_colunas_novas_sem_perder_registros()
    {
        using (SqliteConnection connection = SqliteConnectionFactory.OpenConnection())
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "CREATE TABLE ObjetosDetectados (" +
                "Id INTEGER PRIMARY KEY AUTOINCREMENT, Tipo TEXT NOT NULL, X REAL NOT NULL, Y REAL NOT NULL, " +
                "Z REAL NULL, Quadrante TEXT NOT NULL, DataHora TEXT NOT NULL, Dispositivo TEXT NOT NULL, " +
                "NivelConfianca REAL NULL, Observacao TEXT NULL, ReferenciaImagem TEXT NULL);" +
                "INSERT INTO ObjetosDetectados (Tipo, X, Y, Quadrante, DataHora, Dispositivo) " +
                "VALUES ('Alvo genérico', 1.0, 2.0, 'Q2', '2026-01-01T10:00:00.0000000', 'Arduino');";
            command.ExecuteNonQuery();
        }

        var repositorio = new SqliteObjetoDetectadoRepository();
        List<ObjetoDetectado> todas = repositorio.GetAll().ToList();

        Assert.Single(todas);
        Assert.Equal("Alvo genérico", todas[0].Tipo);
        Assert.Equal("Q2", todas[0].Quadrante);

        // Colunas novas chegam nulas e com o estado padrão, sem descartar nada.
        Assert.Null(todas[0].PerfilAlvoId);
        Assert.Null(todas[0].RastreamentoId);
        Assert.Equal(EstadoOcorrencia.Detectado, todas[0].Estado);

        // E o registro antigo (sem rastreamento) convive com os novos.
        repositorio.AdicionarSeNaoExistir(Ocorrencia("rastreio-novo", "Q1"));
        Assert.Equal(2, repositorio.GetAll().Count);
    }

    [Fact]
    public void Registro_do_radar_sem_rastreamento_pode_repetir_quadrante()
    {
        var repositorio = new SqliteObjetoDetectadoRepository();

        // O índice único é parcial (só vale quando há rastreamento), senão o radar — que não tem
        // rastreamento visual — ficaria limitado a uma linha por quadrante.
        repositorio.Add(new ObjetoDetectado { Tipo = "Alvo genérico", Quadrante = "Q1", DataHora = DateTime.Now, Dispositivo = "Arduino" });
        repositorio.Add(new ObjetoDetectado { Tipo = "Alvo genérico", Quadrante = "Q1", DataHora = DateTime.Now, Dispositivo = "Arduino" });

        Assert.Equal(2, repositorio.GetAll().Count);
    }

    // ---------------------------------------------------------------- Câmera → quadrante

    [Fact]
    public void Camera_e_quadrante_sao_persistidos_e_reatribuiveis()
    {
        var repositorio = new SqliteCameraQuadranteRepository();

        repositorio.Salvar(new CameraQuadrante { DispositivoId = "cam-1", Nome = "Frontal", Quadrante = Quadrant.Q1, AtualizadoEm = DateTime.Now });
        repositorio.Salvar(new CameraQuadrante { DispositivoId = "cam-1", Nome = "Frontal", Quadrante = Quadrant.Q3, AtualizadoEm = DateTime.Now });

        CameraQuadrante unica = Assert.Single(repositorio.GetAll());

        Assert.Equal(Quadrant.Q3, unica.Quadrante);
        Assert.Equal("cam-1", unica.DispositivoId);
    }

    [Fact]
    public void Atribuir_quadrante_ocupado_transfere_a_responsabilidade()
    {
        var repositorio = new SqliteCameraQuadranteRepository();

        repositorio.Salvar(new CameraQuadrante { DispositivoId = "cam-1", Nome = "Frontal", Quadrante = Quadrant.Q1, AtualizadoEm = DateTime.Now });
        repositorio.Salvar(new CameraQuadrante { DispositivoId = "cam-2", Nome = "Lateral", Quadrante = Quadrant.Q1, AtualizadoEm = DateTime.Now });

        CameraQuadrante unica = Assert.Single(repositorio.GetAll());

        Assert.Equal("cam-2", unica.DispositivoId);
        Assert.Equal(Quadrant.Q1, unica.Quadrante);
    }

    [Fact]
    public void Servico_de_quadrante_valida_e_responde_as_consultas()
    {
        var servico = new CameraQuadranteService(new SqliteCameraQuadranteRepository(), new LoggerFalso());
        var camera = new CameraDevice("cam-1", "Frontal");

        Assert.True(servico.Atribuir(camera, Quadrant.Q2).Sucesso);
        Assert.Equal(Quadrant.Q2, servico.ObterQuadrante("cam-1"));
        Assert.Equal("cam-1", servico.ObterCameraDoQuadrante(Quadrant.Q2)!.DispositivoId);

        // Remoção volta a "sem quadrante".
        Assert.True(servico.Atribuir(camera, Quadrant.None).Sucesso);
        Assert.Equal(Quadrant.None, servico.ObterQuadrante("cam-1"));
        Assert.Null(servico.ObterCameraDoQuadrante(Quadrant.Q2));

        // Câmera sem identificador é recusada.
        Assert.False(servico.Atribuir(new CameraDevice("", "Sem id"), Quadrant.Q1).Sucesso);
    }
}
