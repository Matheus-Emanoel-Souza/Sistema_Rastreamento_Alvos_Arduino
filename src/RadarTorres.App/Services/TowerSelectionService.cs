using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RadarTorres.App.Configuration;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.NeuralEvolution;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementa o algoritmo de seleção de torre descrito no enunciado do TCC:
/// 1) considera apenas torres disponíveis; 2) prioriza torres cujo quadrante de cobertura
/// coincide com o do alvo; 3) dentre as candidatas, escolhe a melhor avaliada; 4) se nenhuma
/// torre do quadrante preferencial estiver disponível, cai para qualquer torre disponível do
/// sistema (para não deixar um alvo sem cobertura).
/// </summary>
/// <remarks>
/// Deliberadamente livre de qualquer referência a WPF/XAML — é pura lógica de negócio,
/// testável isoladamente e reaproveitável caso a interface mude no futuro.
/// A quantidade de torres não é fixa: é lida de <see cref="AppSettings.Towers"/>, então
/// adicionar/remover torres é apenas uma questão de editar o appsettings.json.
///
/// <para>
/// Dentro de cada camada, a escolha entre candidatas é feita por
/// <see cref="ControladorHibrido.Pontuar"/> — a mesma combinação heurística+rede neural
/// treinada por neuroevolução na tela "Treinamento do Controlador" (ver
/// <see cref="IControladorTreinadoProvider"/>) — e cai para a heurística pura (menor distância)
/// se nenhum controlador treinado estiver embutido. Isto decide SÓ qual torre persegue o alvo;
/// o disparo em si continua exclusivamente manual (<see cref="IFireControlService"/>), por
/// decisão de projeto — a rede neural não aciona nada por conta própria.
/// </para>
/// <para>
/// Além da camada heurística+rede, seis regras adicionais (anti-oscilação, priorização por
/// urgência, corredor de tiro, balanceamento de carga, predição de trajetória e ajuda de torre
/// adjacente contra alvo rápido) refinam a decisão — as cinco primeiras independentemente
/// desligáveis em <see cref="ConfiguracoesAplicativo"/> (tela de Configurações), para o usuário
/// poder isolar o efeito de cada regra ou voltar ao comportamento anterior sem recompilar; a
/// sexta (ajuda de torre adjacente) é sempre ativa, só o limiar de velocidade é ajustável.
/// </para>
/// </remarks>
public sealed class TowerSelectionService : ITowerSelectionService
{
    /// <summary>Mesmo padrão usado como velocidade máxima no ambiente de treinamento (ver <see cref="ParametrosTreinamento"/>) — só normaliza a entrada da rede.</summary>
    private const double VelocidadeMaximaAlvoMetrosPorSegundo = 2.5;

    /// <summary>Mesmo padrão usado como tempo de detecção relevante no ambiente de treinamento (<see cref="ParametrosTreinamento.TempoDeteccaoRelevanteSegundos"/>) — só normaliza a entrada da rede.</summary>
    private const double TempoDeteccaoRelevanteSegundos = 3.0;

    /// <summary>Diferença de pontuação (ou de distância, quando não há rede embutida) abaixo da qual duas candidatas são consideradas "empatadas" — usada tanto pela regra 1 (anti-oscilação) quanto pela 4 (balanceamento de carga).</summary>
    private const double MargemEmpateDistanciaMetros = 0.3;
    private const double MargemEmpatePontuacaoRede = 0.1;

    /// <summary>Regra 2 (priorização por urgência): um alvo é "urgente" até esta distância (múltiplo da distância mínima de segurança) — e só rouba a torre de outro alvo que esteja claramente mais longe que isso.</summary>
    private const double MultiplicadorZonaDeUrgencia = 1.5;
    private const double MargemParaRouboDeTorreMetros = 1.0;

    /// <summary>Regra 5 (predição de trajetória): quanto tempo à frente a mira aponta, com base na velocidade estimada do alvo.</summary>
    private const double TempoDePredicaoSegundos = 0.3;

