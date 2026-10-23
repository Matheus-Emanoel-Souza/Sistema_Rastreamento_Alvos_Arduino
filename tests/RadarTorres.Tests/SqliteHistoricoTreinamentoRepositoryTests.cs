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
}
