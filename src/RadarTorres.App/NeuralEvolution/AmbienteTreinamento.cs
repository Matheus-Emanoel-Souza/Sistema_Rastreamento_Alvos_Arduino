using System;
using System.Collections.Generic;
using System.Linq;
using RadarTorres.App.Configuration;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// Mundo 2D de um episódio de treinamento: torres fixas (mesma disposição do sistema real,
/// lida de <see cref="AppConfig"/>) e alvos que nascem na borda e avançam em direção à base.
/// Expõe duas formas de uso sobre a MESMA lógica de passo (<see cref="Avancar"/>), para que a
/// reprodução visual na tela seja garantidamente fiel ao que o algoritmo genético avaliou:
/// <list type="bullet">
/// <item><see cref="ExecutarEpisodioRapido"/> — roda um episódio inteiro de uma vez (sem UI), usado como função de fitness.</item>
/// <item><see cref="NovoEpisodio"/> + <see cref="Avancar"/> — avança um passo por vez, usado pela tela para animar o melhor genoma.</item>
/// </list>
/// </summary>
public sealed class AmbienteTreinamento
{
    private readonly List<TorreTreinamento> _torresBase;
    private readonly ParametrosTreinamento _parametros;

    public ParametrosTreinamento Parametros => _parametros;

    /// <summary>Torres na posição base (sem nenhum alvo comprometido) — usado pela tela para desenhar as posições fixas.</summary>
    public IReadOnlyList<TorreTreinamento> TorresBase => _torresBase;

    public AmbienteTreinamento(ParametrosTreinamento? parametros = null, IReadOnlyList<TowerDefinition>? torres = null)
    {
        _parametros = parametros ?? new ParametrosTreinamento();
        _torresBase = CarregarTorres(torres ?? AppConfig.Current.Towers);
    }

    private static List<TorreTreinamento> CarregarTorres(IReadOnlyList<TowerDefinition> definicoes)
    {
        if (definicoes.Count == 0)
        {
            // Sem appsettings.json carregado (ex.: testes automatizados) — layout padrão de 4
            // torres, uma por quadrante, para o ambiente continuar utilizável isoladamente.
            definicoes =
            [
                new TowerDefinition { Id = 1, Name = "Torre 1", X = 5, Y = 5 },
                new TowerDefinition { Id = 2, Name = "Torre 2", X = -5, Y = 5 },
                new TowerDefinition { Id = 3, Name = "Torre 3", X = -5, Y = -5 },
                new TowerDefinition { Id = 4, Name = "Torre 4", X = 5, Y = -5 },
            ];
        }

        return definicoes
            .Select(d => new TorreTreinamento
            {
                Id = d.Id,
                X = d.X,
                Y = d.Y,
                QuadrantePreferido = QuadrantHelper.Determine(d.X, d.Y)
            })
            .ToList();
    }

    /// <summary>
    /// Cria um episódio novo (torres na posição base, sem alvos) — opcionalmente reproduzível via
    /// <paramref name="seed"/>. <paramref name="cenario"/> troca o nascimento aleatório padrão por
    /// um script determinístico de um caso tático específico (ver <see cref="CatalogoCenarios"/>);
    /// <see cref="TipoCenarioTreinamento.Aleatorio"/> (padrão) mantém o comportamento histórico.
    /// </summary>
    public EstadoEpisodio NovoEpisodio(int? seed = null, TipoCenarioTreinamento cenario = TipoCenarioTreinamento.Aleatorio)
    {
        EstadoEpisodio estado = CriarEstadoComTorres(seed);

        IReadOnlyList<SpawnScriptado>? spawns = CatalogoCenarios.ObterSpawns(cenario);
        estado.SpawnsScriptados = spawns;
        estado.ZonaMortaForcadaQuadrante = CatalogoCenarios.QuadranteZonaMortaParaForcar(cenario);
        estado.ProximoSpawnSegundos = spawns is null
            ? ProximoIntervaloSpawn(estado)
            : spawns.Count > 0 ? spawns[0].InstanteSegundos : double.MaxValue;

        return estado;
    }

    /// <summary>
    /// Cria um episódio com nascimentos 100% definidos por <paramref name="spawns"/> — usado pela
    /// tela quando o usuário desenha o próprio cenário (posição e velocidade de cada alvo, sem
    /// nenhum sorteio), em vez de escolher um caso já catalogado em <see cref="CatalogoCenarios"/>.
    /// </summary>
    public EstadoEpisodio NovoEpisodioComSpawnsPersonalizados(IReadOnlyList<SpawnScriptado> spawns, int? seed = null)
    {
        EstadoEpisodio estado = CriarEstadoComTorres(seed);

        estado.SpawnsScriptados = spawns;
        estado.ZonaMortaForcadaQuadrante = null;
        estado.ProximoSpawnSegundos = spawns.Count > 0 ? spawns[0].InstanteSegundos : double.MaxValue;

        return estado;
    }

    private EstadoEpisodio CriarEstadoComTorres(int? seed)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var estado = new EstadoEpisodio(rng);

        foreach (TorreTreinamento baseTorre in _torresBase)
        {
            // Mira física inicial "voltada para fora da base" — mesma convenção do campo de
            // visão fixo da torre (ver TreinamentoControladorViewModel.AnguloCampoDeVisaoGraus) —
            // para não começar o episódio já com um giro grande pendente rumo ao primeiro alvo.
            double azimuteInicial = (Math.Atan2(baseTorre.X, baseTorre.Y) * 180.0 / Math.PI + 360.0) % 360.0;

            estado.Torres.Add(new TorreTreinamento
            {
                Id = baseTorre.Id,
                X = baseTorre.X,
                Y = baseTorre.Y,
                QuadrantePreferido = baseTorre.QuadrantePreferido,
                AnguloAzimuteAtualGraus = azimuteInicial
            });
        }

