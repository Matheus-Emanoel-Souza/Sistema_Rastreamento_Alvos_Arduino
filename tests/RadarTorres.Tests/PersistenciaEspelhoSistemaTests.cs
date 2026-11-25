using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobre as duas tabelas que fecham o modelo de dados (2026-11-25): <c>TABEXE</c>, a execução de
/// treinamento que dá sentido aos <c>EXECUCAO_ID</c> espalhados pelas tabelas de neuroevolução, e
/// <c>TABESP</c>, o espelho periódico do sistema inteiro.
/// </summary>
public sealed class PersistenciaEspelhoSistemaTests : IDisposable
{
    private readonly string _pasta;

    public PersistenciaEspelhoSistemaTests()
    {
        _pasta = FotosDeTeste.CriarPasta();
        SqliteConnectionFactory.UsarBancoDeTeste(Path.Combine(_pasta, "teste.db"));
        PendingWriteQueue.UsarArquivoDeTeste(Path.Combine(_pasta, "pending-writes.sql"));
        SqliteSchema.EsquecerEsquemasAplicados();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        SqliteSchema.EsquecerEsquemasAplicados();
        FotosDeTeste.Limpar(_pasta);
    }

    // ------------------------------------------------------------------ TABEXE

    [Fact]
    public void Execucao_AbertaFicaEmAndamentoAteSerFechada()
    {
        var repositorio = new SqliteExecucaoTreinamentoRepository();
        repositorio.Abrir(NovaExecucao("exec-1"));

        ExecucaoTreinamento? emAndamento = repositorio.ObterEmAndamento();
        Assert.NotNull(emAndamento);
        Assert.Equal("exec-1", emAndamento!.ExecucaoId);
        Assert.Null(emAndamento.DataHoraFim);
        Assert.Equal(EncerramentoExecucaoTreinamento.EmAndamento, emAndamento.Encerramento);

        repositorio.Fechar("exec-1", geracoesConcluidas: 120, melhorFitness: 87.5, EncerramentoExecucaoTreinamento.MetaAtingida);

        Assert.Null(repositorio.ObterEmAndamento());

        // Filtra a 'legada', semeada na criação do esquema — ver o teste seguinte.
        ExecucaoTreinamento fechada = Assert.Single(repositorio.ObterUltimas(), e => e.ExecucaoId == "exec-1");
        Assert.NotNull(fechada.DataHoraFim);
        Assert.Equal(120, fechada.GeracoesConcluidas);
        Assert.Equal(87.5, fechada.MelhorFitness);
        Assert.Equal(EncerramentoExecucaoTreinamento.MetaAtingida, fechada.Encerramento);
    }

    [Fact]
    public void Execucao_LegadaExisteParaOsRegistrosMigradosNaoFicaremOrfaos()
    {
        var repositorio = new SqliteExecucaoTreinamentoRepository();
        repositorio.Abrir(NovaExecucao("exec-1"));

        // A 'legada' é semeada na criação do esquema e já nasce fechada — não pode aparecer como
        // execução em andamento nem sumir quando alguém abre uma execução de verdade.
        Assert.Contains(repositorio.ObterUltimas(), e => e.ExecucaoId == "legada" && e.DataHoraFim is not null);
    }

    [Fact]
    public void Execucao_AbertaPorAplicativoQueCaiu_EhFechadaComoInterrompidaNaProximaAbertura()
    {
        var execucoes = new SqliteExecucaoTreinamentoRepository();
        var historico = new SqliteHistoricoTreinamentoRepository();

        execucoes.Abrir(NovaExecucao("exec-caiu"));
        historico.Registrar(new HistoricoGeracaoTreinamento
        {
            Geracao = 7,
            MelhorFitness = 42,
            FitnessMedio = 20,
            AlvosAbatidos = 3,
            AlvosEscapados = 1,
            DataHora = new DateTime(2026, 11, 20, 10, 0, 0),
            ExecucaoId = "exec-caiu",
        });

        int fechadas = execucoes.FecharPendentesDeExecucaoAnterior();

        Assert.Equal(1, fechadas);
        Assert.Null(execucoes.ObterEmAndamento());

        ExecucaoTreinamento interrompida = Assert.Single(execucoes.ObterUltimas(), e => e.ExecucaoId == "exec-caiu");
        Assert.Equal(EncerramentoExecucaoTreinamento.Interrompida, interrompida.Encerramento);
        Assert.Equal(1, interrompida.GeracoesConcluidas);
        Assert.Equal(42, interrompida.MelhorFitness);
        // Datada pelo último sinal de vida (a geração gravada), não pela hora em que o aplicativo
        // voltou a abrir — ver SqliteExecucaoTreinamentoRepository.FecharPendentesDeExecucaoAnterior.
        Assert.Equal(new DateTime(2026, 11, 20, 10, 0, 0), interrompida.DataHoraFim);
    }