    /// <summary>Regra 3 (corredor de tiro): pontos amostrados ao longo do segmento torre→alvo (excluindo as pontas) para checar se cruza uma zona morta ativa.</summary>
    private const int AmostrasDoCorredorDeTiro = 12;

    /// <summary>Regra 6 (ajuda de torre adjacente): raio (m) em torno do alvo dentro do qual uma torre de quadrante adjacente passa a engajar um alvo rápido, mesmo fora do alcance normal de câmera.</summary>
    private const double RaioAjudaTorreAdjacenteMetros = 2.0;

    private readonly ILoggingService _logger;
    private readonly IZonaMortaService _zonaMortaService;
    private readonly IControladorTreinadoProvider _controladorTreinado;
    private readonly IConfiguracoesAplicativoRepository _configuracoes;

    /// <summary>Regra 4 (balanceamento de carga): quantas vezes cada torre já foi escolhida nesta sessão — reinicia ao reiniciar o aplicativo, de propósito (é só um critério de desempate do momento, não uma métrica persistida).</summary>
    private readonly Dictionary<int, int> _usoPorTorre = new();

    public ObservableCollection<Tower> Towers { get; } = new();

    public TowerSelectionService(
        ILoggingService logger,
        IZonaMortaService zonaMortaService,
        IControladorTreinadoProvider controladorTreinado,
        IConfiguracoesAplicativoRepository configuracoes)
    {
        _logger = logger;
        _zonaMortaService = zonaMortaService;
        _controladorTreinado = controladorTreinado;
        _configuracoes = configuracoes;
        LoadTowersFromConfig();
    }

    private void LoadTowersFromConfig()
    {
        Towers.Clear();
        foreach (TowerDefinition def in AppConfig.Current.Towers)
        {
            var tower = new Tower
            {
                Id = def.Id,
                Name = def.Name,
                X = def.X,
                Y = def.Y,
                PreferredQuadrant = QuadrantHelper.Determine(def.X, def.Y)
            };
            Towers.Add(tower);
        }
    }

