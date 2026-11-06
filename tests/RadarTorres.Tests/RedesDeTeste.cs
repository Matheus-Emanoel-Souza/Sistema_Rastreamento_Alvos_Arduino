using System.Collections.Generic;
using RadarTorres.App.NeuralEvolution;
using SharpNeat;
using SharpNeat.IO.Models;
using SharpNeat.NeuralNets;
using SharpNeat.NeuralNets.IO;

namespace RadarTorres.Tests;

/// <summary>
/// Constrói redes <see cref="RedeNeat"/> determinísticas para os testes, montando o
/// <see cref="NetFileModel"/> à mão em vez de evoluir uma população.
/// </summary>
/// <remarks>
/// Antes bastava passar um <c>double[]</c> de pesos para a rede de topologia fixa 7-8-2. Com
/// NEAT não existe "genoma solto": o genoma é um grafo com ids de inovação que só fazem sentido
/// dentro de uma população. O caminho honesto para um teste é descrever a topologia desejada
/// explicitamente (quais conexões, com que pesos) e deixar o próprio SharpNEAT construir a rede
/// a partir dela — é exatamente o que <see cref="App.Services.ControladorTreinadoProvider"/> faz
/// em produção ao ler o arquivo embutido.
/// </remarks>
internal static class RedesDeTeste
{
    /// <summary>Id do primeiro nó de saída — depois do bias e das entradas reais (convenção do SharpNEAT, ver <see cref="RedeNeat"/>).</summary>
    private static int PrimeiroIdSaida => RedeNeat.QuantidadeEntradasComBias;

    private static readonly List<ActivationFnLine> AtivacaoTanH =
    [
        new ActivationFnLine(0, ActivationFunctionId.TanH.ToString())
    ];

    /// <summary>
    /// Rede que devolve 0,0 em todas as saídas — o equivalente ao antigo "genoma zerado", que
    /// fazia o controlador híbrido se comportar exatamente como a heurística pura na seleção de
    /// torre. Implementada como uma conexão de peso zero do bias para cada saída: uma rede sem
    /// nenhuma conexão não é um grafo válido para o framework decodificar.
    /// </summary>
    public static RedeNeat Zerada() => ComPesosParaSaidas(0.0, 0.0);

    /// <summary>
    /// Rede mínima em que o bias alimenta cada saída com um peso fixo — as saídas ficam
    /// constantes em <c>tanh(peso)</c>, independentemente das entradas. Útil para fixar o
    /// comportamento do controlador (ex.: "sempre autoriza disparo") sem depender de evolução.
    /// </summary>
    public static RedeNeat ComPesosParaSaidas(double pesoAjuste, double pesoConfianca)
    {
        var conexoes = new List<ConnectionLine>
        {
            new(0, PrimeiroIdSaida, pesoAjuste),
            new(0, PrimeiroIdSaida + 1, pesoConfianca),
        };

        return Construir(conexoes);
    }

    /// <summary>
    /// Rede em que UMA entrada real (índice 0..<see cref="ControladorHibrido.QuantidadeEntradas"/>-1,
    /// na ordem de <see cref="ControladorHibrido.RotulosEntradas"/>) alimenta uma das saídas.
    /// Deixa o teste isolar a influência de uma única entrada, que é o que a antiga montagem
    /// "mexe num peso específico do vetor" fazia.
    /// </summary>
    public static RedeNeat DeUmaEntradaParaUmaSaida(int indiceEntrada, int indiceSaida, double peso)
    {
        var conexoes = new List<ConnectionLine>
        {
            // +1 porque o nó 0 é o bias e as entradas reais começam no índice 1.
            new(indiceEntrada + 1, PrimeiroIdSaida + indiceSaida, peso),

            // A outra saída precisa existir no grafo, senão o modelo fica com menos saídas do
            // que RedeNeat espera; peso zero deixa-a neutra.
            new(0, PrimeiroIdSaida + (1 - indiceSaida), 0.0),
        };

        return Construir(conexoes);
    }

    private static RedeNeat Construir(List<ConnectionLine> conexoes)
    {
        // O construtor de grafo do SharpNEAT assume a lista ordenada por (origem, destino) —
        // é a ordem em que o próprio NeatGenomeSaver grava.
        conexoes.Sort((a, b) => a.SourceId != b.SourceId
            ? a.SourceId.CompareTo(b.SourceId)
            : a.TargetId.CompareTo(b.TargetId));

        NetFileModel modelo = NetFileModel.CreateAcyclic(
            RedeNeat.QuantidadeEntradasComBias,
            ControladorHibrido.QuantidadeSaidas,
            conexoes,
            AtivacaoTanH);

        IBlackBox<double> caixaPreta = NeuralNetConverter.ToAcyclicNeuralNet(
            modelo,
            enableHardwareAcceleratedNeuralNets: false,
            enableHardwareAcceleratedActivationFunctions: false);

        return new RedeNeat(caixaPreta);
    }
}