    [Fact]
    public void Historico_LinhaGravadaAntesDaColunaExistir_RecebeLegadaNaMigracao()
    {
        using (SqliteConnection connection = SqliteConnectionFactory.OpenConnection())
        {
            // Esquema LEGADO: sem EXECUCAO_ID, como num banco anterior a esta migração.
            using SqliteCommand criar = connection.CreateCommand();
            criar.CommandText =
                "CREATE TABLE TABHTG (ID INTEGER PRIMARY KEY AUTOINCREMENT, GERACAO INTEGER NOT NULL, " +
                "MELHOR_FITNESS REAL NOT NULL, FITNESS_MEDIO REAL NOT NULL, ALVOS_ABATIDOS INTEGER NOT NULL, " +
                "ALVOS_ESCAPADOS INTEGER NOT NULL, DH_REGISTRO TEXT NOT NULL)";
            criar.ExecuteNonQuery();

            using SqliteCommand inserir = connection.CreateCommand();
            inserir.CommandText =
                "INSERT INTO TABHTG (GERACAO, MELHOR_FITNESS, FITNESS_MEDIO, ALVOS_ABATIDOS, ALVOS_ESCAPADOS, DH_REGISTRO) " +
                "VALUES (1, 10, 5, 2, 0, '2026-01-01T00:00:00.0000000')";
            inserir.ExecuteNonQuery();
        }

        IReadOnlyList<HistoricoGeracaoTreinamento> lido = new SqliteHistoricoTreinamentoRepository().ObterUltimos();

        HistoricoGeracaoTreinamento antigo = Assert.Single(lido);
        Assert.Equal("legada", antigo.ExecucaoId);
    }

    // ------------------------------------------------------------------ TABESP

    [Fact]
    public void Espelho_BancoVazio_GravaLinhaComTudoZerado()
    {
        EspelhoSistema espelho = new SqliteEspelhoSistemaRepository().Capturar();

        Assert.NotEqual(0, espelho.Id);
        Assert.Equal(0, espelho.AlvosDetectados);
        Assert.Equal(0, espelho.CamerasCadastradas);
        Assert.Null(espelho.ModoId);
        Assert.Null(espelho.ExecucaoId);
    }

    [Fact]
    public void Espelho_ContaAlvosPorEstadoEPorQuadrante()
    {
        var objetos = new SqliteObjetoDetectadoRepository();
        objetos.Add(NovoObjeto("Q1", EstadoOcorrencia.Detectado));
        objetos.Add(NovoObjeto("Q1", EstadoOcorrencia.Detectado));
        objetos.Add(NovoObjeto("Q2", EstadoOcorrencia.AbatePendente));
        // Abatido não conta no quadrante: o espelho descreve o que ESTÁ em cena no instante,
        // e um alvo abatido deixou de estar.
        objetos.Add(NovoObjeto("Q3", EstadoOcorrencia.Abatido));

        EspelhoSistema espelho = new SqliteEspelhoSistemaRepository().Capturar();

        Assert.Equal(2, espelho.AlvosDetectados);
        Assert.Equal(1, espelho.AlvosAbatePendente);
        Assert.Equal(1, espelho.AlvosAbatidos);
        Assert.Equal(2, espelho.AlvosQ1);
        Assert.Equal(1, espelho.AlvosQ2);
        Assert.Equal(0, espelho.AlvosQ3);
        Assert.Equal(0, espelho.AlvosQ4);
    }

    [Fact]
    public void Espelho_GuardaOModoVigenteNoInstanteDaAmostra()
    {
        var modos = new SqliteModoAtualTorreRepository();
        modos.Add(new ModoAtualTorre { ModoAnterior = "Manual", NovoModo = "Automático", DataHora = DateTime.Now });

        var repositorio = new SqliteEspelhoSistemaRepository();
        EspelhoSistema primeiro = repositorio.Capturar();
        Assert.Equal("Automático", primeiro.ModoAtual);

        modos.Add(new ModoAtualTorre { ModoAnterior = "Automático", NovoModo = "Manual", DataHora = DateTime.Now });
        EspelhoSistema segundo = repositorio.Capturar();

        // O espelho anterior NÃO é reescrito: é esse o ponto da tabela — cada linha preserva como
        // o sistema estava naquele instante.
        Assert.Equal("Automático", repositorio.ObterUltimos()[0].ModoAtual);
        Assert.Equal("Manual", segundo.ModoAtual);
    }

