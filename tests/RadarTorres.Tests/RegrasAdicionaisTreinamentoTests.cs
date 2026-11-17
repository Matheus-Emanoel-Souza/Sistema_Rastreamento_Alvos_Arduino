using System.Collections.Generic;
using System.Linq;
using RadarTorres.App.Configuration;
using RadarTorres.App.NeuralEvolution;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobre as três regras novas do ambiente de treinamento (2026-11-13), cada uma independente e
/// desligada por padrão: bônus de "ataque conjunto" (<see cref="ParametrosTreinamento.HabilitarBonusAtaqueConjunto"/>),
/// troca dinâmica de alvo por proximidade (<see cref="ParametrosTreinamento.PermitirTrocaDeAlvoPorProximidade"/>)
/// e desativação manual de torres (<see cref="ParametrosTreinamento.TorresDesativadasIds"/>).
/// </summary>
public sealed class RegrasAdicionaisTreinamentoTests
{
    private static readonly List<TowerDefinition> QuatroTorres =
    [
        new TowerDefinition { Id = 1, Name = "Torre 1", X = 5, Y = 5 },
        new TowerDefinition { Id = 2, Name = "Torre 2", X = -5, Y = 5 },
        new TowerDefinition { Id = 3, Name = "Torre 3", X = -5, Y = -5 },
        new TowerDefinition { Id = 4, Name = "Torre 4", X = 5, Y = -5 },
    ];

    [Fact]
    public void TorresDesativadas_Todas_NenhumAlvoEEngajadoEEleEscapaPelaBase()
    {
        // Todas as 4 torres desativadas manualmente — simula "sistema com zero recursos". O alvo
        // nasce a 0° (eixo X) e anda em linha reta até a base, sem passar perto de nenhuma torre
        // (para isolar "nunca foi selecionado" de "invadiu uma torre por coincidência de rota).
        var parametros = new ParametrosTreinamento
        {
            DuracaoMaximaSegurancaSegundos = 15,
            TorresDesativadasIds = new HashSet<int> { 1, 2, 3, 4 }
        };
        var ambiente = new AmbienteTreinamento(parametros, QuatroTorres);

        var spawns = new List<SpawnScriptado>
        {
            new(InstanteSegundos: 0, AnguloNascimentoGraus: 0, VelocidadeMetrosPorSegundo: 1.5,
                CertezaBase: 0.9, TaxaCrescimentoCertezaPorSegundo: 0.0)
        };

        EstadoEpisodio estado = ambiente.NovoEpisodioComSpawnsPersonalizados(spawns, seed: 1);
        var controlador = new ControladorHibrido(RedesDeTeste.Zerada(), parametros.RaioMundoMetros, parametros.VelocidadeMaximaAlvoMetrosPorSegundo, ControladorHibrido.ModoDecisao.HeuristicaPura);

        const double dt = 0.2;
        while (!estado.Finalizado) ambiente.Avancar(estado, controlador, dt);

        Assert.Equal(0, estado.AlvosAbatidos);
        Assert.Equal(1, estado.AlvosEscapadosPelaBase);
        Assert.Equal(0, estado.AlvosQueInvadiramTorre);
    }

    [Fact]
    public void TorresDesativadas_Vazio_ComportamentoPadraoContinuaEngajando()
    {
        // Controle do teste acima: com o conjunto vazio (padrão), a mesma rota é defendida
        // normalmente — confirma que a ausência de torres desativadas não muda nada.
        var parametros = new ParametrosTreinamento { DuracaoMaximaSegurancaSegundos = 15 };
        var ambiente = new AmbienteTreinamento(parametros, QuatroTorres);

        var spawns = new List<SpawnScriptado>
        {
            new(InstanteSegundos: 0, AnguloNascimentoGraus: 0, VelocidadeMetrosPorSegundo: 1.5,
                CertezaBase: 0.9, TaxaCrescimentoCertezaPorSegundo: 0.0)
        };

        EstadoEpisodio estado = ambiente.NovoEpisodioComSpawnsPersonalizados(spawns, seed: 1);
        var controlador = new ControladorHibrido(RedesDeTeste.Zerada(), parametros.RaioMundoMetros, parametros.VelocidadeMaximaAlvoMetrosPorSegundo, ControladorHibrido.ModoDecisao.HeuristicaPura);

        const double dt = 0.2;
        while (!estado.Finalizado) ambiente.Avancar(estado, controlador, dt);

        Assert.True(estado.AlvosAbatidos > 0 || estado.TirosErrados > 0,
            "Com todas as torres ativas e a heurística pura (sempre dispara quando autorizada), o alvo deveria pelo menos ser engajado (abatido ou tiro errado), não escapar sem nenhuma tentativa.");
    }

