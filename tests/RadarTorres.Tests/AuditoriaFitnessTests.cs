using System.Collections.Generic;
using System.Linq;
using RadarTorres.App.Configuration;
using RadarTorres.App.NeuralEvolution;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobre as correções da auditoria de penalidades (2026-11-12): recusa de disparo não pode mais
/// ser recontada/repenalizada a cada passo de 0,2s dentro da MESMA janela de oportunidade, e a
/// penalidade de sobrecarga entre torres não pode mais punir concentração de trabalho justificada
/// pela posição dos alvos. Também fixa o registro de <see cref="MotivoFimEpisodio"/> e a divisão
/// de <see cref="EstadoEpisodio.AlvosEscapados"/> em fuga-pela-base vs. invasão-de-torre.
/// </summary>
public sealed class AuditoriaFitnessTests
{
    private static readonly List<TowerDefinition> QuatroTorres =
    [
        new TowerDefinition { Id = 1, Name = "Torre 1", X = 5, Y = 5 },
        new TowerDefinition { Id = 2, Name = "Torre 2", X = -5, Y = 5 },
        new TowerDefinition { Id = 3, Name = "Torre 3", X = -5, Y = -5 },
        new TowerDefinition { Id = 4, Name = "Torre 4", X = 5, Y = -5 },
    ];

    /// <summary>
    /// Rede que SEMPRE recusa disparo (confiança bem negativa), independentemente das entradas —
    /// isola a janela de oportunidade de qualquer sorte de decisão.
    /// </summary>
    private static RedeNeat RedeQueSempreRecusa() => RedesDeTeste.ComPesosParaSaidas(pesoAjuste: 0.0, pesoConfianca: -5.0);

    [Fact]
    public void RecusaDeDisparo_JanelaContinua_ContaUmaUnicaOportunidadeNaoUmaPorPasso()
    {
        // Um único alvo, devagar, nascendo perto da torre 1 (Q1) com certeza alta desde o início:
        // a janela de oportunidade abre cedo e fica aberta por VÁRIOS passos de 0,2s até o
        // episódio terminar (fuga pela base) — sem a correção, cada um desses passos seria
        // contado como uma oportunidade nova e penalizado de novo.
        var parametros = new ParametrosTreinamento
        {
            DuracaoMaximaSegurancaSegundos = 30
        };
        var ambiente = new AmbienteTreinamento(parametros, QuatroTorres);

        var spawns = new List<SpawnScriptado>
        {
            new(InstanteSegundos: 0, AnguloNascimentoGraus: 45, VelocidadeMetrosPorSegundo: 0.3,
                CertezaBase: 0.9, TaxaCrescimentoCertezaPorSegundo: 0.0, FracaoRaioNascimento: 0.5)
        };

        EstadoEpisodio estado = ambiente.NovoEpisodioComSpawnsPersonalizados(spawns, seed: 1);
        var controlador = new ControladorHibrido(RedeQueSempreRecusa(), parametros.RaioMundoMetros, parametros.VelocidadeMaximaAlvoMetrosPorSegundo);

        const double dt = 0.2;
        while (!estado.Finalizado) ambiente.Avancar(estado, controlador, dt);

        // A janela ficou aberta por muitos passos (a distância percorrida pelo alvo devagar até a
        // base garante bem mais que um único passo de 0,2s de oportunidade) — a correção exige
        // que isso conte como UMA oportunidade, não uma por passo.
        Assert.Equal(1, estado.OportunidadesDeDisparo);
        Assert.Equal(1, estado.DisparosRecusadosPelaRede);
    }

