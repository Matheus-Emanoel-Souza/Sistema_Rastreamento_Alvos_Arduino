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

    [Fact]
    public void AtivarComCamadas_DevolveUmaEntradaPorCamadaIncluindoAsEntradasCrus()
    {
        double[] genoma = new double[RedeNeural.TamanhoGenoma([3, 4, 2])];
        var rede = new RedeNeural([3, 4, 2], genoma);

        double[][] camadas = rede.AtivarComCamadas([0.1, 0.2, 0.3]);

        Assert.Equal(3, camadas.Length); // entrada + oculta + saída
        Assert.Equal([0.1, 0.2, 0.3], camadas[0]);
        Assert.Equal(4, camadas[1].Length);
        Assert.Equal(2, camadas[2].Length);
    }

    [Fact]
    public void AtivarComCamadas_UltimaCamadaIgualAoAtivar()
    {
        double[] genoma = Enumerable.Range(0, RedeNeural.TamanhoGenoma([6, 8, 2]))
            .Select(i => (i % 5 - 2) * 0.3)
            .ToArray();
        var rede = new RedeNeural([6, 8, 2], genoma);
        double[] entradas = [0.5, 1, 0, 0.5, 0.5, 1];

        double[] saidaDireta = rede.Ativar(entradas);
        double[][] camadas = rede.AtivarComCamadas(entradas);

        Assert.Equal(saidaDireta, camadas[^1]);
    }

    [Fact]
    public void Peso_DevolveOGeneCorrespondenteNoGenoma()
    {
        // Arquitetura [2,2]: genoma = [p00,p01,p10,p11, b0,b1] — Peso(0,1,0) é o 3º gene (índice 2).
        double[] genoma = [10, 20, 30, 40, 0, 0];
        var rede = new RedeNeural([2, 2], genoma);

        Assert.Equal(30, rede.Peso(0, 1, 0));
        Assert.Equal(20, rede.Peso(0, 0, 1));
    }

    [Fact]
    public void TamanhosDasCamadas_EQuantidadeDeCamadasDePesos_RefletemAArquitetura()
    {
        var rede = new RedeNeural([6, 8, 2], new double[RedeNeural.TamanhoGenoma([6, 8, 2])]);

        Assert.Equal(new[] { 6, 8, 2 }, rede.TamanhosDasCamadas);
        Assert.Equal(2, rede.QuantidadeDeCamadasDePesos);
    }
}
