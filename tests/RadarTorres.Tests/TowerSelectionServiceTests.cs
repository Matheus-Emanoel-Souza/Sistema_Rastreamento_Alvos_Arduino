using RadarTorres.App.Configuration;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.NeuralEvolution;
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
    private readonly ZonaMortaServiceFalsa _zonaMortaService = new();
    private readonly ConfiguracoesAplicativoRepositorioFalso _configuracoes = new();
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

        _service = new TowerSelectionService(_logger, _zonaMortaService, new ControladorTreinadoProviderFalso(), _configuracoes);
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

    /// <summary>
    /// Garante a premissa de compatibilidade do controlador treinado: um genoma zerado (padrão
    /// de fábrica, antes de qualquer treinamento) produz EXATAMENTE a mesma escolha que a
    /// heurística pura — porque com pesos/bias zerados a rede sempre devolve ajuste 0, e dentro
    /// de uma camada o fator heurístico é igual para todas as candidatas, então a pontuação
    /// final continua ordenada só pela distância (ver ControladorHibrido.Pontuar).
    /// </summary>
    [Fact]
    public void ComGenomaZeradoEmbutido_EscolheAMesmaTorreQueAHeuristicaPura()
    {
        var redeZerada = new RedeNeural(ControladorHibrido.ArquiteturaPadrao, new double[RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao)]);
        var servicoComRede = new TowerSelectionService(_logger, new ZonaMortaServiceFalsa(), new ControladorTreinadoProviderFalso { Rede = redeZerada }, new ConfiguracoesAplicativoRepositorioFalso());

        Target alvo1 = AlvoEm(1, 1.98, 1.98);  // Q1
        Target alvo2 = AlvoEm(2, 0.50, 3.00);  // também Q1 — precisa de ajuda (camada adjacente com 2 candidatas)
        var ativos = new[] { alvo1, alvo2 };

        servicoComRede.SelectTowerFor(alvo1, ativos);
        TowerSelectionResult comRede = servicoComRede.SelectTowerFor(alvo2, ativos);

        Target alvo1Puro = AlvoEm(1, 1.98, 1.98);
        Target alvo2Puro = AlvoEm(2, 0.50, 3.00);
        var ativosPuro = new[] { alvo1Puro, alvo2Puro };
        _service.SelectTowerFor(alvo1Puro, ativosPuro);
        TowerSelectionResult puro = _service.SelectTowerFor(alvo2Puro, ativosPuro);

        Assert.Equal(puro.SelectedTower!.Id, comRede.SelectedTower!.Id);
    }

    // ---------------------------------------------------------------- Regra 1 — anti-oscilação

    [Fact]
    public void AntiOscilacaoAtiva_MantemATorreJaComprometidaQuandoADiferencaEIrrelevante()
    {
        _service.Towers.Clear();
        var torreA = new Tower { Id = 10, Name = "A", X = 0.5, Y = 3.0, PreferredQuadrant = Quadrant.Q1 };
        var torreB = new Tower { Id = 11, Name = "B", X = 3.0, Y = 0.5, PreferredQuadrant = Quadrant.Q1 };
        _service.Towers.Add(torreA);
        _service.Towers.Add(torreB);

        Target alvo = AlvoEm(1, 0.5, 2.0); // bem mais perto de A — primeira escolha não tem ambiguidade
        _service.SelectTowerFor(alvo, new[] { alvo });
        Assert.Equal(10, alvo.SelectedTower!.Id);

        alvo.X = 1.6; alvo.Y = 1.5; // agora B é um pouco mais próximo, mas dentro da margem de empate
        TowerSelectionResult resultado = _service.SelectTowerFor(alvo, new[] { alvo });

        Assert.Equal(10, resultado.SelectedTower!.Id); // continua com A
    }

    [Fact]
    public void AntiOscilacaoDesativada_TrocaParaATorreMarginalmenteMelhor()
    {
        _configuracoes.Atual.HeuristicaAntiOscilacaoAtiva = false;

        _service.Towers.Clear();
        var torreA = new Tower { Id = 10, Name = "A", X = 0.5, Y = 3.0, PreferredQuadrant = Quadrant.Q1 };
        var torreB = new Tower { Id = 11, Name = "B", X = 3.0, Y = 0.5, PreferredQuadrant = Quadrant.Q1 };
        _service.Towers.Add(torreA);
        _service.Towers.Add(torreB);

        Target alvo = AlvoEm(1, 0.5, 2.0);
        _service.SelectTowerFor(alvo, new[] { alvo });

        alvo.X = 1.6; alvo.Y = 1.5;
        TowerSelectionResult resultado = _service.SelectTowerFor(alvo, new[] { alvo });

        Assert.Equal(11, resultado.SelectedTower!.Id); // sem a regra, troca para a marginalmente melhor
    }

    // ---------------------------------------------------------------- Regra 2 — priorização por urgência

    [Fact]
    public void PriorizacaoPorUrgencia_AlvoUrgenteTomaATorreDeAlvoBemMenosUrgente()
    {
        _service.Towers.Clear();
        var torreCasa = new Tower { Id = 20, Name = "Casa", X = 0.1, Y = 0.1, PreferredQuadrant = Quadrant.Q1 };
        var torreDiagonal = new Tower { Id = 21, Name = "Diagonal", X = -0.1, Y = -0.1, PreferredQuadrant = Quadrant.Q3 };
        _service.Towers.Add(torreCasa);
        _service.Towers.Add(torreDiagonal);

        Target alvoLonge = AlvoEm(1, 0.1, 4.0);
        alvoLonge.Distance = 4.0; // bem acima do limiar de urgência (1.5 * 1.5 = 2.25 m)

        Target alvoUrgente = AlvoEm(2, 0.1, 1.0);
        alvoUrgente.Distance = 0.9; // dentro do limiar de urgência

        var ativos = new[] { alvoLonge, alvoUrgente };
        _service.SelectTowerFor(alvoLonge, ativos);
        Assert.Equal(20, alvoLonge.SelectedTower!.Id);

        TowerSelectionResult resultado = _service.SelectTowerFor(alvoUrgente, ativos);

        Assert.Equal(20, resultado.SelectedTower!.Id); // tomou a torre "de casa", em vez de cair para a diagonal
        Assert.Contains("próprio quadrante", resultado.Reason);
        Assert.Contains(_logger.Mensagens, m => m.Contains("tomou a torre"));
    }

    [Fact]
    public void PriorizacaoPorUrgenciaDesativada_AlvoUrgenteNaoTomaATorreDoOutro()
    {
        _configuracoes.Atual.HeuristicaPriorizacaoPorUrgenciaAtiva = false;

        _service.Towers.Clear();
        var torreCasa = new Tower { Id = 20, Name = "Casa", X = 0.1, Y = 0.1, PreferredQuadrant = Quadrant.Q1 };
        var torreDiagonal = new Tower { Id = 21, Name = "Diagonal", X = -0.1, Y = -0.1, PreferredQuadrant = Quadrant.Q3 };
        _service.Towers.Add(torreCasa);
        _service.Towers.Add(torreDiagonal);

        Target alvoLonge = AlvoEm(1, 0.1, 4.0);
        alvoLonge.Distance = 4.0;

        Target alvoUrgente = AlvoEm(2, 0.1, 1.0);
        alvoUrgente.Distance = 0.9;

        var ativos = new[] { alvoLonge, alvoUrgente };
        _service.SelectTowerFor(alvoLonge, ativos);

        TowerSelectionResult resultado = _service.SelectTowerFor(alvoUrgente, ativos);

        Assert.Equal(21, resultado.SelectedTower!.Id); // casa está ocupada e não pode ser tomada — cai para a diagonal
        Assert.DoesNotContain(_logger.Mensagens, m => m.Contains("tomou a torre"));
    }

    // ---------------------------------------------------------------- Regra 3 — corredor de tiro

    [Fact]
    public void CorredorDeTiro_DescartaATorreCujaLinhaDeTiroCruzaUmaZonaMortaAtiva()
    {
        _zonaMortaService.Zones.Add(new ZonaMorta { Id = 1, Name = "Bloqueio Q4", Type = ZonaMortaType.Quadrant, Quadrant = Quadrant.Q4, Enabled = true });

        _service.Towers.Clear();
        // Fisicamente do outro lado da base (Q4) — mais perto do alvo, mas a linha até ele cruza Q4.
        var torreBloqueada = new Tower { Id = 30, Name = "Bloqueada", X = 1.0, Y = -0.5, PreferredQuadrant = Quadrant.Q1 };
        // Mais longe, mas o caminho nunca sai do Q1 — corredor livre.
        var torreLivre = new Tower { Id = 31, Name = "Livre", X = 3.0, Y = 3.0, PreferredQuadrant = Quadrant.Q1 };
        _service.Towers.Add(torreBloqueada);
        _service.Towers.Add(torreLivre);

        Target alvo = AlvoEm(1, 1.0, 0.5);

        TowerSelectionResult resultado = _service.SelectTowerFor(alvo, new[] { alvo });

        Assert.Equal(31, resultado.SelectedTower!.Id);
    }

    [Fact]
    public void CorredorDeTiroDesativado_EscolheATorreMaisPertoMesmoComALinhaCruzandoUmaZonaMorta()
    {
        _configuracoes.Atual.HeuristicaCorredorDeTiroAtiva = false;
        _zonaMortaService.Zones.Add(new ZonaMorta { Id = 1, Name = "Bloqueio Q4", Type = ZonaMortaType.Quadrant, Quadrant = Quadrant.Q4, Enabled = true });

        _service.Towers.Clear();
        var torreBloqueada = new Tower { Id = 30, Name = "Bloqueada", X = 1.0, Y = -0.5, PreferredQuadrant = Quadrant.Q1 };
        var torreLivre = new Tower { Id = 31, Name = "Livre", X = 3.0, Y = 3.0, PreferredQuadrant = Quadrant.Q1 };
        _service.Towers.Add(torreBloqueada);
        _service.Towers.Add(torreLivre);

        Target alvo = AlvoEm(1, 1.0, 0.5);

        TowerSelectionResult resultado = _service.SelectTowerFor(alvo, new[] { alvo });

        Assert.Equal(30, resultado.SelectedTower!.Id); // sem a regra, vence a mais próxima mesmo cruzando a zona morta
    }

    // ---------------------------------------------------------------- Regra 4 — balanceamento de carga

    [Fact]
    public void BalanceamentoDeCargaAtivo_AlternaEntreTorresPraticamenteEmpatadas()
    {
        _service.Towers.Clear();
        var torreA = new Tower { Id = 40, Name = "A", X = 1.00, Y = 1.00, PreferredQuadrant = Quadrant.Q1 };
        var torreB = new Tower { Id = 41, Name = "B", X = 1.03, Y = 1.00, PreferredQuadrant = Quadrant.Q1 };
        _service.Towers.Add(torreA);
        _service.Towers.Add(torreB);

        int EscolherParaNovoAlvo(int id)
        {
            Target alvo = AlvoEm(id, 5.0, 5.0);
            return _service.SelectTowerFor(alvo, new[] { alvo }).SelectedTower!.Id;
        }

        Assert.Equal(40, EscolherParaNovoAlvo(1)); // A, levemente mais próxima
        Assert.Equal(41, EscolherParaNovoAlvo(2)); // B, porque A já foi usada uma vez
        Assert.Equal(40, EscolherParaNovoAlvo(3)); // A de novo — empate 1 a 1, desempate por Id
    }

    [Fact]
    public void BalanceamentoDeCargaDesativado_SempreEscolheAMesmaTorreMarginalmenteMelhor()
    {
        _configuracoes.Atual.HeuristicaBalanceamentoDeCargaAtiva = false;

        _service.Towers.Clear();
        var torreA = new Tower { Id = 40, Name = "A", X = 1.00, Y = 1.00, PreferredQuadrant = Quadrant.Q1 };
        var torreB = new Tower { Id = 41, Name = "B", X = 1.03, Y = 1.00, PreferredQuadrant = Quadrant.Q1 };
        _service.Towers.Add(torreA);
        _service.Towers.Add(torreB);

        int EscolherParaNovoAlvo(int id)
        {
            Target alvo = AlvoEm(id, 5.0, 5.0);
            return _service.SelectTowerFor(alvo, new[] { alvo }).SelectedTower!.Id;
        }

        // Sem a regra, vence sempre a mesma (B, marginalmente mais próxima) — nunca alterna.
        Assert.Equal(41, EscolherParaNovoAlvo(1));
        Assert.Equal(41, EscolherParaNovoAlvo(2));
        Assert.Equal(41, EscolherParaNovoAlvo(3));
    }

    // ---------------------------------------------------------------- Regra 5 — predição de trajetória

    [Fact]
    public void PredicaoDeTrajetoriaAtiva_MiraParaOndeOAlvoEstaraNaoParaOndeEstaAgora()
    {
        _service.Towers.Clear();
        _service.Towers.Add(new Tower { Id = 50, Name = "Única", X = 0.1, Y = -5.0, PreferredQuadrant = Quadrant.Q4 });

        Target alvo = AlvoEm(1, 0.1, 5.0);
        alvo.VelocityXMetersPerSecond = 5.0;
        alvo.VelocityYMetersPerSecond = 0.0;

        TowerSelectionResult resultado = _service.SelectTowerFor(alvo, new[] { alvo });

        Assert.True(resultado.SelectedTower!.AimAngleDegrees > 1.0); // alvo se movendo para X positivo -> mira desvia para leste
    }

    [Fact]
    public void PredicaoDeTrajetoriaDesativada_MiraParaAPosicaoAtualDoAlvo()
    {
        _configuracoes.Atual.HeuristicaPredicaoDeTrajetoriaAtiva = false;

        _service.Towers.Clear();
        _service.Towers.Add(new Tower { Id = 50, Name = "Única", X = 0.1, Y = -5.0, PreferredQuadrant = Quadrant.Q4 });

        Target alvo = AlvoEm(1, 0.1, 5.0);
        alvo.VelocityXMetersPerSecond = 5.0;
        alvo.VelocityYMetersPerSecond = 0.0;

        TowerSelectionResult resultado = _service.SelectTowerFor(alvo, new[] { alvo });

        Assert.Equal(0.0, resultado.SelectedTower!.AimAngleDegrees, precision: 3); // direto para o norte, sem desvio
    }
}