    [Fact]
    public void Espelho_AcoesContamApenasAsOcorridasDesdeOEspelhoAnterior()
    {
        var acoes = new SqliteAcaoRealizadaRepository();
        var repositorio = new SqliteEspelhoSistemaRepository();

        acoes.Add(new AcaoRealizada { TorreId = 1, DataHora = DateTime.Now, Observacao = "antes" });
        EspelhoSistema primeiro = repositorio.Capturar();
        Assert.Equal(1, primeiro.AcoesNoIntervalo);

        acoes.Add(new AcaoRealizada { TorreId = 2, DataHora = DateTime.Now, Observacao = "depois" });
        acoes.Add(new AcaoRealizada { TorreId = 3, DataHora = DateTime.Now, Observacao = "depois" });
        EspelhoSistema segundo = repositorio.Capturar();

        // Duas, não três: a primeira já tinha sido contabilizada no espelho anterior.
        Assert.Equal(2, segundo.AcoesNoIntervalo);
    }

    [Fact]
    public void Espelho_AmarraATreinamentoEmCursoEAUltimaGeracaoGravada()
    {
        var execucoes = new SqliteExecucaoTreinamentoRepository();
        var historico = new SqliteHistoricoTreinamentoRepository();
        var repositorio = new SqliteEspelhoSistemaRepository();

        execucoes.Abrir(NovaExecucao("exec-viva"));
        historico.Registrar(new HistoricoGeracaoTreinamento
        {
            Geracao = 3,
            MelhorFitness = 11,
            FitnessMedio = 6,
            AlvosAbatidos = 1,
            AlvosEscapados = 0,
            DataHora = DateTime.Now,
            ExecucaoId = "exec-viva",
        });
        historico.Registrar(new HistoricoGeracaoTreinamento
        {
            Geracao = 4,
            MelhorFitness = 15,
            FitnessMedio = 9,
            AlvosAbatidos = 2,
            AlvosEscapados = 0,
            DataHora = DateTime.Now,
            ExecucaoId = "exec-viva",
        });

        EspelhoSistema durante = repositorio.Capturar();
        Assert.Equal("exec-viva", durante.ExecucaoId);
        Assert.Equal(4, durante.GeracaoAtual);
        Assert.Equal(15, durante.MelhorFitness);
        Assert.Equal(9, durante.FitnessMedio);

        execucoes.Fechar("exec-viva", 4, 15, EncerramentoExecucaoTreinamento.ParadaPeloUsuario);
        EspelhoSistema depois = repositorio.Capturar();

        // Execução fechada deixa de ser "a de agora": o espelho seguinte não pode continuar
        // atribuindo gerações de um treinamento que já acabou ao instante atual.
        Assert.Null(depois.ExecucaoId);
        Assert.Null(depois.GeracaoAtual);
    }

    [Fact]
    public void Espelho_ObterPorPeriodo_DevolveSoAJanelaPedidaEmOrdemCronologica()
    {
        var repositorio = new SqliteEspelhoSistemaRepository();
        repositorio.Capturar();
        repositorio.Capturar();
        repositorio.Capturar();

        IReadOnlyList<EspelhoSistema> janela = repositorio.ObterPorPeriodo(DateTime.Now.AddMinutes(-1), DateTime.Now.AddMinutes(1));
        Assert.Equal(3, janela.Count);
        Assert.True(janela[0].DataHora <= janela[1].DataHora && janela[1].DataHora <= janela[2].DataHora);

        Assert.Empty(repositorio.ObterPorPeriodo(DateTime.Now.AddDays(-2), DateTime.Now.AddDays(-1)));
    }

    // ------------------------------------------------------------------ Apoio

    private static ExecucaoTreinamento NovaExecucao(string id) => new()
    {
        ExecucaoId = id,
        DataHoraInicio = DateTime.Now,
        TamanhoPopulacao = 50,
        QuantidadeEspecies = 4,
        TetoComplexidade = 80,
        MetaGeracoes = 1000,
        ConfiguracaoFitness = "v1.1-teste",
    };

    private static ObjetoDetectado NovoObjeto(string quadrante, EstadoOcorrencia estado) => new()
    {
        Tipo = "Alvo genérico",
        X = 1,
        Y = 2,
        Quadrante = quadrante,
        DataHora = DateTime.Now,
        Dispositivo = "Teste",
        Estado = estado,
    };
}
