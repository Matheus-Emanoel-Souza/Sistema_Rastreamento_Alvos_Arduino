using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>Cobertura de <see cref="SqliteDashboardGraficoRepository"/> — os gráficos salvos pelo usuário na tela "Dashboards".</summary>
public sealed class SqliteDashboardGraficoRepositoryTests : IDisposable
{
    private readonly string _pasta;
    private readonly IDashboardGraficoRepository _repositorio = new SqliteDashboardGraficoRepository();

    public SqliteDashboardGraficoRepositoryTests()
    {
        _pasta = FotosDeTeste.CriarPasta();
        SqliteConnectionFactory.UsarBancoDeTeste(Path.Combine(_pasta, "teste.db"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        FotosDeTeste.Limpar(_pasta);
    }

    private static DashboardGrafico NovoCard(string titulo = "Fitness por geração", int ordem = 0) => new()
    {
        Titulo = titulo,
        FonteChave = "MarcosFitness",
        CampoEixoXChave = "GeracaoMarco",
        CampoMetricaChave = "FitnessMedioDoBloco",
        TipoGrafico = TipoGraficoDashboard.Linha,
        Ordem = ordem,
    };

    [Fact]
    public void Registrar_PreencheIdEPreservaCampos()
    {
        DashboardGrafico card = _repositorio.Registrar(NovoCard());

        Assert.True(card.Id > 0);

        DashboardGrafico lido = _repositorio.ObterTodos().Single();
        Assert.Equal("Fitness por geração", lido.Titulo);
        Assert.Equal("MarcosFitness", lido.FonteChave);
        Assert.Equal("GeracaoMarco", lido.CampoEixoXChave);
        Assert.Equal("FitnessMedioDoBloco", lido.CampoMetricaChave);
        Assert.Equal(TipoGraficoDashboard.Linha, lido.TipoGrafico);
    }

    [Fact]
    public void ObterTodos_RespeitaOrdem()
    {
        _repositorio.Registrar(NovoCard("terceiro", ordem: 2));
        _repositorio.Registrar(NovoCard("primeiro", ordem: 0));
        _repositorio.Registrar(NovoCard("segundo", ordem: 1));

        var titulos = _repositorio.ObterTodos().Select(c => c.Titulo).ToList();

        Assert.Equal(["primeiro", "segundo", "terceiro"], titulos);
    }

    [Fact]
    public void Remover_ApagaSoOCardIndicado()
    {
        DashboardGrafico a = _repositorio.Registrar(NovoCard("a"));
        DashboardGrafico b = _repositorio.Registrar(NovoCard("b"));

        _repositorio.Remover(a.Id);

        DashboardGrafico restante = Assert.Single(_repositorio.ObterTodos());
        Assert.Equal(b.Id, restante.Id);
    }

    [Fact]
    public void Registrar_TipoBarra_SobreviveAoRoundTrip()
    {
        var card = NovoCard();
        card.TipoGrafico = TipoGraficoDashboard.Barra;

        _repositorio.Registrar(card);

        Assert.Equal(TipoGraficoDashboard.Barra, _repositorio.ObterTodos().Single().TipoGrafico);
    }
}
