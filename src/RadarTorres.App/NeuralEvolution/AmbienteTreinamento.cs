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

        return new EpisodioResultado(estado.FitnessAcumulado, estado.AlvosAbatidos, estado.AlvosEscapados);
    }

    /// <summary>Avança o episódio em <paramref name="dt"/> segundos: nascimento, movimento, seleção de torre e decisão de disparo.</summary>
    public void Avancar(EstadoEpisodio estado, ControladorHibrido controlador, double dt)
    {
        if (estado.Finalizado) return;

        estado.TempoDecorridoSegundos += dt;

        TalvezNascerAlvo(estado);
        MoverAlvos(estado, dt);
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
            Quadrante = QuadrantHelper.Determine(x, y)
        });
    }

    private static void MoverAlvos(EstadoEpisodio estado, double dt)
    {
        foreach (AlvoTreinamento alvo in estado.Alvos.Where(a => a.Ativo))
        {
            alvo.X += alvo.VelocidadeX * dt;
            alvo.Y += alvo.VelocidadeY * dt;
            alvo.Quadrante = QuadrantHelper.Determine(alvo.X, alvo.Y);

            if (alvo.TorreSelecionadaId is not null)
            {
                alvo.TempoDesdeSelecao += dt;
            }
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

    private void DecidirDisparos(EstadoEpisodio estado, ControladorHibrido controlador)
    {
        foreach (AlvoTreinamento alvo in estado.Alvos.Where(a => a.Ativo && a.TorreSelecionadaId is not null))
        {
            TorreTreinamento? torre = estado.Torres.FirstOrDefault(t => t.Id == alvo.TorreSelecionadaId);
            if (torre is null) continue;

            if (!controlador.DecidirDisparo(alvo, torre, _parametros.DistanciaEngajamentoMetros)) continue;

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
