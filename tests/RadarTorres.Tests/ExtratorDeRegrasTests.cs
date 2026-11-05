using RadarTorres.App.NeuralEvolution;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>Cobertura da análise de sensibilidade usada para aproximar as "regras" aprendidas pela rede (ver NeuralEvolution/ExtratorDeRegras.cs).</summary>
public sealed class ExtratorDeRegrasTests
{
    [Fact]
    public void Extrair_ComRedeZerada_NaoAcusaNenhumaEntradaRelevante()
    {
        // Rede de pesos zero -> toda saída é tanh(0)=0 em qualquer ponto do espaço de entrada,
        // logo nenhuma perturbação muda a saída: nenhuma "regra" deveria ser relevante.
        RedeNeat rede = RedesDeTeste.Zerada();

        RegrasExtraidas regras = ExtratorDeRegras.Extrair(rede, raioMundoMetros: 10, velocidadeMaximaAlvoMetrosPorSegundo: 2.5);

        Assert.NotEmpty(regras.Frases);
        Assert.Contains(regras.Frases, f => f.Contains("não tem efeito relevante") || f.Contains("comportamento aleatório"));
    }

    [Fact]
    public void Extrair_SempreDevolveAoMenosUmaFrase()
    {
        // Rede com influência real de uma entrada (distância) sobre o ajuste de pontuação.
        RedeNeat rede = RedesDeTeste.DeUmaEntradaParaUmaSaida(indiceEntrada: 0, indiceSaida: 0, peso: 1.5);

        RegrasExtraidas regras = ExtratorDeRegras.Extrair(rede, raioMundoMetros: 10, velocidadeMaximaAlvoMetrosPorSegundo: 2.5);

        Assert.NotEmpty(regras.Frases);
    }
}