    public TowerSelectionResult SelectTowerFor(Target target, IEnumerable<Target> activeTargets)
    {
        ZonaMorta? blockingZone = _zonaMortaService.FindBlockingZone(target);
        if (blockingZone is not null)
        {
            string zoneReason = $"ALVO EM ZONA MORTA ({blockingZone.Name} — {blockingZone.Description}) — NENHUMA TORRE SELECIONADA";
            _logger.Warning($"Alvo #{target.Id:D2}: {zoneReason}");
            target.SelectedTower = null;
            target.DistanceToSelectedTower = 0;
            target.EngagingTowers = [];
            return new TowerSelectionResult(false, null, 0, zoneReason);
        }

        List<Tower> available = Towers.Where(t => t.IsAvailable && t.State != TowerState.Offline && t.State != TowerState.Unavailable).ToList();

        if (available.Count == 0)
        {
            const string reason = "NENHUMA TORRE DISPONÍVEL";
            _logger.Warning($"Alvo #{target.Id:D2}: {reason}");
            target.SelectedTower = null;
            target.DistanceToSelectedTower = 0;
            target.EngagingTowers = [];
            return new TowerSelectionResult(false, null, 0, reason);
        }

        List<Target> others = activeTargets.Where(t => t.IsActive && t.Id != target.Id).ToList();

        // Exclusividade: uma torre só aponta para um lugar por vez, então não pode ser escolhida
        // para dois alvos simultaneamente — a torre já comprometida com OUTRO alvo sai das
        // candidatas. EXCEÇÃO (regra 2 — priorização por urgência, se ativa): um alvo claramente
        // mais urgente (perto da distância mínima de segurança) pode tomar a torre de um alvo
        // bem menos urgente — o alvo "roubado" simplesmente procura outra torre na próxima
        // leitura (ele não fica sem cobertura permanentemente, só perde a vez desta rodada).
        double distanciaMinimaSeguranca = AppConfig.Current.RadarSettings.MinSafetyDistanceMeters;
        double limiarUrgencia = distanciaMinimaSeguranca * MultiplicadorZonaDeUrgencia;
        bool alvoEhUrgente = target.Distance <= limiarUrgencia;
        bool priorizacaoPorUrgenciaAtiva = _configuracoes.Atual.HeuristicaPriorizacaoPorUrgenciaAtiva;

        var committed = new HashSet<int>();
        foreach (Target outro in others)
        {
            if (outro.SelectedTower is not { } torreDoOutro) continue;

            bool podeRoubar = priorizacaoPorUrgenciaAtiva
                && alvoEhUrgente
                && outro.Distance > limiarUrgencia
                && outro.Distance > target.Distance + MargemParaRouboDeTorreMetros;

            if (podeRoubar)
            {
                _logger.Info($"Alvo #{target.Id:D2} (urgente, {target.Distance:0.00} m) tomou a torre {torreDoOutro.Name} do alvo #{outro.Id:D2} (menos urgente, {outro.Distance:0.00} m)");
                continue;
            }

            committed.Add(torreDoOutro.Id);
        }

        List<Tower> free = available.Where(t => !committed.Contains(t.Id)).ToList();

        // Quadrantes com pelo menos um alvo ativo que NÃO seja este — usado para saber se a torre
        // "de casa" de um quadrante vizinho está livre para ajudar (não precisa ficar na própria área).
        HashSet<Quadrant> quadrantsWithOtherTargets = others.Select(t => t.Quadrant).ToHashSet();

        // Camada 1: torre(s) do próprio quadrante do alvo, livres.
        List<Tower> home = free.Where(t => t.PreferredQuadrant == target.Quadrant).ToList();

        // Camada 2: torres de quadrante adjacente, livres, cujo próprio quadrante não tem alvo
        // concorrente agora (só ajudam quem não precisa delas em casa).
        List<Tower> adjacent = home.Count > 0
            ? []
            : free.Where(t =>
                QuadrantHelper.AreAdjacent(t.PreferredQuadrant, target.Quadrant) &&
                !quadrantsWithOtherTargets.Contains(t.PreferredQuadrant)).ToList();

        // Camada 3: qualquer torre livre restante (inclusive diagonal) — a que sobrar cobre.
        List<Tower> coverage = home.Count == 0 && adjacent.Count == 0 ? free : [];

        (List<Tower> candidates, string layerLabel) = home.Count > 0
            ? (home, "torre do próprio quadrante")
            : adjacent.Count > 0
                ? (adjacent, $"torre adjacente cobrindo {QuadrantHelper.ToDisplayLabel(target.Quadrant)}")
                : coverage.Count > 0
                    ? (coverage, "cobertura — torre sobressalente")
                    : (available, "sobrecarregada — nenhuma torre livre");

        (Tower? best, double bestDistance) = EscolherMelhorCandidata(candidates, target, fatorCamada: home.Count > 0 ? 1.0 : adjacent.Count > 0 ? 0.5 : coverage.Count > 0 ? 0.0 : -1.0);

        if (best is null)
        {
            const string reason = "NENHUMA TORRE ADEQUADA ENCONTRADA";
            _logger.Warning($"Alvo #{target.Id:D2}: {reason}");
            target.SelectedTower = null;
            target.DistanceToSelectedTower = 0;
            target.EngagingTowers = [];
            return new TowerSelectionResult(false, null, 0, reason);
        }

        target.SelectedTower = best;
        target.DistanceToSelectedTower = bestDistance;
        best.AimAngleDegrees = CalcularAnguloDeMira(best, target);
        target.EngagingTowers = ComputarTorresEngajando(target, available);

        _logger.Info($"{best.Name} selecionada para o alvo #{target.Id:D2} ({layerLabel}, dist={bestDistance:0.00} m, mira={best.AimAngleDegrees:0.0}°)");

        return new TowerSelectionResult(true, best, bestDistance, $"OK ({layerLabel})");
    }