    [Fact]
    public void TrocaDeAlvoPorProximidade_Desligada_TorrePermaneceComAlvoOriginal()
    {
        EstadoEpisodio estado = ExecutarCenarioDeTroca(permitirTroca: false, out AlvoTreinamento alvoA, out AlvoTreinamento alvoB, out TorreTreinamento torre1);

        Assert.Equal(torre1.Id, alvoA.TorreSelecionadaId);
        Assert.Null(alvoB.TorreSelecionadaId);
        Assert.Equal(0, estado.TrocasDeAlvoPorProximidade);
    }

    [Fact]
    public void TrocaDeAlvoPorProximidade_Ligada_TorreTrocaParaOAlvoMaisProximoEOAnteriorContinuaVivo()
    {
        EstadoEpisodio estado = ExecutarCenarioDeTroca(permitirTroca: true, out AlvoTreinamento alvoA, out AlvoTreinamento alvoB, out TorreTreinamento torre1);

        // Alvo B nasceu mais perto da torre 1 do que o A (ver geometria no cenário) — a torre
        // troca para ele, e o A, apesar de abandonado, continua vivo e disponível.
        Assert.Equal(torre1.Id, alvoB.TorreSelecionadaId);
        Assert.Null(alvoA.TorreSelecionadaId);
        Assert.True(alvoA.Ativo);
        Assert.True(estado.TrocasDeAlvoPorProximidade >= 1);
    }

    /// <summary>
    /// Só a torre 1 fica ativa (as outras 3 são desativadas, para a seleção ser inequívoca). O
    /// alvo A nasce na borda do mundo (bem longe da torre 1); 1s depois o alvo B nasce bem mais
    /// perto dela (fração pequena do raio, mesmo ângulo) — mais perto que o A está naquele
    /// instante. A rede recusa SEMPRE disparar, para isolar a seleção de alvo do disparo.
    /// </summary>
    private static EstadoEpisodio ExecutarCenarioDeTroca(bool permitirTroca, out AlvoTreinamento alvoA, out AlvoTreinamento alvoB, out TorreTreinamento torre1)
    {
        var parametros = new ParametrosTreinamento
        {
            DuracaoMaximaSegurancaSegundos = 30,
            PermitirTrocaDeAlvoPorProximidade = permitirTroca,
            TorresDesativadasIds = new HashSet<int> { 2, 3, 4 }
        };
        var ambiente = new AmbienteTreinamento(parametros, QuatroTorres);

        var spawns = new List<SpawnScriptado>
        {
            // Alvo A: borda do mundo, a 45° (mesmo quadrante da torre 1) — distância até a torre
            // 1 (5,5) de ~2,93 m.
            new(InstanteSegundos: 0, AnguloNascimentoGraus: 45, VelocidadeMetrosPorSegundo: 0.1,
                CertezaBase: 1.0, TaxaCrescimentoCertezaPorSegundo: 0.0, FracaoRaioNascimento: 1.0),
            // Alvo B: nasce 1s depois, mesmo ângulo, metade do raio — distância até a torre 1 de
            // ~2,06 m, menor que a do A no mesmo instante.
            new(InstanteSegundos: 1.0, AnguloNascimentoGraus: 45, VelocidadeMetrosPorSegundo: 0.1,
                CertezaBase: 1.0, TaxaCrescimentoCertezaPorSegundo: 0.0, FracaoRaioNascimento: 0.5),
        };

        EstadoEpisodio estado = ambiente.NovoEpisodioComSpawnsPersonalizados(spawns, seed: 1);
        var controlador = new ControladorHibrido(RedesDeTeste.ComPesosParaSaidas(pesoAjuste: 0.0, pesoConfianca: -5.0), parametros.RaioMundoMetros, parametros.VelocidadeMaximaAlvoMetrosPorSegundo);

        const double dt = 0.2;
        for (int i = 0; i < 7; i++) ambiente.Avancar(estado, controlador, dt); // até t=1.4s — alvo B já nasceu em t=1.0 e a reavaliação já rodou

        alvoA = estado.Alvos.Single(a => a.Id == 1);
        alvoB = estado.Alvos.Single(a => a.Id == 2);
        torre1 = estado.Torres.Single(t => t.Id == 1);
        return estado;
    }

