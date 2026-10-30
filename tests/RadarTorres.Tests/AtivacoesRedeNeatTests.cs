using System;
using System.Collections.Generic;
using RadarTorres.App.NeuralEvolution;
using SharpNeat;
using SharpNeat.IO.Models;
using SharpNeat.NeuralNets;
using SharpNeat.NeuralNets.IO;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobertura da propagação própria que devolve a ativação de CADA nó, inclusive os ocultos (ver
/// NeuralEvolution/AtivacoesRedeNeat.cs). É o que permitiu o diagrama da tela de treinamento
/// voltar a colorir os neurônios ocultos pela ativação real, como a rede própria anterior fazia —
/// a caixa-preta do SharpNEAT expõe só entradas e saídas.
/// </summary>
/// <remarks>
/// O teste que importa é o de equivalência: se as saídas calculadas aqui batem com as de
/// <see cref="RedeNeat.Ativar"/> para a mesma entrada, então a propagação reproduz fielmente o que
/// o framework faz — e os valores intermediários que ela expõe são os de verdade, não uma
/// aproximação.
/// </remarks>
public sealed class AtivacoesRedeNeatTests
{
    private const int PrimeiroIdSaida = 8; // 7 entradas reais + bias
    private const int PrimeiroIdOculto = 10; // + 2 saídas

    private static readonly List<ActivationFnLine> AtivacaoTanH =
    [
        new ActivationFnLine(0, ActivationFunctionId.TanH.ToString())
    ];

    /// <summary>Entrada de teste na convenção do SharpNEAT: posição 0 é o bias (1,0).</summary>
    private static double[] EntradasComBias() => [1.0, 0.5, 1.0, 0.0, 0.5, 0.5, 0.0, 1.0];

    private static NetFileModel Modelo(List<ConnectionLine> conexoes)
    {
        conexoes.Sort((a, b) => a.SourceId != b.SourceId
            ? a.SourceId.CompareTo(b.SourceId)
            : a.TargetId.CompareTo(b.TargetId));

        return NetFileModel.CreateAcyclic(
            RedeNeat.QuantidadeEntradasComBias,
            ControladorHibrido.QuantidadeSaidas,
            conexoes,
            AtivacaoTanH);
    }

    private static RedeNeat Rede(NetFileModel modelo) => new(NeuralNetConverter.ToAcyclicNeuralNet(
        modelo,
        enableHardwareAcceleratedNeuralNets: false,
        enableHardwareAcceleratedActivationFunctions: false));

    /// <summary>Rede com um oculto no caminho: entrada 1 → oculto → saída 0, e bias → saída 1.</summary>
    private static NetFileModel ModeloComOculto(double pesoEntradaOculto, double pesoOcultoSaida, double pesoBiasSaida) =>
        Modelo(
        [
            new ConnectionLine(1, PrimeiroIdOculto, pesoEntradaOculto),
            new ConnectionLine(PrimeiroIdOculto, PrimeiroIdSaida, pesoOcultoSaida),
            new ConnectionLine(0, PrimeiroIdSaida + 1, pesoBiasSaida),
        ]);

    [Fact]
    public void Calcular_AsSaidas_BatemComAsDaCaixaPretaDoFramework()
    {
        NetFileModel modelo = ModeloComOculto(pesoEntradaOculto: 1.3, pesoOcultoSaida: -0.7, pesoBiasSaida: 0.4);
        double[] entradas = EntradasComBias();

        IReadOnlyDictionary<int, double>? ativacoes = AtivacoesRedeNeat.Calcular(modelo, entradas);
        Assert.NotNull(ativacoes);

        // RedeNeat.Ativar recebe só as entradas reais; o bias é escrito por ela.
        double[] saidasDoFramework = Rede(modelo).Ativar(entradas[1..]);

        for (int i = 0; i < saidasDoFramework.Length; i++)
        {
            Assert.Equal(saidasDoFramework[i], ativacoes[PrimeiroIdSaida + i], precision: 9);
        }
    }

    [Fact]
    public void Calcular_DevolveAAtivacaoDoNeuronioOculto()
    {
        const double pesoEntradaOculto = 1.3;
        NetFileModel modelo = ModeloComOculto(pesoEntradaOculto, pesoOcultoSaida: -0.7, pesoBiasSaida: 0.4);
        double[] entradas = EntradasComBias();

        IReadOnlyDictionary<int, double>? ativacoes = AtivacoesRedeNeat.Calcular(modelo, entradas);

        Assert.NotNull(ativacoes);
        Assert.True(ativacoes.ContainsKey(PrimeiroIdOculto));
        // O oculto recebe só a entrada 1 (posição 1 do vetor com bias), então seu valor é
        // determinado: tanh(peso * entrada).
        Assert.Equal(Math.Tanh(pesoEntradaOculto * entradas[1]), ativacoes[PrimeiroIdOculto], precision: 9);
    }

    [Fact]
    public void Calcular_RepassaAsEntradasSemAplicarAtivacao()
    {
        NetFileModel modelo = ModeloComOculto(1.0, 1.0, 0.0);
        double[] entradas = EntradasComBias();

        IReadOnlyDictionary<int, double>? ativacoes = AtivacoesRedeNeat.Calcular(modelo, entradas);

        Assert.NotNull(ativacoes);
        for (int i = 0; i < entradas.Length; i++)
        {
            Assert.Equal(entradas[i], ativacoes[i], precision: 9);
        }
    }

    /// <summary>
    /// Com encadeamento de ocultos (um alimentando o outro) a ordem de propagação deixa de ser
    /// trivial — é o caso em que uma ordenação topológica errada produziria números plausíveis
    /// mas falsos, sem quebrar nada.
    /// </summary>
    [Fact]
    public void Calcular_ComOcultosEncadeados_RespeitaAOrdemDePropagacao()
    {
        const int segundoOculto = PrimeiroIdOculto + 1;
        NetFileModel modelo = Modelo(
        [
            new ConnectionLine(1, PrimeiroIdOculto, 0.9),
            new ConnectionLine(PrimeiroIdOculto, segundoOculto, 1.4),
            new ConnectionLine(segundoOculto, PrimeiroIdSaida, -1.1),
            new ConnectionLine(0, PrimeiroIdSaida + 1, 0.2),
        ]);

        double[] entradas = EntradasComBias();
        IReadOnlyDictionary<int, double>? ativacoes = AtivacoesRedeNeat.Calcular(modelo, entradas);
        Assert.NotNull(ativacoes);

        double esperadoPrimeiro = Math.Tanh(0.9 * entradas[1]);
        double esperadoSegundo = Math.Tanh(1.4 * esperadoPrimeiro);

        Assert.Equal(esperadoPrimeiro, ativacoes[PrimeiroIdOculto], precision: 9);
        Assert.Equal(esperadoSegundo, ativacoes[segundoOculto], precision: 9);
        Assert.Equal(Math.Tanh(-1.1 * esperadoSegundo), ativacoes[PrimeiroIdSaida], precision: 9);

        // E continua equivalente ao framework, que é a garantia de que a cadeia inteira está certa.
        double[] saidas = Rede(modelo).Ativar(entradas[1..]);
        Assert.Equal(saidas[0], ativacoes[PrimeiroIdSaida], precision: 9);
    }

    /// <summary>
    /// Saída que a topologia deixou sem nenhuma conexão de chegada: o framework devolve tanh(0),
    /// e a propagação precisa concordar — era o caso que mais facilmente ficaria de fora do laço
    /// de ordenação topológica.
    /// </summary>
    [Fact]
    public void Calcular_SaidaSemConexaoDeChegada_ValeTanhDeZero()
    {
        NetFileModel modelo = Modelo(
        [
            new ConnectionLine(1, PrimeiroIdSaida, 0.8),
            // A saída 1 existe no modelo (OutputCount = 2) mas não recebe conexão alguma.
            new ConnectionLine(0, PrimeiroIdSaida, 0.1),
        ]);

        IReadOnlyDictionary<int, double>? ativacoes = AtivacoesRedeNeat.Calcular(modelo, EntradasComBias());

        Assert.NotNull(ativacoes);
        Assert.Equal(0.0, ativacoes[PrimeiroIdSaida + 1], precision: 9);
    }

    [Fact]
    public void Calcular_ComQuantidadeDeEntradasErrada_DevolveNuloEmVezDeLancar()
    {
        NetFileModel modelo = ModeloComOculto(1.0, 1.0, 0.0);

        Assert.Null(AtivacoesRedeNeat.Calcular(modelo, [1.0, 0.5]));
    }

    /// <summary>
    /// Fora da configuração que a propagação sabe reproduzir (TanH + acíclica), o resultado tem
    /// que ser <c>null</c> — a tela usa isso para pintar os ocultos em cor neutra e avisar o
    /// motivo, em vez de exibir um valor que a rede não produziria.
    /// </summary>
    [Fact]
    public void Calcular_ComOutraFuncaoDeAtivacao_DevolveNulo()
    {
        var conexoes = new List<ConnectionLine>
        {
            new(0, PrimeiroIdSaida, 0.5),
            new(0, PrimeiroIdSaida + 1, 0.5),
        };

        NetFileModel modelo = NetFileModel.CreateAcyclic(
            RedeNeat.QuantidadeEntradasComBias,
            ControladorHibrido.QuantidadeSaidas,
            conexoes,
            [new ActivationFnLine(0, ActivationFunctionId.ReLU.ToString())]);

        Assert.Null(AtivacoesRedeNeat.Calcular(modelo, EntradasComBias()));
    }
}