    /// <summary>
    /// Todas as torres autorizadas a abrir fogo contra este alvo agora — não só a "selecionada"
    /// (<see cref="Target.SelectedTower"/>, que é só a torre principal/mira). Uma torre entra se
    /// sua própria câmera consegue ver o alvo: do quadrante do próprio alvo (câmera de casa
    /// sempre vê), ou de um quadrante vizinho desde que o alvo esteja dentro do alcance máximo
    /// de detecção da câmera (<see cref="RadarSettings.MaxDetectionDistanceMeters"/>) — câmeras
    /// diferentes podem identificar e disparar no MESMO alvo ao mesmo tempo. Fica de fora quem
    /// tiver outro canhão literalmente na frente da linha de tiro.
    /// </summary>
    /// <remarks>
    /// Regra 6 (ajuda de torre adjacente): alvos acima de
    /// <see cref="ConfiguracoesAplicativo.VelocidadeRapidaMetrosPorSegundo"/> puxam para a lista
    /// qualquer torre de quadrante adjacente a até <see cref="RaioAjudaTorreAdjacenteMetros"/> do
    /// alvo, mesmo fora do alcance normal de câmera — um segundo canhão reduz a chance de o alvo
    /// escapar de um disparo único (a chance de errar cresce com a velocidade, ver
    /// <see cref="Helpers.CalculadoraErroDisparo"/>).
    /// </remarks>
    private List<Tower> ComputarTorresEngajando(Target target, List<Tower> available)
    {
        double alcanceCameraMetros = _configuracoes.Atual.AlcanceMiraMetros;

        List<Tower> candidatas = available.Where(t =>
            t.PreferredQuadrant == target.Quadrant ||
            (QuadrantHelper.AreAdjacent(t.PreferredQuadrant, target.Quadrant) && target.Distance <= alcanceCameraMetros))
            .ToList();

        if (target.SpeedMetersPerSecond >= _configuracoes.Atual.VelocidadeRapidaMetrosPorSegundo)
        {
            IEnumerable<Tower> ajuda = available.Where(t =>
                candidatas.All(c => c.Id != t.Id) &&
                QuadrantHelper.AreAdjacent(t.PreferredQuadrant, target.Quadrant) &&
                DistanceCalculator.Between(target, t) <= RaioAjudaTorreAdjacenteMetros);

            candidatas = candidatas.Concat(ajuda).ToList();
        }

        return candidatas.Where(t => !OutraTorreNaFrenteDoTiro(t, target, available)).ToList();
    }

    /// <summary>
    /// Verdadeiro se outra torre qualquer (não só as engajando este alvo) estiver posicionada
    /// entre <paramref name="torre"/> e o alvo, dentro de um corredor estreito — ela literalmente
    /// bloquearia o tiro de <paramref name="torre"/> em direção ao alvo.
    /// </summary>
    private static bool OutraTorreNaFrenteDoTiro(Tower torre, Target alvo, IReadOnlyList<Tower> todasTorres)
    {
        const double larguraCorredorMetros = 0.3;

        double dx = alvo.X - torre.X;
        double dy = alvo.Y - torre.Y;
        double comprimentoQuadrado = dx * dx + dy * dy;
        if (comprimentoQuadrado < 0.0001) return false;

        foreach (Tower outra in todasTorres)
        {
            if (outra.Id == torre.Id) continue;

            double t = ((outra.X - torre.X) * dx + (outra.Y - torre.Y) * dy) / comprimentoQuadrado;
            if (t <= 0.05 || t >= 0.95) continue; // fora do segmento torre→alvo (antes da torre ou depois do alvo)

            double projX = torre.X + dx * t;
            double projY = torre.Y + dy * t;
            double distanciaPerpendicular = DistanceCalculator.Euclidean(outra.X, outra.Y, projX, projY);

            if (distanciaPerpendicular <= larguraCorredorMetros) return true;
        }

        return false;
    }