        return estado;
    }

    /// <summary>
    /// Roda um episódio completo (sem passos intermediários visíveis) e devolve o resultado — é a
    /// função de fitness da evolução. Recebe a rede JÁ DECODIFICADA (e não mais um vetor de
    /// pesos + arquitetura): com NEAT a topologia é parte do genoma, então quem decodifica é o
    /// framework, não o ambiente.
    /// </summary>
    public EpisodioResultado ExecutarEpisodioRapido(RedeNeat rede, int? seed = null, TipoCenarioTreinamento cenario = TipoCenarioTreinamento.Aleatorio, ControladorHibrido.ModoDecisao modo = ControladorHibrido.ModoDecisao.Hibrido)
    {
        EstadoEpisodio estado = NovoEpisodio(seed, cenario);
        var controlador = new ControladorHibrido(rede, _parametros.RaioMundoMetros, _parametros.VelocidadeMaximaAlvoMetrosPorSegundo, modo, _parametros.TempoDeteccaoRelevanteSegundos);

        const double dt = 0.2;
        while (!estado.Finalizado)
        {
            Avancar(estado, controlador, dt);
        }

        return new EpisodioResultado(
            estado.FitnessAcumulado, estado.AlvosAbatidos, estado.AlvosEscapados, estado.TirosErrados, estado.AlvosNascidos,
            estado.OportunidadesDeDisparo, estado.DisparosRecusadosPelaRede, estado.TempoDeRespostaMedioSegundos);
    }

    /// <summary>
    /// Fitness de uma rede, calculado como a MÉDIA de vários episódios com <paramref name="seeds"/>
    /// fixas — as MESMAS seeds para todos os indivíduos da geração (ver
    /// <see cref="ViewModels.TreinamentoControladorViewModel.LacoDeTreinamento"/>), então a
    /// comparação dentro da geração continua justa (todo mundo enfrenta os mesmos cenários), mas
    /// cada genoma é testado em <paramref name="seeds"/>.Count cenários diferentes em vez de um
    /// só — um único episódio é barulhento demais para servir de critério de seleção sozinho
    /// (nascimento, velocidade e certeza dos alvos são sorteados; o MESMO genoma tira um episódio
    /// fácil ou difícil só por sorte). Com pouco mais de um cenário por avaliação, esse ruído de
    /// ambiente passa a dominar sobre a diferença real de qualidade entre genomas, e a seleção
    /// por torneio (e até o elitismo) vira quase um sorteio — exatamente o sintoma de "fitness
    /// não evolui de verdade" apesar do algoritmo genético em si estar correto.
    /// </summary>
    /// <param name="cenariosFixos">
    /// Casos táticos mapeados (ver <see cref="CatalogoCenarios"/>) somados aos episódios
    /// aleatórios de <paramref name="seeds"/> — cada um conta como mais um episódio na média, de
    /// modo que a IA não pode ignorar um caso difícil só porque o sorteio raramente o produz.
    /// Seed fixa (0) nestes: o script de nascimento já é determinístico, então a única fonte de
    /// aleatoriedade restante (erro de disparo, ver <see cref="CalculadoraErroDisparo"/>) nem
    /// precisa variar de geração para geração para o caso continuar isolando a mesma dificuldade.
    /// </param>
    public double AvaliarFitness(RedeNeat rede, IReadOnlyList<int> seeds, IReadOnlyList<TipoCenarioTreinamento>? cenariosFixos = null, ControladorHibrido.ModoDecisao modo = ControladorHibrido.ModoDecisao.Hibrido)
    {
        double soma = 0;
        int total = 0;

        foreach (int seed in seeds)
        {
            soma += ExecutarEpisodioRapido(rede, seed, TipoCenarioTreinamento.Aleatorio, modo).FitnessTotal;
            total++;
        }

        if (cenariosFixos is not null)
        {
            foreach (TipoCenarioTreinamento cenario in cenariosFixos)
            {
                soma += ExecutarEpisodioRapido(rede, seed: 0, cenario, modo).FitnessTotal;
                total++;
            }
        }

        return soma / Math.Max(1, total);
    }

    /// <summary>Avança o episódio em <paramref name="dt"/> segundos: nascimento, movimento, seleção de torre e decisão de disparo.</summary>
    public void Avancar(EstadoEpisodio estado, ControladorHibrido controlador, double dt)
    {
        if (estado.Finalizado) return;

        estado.TempoDecorridoSegundos += dt;

        TalvezNascerAlvo(estado);
        MoverAlvos(estado, dt);
        AcumularTempoComprometido(estado, dt);
        AplicarPenalidadeDeProximidade(estado);
        TratarAlvosNaBase(estado);
        TratarInvasaoDeTorre(estado);
        SelecionarTorresParaAlvosLivres(estado, controlador);
        AtualizarMiraDasTorres(estado, dt);
        DecidirDisparos(estado, controlador);

        estado.Alvos.RemoveAll(a => !a.Ativo);

        double tetoDeTempo = _parametros.SemLimiteDeDuracao
            ? _parametros.DuracaoMaximaSegurancaSegundos
            : _parametros.DuracaoEpisodioSegundos;

        if (estado.TempoDecorridoSegundos >= tetoDeTempo)
        {
            FinalizarEpisodio(estado);
        }
    }

    /// <summary>Única porta de saída de um episódio (fim natural por tempo OU fim antecipado por invasão de torre) — garante que a penalidade de desequilíbrio (ver <see cref="AplicarPenalidadeDesequilibrioTorres"/>) seja aplicada exatamente uma vez.</summary>
    private void FinalizarEpisodio(EstadoEpisodio estado)
    {
        if (estado.Finalizado) return;

        estado.Finalizado = true;
        AplicarPenalidadeDesequilibrioTorres(estado);
    }

    /// <summary>Soma, a cada passo, o tempo em que cada torre esteve ocupada — ver <see cref="TorreTreinamento.TempoComprometidoSegundos"/>.</summary>
    private static void AcumularTempoComprometido(EstadoEpisodio estado, double dt)
    {
        foreach (TorreTreinamento torre in estado.Torres.Where(t => !t.Livre))
        {
            torre.TempoComprometidoSegundos += dt;
        }
    }

    /// <summary>
    /// Penaliza, ao fim do episódio, a desigualdade de uso entre as torres — um desvio padrão
    /// alto significa que uma torre carregou praticamente tudo enquanto outra ficou ociosa,
    /// indicando má gestão de recursos mesmo que o total de abates tenha sido bom (ver
    /// <see cref="ParametrosTreinamento.PenalidadeDesequilibrioTorresPorSegundo"/>).
    /// </summary>
    private void AplicarPenalidadeDesequilibrioTorres(EstadoEpisodio estado)
    {
        if (estado.Torres.Count == 0) return;

        double media = estado.Torres.Average(t => t.TempoComprometidoSegundos);
        double variancia = estado.Torres.Sum(t => Math.Pow(t.TempoComprometidoSegundos - media, 2)) / estado.Torres.Count;
        double desvioPadrao = Math.Sqrt(variancia);

        estado.FitnessAcumulado -= desvioPadrao * _parametros.PenalidadeDesequilibrioTorresPorSegundo;
    }

    /// <summary>
    /// Alvo ainda ativo que chega perto demais de QUALQUER torre rompe a defesa — diferente da
    /// fuga pela base (<see cref="TratarAlvosNaBase"/>, que só penaliza): aqui o episódio acaba
    /// ali mesmo, descartando qualquer pontuação futura possível (ver
    /// <see cref="ParametrosTreinamento.DistanciaInvasaoTorreMetros"/>).
    /// </summary>
    private void TratarInvasaoDeTorre(EstadoEpisodio estado)
    {
        foreach (AlvoTreinamento alvo in estado.Alvos.Where(a => a.Ativo))
        {
            TorreTreinamento? torreInvadida = estado.Torres.FirstOrDefault(t =>
                DistanceCalculator.Euclidean(alvo.X, alvo.Y, t.X, t.Y) <= _parametros.DistanciaInvasaoTorreMetros);
            if (torreInvadida is null) continue;

            alvo.Ativo = false;
            estado.AlvosEscapados++;
            estado.FitnessAcumulado -= _parametros.PenalidadeInvasaoTorre;
            LiberarTorreDe(estado, alvo);
            FinalizarEpisodio(estado);
            return; // episódio já terminou — nenhum outro alvo deste passo precisa ser avaliado
        }
    }

    private double ProximoIntervaloSpawn(EstadoEpisodio estado)
    {
        // Escalada de dificuldade: o intervalo entre nascimentos encolhe linearmente do início
        // (IntervaloSpawnMin/MaxSegundos) ao fim (IntervaloSpawnMinFinalSegundos/MaxFinalSegundos)
        // do episódio — alvos aparecem cada vez mais rápido conforme o tempo passa.
        double fracaoDecorrida = _parametros.DuracaoEpisodioSegundos > 0
            ? Math.Clamp(estado.TempoDecorridoSegundos / _parametros.DuracaoEpisodioSegundos, 0, 1)
            : 0.0;

        double min = Interpolar(_parametros.IntervaloSpawnMinSegundos, _parametros.IntervaloSpawnMinFinalSegundos, fracaoDecorrida);
        double max = Interpolar(_parametros.IntervaloSpawnMaxSegundos, _parametros.IntervaloSpawnMaxFinalSegundos, fracaoDecorrida);

        return min + estado.Rng.NextDouble() * (max - min);
    }

    private static double Interpolar(double inicio, double fim, double fracao) => inicio + (fim - inicio) * fracao;

    private void TalvezNascerAlvo(EstadoEpisodio estado)
    {
        if (estado.SpawnsScriptados is { } spawns)
        {
            TalvezNascerAlvoScriptado(estado, spawns);
            return;
        }

        if (estado.TempoDecorridoSegundos < estado.ProximoSpawnSegundos) return;
        estado.ProximoSpawnSegundos = estado.TempoDecorridoSegundos + ProximoIntervaloSpawn(estado);

        // Nasce num ponto aleatório da borda do mundo (círculo de raio RaioMundoMetros) e se
        // move em direção à base com um pequeno desvio angular — não é uma linha perfeitamente
        // radial, para a torre precisar reavaliar a mira ao longo da aproximação.
        double anguloNascimento = estado.Rng.NextDouble() * 2 * Math.PI;
        double x = _parametros.RaioMundoMetros * Math.Cos(anguloNascimento);
        double y = _parametros.RaioMundoMetros * Math.Sin(anguloNascimento);
        Quadrant quadranteNascimento = QuadrantHelper.Determine(x, y);

        // Teto POR QUADRANTE (não mais global): só descarta o nascimento se o quadrante sorteado
        // já estiver no limite — outro quadrante com folga continua recebendo alvos normalmente.
        if (estado.Alvos.Count(a => a.Ativo && a.Quadrante == quadranteNascimento) >= _parametros.MaxAlvosPorQuadrante) return;

        double desvioRadianos = (estado.Rng.NextDouble() - 0.5) * (Math.PI / 6); // até ±30°
        double anguloMovimento = anguloNascimento + Math.PI + desvioRadianos;

        double velocidade = _parametros.VelocidadeMinimaAlvoMetrosPorSegundo +
            estado.Rng.NextDouble() * (_parametros.VelocidadeMaximaAlvoMetrosPorSegundo - _parametros.VelocidadeMinimaAlvoMetrosPorSegundo);

        // Altura sorteada ANTES do alvo existir, porque ela decide, já no nascimento, se ele será
        // visível para todas as torres (ver VisivelPorTodasAsTorres).
        double altura = SortearAltura(estado);

        estado.Alvos.Add(new AlvoTreinamento
        {
            Id = estado.ProximoIdAlvo(),
            X = x,
            Y = y,
            AlturaMetros = altura,
            VisivelPorTodasAsTorres = VisivelPorTodasAsTorres(altura),
            VelocidadeX = velocidade * Math.Cos(anguloMovimento),
            VelocidadeY = velocidade * Math.Sin(anguloMovimento),
            VelocidadeMetrosPorSegundo = velocidade,
            // Ponto de partida da trajetória ALEATÓRIA (ver MoverAlvos) — só o sorteio padrão
            // ganha trajetória curva; os cenários scriptados continuam em linha reta (deixam isto
            // null, ver TalvezNascerAlvoScriptado) porque precisam continuar 100% reproduzíveis
            // tal como documentados.
            AnguloMovimentoRadianos = anguloMovimento,
            Quadrante = quadranteNascimento,
            // Certeza nasce baixa e SOBE com o tempo de observação (ver AlvoTreinamento.Certeza)
            // — nunca fica presa abaixo do limiar para sempre; só demora mais ou menos conforme
            // a taxa sorteada aqui.
            CertezaBase = 0.20 + estado.Rng.NextDouble() * 0.30,
            TaxaCrescimentoCertezaPorSegundo = 0.08 + estado.Rng.NextDouble() * 0.17
        });
    }

    /// <summary>
    /// Versão scriptada de <see cref="TalvezNascerAlvo"/>: em vez de sortear, consome
    /// <paramref name="spawns"/> em ordem conforme o relógio do episódio alcança cada instante —
    /// movimento sempre em linha reta (sem desvio angular) para o cenário ser 100% reproduzível.
    /// </summary>
    private void TalvezNascerAlvoScriptado(EstadoEpisodio estado, IReadOnlyList<SpawnScriptado> spawns)
    {
        while (estado.ProximoIndiceSpawnScriptado < spawns.Count &&
               estado.TempoDecorridoSegundos >= spawns[estado.ProximoIndiceSpawnScriptado].InstanteSegundos)
        {
            SpawnScriptado spec = spawns[estado.ProximoIndiceSpawnScriptado];
            estado.ProximoIndiceSpawnScriptado++;

            double anguloNascimentoRad = spec.AnguloNascimentoGraus * Math.PI / 180.0;
            double raio = _parametros.RaioMundoMetros * spec.FracaoRaioNascimento;
            double x = raio * Math.Cos(anguloNascimentoRad);
            double y = raio * Math.Sin(anguloNascimentoRad);
            Quadrant quadranteNascimento = QuadrantHelper.Determine(x, y);

            // Teto POR QUADRANTE — ver TalvezNascerAlvo.
            if (estado.Alvos.Count(a => a.Ativo && a.Quadrante == quadranteNascimento) >= _parametros.MaxAlvosPorQuadrante) continue;

            double anguloMovimento = anguloNascimentoRad + Math.PI;

            // Altura vem do próprio roteiro, nunca sorteada: estes são casos táticos mapeados,
            // cujo valor está justamente em serem 100% reproduzíveis. Um caso com alvo alto é um
            // caso declarado como tal (ver SpawnScriptado.AlturaMetros), não um efeito do sorteio.
            double alturaScriptada = _parametros.EixoZHabilitado ? spec.AlturaMetros : 0.0;

            estado.Alvos.Add(new AlvoTreinamento
            {
                Id = estado.ProximoIdAlvo(),
                X = x,
                Y = y,
                AlturaMetros = alturaScriptada,
                VisivelPorTodasAsTorres = VisivelPorTodasAsTorres(alturaScriptada),
                VelocidadeX = spec.VelocidadeMetrosPorSegundo * Math.Cos(anguloMovimento),
                VelocidadeY = spec.VelocidadeMetrosPorSegundo * Math.Sin(anguloMovimento),
                VelocidadeMetrosPorSegundo = spec.VelocidadeMetrosPorSegundo,
                Quadrante = quadranteNascimento,
                CertezaBase = spec.CertezaBase,
                TaxaCrescimentoCertezaPorSegundo = spec.TaxaCrescimentoCertezaPorSegundo
            });
        }

        estado.ProximoSpawnSegundos = estado.ProximoIndiceSpawnScriptado < spawns.Count
            ? spawns[estado.ProximoIndiceSpawnScriptado].InstanteSegundos
            : double.MaxValue;
    }

    /// <summary>
    /// Sorteia a altura de um alvo que está nascendo: uniforme entre 0 e
    /// <see cref="ParametrosTreinamento.AlturaMaximaAlvoMetros"/> com o eixo Z ligado, e
    /// exatamente 0 com ele desligado.
    /// </summary>
    /// <remarks>
    /// Usa o <see cref="EstadoEpisodio.Rng"/> do episódio, e não um gerador próprio, para que um
    /// episódio com a mesma seed continue sendo reproduzível ponta a ponta — é disso que dependem
    /// a comparação justa entre genomas da mesma geração e a reprodução visual de um episódio já
    /// avaliado.
    /// </remarks>
    private double SortearAltura(EstadoEpisodio estado) =>
        _parametros.EixoZHabilitado ? estado.Rng.NextDouble() * _parametros.AlturaMaximaAlvoMetros : 0.0;

    /// <summary>
    /// Regra do eixo Z: acima de <see cref="ParametrosTreinamento.AlturaVisivelPorTodasAsTorresMetros"/>
    /// o alvo sobe o bastante para entrar no campo de visão de TODAS as torres, e não só das do
    /// próprio quadrante e adjacentes.
    /// </summary>
    /// <remarks>
    /// Amplia apenas o conjunto de torres candidatas. Todas as demais condições continuam valendo
    /// sem exceção — zona morta, certeza mínima, tempo mínimo de mira e torre livre —, por isso a
    /// regra vive aqui e não dentro de <see cref="ControladorHibrido"/>: ela é uma propriedade do
    /// mundo simulado, não da política de decisão.
    /// </remarks>
    private bool VisivelPorTodasAsTorres(double alturaMetros) =>
        _parametros.EixoZHabilitado && alturaMetros > _parametros.AlturaVisivelPorTodasAsTorresMetros;

    /// <summary>
    /// Move cada alvo ativo e, para os de trajetória ALEATÓRIA (<see cref="AlvoTreinamento.AnguloMovimentoRadianos"/>
    /// não nulo — ver <see cref="TalvezNascerAlvo"/>), perturba a direção do movimento por um
    /// passeio aleatório contínuo antes de deslocar: a cada passo, soma-se um desvio sorteado
    /// dentro de ±<see cref="ParametrosTreinamento.VariacaoMaximaDirecaoGrausPorSegundo"/>·dt ao
    /// ângulo de movimento, e <see cref="AlvoTreinamento.VelocidadeX"/>/<see cref="AlvoTreinamento.VelocidadeY"/>
    /// são recalculadas a partir do novo ângulo — a MAGNITUDE da velocidade nunca muda, só a
    /// direção, então os limites de <see cref="ParametrosTreinamento.VelocidadeMaximaAlvoMetrosPorSegundo"/>
    /// continuam valendo durante toda a trajetória. Alvos scriptados (ângulo nulo) continuam em
    /// linha reta, sem nenhuma chamada a <see cref="EstadoEpisodio.Rng"/> — determinismo total
    /// preservado para eles.
    /// </summary>
    private void MoverAlvos(EstadoEpisodio estado, double dt)
    {
        double variacaoMaximaRadianos = _parametros.VariacaoMaximaDirecaoGrausPorSegundo * Math.PI / 180.0 * dt;

        foreach (AlvoTreinamento alvo in estado.Alvos.Where(a => a.Ativo))
        {
            if (alvo.AnguloMovimentoRadianos is { } anguloAtual && variacaoMaximaRadianos > 0)
            {
                double desvio = (estado.Rng.NextDouble() * 2 - 1) * variacaoMaximaRadianos;
                double anguloNovo = anguloAtual + desvio;

                alvo.AnguloMovimentoRadianos = anguloNovo;
                alvo.VelocidadeX = alvo.VelocidadeMetrosPorSegundo * Math.Cos(anguloNovo);
                alvo.VelocidadeY = alvo.VelocidadeMetrosPorSegundo * Math.Sin(anguloNovo);
            }

            alvo.X += alvo.VelocidadeX * dt;
            alvo.Y += alvo.VelocidadeY * dt;
            alvo.Quadrante = QuadrantHelper.Determine(alvo.X, alvo.Y);
            alvo.TempoDeVidaSegundos += dt;

            if (alvo.TorreSelecionadaId is not null)
            {
                alvo.TempoDesdeSelecao += dt;
            }
        }
    }

    /// <summary>
    /// Alvo que chega perto demais da base, mesmo sem ainda ter escapado de fato (ver
    /// <see cref="TratarAlvosNaBase"/>, que dispara no raio ainda menor da própria base), já
    /// custa pontos à geração — simula o risco de deixar o alvo se aproximar demais antes de
    /// decidir. Aplicada uma única vez por alvo (<see cref="AlvoTreinamento.PenalidadeProximidadeAplicada"/>),
    /// não a cada passo enquanto ele continuar dentro do raio.
    /// </summary>
    private void AplicarPenalidadeDeProximidade(EstadoEpisodio estado)
    {
        foreach (AlvoTreinamento alvo in estado.Alvos.Where(a => a.Ativo && !a.PenalidadeProximidadeAplicada))
        {
            double distanciaBase = DistanceCalculator.Euclidean(alvo.X, alvo.Y, 0, 0);
            if (distanciaBase > _parametros.DistanciaPerdaPontoMetros) continue;

            alvo.PenalidadeProximidadeAplicada = true;
            estado.FitnessAcumulado -= _parametros.PenalidadePerdaPonto;
        }
    }

    /// <summary>Alvo que passou pela base sem ser abatido é uma fuga — penaliza e libera a torre comprometida.</summary>
    private void TratarAlvosNaBase(EstadoEpisodio estado)
    {
        const double raioBaseMetros = 0.4;

        foreach (AlvoTreinamento alvo in estado.Alvos.Where(a => a.Ativo))
        {
            double distanciaBase = DistanceCalculator.Euclidean(alvo.X, alvo.Y, 0, 0);
            if (distanciaBase > raioBaseMetros) continue;

            alvo.Ativo = false;
            estado.AlvosEscapados++;
            estado.FitnessAcumulado -= _parametros.PenalidadeEscape;
            LiberarTorreDe(estado, alvo);
        }
    }

    private void SelecionarTorresParaAlvosLivres(EstadoEpisodio estado, ControladorHibrido controlador)
    {
        List<AlvoTreinamento> ativos = estado.Alvos.Where(a => a.Ativo).ToList();

        foreach (AlvoTreinamento alvo in ativos.Where(a => a.TorreSelecionadaId is null))
        {
            TorreTreinamento? torre = controlador.SelecionarTorre(alvo, estado.Torres, ativos);
            if (torre is null) continue;

            torre.AlvoComprometidoId = alvo.Id;
            alvo.TorreSelecionadaId = torre.Id;
            alvo.TempoDesdeSelecao = 0;
            torre.MiraAlinhada = false; // alvo novo — a torre ainda não girou na direção dele
        }
    }

    /// <summary>
    /// Gira a mira FÍSICA de cada torre comprometida (os dois motores de passo 28BYJ-48, azimute e
    /// elevação) em direção ao alvo, no máximo <see cref="ParametrosTreinamento.VelocidadeAngularTorreGrausPorSegundo"/>
    /// por eixo neste passo — é isto que modela o motor real não girar instantaneamente de um
    /// ângulo para outro. Atualiza <see cref="TorreTreinamento.MiraAlinhada"/>, o veto inegociável
    /// de disparo consultado por <see cref="DecidirDisparos"/>: enquanto a mira ainda não alcançou
    /// o alvo dentro de <see cref="ParametrosTreinamento.ToleranciaMiraGraus"/>, não há disparo
    /// possível, por mais que a heurística/rede "queiram". É aqui que nasce a situação em que o
    /// ACOMPANHAMENTO NÃO PODE SER GARANTIDO: um alvo rápido o bastante (ou que muda de direção
    /// rápido demais, ver <see cref="MoverAlvos"/>) pode manter a torre permanentemente atrás da
    /// mira ideal, nunca alinhando — ele então escapa ou invade sem nunca ter sido engajado, sem
    /// nenhum "erro" especial além do natural (ver <see cref="TratarAlvosNaBase"/>/<see cref="TratarInvasaoDeTorre"/>).
    /// A elevação só é considerada quando <see cref="ParametrosTreinamento.EixoZHabilitado"/> está
    /// ligado — desligado, ela fica sempre 0 e sempre alinhada, reproduzindo o comportamento
    /// bidimensional anterior (só o azimute importa).
    /// </summary>
    private void AtualizarMiraDasTorres(EstadoEpisodio estado, double dt)
    {
        double passoMaximoGraus = _parametros.VelocidadeAngularTorreGrausPorSegundo * dt;

        foreach (TorreTreinamento torre in estado.Torres.Where(t => !t.Livre))
        {
            AlvoTreinamento? alvo = estado.Alvos.FirstOrDefault(a => a.Id == torre.AlvoComprometidoId);
            if (alvo is null) continue; // alvo já saiu da lista de ativos neste mesmo passo — nada a perseguir até a próxima seleção

            double anguloAzimuteDesejado = (Math.Atan2(alvo.X - torre.X, alvo.Y - torre.Y) * 180.0 / Math.PI + 360.0) % 360.0;
            torre.AnguloAzimuteAtualGraus = GirarAzimuteEmDirecaoA(torre.AnguloAzimuteAtualGraus, anguloAzimuteDesejado, passoMaximoGraus);
            double erroAzimuteGraus = DiferencaAngularAbsoluta(torre.AnguloAzimuteAtualGraus, anguloAzimuteDesejado);

            double erroElevacaoGraus = 0.0;
            if (_parametros.EixoZHabilitado)
            {
                double distanciaHorizontal = DistanceCalculator.Euclidean(alvo.X, alvo.Y, torre.X, torre.Y);
                double anguloElevacaoDesejado = Math.Atan2(alvo.AlturaMetros, Math.Max(distanciaHorizontal, 0.01)) * 180.0 / Math.PI;
                double passo = Math.Clamp(anguloElevacaoDesejado - torre.AnguloElevacaoAtualGraus, -passoMaximoGraus, passoMaximoGraus);
                torre.AnguloElevacaoAtualGraus += passo;
                erroElevacaoGraus = Math.Abs(anguloElevacaoDesejado - torre.AnguloElevacaoAtualGraus);
            }

            torre.MiraAlinhada = erroAzimuteGraus <= _parametros.ToleranciaMiraGraus && erroElevacaoGraus <= _parametros.ToleranciaMiraGraus;
        }
    }

    /// <summary>Gira <paramref name="atualGraus"/> até <paramref name="desejadoGraus"/> pelo caminho mais curto (considera o wraparound 0°/360°), sem passar de <paramref name="passoMaximoGraus"/> neste passo.</summary>
    private static double GirarAzimuteEmDirecaoA(double atualGraus, double desejadoGraus, double passoMaximoGraus)
    {
        double diferenca = ((desejadoGraus - atualGraus + 540.0) % 360.0) - 180.0; // normaliza para (-180, 180]
        double passo = Math.Clamp(diferenca, -passoMaximoGraus, passoMaximoGraus);
        return (atualGraus + passo + 360.0) % 360.0;
    }

    /// <summary>Menor diferença angular (sempre ≥ 0, considera o wraparound 0°/360°) entre dois ângulos em graus.</summary>
    private static double DiferencaAngularAbsoluta(double aGraus, double bGraus)
    {
        double diferenca = Math.Abs(aGraus - bGraus) % 360.0;
        return diferenca > 180.0 ? 360.0 - diferenca : diferenca;
    }

    /// <summary>Regra 6 (ajuda de torre adjacente): mesmo raio usado em produção (ver <see cref="Services.TowerSelectionService"/>).</summary>
    private const double RaioAjudaTorreAdjacenteMetros = 2.0;

    private void DecidirDisparos(EstadoEpisodio estado, ControladorHibrido controlador)
    {
        foreach (AlvoTreinamento alvo in estado.Alvos.Where(a => a.Ativo && a.TorreSelecionadaId is not null))
        {
            // Únicos critérios inegociáveis, independente da distância até o alvo: fora de zona
            // morta e certeza suficiente (mesma regra de Services.FireControlService.Authorize).
            // estado.ZonaMortaForcadaQuadrante soma mais um quadrante só para este episódio (ver
            // CatalogoCenarios.AlvoEmZonaMorta), sem alterar a configuração global do ambiente.
            if (_parametros.ZonasMortasQuadrantes.Contains(alvo.Quadrante) || alvo.Quadrante == estado.ZonaMortaForcadaQuadrante) continue;

            TorreTreinamento? torre = estado.Torres.FirstOrDefault(t => t.Id == alvo.TorreSelecionadaId);
            if (torre is null) continue;

            // Veto físico: os motores de passo ainda não giraram até o alvo (ver
            // AtualizarMiraDasTorres) — sem isto um alvo rápido demais seria "abatido" por uma
            // torre que, fisicamente, ainda estaria apontando para outro lugar.
            if (!torre.MiraAlinhada) continue;

            // Certeza EFETIVA (não a bruta): dentro do alcance de confirmação garantida (ver
            // AlvoTreinamento.CertezaEfetiva) a certeza bruta deixa de ser teto — um alvo recém-
            // nascido (certeza bruta baixa) mas já a 1,2 m da torre é confirmado a 100% mesmo
            // assim, então o antigo atalho "certeza bruta abaixo do limiar dispensa calcular a
            // distância" não vale mais: a distância precisa ser calculada sempre.
            double distanciaTorre = DistanceCalculator.Euclidean(alvo.X, alvo.Y, torre.X, torre.Y);
            double certezaEfetiva = alvo.CertezaEfetiva(distanciaTorre, _parametros.DistanciaMinimaBoaCertezaMetros, _parametros.DistanciaCertezaMaximaMetros);
            if (certezaEfetiva < _parametros.LimiarConfiancaDisparoFracao) continue;

            // Só conta "oportunidade" DEPOIS do tempo mínimo de mira já ter passado (veto
            // compartilhado, igual na heurística pura), para isolar exatamente o ponto em que
            // Hibrido e HeuristicaPura discordam (ver EstadoEpisodio.DisparosRecusadosPelaRede) —
            // contar antes misturaria o veto compartilhado com a decisão exclusiva da rede.
            bool passouTempoMinimoDeMira = alvo.TempoDesdeSelecao >= ControladorHibrido.TempoMinimoDeMiraSegundos;
            if (passouTempoMinimoDeMira) estado.OportunidadesDeDisparo++;

            if (!controlador.DecidirDisparo(alvo, torre))
            {
                if (passouTempoMinimoDeMira)
                {
                    estado.DisparosRecusadosPelaRede++;
                    estado.FitnessAcumulado -= _parametros.PenalidadeRecusaDisparo;
                }
                continue;
            }

            Disparar(estado, torre, alvo);
            if (!alvo.Ativo) continue; // abatido pela torre principal — não precisa de ajuda

            // Regra 6 (ajuda de torre adjacente): alvo acima da velocidade considerada rápida
            // também recebe o disparo de uma torre de quadrante adjacente a até
            // RaioAjudaTorreAdjacenteMetros dele, mesmo sem ser a torre principal comprometida —
            // um segundo canhão reduz a chance de o alvo escapar de um disparo único (a chance de
            // errar cresce com a velocidade, ver CalculadoraErroDisparo).
            if (alvo.VelocidadeMetrosPorSegundo >= _parametros.VelocidadeRapidaMetrosPorSegundo)
            {
                // Com o eixo Z ligado e o alvo alto o bastante, a restrição de quadrante adjacente
                // cai pelo mesmo motivo de ControladorHibrido.SelecionarTorre: todas as torres o
                // enxergam. O raio de proximidade continua valendo — visibilidade não é alcance.
                TorreTreinamento? ajuda = estado.Torres.FirstOrDefault(t =>
                    t.Id != torre.Id &&
                    (alvo.VisivelPorTodasAsTorres || QuadrantHelper.AreAdjacent(t.QuadrantePreferido, alvo.Quadrante)) &&
                    DistanceCalculator.Euclidean(alvo.X, alvo.Y, t.X, t.Y) <= RaioAjudaTorreAdjacenteMetros);

                if (ajuda is not null) Disparar(estado, ajuda, alvo);
            }
        }
    }

    /// <summary>
    /// Um único disparo contra <paramref name="alvo"/>, pela <paramref name="torre"/> indicada —
    /// reaproveitada tanto pela torre principal quanto pela torre de ajuda (regra 6), para as
    /// duas aplicarem exatamente a mesma chance de erro e a mesma recompensa de abate.
    /// </summary>
    private void Disparar(EstadoEpisodio estado, TorreTreinamento torre, AlvoTreinamento alvo)
    {
        RegistrarTempoDeResposta(estado, alvo);

        // Erro probabilístico crescendo com a velocidade do alvo (mesma regra do sistema
        // real — ver CalculadoraErroDisparo): alvo rápido escapando de uma torre sozinha é o
        // sinal de fitness que empurra a rede a não demorar tanto para comprometer/disparar.
        if (estado.Rng.NextDouble() < CalculadoraErroDisparo.ProbabilidadeDeErro(alvo.VelocidadeMetrosPorSegundo))
        {
            estado.TirosErrados++;
            return;
        }

        alvo.Abatido = true;
        alvo.Ativo = false;
        estado.AlvosAbatidos++;

        // Guardado antes de liberar a torre: o alvo é removido da lista no fim deste mesmo
        // passo (ver Avancar/RemoveAll), então sem isto a tela não teria como desenhar o
        // disparo depois de Avancar retornar.
        torre.DisparadoEmSegundos = estado.TempoDecorridoSegundos;
        torre.UltimoAlvoDisparoX = alvo.X;
        torre.UltimoAlvoDisparoY = alvo.Y;

        // Bônus por abater longe da base (mais tempo de reação sobrando) — incentiva a rede
        // a não deixar o alvo se aproximar demais antes de decidir disparar.
        double distanciaBase = DistanceCalculator.Euclidean(alvo.X, alvo.Y, 0, 0);
        double bonusDistancia = Math.Clamp(distanciaBase / _parametros.RaioMundoMetros, 0, 1);
        estado.FitnessAcumulado += _parametros.RecompensaAbate * (0.7 + 0.3 * bonusDistancia);

        LiberarTorreDe(estado, alvo);
    }

    private static void LiberarTorreDe(EstadoEpisodio estado, AlvoTreinamento alvo)
    {
        if (alvo.TorreSelecionadaId is not { } torreId) return;

        TorreTreinamento? torre = estado.Torres.FirstOrDefault(t => t.Id == torreId);
        if (torre is not null && torre.AlvoComprometidoId == alvo.Id)
        {
            torre.AlvoComprometidoId = null;
        }
    }

    /// <summary>
    /// Mede o "tempo de resposta" (seleção → primeiro disparo, acerto ou erro) de um alvo —
    /// sempre contabilizado nas estatísticas do episódio (ver <see cref="EstadoEpisodio.TempoDeRespostaMedioSegundos"/>),
    /// em QUALQUER modo/versão de controlador; só entra no FITNESS quando
    /// <see cref="ParametrosTreinamento.UsarFitnessV2"/> está ligado — por isso V1 com parâmetros
    /// padrão continua produzindo exatamente o mesmo fitness de antes desta mudança.
    /// </summary>
    private void RegistrarTempoDeResposta(EstadoEpisodio estado, AlvoTreinamento alvo)
    {
        if (alvo.TempoDeRespostaRegistrado) return;
        alvo.TempoDeRespostaRegistrado = true;

        estado.SomaTemposRespostaSegundos += alvo.TempoDesdeSelecao;
        estado.ContagemTemposResposta++;

        if (!_parametros.UsarFitnessV2) return;

        double fatorResposta = Math.Clamp(
            1.0 - alvo.TempoDesdeSelecao / Math.Max(2.0 * _parametros.TempoRespostaReferenciaSegundos, 0.01),
            0.0, 1.0);
        estado.FitnessAcumulado += _parametros.RecompensaTempoRespostaMaximo * fatorResposta;
    }

    // =====================================================================================
    // Controlador V2 (ver ControladorHibridoV2) — espelha Avancar/SelecionarTorresParaAlvosLivres/
    // DecidirDisparos, mas em métodos PRÓPRIOS em vez de ramificar os existentes: o controlador V1
    // acima desta linha não foi tocado por nenhuma mudança desta seção, que é exatamente o que
    // "manter o controlador atual como referência, testável separadamente" pede. O nascimento de
    // alvo, movimento, penalidades de proximidade/invasão/desequilíbrio e o próprio Disparar são
    // REAPROVEITADOS (não dependem de qual controlador decide torre/disparo).
    // =====================================================================================

    /// <summary>Episódio completo com o controlador V2 e rede SEM estado entre ativações (padrão) — mesmo papel de <see cref="ExecutarEpisodioRapido"/>.</summary>
    public EpisodioResultado ExecutarEpisodioRapidoV2(RedeNeat rede, int? seed = null, TipoCenarioTreinamento cenario = TipoCenarioTreinamento.Aleatorio)
    {
        if (_parametros.RedeRecorrente) rede.Reiniciar(); // zera estado de um episódio anterior avaliado com a MESMA caixa-preta (ver AvaliadorAmbienteV2)

        EstadoEpisodio estado = NovoEpisodio(seed, cenario);
        var controlador = new ControladorHibridoV2(
            rede, _parametros.RaioMundoMetros, _parametros.VelocidadeMaximaAlvoMetrosPorSegundo,
            _parametros.TempoDeteccaoRelevanteSegundos, _parametros.AlturaMaximaAlvoMetros, _parametros.JanelaTrocaDeAlvoSegundos);

        const double dt = 0.2;
        while (!estado.Finalizado) AvancarV2(estado, controlador, dt);

        return new EpisodioResultado(
            estado.FitnessAcumulado, estado.AlvosAbatidos, estado.AlvosEscapados, estado.TirosErrados, estado.AlvosNascidos,
            estado.OportunidadesDeDisparo, estado.DisparosRecusadosPelaRede, estado.TempoDeRespostaMedioSegundos,
            estado.TrocasDeAlvoPenalizadas, estado.CooperacoesConcedidas, estado.CooperacoesNegadasPorConcentracao);
    }

    /// <summary>Episódio completo com o controlador V2 e rede RECORRENTE com estado isolado por torre — ver observações de escopo em <see cref="RedeNeatRecorrente"/> (usado para replay/avaliação isolada de um genoma já escolhido, não dentro do laço evolutivo).</summary>
    public EpisodioResultado ExecutarEpisodioRapidoV2(RedeNeatRecorrente redeRecorrente, int? seed = null, TipoCenarioTreinamento cenario = TipoCenarioTreinamento.Aleatorio)
    {
        redeRecorrente.ReiniciarTudo(); // nenhuma torre começa este episódio lembrando de um episódio anterior

        EstadoEpisodio estado = NovoEpisodio(seed, cenario);
        var controlador = new ControladorHibridoV2(
            redeRecorrente, _parametros.RaioMundoMetros, _parametros.VelocidadeMaximaAlvoMetrosPorSegundo,
            _parametros.TempoDeteccaoRelevanteSegundos, _parametros.AlturaMaximaAlvoMetros, _parametros.JanelaTrocaDeAlvoSegundos);

        const double dt = 0.2;
        while (!estado.Finalizado) AvancarV2(estado, controlador, dt);

        return new EpisodioResultado(
            estado.FitnessAcumulado, estado.AlvosAbatidos, estado.AlvosEscapados, estado.TirosErrados, estado.AlvosNascidos,
            estado.OportunidadesDeDisparo, estado.DisparosRecusadosPelaRede, estado.TempoDeRespostaMedioSegundos,
            estado.TrocasDeAlvoPenalizadas, estado.CooperacoesConcedidas, estado.CooperacoesNegadasPorConcentracao);
    }

    /// <summary>Fitness médio sobre várias seeds — mesmo papel de <see cref="AvaliarFitness"/>, controlador V2, rede sem estado entre ativações.</summary>
    public double AvaliarFitnessV2(RedeNeat rede, IReadOnlyList<int> seeds, IReadOnlyList<TipoCenarioTreinamento>? cenariosFixos = null)
    {
        double soma = 0;
        int total = 0;

        foreach (int seed in seeds)
        {
            soma += ExecutarEpisodioRapidoV2(rede, seed, TipoCenarioTreinamento.Aleatorio).FitnessTotal;
            total++;
        }

        if (cenariosFixos is not null)
        {
            foreach (TipoCenarioTreinamento cenario in cenariosFixos)
            {
                soma += ExecutarEpisodioRapidoV2(rede, seed: 0, cenario).FitnessTotal;
                total++;
            }
        }

        return soma / Math.Max(1, total);
    }

    /// <summary>
    /// Equivalente V2 de <see cref="Avancar"/> — público pela mesma razão: permite montar um
    /// episódio com nascimentos customizados (<see cref="NovoEpisodioComSpawnsPersonalizados"/>) e
    /// avançar passo a passo com o controlador V2, seja na tela (replay) ou em teste isolado de um
    /// cenário específico (ex.: geometria controlada para validar a cooperação entre torres).
    /// </summary>
    public void AvancarV2(EstadoEpisodio estado, ControladorHibridoV2 controlador, double dt)
    {
        if (estado.Finalizado) return;

        estado.TempoDecorridoSegundos += dt;

        TalvezNascerAlvo(estado);
        MoverAlvos(estado, dt);
        AcumularTempoComprometido(estado, dt);
        AplicarPenalidadeDeProximidade(estado);
        TratarAlvosNaBase(estado);
        TratarInvasaoDeTorre(estado);
        SelecionarTorresParaAlvosLivresV2(estado, controlador);
        AtualizarMiraDasTorres(estado, dt);
        DecidirDisparosV2(estado, controlador);

        estado.Alvos.RemoveAll(a => !a.Ativo);

        double tetoDeTempo = _parametros.SemLimiteDeDuracao
            ? _parametros.DuracaoMaximaSegurancaSegundos
            : _parametros.DuracaoEpisodioSegundos;

        if (estado.TempoDecorridoSegundos >= tetoDeTempo)
        {
            FinalizarEpisodio(estado);
        }
    }

    private void SelecionarTorresParaAlvosLivresV2(EstadoEpisodio estado, ControladorHibridoV2 controlador)
    {
        List<AlvoTreinamento> ativos = estado.Alvos.Where(a => a.Ativo).ToList();

        foreach (AlvoTreinamento alvo in ativos.Where(a => a.TorreSelecionadaId is null))
        {
            TorreTreinamento? torre = controlador.SelecionarTorre(alvo, estado.Torres, ativos);
            if (torre is null) continue;

            bool trocaExcessiva = _parametros.UsarFitnessV2 &&
                torre.ContarTrocasRecentes(alvo.TempoDeVidaSegundos, _parametros.JanelaTrocaDeAlvoSegundos) >= _parametros.LimiteTrocasAntesDePenalizar;
            if (trocaExcessiva)
            {
                estado.FitnessAcumulado -= _parametros.PenalidadeTrocaDeAlvoExcessiva;
                estado.TrocasDeAlvoPenalizadas++;
            }

            torre.RegistrarNovoCompromisso(alvo.TempoDeVidaSegundos);
            torre.AlvoComprometidoId = alvo.Id;
            alvo.TorreSelecionadaId = torre.Id;
            alvo.TempoDesdeSelecao = 0;
            torre.MiraAlinhada = false; // alvo novo — a torre ainda não girou na direção dele
        }
    }

    private void DecidirDisparosV2(EstadoEpisodio estado, ControladorHibridoV2 controlador)
    {
        foreach (AlvoTreinamento alvo in estado.Alvos.Where(a => a.Ativo && a.TorreSelecionadaId is not null))
        {
            // Mesmos vetos inegociáveis da V1 — ver DecidirDisparos.
            if (_parametros.ZonasMortasQuadrantes.Contains(alvo.Quadrante) || alvo.Quadrante == estado.ZonaMortaForcadaQuadrante) continue;
            if (alvo.Certeza < _parametros.LimiarConfiancaDisparoFracao) continue;

            TorreTreinamento? torre = estado.Torres.FirstOrDefault(t => t.Id == alvo.TorreSelecionadaId);
            if (torre is null) continue;

            // Mesmo veto físico da V1 (ver DecidirDisparos/AtualizarMiraDasTorres): motores de
            // passo ainda não giraram até o alvo.
            if (!torre.MiraAlinhada) continue;

            double distanciaTorre = DistanceCalculator.Euclidean(alvo.X, alvo.Y, torre.X, torre.Y);
            double certezaEfetiva = alvo.CertezaEfetiva(distanciaTorre, _parametros.DistanciaMinimaBoaCertezaMetros, _parametros.DistanciaCertezaMaximaMetros);
            if (certezaEfetiva < _parametros.LimiarConfiancaDisparoFracao) continue;

            bool passouTempoMinimoDeMira = alvo.TempoDesdeSelecao >= ControladorHibridoV2.TempoMinimoDeMiraSegundos;
            if (passouTempoMinimoDeMira) estado.OportunidadesDeDisparo++;

            (bool disparar, double prioridadeCooperacao) = controlador.DecidirDisparo(alvo, torre, estado.Torres);

            if (!disparar)
            {
                if (passouTempoMinimoDeMira)
                {
                    estado.DisparosRecusadosPelaRede++;
                    estado.FitnessAcumulado -= _parametros.PenalidadeRecusaDisparo;
                }
                continue;
            }

            Disparar(estado, torre, alvo);
            if (!alvo.Ativo) continue; // abatido pela torre principal — não precisa de ajuda

            // Cooperação: pede ajuda tanto pela regra de velocidade da V1 (compatibilidade com o
            // mesmo caso tático) quanto pela saída nova "prioridade de cooperação" — qualquer uma
            // das duas aciona a tentativa de ajuda.
            bool pedirAjuda = alvo.VelocidadeMetrosPorSegundo >= _parametros.VelocidadeRapidaMetrosPorSegundo || prioridadeCooperacao > 0;
            if (!pedirAjuda) continue;

            // "Evite concentrar todas as torres num alvo quando outro precisa de atendimento":
            // com UsarFitnessV2, se existe AGORA outro alvo ativo sem nenhuma torre, a cooperação
            // é negada e penalizada — a torre de ajuda faria mais falta lá.
            bool existeAlvoSemTorre = estado.Alvos.Any(a => a.Ativo && a.Id != alvo.Id && a.TorreSelecionadaId is null);
            if (_parametros.UsarFitnessV2 && existeAlvoSemTorre)
            {
                estado.CooperacoesNegadasPorConcentracao++;
                estado.FitnessAcumulado -= _parametros.PenalidadeCooperacaoConcentrada;
                continue;
            }

            TorreTreinamento? ajuda = estado.Torres.FirstOrDefault(t =>
                t.Id != torre.Id &&
                (alvo.VisivelPorTodasAsTorres || QuadrantHelper.AreAdjacent(t.QuadrantePreferido, alvo.Quadrante)) &&
                DistanceCalculator.Euclidean(alvo.X, alvo.Y, t.X, t.Y) <= RaioAjudaTorreAdjacenteMetros);

            if (ajuda is null) continue;

            estado.CooperacoesConcedidas++;
            Disparar(estado, ajuda, alvo);
            RecompensarCooperacaoSeConcedida(estado, alvo, cooperacaoFoiConcedida: true);
        }
    }

    /// <summary>Bônus de fitness (só com <see cref="ParametrosTreinamento.UsarFitnessV2"/>) quando uma cooperação concedida terminou com o alvo abatido — recompensa o RESULTADO da cooperação, não só o pedido.</summary>
    private void RecompensarCooperacaoSeConcedida(EstadoEpisodio estado, AlvoTreinamento alvo, bool cooperacaoFoiConcedida)
    {
        if (!_parametros.UsarFitnessV2 || !cooperacaoFoiConcedida || alvo.Ativo) return;
        estado.FitnessAcumulado += _parametros.RecompensaCooperacaoBemSucedida;
    }
}
