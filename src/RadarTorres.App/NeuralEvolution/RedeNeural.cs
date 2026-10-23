using System;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// Rede neural feedforward simples (fully-connected, ativação tanh em todas as camadas),
/// implementada do zero — sem dependência de biblioteca de ML. Os pesos e bias não são
/// aprendidos por backpropagation: vêm de fora, prontos, como um genoma plano de
/// <see cref="double"/> (ver <see cref="AlgoritmoGenetico"/>) — é essa combinação que
/// caracteriza a neuroevolução deste controlador.
/// </summary>
public sealed class RedeNeural
{
    private readonly int[] _camadas;
    private readonly double[][,] _pesos;
    private readonly double[][] _bias;

    /// <summary>Quantidade total de genes (pesos + bias) necessária para uma arquitetura com estas camadas.</summary>
    public static int TamanhoGenoma(int[] camadas)
    {
        int total = 0;
        for (int i = 1; i < camadas.Length; i++)
        {
            total += camadas[i - 1] * camadas[i]; // pesos da camada i
            total += camadas[i];                  // bias da camada i
        }
        return total;
    }

    /// <summary>
    /// Constrói a rede a partir de uma arquitetura (ex.: [6, 8, 1] = 6 entradas, 1 camada
    /// oculta de 8 neurônios, 1 saída) e um genoma já com o tamanho exato de <see cref="TamanhoGenoma"/>.
    /// </summary>
    public RedeNeural(int[] camadas, double[] genoma)
    {
        if (camadas.Length < 2)
            throw new ArgumentException("A rede precisa de pelo menos uma camada de entrada e uma de saída.", nameof(camadas));

        int esperado = TamanhoGenoma(camadas);
        if (genoma.Length != esperado)
            throw new ArgumentException($"Genoma com {genoma.Length} genes, esperado {esperado} para esta arquitetura.", nameof(genoma));

        _camadas = camadas;
        _pesos = new double[camadas.Length - 1][,];
        _bias = new double[camadas.Length - 1][];

        int cursor = 0;
        for (int i = 1; i < camadas.Length; i++)
        {
            int entradas = camadas[i - 1];
            int saidas = camadas[i];

            var pesosCamada = new double[entradas, saidas];
            for (int e = 0; e < entradas; e++)
            {
                for (int s = 0; s < saidas; s++)
                {
                    pesosCamada[e, s] = genoma[cursor++];
                }
            }

            var biasCamada = new double[saidas];
            for (int s = 0; s < saidas; s++)
            {
                biasCamada[s] = genoma[cursor++];
            }

            _pesos[i - 1] = pesosCamada;
            _bias[i - 1] = biasCamada;
        }
    }

    /// <summary>Propaga as entradas pela rede e retorna as saídas (cada uma no intervalo -1..1, por causa do tanh).</summary>
    public double[] Ativar(double[] entradas)
    {
        if (entradas.Length != _camadas[0])
            throw new ArgumentException($"Esperadas {_camadas[0]} entradas, recebidas {entradas.Length}.", nameof(entradas));

        double[] valores = entradas;
        for (int i = 0; i < _pesos.Length; i++)
        {
            double[,] pesosCamada = _pesos[i];
            double[] biasCamada = _bias[i];
            int entradasCamada = pesosCamada.GetLength(0);
            int saidasCamada = pesosCamada.GetLength(1);

            var proximos = new double[saidasCamada];
            for (int s = 0; s < saidasCamada; s++)
            {
                double soma = biasCamada[s];
                for (int e = 0; e < entradasCamada; e++)
                {
                    soma += valores[e] * pesosCamada[e, s];
                }
                proximos[s] = Math.Tanh(soma);
            }
            valores = proximos;
        }

        return valores;
    }

    /// <summary>Tamanho de cada camada (entrada, ocultas, saída), na ordem passada ao construtor.</summary>
    public int[] TamanhosDasCamadas => _camadas;

    /// <summary>Quantidade de camadas de pesos (= <see cref="TamanhosDasCamadas"/>.Length - 1).</summary>
    public int QuantidadeDeCamadasDePesos => _pesos.Length;

    /// <summary>Peso da conexão entre o neurônio <paramref name="indiceEntrada"/> da camada <paramref name="indiceCamada"/> e o neurônio <paramref name="indiceSaida"/> da camada seguinte.</summary>
    public double Peso(int indiceCamada, int indiceEntrada, int indiceSaida) => _pesos[indiceCamada][indiceEntrada, indiceSaida];

    /// <summary>
    /// Mesma propagação de <see cref="Ativar"/>, mas devolve a ativação de TODAS as camadas
    /// (índice 0 = entradas cruas, até o índice <see cref="TamanhosDasCamadas"/>.Length-1 =
    /// saída) — usado só para visualização (ver <c>DiagramaRedeNeuralViewModel</c>), nunca no
    /// laço de treinamento/avaliação (custo extra de alocar e devolver cada camada
    /// intermediária, desnecessário fora da tela).
    /// </summary>
    public double[][] AtivarComCamadas(double[] entradas)
    {
        if (entradas.Length != _camadas[0])
            throw new ArgumentException($"Esperadas {_camadas[0]} entradas, recebidas {entradas.Length}.", nameof(entradas));

        var todasAsCamadas = new double[_camadas.Length][];
        todasAsCamadas[0] = entradas;

        double[] valores = entradas;
        for (int i = 0; i < _pesos.Length; i++)
        {
            double[,] pesosCamada = _pesos[i];
            double[] biasCamada = _bias[i];
            int entradasCamada = pesosCamada.GetLength(0);
            int saidasCamada = pesosCamada.GetLength(1);

            var proximos = new double[saidasCamada];
            for (int s = 0; s < saidasCamada; s++)
            {
                double soma = biasCamada[s];
                for (int e = 0; e < entradasCamada; e++)
                {
                    soma += valores[e] * pesosCamada[e, s];
                }
                proximos[s] = Math.Tanh(soma);
            }
            valores = proximos;
            todasAsCamadas[i + 1] = proximos;
        }

        return todasAsCamadas;
    }
}
