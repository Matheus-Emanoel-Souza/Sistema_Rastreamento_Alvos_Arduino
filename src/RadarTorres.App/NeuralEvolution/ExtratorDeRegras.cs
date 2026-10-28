using System;
using System.Collections.Generic;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>Frases em português, geradas por análise de sensibilidade, aproximando as "regras" que a rede neural aprendeu.</summary>
public sealed record RegrasExtraidas(IReadOnlyList<string> Frases);

/// <summary>
/// A rede neural evoluída não tem regras explícitas (é uma matriz de pesos) — esta classe
/// aproxima, por análise de sensibilidade (variação de cada entrada em torno de um ponto de
/// referência típico), quais tendências ela acabou aprendendo. Não é uma extração exata (uma
/// rede não-linear pode se comportar diferente em outras regiões do espaço de entrada), mas dá
/// ao usuário uma leitura qualitativa de para onde a evolução está empurrando o controlador —
/// ideal para acompanhar a tela de treinamento geração a geração.
/// </summary>
public static class ExtratorDeRegras
{
    private const double PassoSensibilidade = 0.1;
    private const double LimiarRelevancia = 0.02;

    private static readonly string[] NomesEntradas =
    [
        "a distância até o alvo",
        "estar no quadrante do alvo",
        "estar em quadrante adjacente ao do alvo",
        "o ângulo de mira",
        "a velocidade do alvo",
        "o tempo que o alvo já está sendo detectado sem ser abatido",
        "a camada heurística (torre do próprio quadrante vs. sobressalente)"
    ];

    public static RegrasExtraidas Extrair(RedeNeural rede, double raioMundoMetros, double velocidadeMaximaAlvoMetrosPorSegundo)
    {
        // Ponto de referência: torre do próprio quadrante (fatorCamada=1), alvo a meia distância,
        // de frente, com velocidade média, recém-detectado (tempo de detecção=0) — a situação
        // "típica" de engajamento.
        double[] referencia = [0.5, 1.0, 0.0, 0.5, 0.5, 0.0, 1.0];
        double[] saidaReferencia = rede.Ativar(referencia);

        var frases = new List<string>();

        double? distanciaDeDisparo = EncontrarDistanciaDeDisparo(rede, referencia);
        if (distanciaDeDisparo is { } d)
        {
            frases.Add($"Em condições típicas (torre do próprio quadrante, alvo em velocidade média), a rede tende a disparar quando a distância cai abaixo de ~{d * raioMundoMetros:0.0} m.");
        }
        else
        {
            frases.Add("Em condições típicas, a rede não encontra um limiar claro de disparo por distância — ou dispara sempre, ou nunca, dentro do alcance avaliado.");
        }

        for (int i = 0; i < referencia.Length; i++)
        {
            double[] perturbada = (double[])referencia.Clone();
            perturbada[i] = Math.Clamp(referencia[i] + PassoSensibilidade, -1.0, 1.0);
            double[] saidaPerturbada = rede.Ativar(perturbada);

            double deltaPreferenciaTorre = saidaPerturbada[0] - saidaReferencia[0];
            double deltaConfiancaDisparo = saidaPerturbada[1] - saidaReferencia[1];

            if (Math.Abs(deltaPreferenciaTorre) >= LimiarRelevancia)
            {
                frases.Add(FormatarFrase("a preferência por essa torre", NomesEntradas[i], deltaPreferenciaTorre));
            }

            if (Math.Abs(deltaConfiancaDisparo) >= LimiarRelevancia)
            {
                frases.Add(FormatarFrase("a confiança de disparo", NomesEntradas[i], deltaConfiancaDisparo));
            }
        }

        if (frases.Count == 1)
        {
            frases.Add("Nenhuma entrada tem efeito relevante perto do ponto de referência — a rede ainda está próxima do comportamento aleatório inicial.");
        }

        return new RegrasExtraidas(frases);
    }

    private static string FormatarFrase(string saida, string entrada, double delta)
    {
        string direcao = delta > 0 ? "aumenta" : "diminui";
        return $"Quando {entrada} aumenta, {saida} {direcao} (Δ={delta:+0.00;-0.00}).";
    }

    /// <summary>
    /// Varre a distância normalizada de 0 a 1 (demais entradas fixas na referência) e devolve o
    /// ponto em que a confiança de disparo cruza zero — aproximação do "alcance de disparo"
    /// aprendido pela rede nesta configuração.
    /// </summary>
    private static double? EncontrarDistanciaDeDisparo(RedeNeural rede, double[] referencia)
    {
        double confiancaAnterior = double.NaN;

        for (double distancia = 0.0; distancia <= 1.0; distancia += 0.02)
        {
            double[] entrada = (double[])referencia.Clone();
            entrada[0] = distancia;
            double confianca = rede.Ativar(entrada)[1];

            if (!double.IsNaN(confiancaAnterior) && Math.Sign(confiancaAnterior) != Math.Sign(confianca))
            {
                return distancia;
            }
            confiancaAnterior = confianca;
        }

        return null;
    }
}
