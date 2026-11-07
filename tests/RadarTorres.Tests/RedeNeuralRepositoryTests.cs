using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// <see cref="SqliteRedeNeuralRepository"/> e <see cref="SqliteExperimentoRedeNeuralRepository"/>
/// (registro das versões da rede do controlador e das baterias de comparação táticas entre elas —
/// ver Software/Qualidade/REDESENHO_CONTROLADOR_V2.md).
/// </summary>
public sealed class RedeNeuralRepositoryTests : IDisposable
{
    private readonly string _pasta;
    private readonly IRedeNeuralRepository _redes = new SqliteRedeNeuralRepository();
    private readonly IExperimentoRedeNeuralRepository _experimentos = new SqliteExperimentoRedeNeuralRepository();

    public RedeNeuralRepositoryTests()
    {
        _pasta = FotosDeTeste.CriarPasta();
        SqliteConnectionFactory.UsarBancoDeTeste(Path.Combine(_pasta, "teste.db"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        FotosDeTeste.Limpar(_pasta);
    }

    [Fact]
    public void Registrar_e_ObterTodos_preserva_campos_inclusive_nulos()
    {
        _redes.Registrar(new RedeNeural
        {
            Versao = "1",
            Nome = "Reservada",
            Entradas = null,
            Saidas = null,
            TipoCamadaOculta = null,
            NeuroniosOcultos = null,
            Biblioteca = null,
            Observacoes = "placeholder",
            DataRegistro = DateTime.UtcNow,
        });
        _redes.Registrar(new RedeNeural
        {
            Versao = "2",
            Nome = "ControladorHibrido",
            Entradas = 7,
            Saidas = 2,
            TipoCamadaOculta = RedeNeural.TipoCamadaOcultaVariavel,
            NeuroniosOcultos = null,
            Biblioteca = "SharpNEAT 4.1.0",
            Observacoes = null,
            DataRegistro = DateTime.UtcNow,
        });

        var todos = _redes.ObterTodos();

        Assert.Equal(2, todos.Count);
        RedeNeural v1 = todos.Single(r => r.Versao == "1");
        Assert.Null(v1.Entradas);
        Assert.Null(v1.TipoCamadaOculta);
        Assert.Equal("placeholder", v1.Observacoes);

        RedeNeural v2 = todos.Single(r => r.Versao == "2");
        Assert.Equal(7, v2.Entradas);
        Assert.Equal(2, v2.Saidas);
        Assert.Equal(RedeNeural.TipoCamadaOcultaVariavel, v2.TipoCamadaOculta);
        Assert.Null(v2.NeuroniosOcultos);
        Assert.Equal("SharpNEAT 4.1.0", v2.Biblioteca);
    }

    [Fact]
    public void ObterPorVersao_devolve_null_quando_nao_registrada()
    {
        Assert.Null(_redes.ObterPorVersao("inexistente"));
    }

    [Fact]
    public void Versao_duplicada_falha_por_indice_unico()
    {
        _redes.Registrar(new RedeNeural { Versao = "2", Nome = "A", DataRegistro = DateTime.UtcNow });

        Assert.Throws<SqliteException>(() =>
            _redes.Registrar(new RedeNeural { Versao = "2", Nome = "B", DataRegistro = DateTime.UtcNow }));
    }

    [Fact]
    public void Experimento_fica_vinculado_a_rede_neural_correta()
    {
        _redes.Registrar(new RedeNeural { Versao = "2", Nome = "ControladorHibrido", DataRegistro = DateTime.UtcNow });
        _redes.Registrar(new RedeNeural { Versao = "3", Nome = "ControladorHibridoV2", DataRegistro = DateTime.UtcNow });
        int idV2 = _redes.ObterPorVersao("2")!.Id;
        int idV3 = _redes.ObterPorVersao("3")!.Id;

        _experimentos.Registrar(NovoExperimento(idV2, "V1-referencia"));
        _experimentos.Registrar(NovoExperimento(idV3, "V2"));
        _experimentos.Registrar(NovoExperimento(idV3, "V2"));

        Assert.Single(_experimentos.ObterPorRedeNeural(idV2));
        Assert.Equal(2, _experimentos.ObterPorRedeNeural(idV3).Count);
        Assert.Equal(3, _experimentos.ObterTodos().Count);
    }

    private static ExperimentoRedeNeural NovoExperimento(int redeNeuralId, string variante) => new()
    {
        RedeNeuralId = redeNeuralId,
        Variante = variante,
        Repeticao = 0,
        Populacao = 24,
        Geracoes = 40,
        TetoComplexidade = 60,
        RedeRecorrente = false,
        FitnessV2 = variante != "V1-referencia",
        FitnessTreinoPico = 68.2,
        GeracaoDoPico = 26,
        SegundosTreino = 1.16,
        ValFitnessMedio = 45.0,
        ValAlvosNascidosMedio = 9,
        ValAbatidosMedio = 6.8,
        ValEscapadosMedio = 1,
        ValTirosErradosMedio = 4.3,
        ValTempoRespostaMedioSegundos = 2.09,
        ValTrocasPenalizadasMedia = 0,
        ValCooperacoesConcedidasMedia = 0,
        ValCooperacoesNegadasMedia = 0,
        FonteDados = "teste",
        DataRegistro = DateTime.UtcNow,
    };
}