    [Fact]
    public void BonusAtaqueConjunto_QuandoLigadoSoSomaNoAbateQueDependeuDaTorreDeAjuda()
    {
        // Geometria: torre 1 é a principal (mais próxima do nascimento), torre 2 fica a uma
        // distância da trajetória sempre dentro do raio de ajuda (2 m) mas fora do raio de
        // invasão (1 m) — ver constantes abaixo. O eixo Z liga só para que TODAS as torres
        // contem como candidatas/ajuda, independente de quadrante (VisivelPorTodasAsTorres),
        // o que elimina a necessidade de alinhar quadrantes nesta geometria sintética.
        var parametros = new ParametrosTreinamento
        {
            DuracaoMaximaSegurancaSegundos = 10,
            RaioMundoMetros = 2.5,
            VelocidadeAngularTorreGrausPorSegundo = 2000, // mira alinha "instantaneamente" — isola a regra de pontuação da física de giro, já cobertas noutros testes
            EixoZHabilitado = true,
            AlturaVisivelPorTodasAsTorresMetros = 0.0,
            VelocidadeRapidaMetrosPorSegundo = 1.0,
        };

        var torres = new List<TowerDefinition>
        {
            new() { Id = 1, Name = "Torre 1", X = 1.5, Y = 1.0 },
            new() { Id = 2, Name = "Torre 2", X = 1.6, Y = 0.9 },
        };

        var spawns = new List<SpawnScriptado>
        {
            new(InstanteSegundos: 0, AnguloNascimentoGraus: 90, VelocidadeMetrosPorSegundo: 2.0,
                CertezaBase: 1.0, TaxaCrescimentoCertezaPorSegundo: 0.0, AlturaMetros: 1.0),
        };

        // Procura (determinística — mesma seed sempre dá o mesmo episódio) a primeira seed em
        // que a cooperação de fato decide um abate, com o bônus ligado.
        int? seedEncontrada = null;
        int ataquesNaSeedEncontrada = 0;

        for (int seed = 1; seed <= 500; seed++)
        {
            var parametrosComBonus = parametros with { HabilitarBonusAtaqueConjunto = true, RecompensaAtaqueConjunto = 5.0 };
            var ambienteComBonus = new AmbienteTreinamento(parametrosComBonus, torres);
            EstadoEpisodio estadoComBonus = ambienteComBonus.NovoEpisodioComSpawnsPersonalizados(spawns, seed);
            var controladorComBonus = new ControladorHibrido(RedesDeTeste.Zerada(), parametrosComBonus.RaioMundoMetros, parametrosComBonus.VelocidadeMaximaAlvoMetrosPorSegundo, ControladorHibrido.ModoDecisao.HeuristicaPura);

            const double dt = 0.2;
            while (!estadoComBonus.Finalizado) ambienteComBonus.Avancar(estadoComBonus, controladorComBonus, dt);

            if (estadoComBonus.AtaquesConjuntosBemSucedidos > 0)
            {
                seedEncontrada = seed;
                ataquesNaSeedEncontrada = estadoComBonus.AtaquesConjuntosBemSucedidos;

                // Mesma seed, bônus DESLIGADO: o RNG é o mesmo objeto (Random(seed)) consumido
                // na MESMA ordem pelas mesmas chamadas — only a pontuação deveria diferir, não
                // quem foi abatido nem quantos tiros erraram.
                var parametrosSemBonus = parametros with { HabilitarBonusAtaqueConjunto = false };
                var ambienteSemBonus = new AmbienteTreinamento(parametrosSemBonus, torres);
                EstadoEpisodio estadoSemBonus = ambienteSemBonus.NovoEpisodioComSpawnsPersonalizados(spawns, seed);
                var controladorSemBonus = new ControladorHibrido(RedesDeTeste.Zerada(), parametrosSemBonus.RaioMundoMetros, parametrosSemBonus.VelocidadeMaximaAlvoMetrosPorSegundo, ControladorHibrido.ModoDecisao.HeuristicaPura);
                while (!estadoSemBonus.Finalizado) ambienteSemBonus.Avancar(estadoSemBonus, controladorSemBonus, dt);

                Assert.Equal(estadoComBonus.AlvosAbatidos, estadoSemBonus.AlvosAbatidos);
                Assert.Equal(estadoComBonus.TirosErrados, estadoSemBonus.TirosErrados);
                Assert.Equal(0, estadoSemBonus.AtaquesConjuntosBemSucedidos);
                Assert.Equal(
                    estadoSemBonus.FitnessAcumulado + parametrosComBonus.RecompensaAtaqueConjunto * ataquesNaSeedEncontrada,
                    estadoComBonus.FitnessAcumulado, precision: 6);
                break;
            }
        }

        Assert.True(seedEncontrada.HasValue,
            "Nenhuma das 500 seeds testadas produziu um abate cooperativo (torre principal erra, torre de ajuda acerta) — a geometria do cenário pode ter deixado de colocar a torre de ajuda dentro do raio na janela de disparo.");
    }
}
