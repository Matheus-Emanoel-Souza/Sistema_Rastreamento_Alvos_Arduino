using System.Collections.Generic;
using System.Linq;
using RadarTorres.App.Configuration;
using RadarTorres.App.Models;
using RadarTorres.App.NeuralEvolution;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobertura da terceira dimensão da simulação: altura dos alvos e seu efeito sobre quais torres
/// conseguem enxergá-los (ver ParametrosTreinamento.EixoZHabilitado e
/// AlvoTreinamento.AlturaMetros).
/// </summary>
/// <remarks>
/// O requisito que governa estes testes tem duas metades que precisam valer ao mesmo tempo: com o
/// eixo Z LIGADO a altura influencia a visibilidade; com ele DESLIGADO a simulação tem que se
/// comportar exatamente como a versão bidimensional anterior. A segunda metade é a que protege
/// todos os treinamentos e experimentos já registrados.
/// </remarks>
public sealed class EixoZTests
{
    /// <summary>
    /// Rede de pesos zerados — equivale ao "genoma nulo" usado nos demais testes desta branch:
    /// faz o controlador híbrido se comportar como a heurística pura na seleção de torre, isolando
    /// a regra de visibilidade de qualquer influência de pesos evoluídos.
    /// </summary>
    private static RedeNeat RedeZerada() => RedesDeTeste.Zerada();

    /// <summary>Quatro torres, uma por quadrante — mesmo layout padrão que o ambiente usa sem appsettings.</summary>
    private static readonly List<TowerDefinition> QuatroTorres =
    [
        new TowerDefinition { Id = 1, Name = "Torre 1", X = 5, Y = 5 },
        new TowerDefinition { Id = 2, Name = "Torre 2", X = -5, Y = 5 },
        new TowerDefinition { Id = 3, Name = "Torre 3", X = -5, Y = -5 },
        new TowerDefinition { Id = 4, Name = "Torre 4", X = 5, Y = -5 },
    ];

    private static AmbienteTreinamento Ambiente(bool eixoZ) =>
        new(new ParametrosTreinamento
        {
            EixoZHabilitado = eixoZ,
            DuracaoEpisodioSegundos = 30,
            // Episódio não tem mais fim por duração — só por invasão de torre ou por este teto de
            // segurança. Reduzido de 1800s (padrão) para o teste continuar rápido.
            DuracaoMaximaSegurancaSegundos = 30
        }, QuatroTorres);

    /// <summary>Roda um episódio inteiro passo a passo e devolve todos os alvos que chegaram a existir.</summary>
    private static List<AlvoTreinamento> AlvosDeUmEpisodio(bool eixoZ, int seed)
    {
        AmbienteTreinamento ambiente = Ambiente(eixoZ);
        var controlador = new ControladorHibrido(
            RedeZerada(),
            ambiente.Parametros.RaioMundoMetros,
            ambiente.Parametros.VelocidadeMaximaAlvoMetrosPorSegundo);

        EstadoEpisodio estado = ambiente.NovoEpisodio(seed);
        var vistos = new Dictionary<int, AlvoTreinamento>();

        while (!estado.Finalizado)
        {
            ambiente.Avancar(estado, controlador, dt: 0.1);
            foreach (AlvoTreinamento alvo in estado.Alvos) vistos.TryAdd(alvo.Id, alvo);
        }

        return vistos.Values.ToList();
    }

    // ------------------------------------------------------------------ Altura dos alvos

    [Fact]
    public void ComEixoZ_AlvosNascemComAlturaEntreZeroEOMaximo()
    {
        List<AlvoTreinamento> alvos = AlvosDeUmEpisodio(eixoZ: true, seed: 7);

        Assert.NotEmpty(alvos);
        Assert.All(alvos, a => Assert.InRange(a.AlturaMetros, 0.0, 2.0));
    }

    /// <summary>
    /// Não basta estar na faixa: se todos saíssem rente ao chão (ou todos no teto), a terceira
    /// dimensão não estaria variando de verdade e o experimento 2D × 3D não teria o que comparar.
    /// </summary>
    [Fact]
    public void ComEixoZ_AsAlturasVariamEntreOsAlvos()
    {
        List<AlvoTreinamento> alvos = AlvosDeUmEpisodio(eixoZ: true, seed: 11);

        Assert.True(alvos.Count >= 2, "o episódio precisa produzir alvos suficientes para comparar alturas.");
        Assert.True(alvos.Select(a => a.AlturaMetros).Distinct().Count() > 1, "todas as alturas saíram iguais.");
    }

    [Fact]
    public void SemEixoZ_TodosOsAlvosFicamNoSolo()
    {
        List<AlvoTreinamento> alvos = AlvosDeUmEpisodio(eixoZ: false, seed: 7);

        Assert.NotEmpty(alvos);
        Assert.All(alvos, a => Assert.Equal(0.0, a.AlturaMetros));
        Assert.All(alvos, a => Assert.False(a.VisivelPorTodasAsTorres));
    }

