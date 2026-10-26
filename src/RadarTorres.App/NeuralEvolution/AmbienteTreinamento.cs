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

    /// <summary>Cria um episódio novo (torres na posição base, sem alvos) — opcionalmente reproduzível via <paramref name="seed"/>.</summary>
    public EstadoEpisodio NovoEpisodio(int? seed = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var estado = new EstadoEpisodio(rng);

        foreach (TorreTreinamento baseTorre in _torresBase)
        {
            estado.Torres.Add(new TorreTreinamento
            {
                Id = baseTorre.Id,
                X = baseTorre.X,
                Y = baseTorre.Y,
                QuadrantePreferido = baseTorre.QuadrantePreferido
            });
        }

        estado.ProximoSpawnSegundos = ProximoIntervaloSpawn(rng);
        return estado;
    }

    /// <summary>Roda um episódio completo (sem passos intermediários visíveis) e devolve o resultado — é a função de fitness do algoritmo genético.</summary>
    public EpisodioResultado ExecutarEpisodioRapido(double[] genoma, int? seed = null)
    {
        EstadoEpisodio estado = NovoEpisodio(seed);
        var rede = new RedeNeural(ControladorHibrido.ArquiteturaPadrao, genoma);
        var controlador = new ControladorHibrido(rede, _parametros.RaioMundoMetros, _parametros.VelocidadeMaximaAlvoMetrosPorSegundo);

        const double dt = 0.2;
        while (!estado.Finalizado)
        {
            Avancar(estado, controlador, dt);
        }

        return new EpisodioResultado(estado.FitnessAcumulado, estado.AlvosAbatidos, estado.AlvosEscapados, estado.TirosErrados);
    }

    /// <summary>
    /// Fitness de um genoma, calculado como a MÉDIA de vários episódios com <paramref name="seeds"/>
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
    public double AvaliarFitness(double[] genoma, IReadOnlyList<int> seeds)
    {
        double soma = 0;
        foreach (int seed in seeds)
        {
            soma += ExecutarEpisodioRapido(genoma, seed).FitnessTotal;
        }
        return soma / seeds.Count;
    }

    /// <summary>Avança o episódio em <paramref name="dt"/> segundos: nascimento, movimento, seleção de torre e decisão de disparo.</summary>
    public void Avancar(EstadoEpisodio estado, ControladorHibrido controlador, double dt)
    {
        if (estado.Finalizado) return;

        estado.TempoDecorridoSegundos += dt;

        TalvezNascerAlvo(estado);
        MoverAlvos(estado, dt);
        AplicarPenalidadeDeProximidade(estado);
        TratarAlvosNaBase(estado);
        SelecionarTorresParaAlvosLivres(estado, controlador);
        DecidirDisparos(estado, controlador);

        estado.Alvos.RemoveAll(a => !a.Ativo);

        if (estado.TempoDecorridoSegundos >= _parametros.DuracaoEpisodioSegundos)
        {
            estado.Finalizado = true;
        }
    }

    private double ProximoIntervaloSpawn(Random rng) =>
        _parametros.IntervaloSpawnMinSegundos + rng.NextDouble() * (_parametros.IntervaloSpawnMaxSegundos - _parametros.IntervaloSpawnMinSegundos);

    private void TalvezNascerAlvo(EstadoEpisodio estado)
    {
        if (estado.TempoDecorridoSegundos < estado.ProximoSpawnSegundos) return;
        estado.ProximoSpawnSegundos = estado.TempoDecorridoSegundos + ProximoIntervaloSpawn(estado.Rng);

        if (estado.Alvos.Count(a => a.Ativo) >= _parametros.MaxAlvosSimultaneos) return;

        // Nasce num ponto aleatório da borda do mundo (círculo de raio RaioMundoMetros) e se
        // move em direção à base com um pequeno desvio angular — não é uma linha perfeitamente
        // radial, para a torre precisar reavaliar a mira ao longo da aproximação.
        double anguloNascimento = estado.Rng.NextDouble() * 2 * Math.PI;
        double x = _parametros.RaioMundoMetros * Math.Cos(anguloNascimento);
        double y = _parametros.RaioMundoMetros * Math.Sin(anguloNascimento);

        double desvioRadianos = (estado.Rng.NextDouble() - 0.5) * (Math.PI / 6); // até ±30°
        double anguloMovimento = anguloNascimento + Math.PI + desvioRadianos;

        double velocidade = _parametros.VelocidadeMinimaAlvoMetrosPorSegundo +
            estado.Rng.NextDouble() * (_parametros.VelocidadeMaximaAlvoMetrosPorSegundo - _parametros.VelocidadeMinimaAlvoMetrosPorSegundo);

        estado.Alvos.Add(new AlvoTreinamento
        {
            Id = estado.ProximoIdAlvo(),
            X = x,
            Y = y,
            VelocidadeX = velocidade * Math.Cos(anguloMovimento),
            VelocidadeY = velocidade * Math.Sin(anguloMovimento),
            VelocidadeMetrosPorSegundo = velocidade,
            Quadrante = QuadrantHelper.Determine(x, y),
            // Certeza nasce baixa e SOBE com o tempo de observação (ver AlvoTreinamento.Certeza)
            // — nunca fica presa abaixo do limiar para sempre; só demora mais ou menos conforme
            // a taxa sorteada aqui.
            CertezaBase = 0.20 + estado.Rng.NextDouble() * 0.30,
            TaxaCrescimentoCertezaPorSegundo = 0.08 + estado.Rng.NextDouble() * 0.17
        });
    }

    private static void MoverAlvos(EstadoEpisodio estado, double dt)
    {
        foreach (AlvoTreinamento alvo in estado.Alvos.Where(a => a.Ativo))
        {
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
        }
    }

    /// <summary>Regra 6 (ajuda de torre adjacente): mesmo raio usado em produção (ver <see cref="Services.TowerSelectionService"/>).</summary>
    private const double RaioAjudaTorreAdjacenteMetros = 2.0;

    private void DecidirDisparos(EstadoEpisodio estado, ControladorHibrido controlador)
    {
        foreach (AlvoTreinamento alvo in estado.Alvos.Where(a => a.Ativo && a.TorreSelecionadaId is not null))
        {
            // Únicos critérios inegociáveis, independente da distância até o alvo: fora de zona
            // morta e certeza suficiente (mesma regra de Services.FireControlService.Authorize).
            if (_parametros.ZonasMortasQuadrantes.Contains(alvo.Quadrante)) continue;
            if (alvo.Certeza < _parametros.LimiarConfiancaDisparoFracao) continue;

            TorreTreinamento? torre = estado.Torres.FirstOrDefault(t => t.Id == alvo.TorreSelecionadaId);
            if (torre is null) continue;

            if (!controlador.DecidirDisparo(alvo, torre)) continue;

            Disparar(estado, torre, alvo);
            if (!alvo.Ativo) continue; // abatido pela torre principal — não precisa de ajuda

            // Regra 6 (ajuda de torre adjacente): alvo acima da velocidade considerada rápida
            // também recebe o disparo de uma torre de quadrante adjacente a até
            // RaioAjudaTorreAdjacenteMetros dele, mesmo sem ser a torre principal comprometida —
            // um segundo canhão reduz a chance de o alvo escapar de um disparo único (a chance de
            // errar cresce com a velocidade, ver CalculadoraErroDisparo).
            if (alvo.VelocidadeMetrosPorSegundo >= _parametros.VelocidadeRapidaMetrosPorSegundo)
            {
                TorreTreinamento? ajuda = estado.Torres.FirstOrDefault(t =>
                    t.Id != torre.Id &&
                    QuadrantHelper.AreAdjacent(t.QuadrantePreferido, alvo.Quadrante) &&
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
}
