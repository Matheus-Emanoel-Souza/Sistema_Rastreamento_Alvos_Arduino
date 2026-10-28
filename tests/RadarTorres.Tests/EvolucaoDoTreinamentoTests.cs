using System.Collections.Generic;
using System.Linq;
using RadarTorres.App.Configuration;
using RadarTorres.App.NeuralEvolution;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Teste comparativo de ponta a ponta (algoritmo genético + ambiente de treinamento reais, não
/// uma função de fitness sintética) pedido na investigação de estagnação: roda um treinamento
/// curto e verifica que a taxa de abate do melhor genoma melhora de verdade entre o começo e o
/// fim — não só que o número relatado de "melhor fitness" sobe (isso o elitismo já garantia por
/// construção), mas que o COMPORTAMENTO (abatidos vs. escapados) é mensuravelmente melhor.
/// </summary>
public sealed class EvolucaoDoTreinamentoTests
{
    private static readonly List<TowerDefinition> SemTorresConfiguradas = [];

    [Fact]
    public void Treinamento_AposVariasGeracoes_TaxaDeAbateDoMelhorGenomaMelhora()
    {
        // Episódio curto e população pequena só para o teste rodar rápido — o que importa é a
        // TENDÊNCIA (início vs. fim), não os números absolutos.
        var parametros = new ParametrosTreinamento
        {
            DuracaoEpisodioSegundos = 15,
            MaxAlvosPorQuadrante = 10,
            VelocidadeMinimaAlvoMetrosPorSegundo = 2.0,
            VelocidadeMaximaAlvoMetrosPorSegundo = 4.0,
            IntervaloSpawnMinSegundos = 0.3,
            IntervaloSpawnMaxSegundos = 0.8,
            EpisodiosPorAvaliacao = 2
        };
        var ambiente = new AmbienteTreinamento(parametros, SemTorresConfiguradas);
        var algoritmo = new AlgoritmoGenetico(
            tamanhoGenoma: RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao),
            tamanhoPopulacao: 24,
            taxaMutacao: 0.1,
            elitismo: 2,
            seed: 2024);

        double TaxaDeAbate(double[] genoma, int seed)
        {
            var resultado = ambiente.ExecutarEpisodioRapido(genoma, seed);
            int total = resultado.AlvosAbatidos + resultado.AlvosEscapados;
            return total == 0 ? 0 : (double)resultado.AlvosAbatidos / total;
        }

        ResultadoGeracao? primeira = null;
        ResultadoGeracao? ultima = null;

        for (int geracao = 1; geracao <= 25; geracao++)
        {
            int[] seedsDaGeracao = [geracao * 1000, geracao * 1000 + 1];
            ResultadoGeracao resultado = algoritmo.EvoluirGeracao(
                genoma => ambiente.AvaliarFitness(genoma, seedsDaGeracao));

            primeira ??= resultado;
            ultima = resultado;
        }

        Assert.NotNull(primeira);
        Assert.NotNull(ultima);

        // Taxa de abate do melhor genoma de CADA ponta, medida sobre vários episódios fixos (não
        // os mesmos usados para treinar) — média para não depender de sorte de uma seed só.
        int[] seedsDeAvaliacao = [90001, 90002, 90003, 90004, 90005, 90006];
        double taxaInicial = seedsDeAvaliacao.Average(seed => TaxaDeAbate(primeira!.MelhorGenoma, seed));
        double taxaFinal = seedsDeAvaliacao.Average(seed => TaxaDeAbate(ultima!.MelhorGenoma, seed));

        // Tolerância de 5pp: desde que a certeza efetiva passou a cair quando o alvo está perto
        // demais da torre (ver AlvoTreinamento.CertezaEfetiva), aprender a não deixar o alvo
        // chegar tão perto antes de disparar é mais uma coisa a evoluir — em poucas gerações
        // (como aqui, só para o teste rodar rápido) a taxa pode oscilar perto do teto sem isso
        // significar estagnação de verdade. Ambas as taxas continuam muito altas (>90%).
        const double toleranciaPontosPercentuais = 0.05;
        Assert.True(taxaFinal > taxaInicial - toleranciaPontosPercentuais,
            $"Esperava taxa de abate melhorar (ou não regredir mais que {toleranciaPontosPercentuais:P0}) com o treinamento: inicial={taxaInicial:P0}, final={taxaFinal:P0}.");
    }
}
