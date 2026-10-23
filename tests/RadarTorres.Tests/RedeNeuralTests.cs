using System;
using System.Linq;
using RadarTorres.App.NeuralEvolution;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>Cobertura da rede neural feedforward do controlador híbrido (ver NeuralEvolution/RedeNeural.cs).</summary>
public sealed class RedeNeuralTests
{
    [Fact]
    public void TamanhoGenoma_ContaPesosEBiasDeTodasAsCamadas()
    {
        // 2 entradas -> 3 ocultos -> 1 saída: (2*3 + 3) + (3*1 + 1) = 9 + 4 = 13
        int tamanho = RedeNeural.TamanhoGenoma([2, 3, 1]);

        Assert.Equal(13, tamanho);
    }

    [Fact]
    public void Ativar_ComGenomaConstruido_ProduzSaidaDeterministica()
    {
        // Pesos e bias zerados -> tanh(0) = 0 em toda a rede, independente da entrada.
        double[] genomaZerado = new double[RedeNeural.TamanhoGenoma([2, 2, 1])];
        var rede = new RedeNeural([2, 2, 1], genomaZerado);

        double[] saida = rede.Ativar([0.5, -0.3]);

        double unicaSaida = Assert.Single(saida);
        Assert.Equal(0.0, unicaSaida, precision: 10);
    }

    [Fact]
    public void Ativar_MesmoGenomaEMesmaEntrada_ProduzSempreAMesmaSaida()
    {
        double[] genoma = Enumerable.Range(0, RedeNeural.TamanhoGenoma([3, 4, 2]))
            .Select(i => (i % 7 - 3) * 0.1)
            .ToArray();
        var rede = new RedeNeural([3, 4, 2], genoma);

        double[] primeira = rede.Ativar([0.1, 0.2, 0.3]);
        double[] segunda = rede.Ativar([0.1, 0.2, 0.3]);

        Assert.Equal(primeira, segunda);
    }

    [Fact]
    public void Ativar_SaidasSempreDentroDoIntervaloDoTanh()
    {
        double[] genoma = Enumerable.Range(0, RedeNeural.TamanhoGenoma([6, 8, 2]))
            .Select(i => (i % 5 - 2) * 2.0) // valores grandes o suficiente para saturar o tanh
            .ToArray();
        var rede = new RedeNeural([6, 8, 2], genoma);

        double[] saida = rede.Ativar([1, 1, 1, 1, 1, 1]);

        Assert.All(saida, v => Assert.InRange(v, -1.0, 1.0));
    }

    [Fact]
    public void Construtor_ComGenomaDeTamanhoErrado_Lanca()
    {
        double[] genomaCurto = new double[3];

        Assert.Throws<ArgumentException>(() => new RedeNeural([2, 2, 1], genomaCurto));
    }
}
