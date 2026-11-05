using System;
using System.IO;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobertura do buffer circular de histórico da tela "Treinamento do Controlador" (ver
/// SqliteHistoricoTreinamentoRepository): limite de 300 registros, descartando o mais antigo
/// primeiro quando excede.
/// </summary>
/// <remarks>Usa um arquivo SQLite temporário via <c>SqliteConnectionFactory.UsarBancoDeTeste</c> — mesmo isolamento de <see cref="PersistenciaSqliteTests"/>.</remarks>
public sealed class SqliteHistoricoTreinamentoRepositoryTests : IDisposable
{
    private readonly string _pasta;
    private readonly SqliteHistoricoTreinamentoRepository _repositorio = new();

    public SqliteHistoricoTreinamentoRepositoryTests()
    {
        _pasta = FotosDeTeste.CriarPasta();
        SqliteConnectionFactory.UsarBancoDeTeste(Path.Combine(_pasta, "teste.db"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        FotosDeTeste.Limpar(_pasta);
    }

    private static HistoricoGeracaoTreinamento Registro(int geracao) => new()
    {
        Geracao = geracao,
        MelhorFitness = geracao * 1.5,
        FitnessMedio = geracao * 1.0,
        AlvosAbatidos = geracao,
        AlvosEscapados = 0,
        DataHora = DateTime.Now
    };

    [Fact]
    public void Registrar_AteOLimite_MantemTodosOsRegistros()
    {
        for (int i = 1; i <= 50; i++) _repositorio.Registrar(Registro(i));

        var historico = _repositorio.ObterUltimos();

        Assert.Equal(50, historico.Count);
        Assert.Equal(1, historico[0].Geracao);
        Assert.Equal(50, historico[^1].Geracao);
    }

    [Fact]
    public void Registrar_AoExceder300Registros_DescartaOsMaisAntigosPrimeiro()
    {
        for (int i = 1; i <= 305; i++) _repositorio.Registrar(Registro(i));

        var historico = _repositorio.ObterUltimos(500);

        Assert.Equal(300, historico.Count);
        // As 5 primeiras gerações (1 a 5) devem ter sido descartadas; sobra 6..305.
        Assert.Equal(6, historico[0].Geracao);
        Assert.Equal(305, historico[^1].Geracao);
    }

    [Fact]
    public void ObterUltimos_DevolveEmOrdemCronologica()
    {
        _repositorio.Registrar(Registro(1));
        _repositorio.Registrar(Registro(2));
        _repositorio.Registrar(Registro(3));

        var historico = _repositorio.ObterUltimos();

        Assert.Equal([1, 2, 3], [historico[0].Geracao, historico[1].Geracao, historico[2].Geracao]);
    }

    /// <summary>
    /// Complexidade e nº de espécies são as duas colunas que passaram a existir com o NEAT (com a
    /// topologia fixa anterior não havia nada a registrar). Se não voltarem da consulta, a coluna
    /// correspondente do histórico na tela fica vazia e o dado morre na gravação.
    /// </summary>
    [Fact]
    public void Registrar_PreservaComplexidadeEQuantidadeDeEspecies()
    {
        HistoricoGeracaoTreinamento registro = Registro(7);
        registro.ComplexidadeMelhor = 23.0;
        registro.QuantidadeEspecies = 6;

        _repositorio.Registrar(registro);
        var historico = _repositorio.ObterUltimos();

        Assert.Equal(23.0, historico[^1].ComplexidadeMelhor);
        Assert.Equal(6, historico[^1].QuantidadeEspecies);
    }

    /// <summary>
    /// Gerações gravadas pelo algoritmo genético anterior não têm essas métricas — e <c>null</c> é
    /// a representação correta disso, não zero: zero conexões ou zero espécies seria um dado falso
    /// (e apareceria como tal no gráfico e na tabela).
    /// </summary>
    [Fact]
    public void Registrar_SemAsMetricasDoNeat_PreservaNuloEmVezDeZero()
    {
        _repositorio.Registrar(Registro(3));

        var historico = _repositorio.ObterUltimos();

        Assert.Null(historico[^1].ComplexidadeMelhor);
        Assert.Null(historico[^1].QuantidadeEspecies);
    }
}