    /// <summary>
    /// Escolhe a melhor torre dentro de UMA camada já definida (própria/adjacente/sobressalente)
    /// — usa a rede treinada quando disponível (<see cref="ControladorHibrido.Pontuar"/>, mesma
    /// combinação heurística+rede da tela de treinamento), senão a heurística pura de menor
    /// distância (comportamento anterior, idêntico ao que a rede produz quando o genoma embutido
    /// é o zerado de fábrica). Depois de achar a vencedora "crua", aplica a regra 4
    /// (balanceamento de carga) e a regra 1 (anti-oscilação), cada uma só se estiver ativa.
    /// </summary>
    private (Tower? Best, double BestDistance) EscolherMelhorCandidata(List<Tower> candidatasOriginais, Target target, double fatorCamada)
    {
        ConfiguracoesAplicativo config = _configuracoes.Atual;
        RedeNeural? rede = _controladorTreinado.Rede;
        double raioMundoMetros = config.AlcanceMiraMetros;

        // Regra 3 — corredor de tiro: descarta candidatas cuja linha até o alvo atravesse uma
        // zona morta ativa. Nunca deixa o alvo sem NENHUMA candidata só por causa disto — melhor
        // arriscar com a torre menos ideal do que deixar de cobrir o alvo inteiramente.
        List<Tower> candidatas = candidatasOriginais;
        if (config.HeuristicaCorredorDeTiroAtiva)
        {
            List<Tower> semCorredorBloqueado = candidatasOriginais.Where(t => !LinhaDeTiroCruzaZonaMorta(t, target)).ToList();
            if (semCorredorBloqueado.Count > 0) candidatas = semCorredorBloqueado;
        }

        var pontuacoes = new Dictionary<int, double>();
        Tower? melhor = null;
        double melhorPontuacao = double.NegativeInfinity;

        foreach (Tower candidate in candidatas)
        {
            double distance = DistanceCalculator.Between(target, candidate);
            candidate.DistanceToTarget = distance;

            // Pontuação unificada: sem rede, "quanto menor a distância, melhor" se torna
            // -distância, na MESMA escala (metros) que a margem de empate abaixo usa.
            double pontuacao;
            if (rede is null)
            {
                pontuacao = -distance;
            }
            else
            {
                double[] entradas = ControladorHibrido.ConstruirEntradas(
                    distanciaMetros: distance,
                    raioMundoMetros: raioMundoMetros,
                    mesmoQuadrante: candidate.PreferredQuadrant == target.Quadrant,
                    quadranteAdjacente: QuadrantHelper.AreAdjacent(candidate.PreferredQuadrant, target.Quadrant),
                    anguloGraus: BearingCalculator.AngleTo(candidate, target),
                    velocidadeMetrosPorSegundo: target.SpeedMetersPerSecond,
                    velocidadeMaximaMetrosPorSegundo: VelocidadeMaximaAlvoMetrosPorSegundo,
                    // Mesma grandeza usada no treinamento (AlvoTreinamento.TempoDeVidaSegundos):
                    // tempo desde a PRIMEIRA detecção deste alvo, não desde a seleção de torre.
                    tempoDeteccaoSegundos: (DateTime.UtcNow - target.Timestamp).TotalSeconds,
                    tempoDeteccaoRelevanteSegundos: TempoDeteccaoRelevanteSegundos,
                    fatorCamada: fatorCamada);

                pontuacao = ControladorHibrido.Pontuar(rede, entradas, fatorCamada);
            }

            pontuacoes[candidate.Id] = pontuacao;
            if (pontuacao > melhorPontuacao)
            {
                melhorPontuacao = pontuacao;
                melhor = candidate;
            }
        }

        if (melhor is null) return (null, 0);

        double margemEmpate = rede is null ? MargemEmpateDistanciaMetros : MargemEmpatePontuacaoRede;

        // Regra 4 — balanceamento de carga: entre as candidatas praticamente empatadas com a
        // vencedora, prefere a usada com menos frequência nesta sessão em vez de sempre a mesma.
        if (config.HeuristicaBalanceamentoDeCargaAtiva)
        {
            List<Tower> empatadas = candidatas.Where(t => melhorPontuacao - pontuacoes[t.Id] <= margemEmpate).ToList();
            if (empatadas.Count > 1)
            {
                melhor = empatadas.OrderBy(t => _usoPorTorre.GetValueOrDefault(t.Id)).ThenBy(t => t.Id).First();
            }
        }

        // Regra 1 — anti-oscilação: se a torre já comprometida com este alvo ainda é uma
        // candidata válida e está dentro da margem de empate da vencedora, mantém — evita
        // trocar de torre por uma diferença irrelevante causada por ruído na leitura.
        if (config.HeuristicaAntiOscilacaoAtiva
            && target.SelectedTower is { } torreAtual
            && candidatas.FirstOrDefault(t => t.Id == torreAtual.Id) is { } aindaCandidata
            && melhorPontuacao - pontuacoes[aindaCandidata.Id] <= margemEmpate)
        {
            melhor = aindaCandidata;
        }

        _usoPorTorre[melhor.Id] = _usoPorTorre.GetValueOrDefault(melhor.Id) + 1;

        return (melhor, DistanceCalculator.Between(target, melhor));
    }