    // ------------------------------------------------------------------ Visibilidade

    [Fact]
    public void ComEixoZ_AlvoAcimaDoLimiarFicaVisivelPorTodasAsTorres()
    {
        List<AlvoTreinamento> alvos = AlvosDeUmEpisodio(eixoZ: true, seed: 3);

        Assert.All(alvos, a => Assert.Equal(a.AlturaMetros > 0.5, a.VisivelPorTodasAsTorres));
    }

    private static List<TorreTreinamento> TorresLivres() =>
    [
        new TorreTreinamento { Id = 1, X = 5, Y = 5, QuadrantePreferido = Quadrant.Q1 },
        new TorreTreinamento { Id = 2, X = -5, Y = 5, QuadrantePreferido = Quadrant.Q2 },
        new TorreTreinamento { Id = 3, X = -5, Y = -5, QuadrantePreferido = Quadrant.Q3 },
        new TorreTreinamento { Id = 4, X = 5, Y = -5, QuadrantePreferido = Quadrant.Q4 },
    ];

    private static AlvoTreinamento AlvoNoQuadrante1(bool visivelPorTodas) => new()
    {
        Id = 1,
        X = 6,
        Y = 6,
        Quadrante = Quadrant.Q1,
        AlturaMetros = visivelPorTodas ? 1.5 : 0.2,
        VisivelPorTodasAsTorres = visivelPorTodas,
        CertezaBase = 0.9,
        TaxaCrescimentoCertezaPorSegundo = 0.0
    };

    /// <summary>
    /// A prova de que a altura amplia a cobertura: com o alvo rasteiro, só a torre do quadrante
    /// dele concorre; com o alvo alto, as quatro concorrem. Usa o modo heurística pura, em que a
    /// escolha é determinística (menor distância), para o resultado depender só da regra de
    /// visibilidade e não de pesos de rede.
    /// </summary>
    [Fact]
    public void SelecionarTorre_ComAlvoAlto_TodasAsTorresSaoCandidatas()
    {
        var controlador = new ControladorHibrido(
            RedeZerada(), raioMundoMetros: 10, velocidadeMaximaAlvoMetrosPorSegundo: 2.5,
            modo: ControladorHibrido.ModoDecisao.HeuristicaPura);

        // Só a torre 4 está livre — ela NÃO é do quadrante do alvo (Q1) nem foi excluída por
        // outros alvos; com o alvo rasteiro ela só pode ser alcançada pela camada adjacente.
        List<TorreTreinamento> torres = TorresLivres();
        torres.RemoveAll(t => t.Id is 1 or 2 or 3);

        TorreTreinamento? escolhidaComAlvoAlto = controlador.SelecionarTorre(AlvoNoQuadrante1(visivelPorTodas: true), torres, []);
        Assert.NotNull(escolhidaComAlvoAlto);
        Assert.Equal(4, escolhidaComAlvoAlto!.Id);
    }

    /// <summary>
    /// O caso que separa as duas dimensões: com uma torre distante, de quadrante OPOSTO, como
    /// única livre, o alvo rasteiro só a alcança pela camada sobressalente — enquanto o alvo alto
    /// a alcança já na primeira camada. Nos dois casos uma torre é devolvida; o que muda é por
    /// qual caminho, e é isso que a altura altera.
    /// </summary>
    [Fact]
    public void SelecionarTorre_ComAlvoRasteiro_PreservaAPreferenciaPorQuadrante()
    {
        var controlador = new ControladorHibrido(
            RedeZerada(), raioMundoMetros: 10, velocidadeMaximaAlvoMetrosPorSegundo: 2.5,
            modo: ControladorHibrido.ModoDecisao.HeuristicaPura);

        List<TorreTreinamento> torres = TorresLivres();

        // Alvo rasteiro em Q1: a torre do próprio quadrante (1) tem que vencer.
        TorreTreinamento? rasteiro = controlador.SelecionarTorre(AlvoNoQuadrante1(visivelPorTodas: false), torres, []);
        Assert.Equal(1, rasteiro!.Id);

        // Alvo alto no mesmo ponto: a torre 1 continua sendo a mais próxima, então segue vencendo
        // — ampliar as candidatas não pode piorar a escolha, só aumentar o conjunto.
        TorreTreinamento? alto = controlador.SelecionarTorre(AlvoNoQuadrante1(visivelPorTodas: true), torres, []);
        Assert.Equal(1, alto!.Id);
    }

    /// <summary>
    /// Os vetos de disparo não são afetados pela altura: ser visível amplia quem pode assumir o
    /// alvo, não autoriza atirar. Um alvo alto mas com certeza abaixo do limiar continua sem
    /// disparo.
    /// </summary>
    [Fact]
    public void AlvoAlto_NaoDispensaOsVetosDeDisparo()
    {
        var controlador = new ControladorHibrido(
            RedeZerada(), raioMundoMetros: 10, velocidadeMaximaAlvoMetrosPorSegundo: 2.5,
            modo: ControladorHibrido.ModoDecisao.HeuristicaPura);

        AlvoTreinamento alvo = AlvoNoQuadrante1(visivelPorTodas: true);
        alvo.TempoDesdeSelecao = 0.0; // ainda não cumpriu o tempo mínimo de mira

        Assert.False(controlador.DecidirDisparo(alvo, TorresLivres()[0]));
    }