    [Fact]
    public void SobrecargaEvitavel_ConcentracaoDeAlvosNoMesmoQuadrante_NaoGeraPenalidadeAlta()
    {
        // TODOS os alvos nascem no mesmo quadrante (45° => Q1, torre do canto 5,5): só essa torre
        // tem qualquer demanda ou comprometimento durante o episódio inteiro — é concentração
        // justificada pela posição dos alvos, não sobrecarga evitável. A fórmula ANTIGA (desvio
        // padrão da desigualdade bruta entre torres) penalizaria isso pesadamente, já que as
        // outras 3 torres ficam em 0 segundos comprometidos o episódio inteiro.
        var parametros = new ParametrosTreinamento
        {
            DuracaoMaximaSegurancaSegundos = 15
        };
        var ambiente = new AmbienteTreinamento(parametros, QuatroTorres);

        var spawns = Enumerable.Range(0, 4)
            .Select(i => new SpawnScriptado(
                InstanteSegundos: i * 1.0,
                AnguloNascimentoGraus: 45,
                VelocidadeMetrosPorSegundo: 1.5,
                CertezaBase: 0.9,
                TaxaCrescimentoCertezaPorSegundo: 0.0))
            .ToList();

        EstadoEpisodio estado = ambiente.NovoEpisodioComSpawnsPersonalizados(spawns, seed: 7);
        var controlador = new ControladorHibrido(RedesDeTeste.Zerada(), parametros.RaioMundoMetros, parametros.VelocidadeMaximaAlvoMetrosPorSegundo, ControladorHibrido.ModoDecisao.HeuristicaPura);

        const double dt = 0.2;
        while (!estado.Finalizado) ambiente.Avancar(estado, controlador, dt);

        // Residual pequeno (comprometido ~= demanda na única torre ocupada, 0 nas demais que
        // nunca tiveram alvo nenhum) — bem abaixo do que a desigualdade bruta acusaria.
        Assert.True(estado.PenalidadeSobrecargaAplicada < 1.0,
            $"Penalidade de sobrecarga alta ({estado.PenalidadeSobrecargaAplicada:0.00}) para uma concentração justificada pela posição dos alvos — a fórmula deveria usar o resíduo contra a demanda, não a desigualdade bruta.");
    }

    [Fact]
    public void MotivoFim_InvasaoDeTorre_EDistintoDeFugaPelaBase()
    {
        // Alvo nasce na borda a 45° e se move em linha reta rumo à base: como a torre 1 está
        // EXATAMENTE sobre essa mesma reta (5,5), a trajetória passa a menos de 1m dela bem antes
        // de chegar perto o bastante da base (raio 0,4m) para contar como fuga.
        var parametros = new ParametrosTreinamento { DuracaoMaximaSegurancaSegundos = 15 };
        var ambiente = new AmbienteTreinamento(parametros, QuatroTorres);

        var spawns = new List<SpawnScriptado>
        {
            new(InstanteSegundos: 0, AnguloNascimentoGraus: 45, VelocidadeMetrosPorSegundo: 1.0,
                CertezaBase: 0.9, TaxaCrescimentoCertezaPorSegundo: 0.0)
        };

        EstadoEpisodio estado = ambiente.NovoEpisodioComSpawnsPersonalizados(spawns, seed: 3);
        var controlador = new ControladorHibrido(RedesDeTeste.Zerada(), parametros.RaioMundoMetros, parametros.VelocidadeMaximaAlvoMetrosPorSegundo);

        const double dt = 0.2;
        while (!estado.Finalizado) ambiente.Avancar(estado, controlador, dt);

        Assert.Equal(MotivoFimEpisodio.InvasaoDeTorre, estado.MotivoFim);
        Assert.Equal(1, estado.AlvosQueInvadiramTorre);
        Assert.Equal(0, estado.AlvosEscapadosPelaBase);
        Assert.Equal(estado.AlvosEscapadosPelaBase + estado.AlvosQueInvadiramTorre, estado.AlvosEscapados);
    }

    [Fact]
    public void MotivoFim_SemNenhumAlvo_TerminaNoTetoDeSeguranca()
    {
        var parametros = new ParametrosTreinamento { DuracaoMaximaSegurancaSegundos = 2 };
        var ambiente = new AmbienteTreinamento(parametros, QuatroTorres);

        EstadoEpisodio estado = ambiente.NovoEpisodioComSpawnsPersonalizados([], seed: 1);
        var controlador = new ControladorHibrido(RedesDeTeste.Zerada(), parametros.RaioMundoMetros, parametros.VelocidadeMaximaAlvoMetrosPorSegundo);

        const double dt = 0.2;
        while (!estado.Finalizado) ambiente.Avancar(estado, controlador, dt);

        Assert.Equal(MotivoFimEpisodio.TetoDeSeguranca, estado.MotivoFim);
    }
}
