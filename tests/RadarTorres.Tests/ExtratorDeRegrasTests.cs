using RadarTorres.App.NeuralEvolution;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>Cobertura da análise de sensibilidade usada para aproximar as "regras" aprendidas pela rede (ver NeuralEvolution/ExtratorDeRegras.cs).</summary>
public sealed class ExtratorDeRegrasTests
{
    [Fact]
    public void Extrair_ComRedeZerada_NaoAcusaNenhumaEntradaRelevante()
    {
        // Pesos/bias zerados -> toda saída é tanh(0)=0 em qualquer ponto do espaço de entrada,
        // logo nenhuma perturbação muda a saída: nenhuma "regra" deveria ser relevante.
        double[] genomaZerado = new double[RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao)];
        var rede = new RedeNeural(ControladorHibrido.ArquiteturaPadrao, genomaZerado);

        RegrasExtraidas regras = ExtratorDeRegras.Extrair(rede, raioMundoMetros: 10, velocidadeMaximaAlvoMetrosPorSegundo: 2.5);

        Assert.NotEmpty(regras.Frases);
        Assert.Contains(regras.Frases, f => f.Contains("não tem efeito relevante") || f.Contains("comportamento aleatório"));
    }

    [Fact]
    public void Extrair_SempreDevolveAoMenosUmaFrase()
    {
        double[] genoma = new double[RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao)];
        for (int i = 0; i < genoma.Length; i++) genoma[i] = (i % 7 - 3) * 0.4;
        var rede = new RedeNeural(ControladorHibrido.ArquiteturaPadrao, genoma);

        RegrasExtraidas regras = ExtratorDeRegras.Extrair(rede, raioMundoMetros: 10, velocidadeMaximaAlvoMetrosPorSegundo: 2.5);

        Assert.NotEmpty(regras.Frases);
    }
}