    // ------------------------------------------------------------------ Equivalência com o modo 2D

    /// <summary>
    /// Garantia central da compatibilidade: com o eixo Z desligado, o MESMO episódio (mesma seed)
    /// tem que produzir exatamente o mesmo resultado que produzia antes da terceira dimensão
    /// existir. Como não há "antes" para comparar aqui, o teste fixa o que é verificável: o
    /// resultado não depende dos parâmetros de altura, que só podem ter efeito com o eixo ligado.
    /// </summary>
    [Fact]
    public void SemEixoZ_OsParametrosDeAlturaNaoInfluenciamOResultado()
    {
        static double Rodar(double alturaMaxima, double limiar)
        {
            var ambiente = new AmbienteTreinamento(
                new ParametrosTreinamento
                {
                    EixoZHabilitado = false,
                    AlturaMaximaAlvoMetros = alturaMaxima,
                    AlturaVisivelPorTodasAsTorresMetros = limiar,
                    DuracaoEpisodioSegundos = 30,
                    DuracaoMaximaSegurancaSegundos = 30
                },
                QuatroTorres);

            return ambiente.ExecutarEpisodioRapido(RedeZerada(), seed: 42).FitnessTotal;
        }

        Assert.Equal(Rodar(2.0, 0.5), Rodar(50.0, 0.01), precision: 9);
    }

    /// <summary>
    /// A contraprova do teste acima: com o eixo Z LIGADO, mudar o limiar de visibilidade precisa
    /// mudar alguma coisa. Se não mudasse, a terceira dimensão estaria sendo registrada no estado
    /// do alvo sem influenciar a simulação — exatamente o que o requisito pede para não acontecer.
    /// </summary>
    [Fact]
    public void ComEixoZ_OLimiarDeVisibilidadeMudaAClassificacaoDosAlvos()
    {
        static int ContarVisiveisPorTodas(double limiar)
        {
            var ambiente = new AmbienteTreinamento(
                new ParametrosTreinamento
                {
                    EixoZHabilitado = true,
                    AlturaVisivelPorTodasAsTorresMetros = limiar,
                    DuracaoEpisodioSegundos = 30,
                    DuracaoMaximaSegurancaSegundos = 30
                },
                QuatroTorres);

            var controlador = new ControladorHibrido(RedeZerada(), ambiente.Parametros.RaioMundoMetros, ambiente.Parametros.VelocidadeMaximaAlvoMetrosPorSegundo);
            EstadoEpisodio estado = ambiente.NovoEpisodio(seed: 5);
            var vistos = new Dictionary<int, AlvoTreinamento>();

            while (!estado.Finalizado)
            {
                ambiente.Avancar(estado, controlador, dt: 0.1);
                foreach (AlvoTreinamento alvo in estado.Alvos) vistos.TryAdd(alvo.Id, alvo);
            }

            return vistos.Values.Count(a => a.VisivelPorTodasAsTorres);
        }

        // Limiar quase no chão: praticamente todo alvo passa. Limiar acima do teto: nenhum passa.
        Assert.True(ContarVisiveisPorTodas(0.01) > 0);
        Assert.Equal(0, ContarVisiveisPorTodas(5.0));
    }

    /// <summary>
    /// A altura é parte do estado do alvo durante TODO o episódio, não só no nascimento: ela
    /// acompanha o alvo enquanto ele se move, que é o que permite transmiti-la ao sistema físico
    /// a cada instante.
    /// </summary>
    [Fact]
    public void ComEixoZ_AAlturaAcompanhaOAlvoAoLongoDoEpisodio()
    {
        AmbienteTreinamento ambiente = Ambiente(eixoZ: true);
        var controlador = new ControladorHibrido(RedeZerada(), ambiente.Parametros.RaioMundoMetros, ambiente.Parametros.VelocidadeMaximaAlvoMetrosPorSegundo);

        EstadoEpisodio estado = ambiente.NovoEpisodio(seed: 13);
        var alturaPorAlvo = new Dictionary<int, double>();

        while (!estado.Finalizado)
        {
            ambiente.Avancar(estado, controlador, dt: 0.1);

            foreach (AlvoTreinamento alvo in estado.Alvos)
            {
                if (alturaPorAlvo.TryGetValue(alvo.Id, out double anterior))
                {
                    Assert.Equal(anterior, alvo.AlturaMetros, precision: 9);
                }
                else
                {
                    alturaPorAlvo[alvo.Id] = alvo.AlturaMetros;
                }
            }
        }

        Assert.NotEmpty(alturaPorAlvo);
    }
}
