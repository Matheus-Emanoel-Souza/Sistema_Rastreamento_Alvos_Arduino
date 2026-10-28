using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using RadarTorres.App.Configuration;
using RadarTorres.App.Data;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Regressões das correções levantadas na auditoria de código (camadas Services/, Repositories/,
/// Data/ e Helpers/): criação de esquema SQLite repetida a cada operação, estado do modo de
/// simulação depois de desligado e processamento de quadro da câmera sem cópia redundante.
/// </summary>
/// <remarks>
/// Cada teste aqui falhava antes da correção correspondente — não são testes de caracterização.
/// </remarks>
public sealed class AuditoriaCorrecoesTests : IDisposable
{
    private readonly string _pasta;

    public AuditoriaCorrecoesTests()
    {
        _pasta = FotosDeTeste.CriarPasta();
        SqliteConnectionFactory.UsarBancoDeTeste(Path.Combine(_pasta, "auditoria.db"));
        PendingWriteQueue.UsarArquivoDeTeste(Path.Combine(_pasta, "pending-writes.sql"));
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        FotosDeTeste.Limpar(_pasta);
    }

    // ------------------------------------------------------------------ Esquema SQLite

    /// <summary>
    /// Antes da correção, CADA operação de repositório reexecutava o bloco inteiro de
    /// CREATE TABLE + PRAGMA table_info (um por coluna migrada) + CREATE INDEX. No caminho
    /// quente (uma gravação por quadro, por câmera) isso é mais de dez instruções jogadas fora
    /// por chamada. Agora o bloco roda uma única vez por banco.
    /// </summary>
    [Fact]
    public void Esquema_de_uma_tabela_e_criado_uma_unica_vez_por_banco()
    {
        var repositorio = new SqliteObjetoDetectadoRepository();

        repositorio.GetAll();
        int depoisDaPrimeira = SqliteSchema.ExecucoesDeEsquema;

        for (int i = 0; i < 5; i++)
        {
            repositorio.GetAll();
            repositorio.Add(Ocorrencia($"rastreio-{i}"));
        }

        Assert.Equal(depoisDaPrimeira, SqliteSchema.ExecucoesDeEsquema);
    }

    /// <summary>
    /// O cache não pode "pular" a criação quando o banco é outro — senão trocar de banco
    /// (inclusive entre testes) deixaria as tabelas sem criar.
    /// </summary>
    [Fact]
    public void Banco_diferente_recria_o_esquema()
    {
        new SqliteObjetoDetectadoRepository().GetAll();
        int antes = SqliteSchema.ExecucoesDeEsquema;

        SqliteConnectionFactory.UsarBancoDeTeste(Path.Combine(_pasta, "outro.db"));
        var outro = new SqliteObjetoDetectadoRepository();

        Assert.Empty(outro.GetAll());
        Assert.True(SqliteSchema.ExecucoesDeEsquema > antes);
    }

    /// <summary>O esquema continuar sendo aplicado é o que garante que os dados ainda gravam.</summary>
    [Fact]
    public void Gravacao_continua_funcionando_com_o_esquema_aplicado_uma_vez_so()
    {
        var repositorio = new SqliteObjetoDetectadoRepository();

        repositorio.AdicionarSeNaoExistir(Ocorrencia("rastreio-a"));
        repositorio.AdicionarSeNaoExistir(Ocorrencia("rastreio-b"));
        repositorio.AdicionarSeNaoExistir(Ocorrencia("rastreio-a")); // mesma identidade: não duplica

        Assert.Equal(2, repositorio.GetAll().Count);
    }

    private static ObjetoDetectado Ocorrencia(string rastreamento) => new()
    {
        Tipo = "Bola vermelha",
        X = 1.0,
        Y = 2.0,
        Quadrante = "Q1",
        DataHora = DateTime.Now,
        Dispositivo = "Câmera",
        RastreamentoId = rastreamento,
        Estado = EstadoOcorrencia.Detectado
    };

    // ------------------------------------------------------------------ Modo de simulação

    /// <summary>
    /// Desligar o modo de simulação precisa deixar o serviço realmente vazio: antes, os alvos
    /// fictícios continuavam no dicionário e uma execução de Tick já agendada ainda emitia
    /// leituras depois do desligamento (alvo "fantasma" reaparecendo no radar).
    /// </summary>
    [Fact]
    public void Parar_a_simulacao_zera_os_alvos_ficticios_e_as_leituras()
    {
        var logger = new LoggerFalso();
        var simulacao = new SimulationService(logger, new ConfiguracoesAplicativoRepositorioFalso());

        simulacao.Start(initialTargetCount: 3);
        Assert.True(simulacao.IsRunning);
        Assert.Equal(3, simulacao.ActiveSimulatedTargetIds.Count);

        simulacao.Stop();

        int leiturasDepoisDoStop = 0;
        simulacao.ReadingGenerated += (_, _) => Interlocked.Increment(ref leiturasDepoisDoStop);

        Thread.Sleep(120); // tempo de sobra para qualquer ciclo pendente do timer acordar

        Assert.False(simulacao.IsRunning);
        Assert.Empty(simulacao.ActiveSimulatedTargetIds);
        Assert.Equal(0, Volatile.Read(ref leiturasDepoisDoStop));
    }

    /// <summary>Parar algo que nunca foi iniciado não é um evento — não pode ir para o log do operador.</summary>
    [Fact]
    public void Parar_simulacao_que_nunca_rodou_nao_registra_desativacao()
    {
        var logger = new LoggerFalso();
        var simulacao = new SimulationService(logger, new ConfiguracoesAplicativoRepositorioFalso());

        simulacao.Stop();
        simulacao.Dispose();

        Assert.DoesNotContain(logger.Mensagens, m => m.Contains("simulação desativado"));
    }

    /// <summary>O primeiro ciclo do timer dispara imediatamente; ligar precisa valer já na largada.</summary>
    [Fact]
    public void Iniciar_a_simulacao_ja_emite_leituras()
    {
        var simulacao = new SimulationService(new LoggerFalso(), new ConfiguracoesAplicativoRepositorioFalso());

        int leituras = 0;
        simulacao.ReadingGenerated += (_, _) => Interlocked.Increment(ref leituras);

        simulacao.Start(initialTargetCount: 2);
        try
        {
            Assert.True(Volatile.Read(ref leituras) >= 2);
            Assert.True(simulacao.IsRunning);
        }
        finally
        {
            simulacao.Dispose();
        }
    }

    // ------------------------------------------------------------------ Quadros da câmera

    /// <summary>
    /// O quadro submetido tem que chegar íntegro ao detector (mesmas dimensões e mesmos bytes)
    /// depois da troca do AllocHGlobal + Marshal.Copy por fixação direta do vetor já copiado.
    /// </summary>
    [Fact]
    public void Quadro_submetido_chega_integro_ao_detector()
    {
        using var configuracao = new ConfiguracaoDeTeste(v => v.IntervaloProcessamentoMs = 0);

        const int largura = 8, altura = 4;
        int stride = largura * 3;
        byte[] pixels = new byte[stride * altura];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = (byte)(i % 251);

        var detector = new AlvoVisualServicoFalso();
        using var analisador = new AnalisadorQuadros(detector, new LoggerFalso());

        using var concluido = new ManualResetEventSlim(false);
        AvaliacaoQuadroEventArgs? recebido = null;
        analisador.QuadroAvaliado += (_, e) => { recebido = e; concluido.Set(); };

        analisador.Submeter(new CameraFrameCapturedEventArgs(pixels, largura, altura, stride));

        Assert.True(concluido.Wait(TimeSpan.FromSeconds(5)), "a rodada de detecção não terminou a tempo");
        Assert.NotNull(recebido);
        Assert.Equal(largura, recebido!.Largura);
        Assert.Equal(altura, recebido.Altura);

        Assert.Equal(largura, detector.UltimaLargura);
        Assert.Equal(altura, detector.UltimaAltura);
        Assert.Equal(pixels[0], detector.PrimeiroPixelAzul);
    }

    private sealed class AlvoVisualServicoFalso : IAlvoVisualService
    {
        public int UltimaLargura { get; private set; }
        public int UltimaAltura { get; private set; }
        public byte PrimeiroPixelAzul { get; private set; }

        public AlvoVisualAvaliacao AvaliarFrame(Bitmap frame)
        {
            UltimaLargura = frame.Width;
            UltimaAltura = frame.Height;
            PrimeiroPixelAzul = frame.GetPixel(0, 0).B;

            return new AlvoVisualAvaliacao(
                TotalDeteccoes: 0,
                MelhorPontuacao: 0,
                UsouFotoReferencia: false,
                Resultado: null,
                Candidatos: new List<AlvoVisualCandidato>(),
                Tempos: TemposRodada.Zero);
        }
    }

    // ------------------------------------------------------------------ Porta serial

    /// <summary>
    /// Desconectar repetidamente (inclusive sem nunca ter conectado) não pode lançar nem deixar
    /// o CancellationTokenSource do laço de leitura vivo — antes ele nunca era descartado.
    /// </summary>
    [Fact]
    public void Desconectar_repetidamente_e_seguro()
    {
        var serial = new SerialCommunicationService(new LoggerFalso());

        serial.Disconnect();
        serial.Disconnect();
        serial.Dispose();

        Assert.Equal(ConnectionState.Disconnected, serial.State);
    }
}
