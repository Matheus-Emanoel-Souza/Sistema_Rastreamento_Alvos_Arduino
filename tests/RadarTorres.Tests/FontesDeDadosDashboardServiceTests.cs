using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobertura de <see cref="FontesDeDadosDashboardService"/> — usa os repositórios SQLite reais
/// (não dublês) contra um banco de teste, porque o ponto central a verificar é justamente que o
/// serviço lê CORRETAMENTE os dados que esses repositórios já persistem, sem reimplementar a
/// consulta.
/// </summary>
public sealed class FontesDeDadosDashboardServiceTests : IDisposable
{
    private readonly string _pasta;
    private readonly IMarcoFitnessTreinamentoRepository _marcos = new SqliteMarcoFitnessTreinamentoRepository();
    private readonly IInstantaneoComportamentoRepository _instantaneos = new SqliteInstantaneoComportamentoRepository();
    private readonly IHistoricoTreinamentoRepository _historico = new SqliteHistoricoTreinamentoRepository();
    private readonly IExperimentoHiperparametrosRepository _experimentosHiperparametros = new SqliteExperimentoHiperparametrosRepository();
    private readonly IExperimentoRedeNeuralRepository _experimentosRedeNeural = new SqliteExperimentoRedeNeuralRepository();
    private readonly FontesDeDadosDashboardService _service;

    public FontesDeDadosDashboardServiceTests()
    {
        _pasta = FotosDeTeste.CriarPasta();
        SqliteConnectionFactory.UsarBancoDeTeste(Path.Combine(_pasta, "teste.db"));
        _service = new FontesDeDadosDashboardService(_marcos, _instantaneos, _historico, _experimentosHiperparametros, _experimentosRedeNeural);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        FotosDeTeste.Limpar(_pasta);
    }

    [Fact]
    public void ObterFontes_DevolveAsCincoFontesComCamposNaoVazios()
    {
        var fontes = _service.ObterFontes();

        Assert.Equal(5, fontes.Count);
        Assert.All(fontes, f =>
        {
            Assert.NotEmpty(f.CamposEixoX);
            Assert.NotEmpty(f.CamposMetrica);
        });
        Assert.Contains(fontes, f => f.Chave == "MarcosFitness");
        Assert.Contains(fontes, f => f.Chave == "ExperimentosRedeNeural");
    }

    [Fact]
    public void ObterSerie_MarcosFitness_DevolvePontosOrdenadosPorGeracao()
    {
        _marcos.Registrar(new MarcoFitnessTreinamento { GeracaoMarco = 1000, FitnessMedioDoBloco = 50.0, MelhorFitnessDoBloco = 60.0, DataHora = DateTime.Now, ExecucaoId = "x" });
        _marcos.Registrar(new MarcoFitnessTreinamento { GeracaoMarco = 500, FitnessMedioDoBloco = 30.0, MelhorFitnessDoBloco = 40.0, DataHora = DateTime.Now, ExecucaoId = "x" });

        var pontos = _service.ObterSerie("MarcosFitness", "GeracaoMarco", "FitnessMedioDoBloco");

        Assert.Equal(2, pontos.Count);
        Assert.Equal(500, pontos[0].OrdinalEixoX);
        Assert.Equal(30.0, pontos[0].ValorMetrica);
        Assert.Equal(1000, pontos[1].OrdinalEixoX);
        Assert.Equal(50.0, pontos[1].ValorMetrica);
    }

    [Fact]
    public void ObterSerie_CampoMetricaNulo_DescartaOPontoEmVezDeVirarZero()
    {
        _instantaneos.Registrar(new InstantaneoComportamentoMelhorGene
        {
            GeracaoMarco = 1000,
            Fitness = 10,
            AlvosAbatidos = 0,
            AlvosEscapados = 0,
            TirosErrados = 0,
            TaxaDeAcerto = null, // nenhum disparo tentado
            TaxaDeCobertura = null,
            GenomaJson = "{}",
            DataHora = DateTime.Now,
        });

        var pontos = _service.ObterSerie("InstantaneosComportamento", "GeracaoMarco", "TaxaDeAcerto");

        Assert.Empty(pontos);
    }

    [Fact]
    public void ObterSerie_FonteOuCampoDesconhecido_DevolveListaVaziaSemLancar()
    {
        Assert.Empty(_service.ObterSerie("FonteInexistente", "x", "y"));
        Assert.Empty(_service.ObterSerie("MarcosFitness", "CampoInexistente", "FitnessMedioDoBloco"));
    }

    [Fact]
    public void ObterSerie_SemNenhumDadoAinda_DevolveListaVazia()
    {
        Assert.Empty(_service.ObterSerie("ExperimentosRedeNeural", "Id", "ValFitnessMedio"));
    }
}
