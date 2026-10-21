using RadarTorres.App.Configuration;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Services;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobertura do algoritmo em camadas de <see cref="TowerSelectionService.SelectTowerFor"/>:
/// torre do próprio quadrante, cooperação entre torres adjacentes quando o quadrante tem mais de
/// um alvo simultâneo, e cobertura pela torre sobressalente. Ver
/// Docs/Tecnica/ALGORITMO_SELECAO_TORRE.md, seção 4.
/// </summary>
public sealed class TowerSelectionServiceTests : IDisposable
{
    private readonly AppSettings _configuracaoAnterior;
    private readonly LoggerFalso _logger = new();
    private readonly TowerSelectionService _service;

    public TowerSelectionServiceTests()
    {
        _configuracaoAnterior = AppConfig.Current;

        var configuracao = new AppSettings();
        configuracao.Towers.Add(new TowerDefinition { Id = 1, Name = "Torre 1", X = 1.75, Y = 1.75 });   // Q1
        configuracao.Towers.Add(new TowerDefinition { Id = 2, Name = "Torre 2", X = -1.75, Y = 1.75 });  // Q2
        configuracao.Towers.Add(new TowerDefinition { Id = 3, Name = "Torre 3", X = -1.75, Y = -1.75 }); // Q3
        configuracao.Towers.Add(new TowerDefinition { Id = 4, Name = "Torre 4", X = 1.75, Y = -1.75 });  // Q4
        AppConfig.Current = configuracao;

        _service = new TowerSelectionService(_logger, new ZonaMortaServiceFalsa());
    }

    public void Dispose() => AppConfig.Current = _configuracaoAnterior;

    private static Target AlvoEm(int id, double x, double y) => new()
    {
        Id = id,
        X = x,
        Y = y,
        Quadrant = QuadrantHelper.Determine(x, y),
        IsActive = true
    };

    [Fact]
    public void UmAlvoSozinho_UsaATorreDoProprioQuadrante()
    {
        Target alvo = AlvoEm(1, 1.98, 1.98); // Q1

        TowerSelectionResult resultado = _service.SelectTowerFor(alvo, new[] { alvo });

        Assert.True(resultado.Success);
        Assert.Equal(1, resultado.SelectedTower!.Id);
        Assert.Contains("próprio quadrante", resultado.Reason);
    }

    [Fact]
    public void DoisAlvosNoMesmoQuadrante_TorreAdjacenteLivreAjudaOSegundo()
    {
        Target alvo1 = AlvoEm(1, 1.98, 1.98);  // Q1 — Torre 1 fica comprometida com este
        Target alvo2 = AlvoEm(2, 0.50, 3.00);  // também Q1 — precisa de ajuda
        var ativos = new[] { alvo1, alvo2 };

        TowerSelectionResult primeiro = _service.SelectTowerFor(alvo1, ativos);
        TowerSelectionResult segundo = _service.SelectTowerFor(alvo2, ativos);

        Assert.True(primeiro.Success);
        Assert.Equal(1, primeiro.SelectedTower!.Id);

        Assert.True(segundo.Success);
        Assert.NotEqual(1, segundo.SelectedTower!.Id); // não pode ser a mesma torre do alvo 1
        Assert.True(QuadrantHelper.AreAdjacent(segundo.SelectedTower.PreferredQuadrant, Quadrant.Q1));
        Assert.Contains("adjacente", segundo.Reason);
    }

    [Fact]
    public void TorreAdjacenteOcupadaComAlvoDoProprioQuadrante_NaoAjuda_CaiParaCobertura()
    {
        // Q1 com 2 alvos (Torre 1 comprometida) e Q2 com o seu próprio alvo (Torre 2 comprometida
        // em casa) — só a diagonal (Torre 3) ou Q4 (Torre 4, adjacente livre) podem sobrar.
        Target alvoQ1A = AlvoEm(1, 1.98, 1.98);
        Target alvoQ1B = AlvoEm(2, 0.50, 3.00);
        Target alvoQ2 = AlvoEm(3, -2.00, 2.00);
        var ativos = new[] { alvoQ1A, alvoQ1B, alvoQ2 };

        _service.SelectTowerFor(alvoQ1A, ativos);
        _service.SelectTowerFor(alvoQ2, ativos);
        TowerSelectionResult segundoDeQ1 = _service.SelectTowerFor(alvoQ1B, ativos);

        Assert.True(segundoDeQ1.Success);
        Assert.NotEqual(1, segundoDeQ1.SelectedTower!.Id);
        Assert.NotEqual(2, segundoDeQ1.SelectedTower.Id); // Torre 2 está ocupada em casa com alvoQ2, não pode ajudar
    }

    [Fact]
    public void TorreDiagonalNuncaEscolhidaAntesDeEsgotarCasaEAdjacente()
    {
        // Alvo em Q1 com Torre 1 (casa) e Torre 2/Torre 4 (adjacentes) todas livres — a
        // diagonal (Torre 3, Q3) não deve ser considerada enquanto houver candidata melhor.
        Target alvo = AlvoEm(1, 1.98, 1.98);

        TowerSelectionResult resultado = _service.SelectTowerFor(alvo, new[] { alvo });

        Assert.True(resultado.Success);
        Assert.NotEqual(3, resultado.SelectedTower!.Id);
    }

    [Fact]
    public void AoSelecionarTorre_AtualizaOAnguloDeMiraDaTorre()
    {
        Target alvo = AlvoEm(1, 1.98, 1.98);

        TowerSelectionResult resultado = _service.SelectTowerFor(alvo, new[] { alvo });

        Assert.True(resultado.Success);
        double esperado = BearingCalculator.AngleTo(resultado.SelectedTower!, alvo);
        Assert.Equal(esperado, resultado.SelectedTower!.AimAngleDegrees, precision: 6);
    }
}
