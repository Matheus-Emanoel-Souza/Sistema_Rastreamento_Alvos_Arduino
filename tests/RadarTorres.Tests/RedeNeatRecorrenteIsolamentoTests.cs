using RadarTorres.App.NeuralEvolution;
using SharpNeat.Neat.Genome;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Prova, com um genoma REAL (decodificado por um <see cref="TreinadorNeat"/> treinado por uma
/// única geração — população mínima, só para existir um genoma cíclico de verdade), que
/// <see cref="RedeNeatRecorrente"/> isola o estado POR CONTEXTO: duas torres diferentes não se
/// contaminam, e <see cref="RedeNeatRecorrente.ReiniciarTudo"/> zera as duas de uma vez.
/// </summary>
public sealed class RedeNeatRecorrenteIsolamentoTests
{
    private static (TreinadorNeat Treinador, NeatGenome<double> Genoma) TreinarUmaGeracaoV2Recorrente()
    {
        var ambiente = new AmbienteTreinamento(new ParametrosTreinamento { TamanhoPopulacao = 16, TetoComplexidade = 20 });
        var treinador = new TreinadorNeat(ambiente, tamanhoPopulacao: 16, quantidadeEspecies: 2, tetoComplexidade: 20, controladorV2: true, redeRecorrente: true);
        treinador.EvoluirGeracao([0]);
        return (treinador, treinador.EvoluirGeracao([1]).MelhorGenoma);
    }

    [Fact]
    public void Ativar_ComContextosDiferentes_NaoCompartilhaEstado()
    {
        (TreinadorNeat treinador, NeatGenome<double> genoma) = TreinarUmaGeracaoV2Recorrente();
        RedeNeatRecorrente rede = treinador.CriarRedeRecorrente(genoma);
        var entradas = new double[ControladorHibridoV2.QuantidadeEntradas];

        double[] contexto1PrimeiraVez = rede.Ativar(idContexto: 1, entradas);
        double[] contexto2PrimeiraVez = rede.Ativar(idContexto: 2, entradas);

        // Mesmo genoma, mesma entrada, PRIMEIRA ativação de cada contexto: se o estado fosse
        // compartilhado, a segunda chamada (contexto 2) já carregaria o efeito da primeira
        // (contexto 1) — aqui as duas devem ser idênticas, porque cada contexto decodifica a
        // PRÓPRIA caixa-preta do zero.
        Assert.Equal(contexto1PrimeiraVez, contexto2PrimeiraVez);
    }

    [Fact]
    public void ReiniciarTudo_DescartaOEstadoDeTodosOsContextos()
    {
        (TreinadorNeat treinador, NeatGenome<double> genoma) = TreinarUmaGeracaoV2Recorrente();
        RedeNeatRecorrente rede = treinador.CriarRedeRecorrente(genoma);
        var entradas = new double[ControladorHibridoV2.QuantidadeEntradas];

        double[] primeiraAtivacaoDoContexto = rede.Ativar(idContexto: 5, entradas);
        rede.Ativar(idContexto: 5, entradas); // muda o estado interno do contexto 5

        rede.ReiniciarTudo();
        double[] depoisDeReiniciarTudo = rede.Ativar(idContexto: 5, entradas);

        Assert.Equal(primeiraAtivacaoDoContexto, depoisDeReiniciarTudo);
    }
}
