using System;
using System.IO;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;
using RadarTorres.App.NeuralEvolution;
using RadarTorres.App.Repositories;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobre a coluna <c>ConfiguracaoFitness</c> (2026-11-13): o rótulo que junta
/// <see cref="ParametrosTreinamento.VersaoFormulaFitness"/> com o estado das três regras
/// adicionais (ataque conjunto, troca de alvo por proximidade, torres desativadas), gravado
/// junto com cada marco/instantâneo de treinamento — e a migração que adiciona essa coluna sem
/// apagar linhas já existentes (igual, em espírito, à migração de <c>ExecucaoId</c>).
/// </summary>
public sealed class PersistenciaConfiguracaoFitnessTests : IDisposable
{
    private readonly string _pasta;

    public PersistenciaConfiguracaoFitnessTests()
    {
        _pasta = FotosDeTeste.CriarPasta();
        SqliteConnectionFactory.UsarBancoDeTeste(Path.Combine(_pasta, "teste.db"));
        PendingWriteQueue.UsarArquivoDeTeste(Path.Combine(_pasta, "pending-writes.sql"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        FotosDeTeste.Limpar(_pasta);
    }

    [Theory]
    [InlineData(false, false, false, "v1.1-teste")]
    [InlineData(true, false, false, "v1.1-teste+ataque-conjunto")]
    [InlineData(false, true, false, "v1.1-teste+troca-por-proximidade")]
    [InlineData(true, true, true, "v1.1-teste+ataque-conjunto+troca-por-proximidade+torres-desativadas=2,4")]
    public void DescricaoConfiguracaoFitness_CombinaVersaoComAsRegrasLigadas(
        bool ataqueConjunto, bool trocaPorProximidade, bool torresDesativadas, string esperado)
    {
        var parametros = new ParametrosTreinamento
        {
            VersaoFormulaFitness = "v1.1-teste",
            HabilitarBonusAtaqueConjunto = ataqueConjunto,
            PermitirTrocaDeAlvoPorProximidade = trocaPorProximidade,
            TorresDesativadasIds = torresDesativadas ? new System.Collections.Generic.HashSet<int> { 4, 2 } : new System.Collections.Generic.HashSet<int>()
        };

        Assert.Equal(esperado, parametros.DescricaoConfiguracaoFitness());
    }

    [Fact]
    public void MarcoFitness_LinhaGravadaAntesDaColunaExistir_RecebeDesconhecidaNaMigracao()
    {
        using (SqliteConnection connection = SqliteConnectionFactory.OpenConnection())
        {
            // Esquema LEGADO, de propósito: sem ConfiguracaoFitness (nem ExecucaoId), simulando
            // um banco de antes das duas migrações — a mesma situação de quem atualiza o app.
            using SqliteCommand criar = connection.CreateCommand();
            criar.CommandText =
                "CREATE TABLE marcos_fitness_treinamento (" +
                "Id INTEGER PRIMARY KEY AUTOINCREMENT, GeracaoMarco INTEGER NOT NULL, " +
                "FitnessMedioDoBloco REAL NOT NULL, MelhorFitnessDoBloco REAL NOT NULL, DataHora TEXT NOT NULL)";
            criar.ExecuteNonQuery();

            using SqliteCommand inserir = connection.CreateCommand();
            inserir.CommandText =
                "INSERT INTO marcos_fitness_treinamento (GeracaoMarco, FitnessMedioDoBloco, MelhorFitnessDoBloco, DataHora) " +
                "VALUES (500, 42.0, 50.0, '2026-01-01T00:00:00.0000000')";
            inserir.ExecuteNonQuery();
        }

        var repositorio = new SqliteMarcoFitnessTreinamentoRepository();

        // A simples leitura já dispara a migração (EnsureColumn) — a linha legada precisa
        // continuar existindo, só com o valor padrão na coluna nova.
        var antes = repositorio.ObterTodos();
        Assert.Single(antes);
        Assert.Equal("desconhecida", antes[0].ConfiguracaoFitness);

        repositorio.Registrar(new MarcoFitnessTreinamento
        {
            GeracaoMarco = 1000,
            FitnessMedioDoBloco = 80.0,
            MelhorFitnessDoBloco = 90.0,
            DataHora = DateTime.Now,
            ExecucaoId = "sessao-nova",
            ConfiguracaoFitness = "v1.1-recusa-por-janela-sobrecarga-por-demanda+ataque-conjunto"
        });

        var depois = repositorio.ObterTodos();
        Assert.Equal(2, depois.Count);
        MarcoFitnessTreinamento novo = depois[^1];
        Assert.Equal("v1.1-recusa-por-janela-sobrecarga-por-demanda+ataque-conjunto", novo.ConfiguracaoFitness);
    }

    [Fact]
    public void InstantaneoComportamento_LinhaGravadaAntesDaColunaExistir_RecebeDesconhecidaNaMigracao()
    {
        using (SqliteConnection connection = SqliteConnectionFactory.OpenConnection())
        {
            using SqliteCommand criar = connection.CreateCommand();
            criar.CommandText =
                "CREATE TABLE instantaneos_comportamento_melhor_gene (" +
                "Id INTEGER PRIMARY KEY AUTOINCREMENT, GeracaoMarco INTEGER NOT NULL, Fitness REAL NOT NULL, " +
                "AlvosAbatidos INTEGER NOT NULL, AlvosEscapados INTEGER NOT NULL, TirosErrados INTEGER NOT NULL, " +
                "TaxaDeAcerto REAL NULL, TaxaDeCobertura REAL NULL, GenomaJson TEXT NOT NULL, DataHora TEXT NOT NULL)";
            criar.ExecuteNonQuery();

            using SqliteCommand inserir = connection.CreateCommand();
            inserir.CommandText =
                "INSERT INTO instantaneos_comportamento_melhor_gene " +
                "(GeracaoMarco, Fitness, AlvosAbatidos, AlvosEscapados, TirosErrados, TaxaDeAcerto, TaxaDeCobertura, GenomaJson, DataHora) " +
                "VALUES (1000, 70.0, 10, 2, 1, 0.9, 0.83, '{}', '2026-01-01T00:00:00.0000000')";
            inserir.ExecuteNonQuery();
        }

        var repositorio = new SqliteInstantaneoComportamentoRepository();

        var antes = repositorio.ObterTodos();
        Assert.Single(antes);
        Assert.Equal("desconhecida", antes[0].ConfiguracaoFitness);

        repositorio.Registrar(new InstantaneoComportamentoMelhorGene
        {
            GeracaoMarco = 2000,
            Fitness = 95.0,
            AlvosAbatidos = 20,
            AlvosEscapados = 1,
            TirosErrados = 2,
            GenomaJson = "{}",
            DataHora = DateTime.Now,
            ExecucaoId = "sessao-nova",
            ConfiguracaoFitness = "v1.1-recusa-por-janela-sobrecarga-por-demanda+troca-por-proximidade"
        });

        var depois = repositorio.ObterTodos();
        Assert.Equal(2, depois.Count);
        InstantaneoComportamentoMelhorGene novo = depois[^1];
        Assert.Equal("v1.1-recusa-por-janela-sobrecarga-por-demanda+troca-por-proximidade", novo.ConfiguracaoFitness);
    }
}