    /// <summary>
    /// Regra 3 (corredor de tiro): verdadeiro se o segmento entre a torre e o alvo atravessa
    /// alguma zona morta ativa — amostra pontos ao longo do segmento (excluindo as pontas, já
    /// cobertas por outras checagens) e testa cada um contra as zonas como
    /// <see cref="IZonaMortaService.FindBlockingZone"/> testaria um alvo ali.
    /// </summary>
    private bool LinhaDeTiroCruzaZonaMorta(Tower torre, Target alvo)
    {
        for (int i = 1; i < AmostrasDoCorredorDeTiro; i++)
        {
            double t = (double)i / AmostrasDoCorredorDeTiro;
            double x = torre.X + (alvo.X - torre.X) * t;
            double y = torre.Y + (alvo.Y - torre.Y) * t;
            double distanciaDaBase = DistanceCalculator.Euclidean(0, 0, x, y);
            Quadrant quadrante = QuadrantHelper.Determine(x, y);

            foreach (ZonaMorta zona in _zonaMortaService.Zones)
            {
                if (!zona.Enabled) continue;

                bool bloqueado = zona.Type == ZonaMortaType.Quadrant
                    ? zona.Quadrant == quadrante
                    : distanciaDaBase >= zona.MinDistance && distanciaDaBase <= zona.MaxDistance;

                if (bloqueado) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Regra 5 (predição de trajetória): aponta para onde o alvo estará
    /// <see cref="TempoDePredicaoSegundos"/> à frente, com base na velocidade estimada — não
    /// quando desativada, ou quando não há velocidade estimada ainda (alvo recém-detectado).
    /// </summary>
    private double CalcularAnguloDeMira(Tower torre, Target alvo)
    {
        double x = alvo.X;
        double y = alvo.Y;

        if (_configuracoes.Atual.HeuristicaPredicaoDeTrajetoriaAtiva)
        {
            x += alvo.VelocityXMetersPerSecond * TempoDePredicaoSegundos;
            y += alvo.VelocityYMetersPerSecond * TempoDePredicaoSegundos;
        }

        return (Math.Atan2(x - torre.X, y - torre.Y) * 180.0 / Math.PI + 360.0) % 360.0;
    }

    public void RecomputeTowerStates(IEnumerable<Target> activeTargets)
    {
        var selectedTowerIds = activeTargets
            .Where(t => t.IsActive && t.SelectedTower is not null)
            .Select(t => t.SelectedTower!.Id)
            .ToHashSet();

        foreach (Tower tower in Towers)
        {
            if (tower.State is TowerState.Unavailable or TowerState.Offline or TowerState.Firing)
            {
                continue; // Estados administrativos ou de acionamento em andamento não são sobrescritos aqui.
            }

            tower.State = selectedTowerIds.Contains(tower.Id) ? TowerState.Selected : TowerState.Idle;
        }
    }
}
