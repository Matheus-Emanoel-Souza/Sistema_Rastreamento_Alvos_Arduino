using System;
using RadarTorres.App.NeuralEvolution;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobertura do adaptador sobre a rede do SharpNEAT (ver NeuralEvolution/RedeNeat.cs).
/// Substitui <c>RedeNeuralTests</c>: não faz mais sentido testar propagação camada a camada —
/// quem calcula é o framework. O que precisa de teste aqui é o CONTRATO do adaptador, que é
/// onde um erro nosso passaria despercebido: a convenção do nó de bias, a validação de
/// tamanhos e o fato de a ativação não guardar estado entre chamadas.
/// </summary>
public sealed class RedeNeatTests
{
    [Fact]
    public void QuantidadeEntradasComBias_EUmaAMaisQueAsEntradasDoControlador()
    {
        Assert.Equal(ControladorHibrido.QuantidadeEntradas + 1, RedeNeat.QuantidadeEntradasComBias);
    }

    [Fact]
    public void Ativar_ComRedeDePesosZero_DevolveZeroEmTodasAsSaidas()
    {
        RedeNeat rede = RedesDeTeste.Zerada();

        double[] saidas = rede.Ativar(EntradaTipica());

        Assert.Equal(ControladorHibrido.QuantidadeSaidas, saidas.Length);
        Assert.All(saidas, s => Assert.Equal(0.0, s, precision: 10));
    }

    /// <summary>
    /// O bias precisa chegar à rede como 1,0 — é o que torna uma conexão bias→saída capaz de
    /// produzir saída constante. Se <see cref="RedeNeat.Ativar"/> esquecesse de escrevê-lo, a
    /// saída aqui seria 0 em vez de tanh(peso).
    /// </summary>
    [Fact]
    public void Ativar_EscreveONoDeBiasComoUm()
    {
        const double peso = 0.8;
        RedeNeat rede = RedesDeTeste.ComPesosParaSaidas(pesoAjuste: peso, pesoConfianca: 0.0);

        double[] saidas = rede.Ativar(EntradaTipica());

        Assert.Equal(Math.Tanh(peso), saidas[0], precision: 10);
    }

    /// <summary>
    /// Com a rede ligada só ao bias, a saída não pode depender das entradas — garante que o
    /// adaptador não está embaralhando os índices (escrevendo a entrada real no nó do bias, por
    /// exemplo, o erro mais fácil de cometer nessa convenção).
    /// </summary>
    [Fact]
    public void Ativar_ComRedeLigadaSoAoBias_IgnoraAsEntradas()
    {
        RedeNeat rede = RedesDeTeste.ComPesosParaSaidas(pesoAjuste: 0.5, pesoConfianca: -0.5);

        double[] comZeros = rede.Ativar(new double[ControladorHibrido.QuantidadeEntradas]);
        double[] comValores = rede.Ativar(EntradaTipica());

        Assert.Equal(comZeros[0], comValores[0], precision: 10);
        Assert.Equal(comZeros[1], comValores[1], precision: 10);
    }

    [Fact]
    public void Ativar_ComUmaEntradaLigada_RespondeAEssaEntrada()
    {
        // Entrada 0 = distância normalizada; peso positivo para a saída 0 (ajuste de pontuação).
        RedeNeat rede = RedesDeTeste.DeUmaEntradaParaUmaSaida(indiceEntrada: 0, indiceSaida: 0, peso: 2.0);

        double[] perto = new double[ControladorHibrido.QuantidadeEntradas];
        double[] longe = new double[ControladorHibrido.QuantidadeEntradas];
        longe[0] = 1.0;

        Assert.True(rede.Ativar(longe)[0] > rede.Ativar(perto)[0]);
    }

    /// <summary>
    /// Mesmo vetor de entrada tem que dar sempre a mesma saída — é a premissa de
    /// <see cref="ExtratorDeRegras"/> (análise de sensibilidade) e da comparação app↔Arduino.
    /// Garante que o <c>Reset</c> ao final de cada ativação está sendo feito.
    /// </summary>
    [Fact]
    public void Ativar_RepetidamenteComAMesmaEntrada_EDeterministico()
    {
        RedeNeat rede = RedesDeTeste.DeUmaEntradaParaUmaSaida(indiceEntrada: 3, indiceSaida: 1, peso: 1.2);
        double[] entrada = EntradaTipica();

        double[] primeira = rede.Ativar(entrada);
        double[] segunda = rede.Ativar(entrada);
        double[] terceira = rede.Ativar(entrada);

        Assert.Equal(primeira[0], segunda[0], precision: 12);
        Assert.Equal(primeira[1], segunda[1], precision: 12);
        Assert.Equal(primeira[1], terceira[1], precision: 12);
    }

    [Fact]
    public void Ativar_ComQuantidadeDeEntradasErrada_Lanca()
    {
        RedeNeat rede = RedesDeTeste.Zerada();

        Assert.Throws<ArgumentException>(() => rede.Ativar(new double[ControladorHibrido.QuantidadeEntradas - 1]));
        Assert.Throws<ArgumentException>(() => rede.Ativar(new double[ControladorHibrido.QuantidadeEntradas + 1]));
    }

    /// <summary>Cenário de referência (mesmo ponto típico usado pelo extrator de regras e pelo diagrama).</summary>
    private static double[] EntradaTipica() => [0.5, 1.0, 0.0, 0.5, 0.5, 0.0, 1.0];
}
