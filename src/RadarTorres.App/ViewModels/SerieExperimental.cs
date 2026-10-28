using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// Uma série comparativa da análise experimental de hiperparâmetros: as linhas em que UM
/// hiperparâmetro varia (4/8/16 neurônios, 30/60/120 indivíduos ou 0,04/0,08/0,16 de taxa de
/// mutação) e os outros ficam travados em um valor só. É a unidade que permite atribuir a
/// diferença de fitness ao hiperparâmetro variado — comparar linhas de séries diferentes
/// misturaria os efeitos, por isso os destaques são calculados DENTRO da série.
///
/// Cada tabela da tela "Estatísticas de Treinamento" é uma lista de séries: a do plano principal
/// (valores travados no baseline) e, abaixo dela, as séries adicionais que já existem no banco
/// porque foram treinadas antes — aproveitadas em vez de descartadas.
/// </summary>
public sealed class SerieExperimental
{
    public SerieExperimental(
        string titulo,
        bool ehPlanoPrincipal,
        IReadOnlyList<LinhaExperimentoComparativo> linhas,
        string unidadeVariada,
        Func<LinhaExperimentoComparativo, string> valorVariado)
    {
        Titulo = titulo;
        EhPlanoPrincipal = ehPlanoPrincipal;
        Linhas = linhas;

        MarcarDestaques(linhas);
        Conclusao = MontarConclusao(linhas, unidadeVariada, valorVariado);
    }

    /// <summary>Ex.: "População 60 · mutação 0,08".</summary>
    public string Titulo { get; }

    /// <summary>True na série cujos valores travados são o baseline — a que responde diretamente ao pedido do parecer.</summary>
    public bool EhPlanoPrincipal { get; }

    public string Rotulo => EhPlanoPrincipal ? "série de referência" : "série adicional";

    public IReadOnlyList<LinhaExperimentoComparativo> Linhas { get; }

    /// <summary>Leitura em uma frase: qual valor venceu em fitness, em velocidade e em custo-benefício.</summary>
    public string Conclusao { get; }

    /// <summary>Quantas das <see cref="Linhas"/> têm execução real registrada ("2 de 3 executadas").</summary>
    public string Cobertura => $"{Linhas.Count(l => l.Executado)} de {Linhas.Count} executadas";

    public bool Completa => Linhas.All(l => l.Executado);

    private const double Tolerancia = 1e-9;

    private static void MarcarDestaques(IReadOnlyList<LinhaExperimentoComparativo> linhas)
    {
        List<LinhaExperimentoComparativo> executadas = linhas.Where(l => l.Executado).ToList();
        if (executadas.Count == 0) return;

        // Empates são marcados em TODAS as linhas empatadas, não só na primeira: com o algoritmo
        // convergindo para o mesmo ótimo local em configurações diferentes, apontar uma
        // "vencedora" arbitrária daria a impressão falsa de que o hiperparâmetro fez diferença.
        double melhorFitness = executadas.Max(l => l.FitnessFinal);
        foreach (LinhaExperimentoComparativo linha in executadas.Where(l => Math.Abs(l.FitnessFinal - melhorFitness) < Tolerancia))
        {
            linha.MelhorFitness = true;
        }

        List<LinhaExperimentoComparativo> comTempo = executadas.Where(l => l.FitnessPorSegundo is not null).ToList();
        if (comTempo.Count > 0)
        {
            double melhorCusto = comTempo.Max(l => l.FitnessPorSegundo!.Value);
            foreach (LinhaExperimentoComparativo linha in comTempo.Where(l => Math.Abs(l.FitnessPorSegundo!.Value - melhorCusto) < Tolerancia))
            {
                linha.MelhorCustoBeneficio = true;
            }
        }

        // Velocidade de evolução: menor geração do pico. Só faz sentido destacar entre linhas que
        // chegaram ao MESMO fitness — "convergiu antes para um resultado pior" não é mérito.
        List<LinhaExperimentoComparativo> vencedorasComPico = executadas
            .Where(l => l.MelhorFitness && l.GeracaoDoPico is not null)
            .ToList();

        if (vencedorasComPico.Count == 0) return;

        int menorPico = vencedorasComPico.Min(l => l.GeracaoDoPico!.Value);
        foreach (LinhaExperimentoComparativo linha in vencedorasComPico.Where(l => l.GeracaoDoPico == menorPico))
        {
            linha.MaisRapido = true;
        }
    }

    private static string MontarConclusao(
        IReadOnlyList<LinhaExperimentoComparativo> linhas,
        string unidadeVariada,
        Func<LinhaExperimentoComparativo, string> valorVariado)
    {
        List<LinhaExperimentoComparativo> executadas = linhas.Where(l => l.Executado).ToList();

        if (executadas.Count == 0) return "Nenhuma configuração desta série foi executada ainda.";
        if (executadas.Count == 1)
            return $"Só uma configuração desta série foi executada ({valorVariado(executadas[0])} {unidadeVariada}) — rode as demais para a comparação ter sentido.";

        List<LinhaExperimentoComparativo> vencedoras = executadas.Where(l => l.MelhorFitness).ToList();
        var texto = new List<string>();

        if (vencedoras.Count == executadas.Count)
        {
            // Todas empatadas: o hiperparâmetro não alterou o resultado nesta série — informação
            // tão relevante para o artigo quanto um vencedor, e não pode ser escondida.
            texto.Add($"Empate em fitness: todas as configurações executadas chegaram a {executadas[0].FitnessFinalTexto} — nesta série, variar {unidadeVariada} não mudou o resultado final.");
        }
        else if (vencedoras.Count > 1)
        {
            texto.Add($"Melhor fitness empatado entre {string.Join(" e ", vencedoras.Select(valorVariado))} {unidadeVariada} ({vencedoras[0].FitnessFinalTexto}).");
        }
        else
        {
            texto.Add($"Melhor fitness: {valorVariado(vencedoras[0])} {unidadeVariada} ({vencedoras[0].FitnessFinalTexto}).");
        }

        texto.Add(DescreverVelocidade(executadas, vencedoras, unidadeVariada, valorVariado));

        LinhaExperimentoComparativo? custo = executadas.FirstOrDefault(l => l.MelhorCustoBeneficio);
        if (custo is not null && !(vencedoras.Count == 1 && custo.MelhorFitness))
        {
            texto.Add($"Melhor custo-benefício: {valorVariado(custo)} {unidadeVariada} ({custo.FitnessPorSegundoTexto} de fitness por segundo).");
        }

        return string.Join(" ", texto.Where(t => t.Length > 0));
    }

    /// <summary>
    /// A resposta para "evoluem mais rápido ou não": compara a geração em que cada configuração
    /// atingiu seu pico. Quando todas empataram em fitness, é a ÚNICA diferença que resta — e é
    /// o que distingue "chegou no mesmo lugar" de "chegou no mesmo lugar muito antes".
    /// </summary>
    private static string DescreverVelocidade(
        IReadOnlyList<LinhaExperimentoComparativo> executadas,
        IReadOnlyList<LinhaExperimentoComparativo> vencedoras,
        string unidadeVariada,
        Func<LinhaExperimentoComparativo, string> valorVariado)
    {
        List<LinhaExperimentoComparativo> comPico = executadas.Where(l => l.GeracaoDoPico is not null).ToList();

        if (comPico.Count == 0)
            return "Velocidade de convergência não disponível (execuções anteriores à medição) — use \"Refazer tudo\" para medi-la.";

        if (comPico.Count < executadas.Count)
            return $"Velocidade medida em apenas {comPico.Count} das {executadas.Count} execuções desta série — comparação parcial.";

        int menor = comPico.Min(l => l.GeracaoDoPico!.Value);
        int maior = comPico.Max(l => l.GeracaoDoPico!.Value);

        if (menor == maior)
            return $"Todas atingiram o pico na mesma geração ({menor}) — nenhuma evoluiu mais rápido.";

        List<LinhaExperimentoComparativo> rapidas = comPico.Where(l => l.GeracaoDoPico == menor).ToList();
        string quem = string.Join(" e ", rapidas.Select(valorVariado));
        string observacao = rapidas.Any(r => vencedoras.Contains(r))
            ? string.Empty
            : " (mas com fitness final menor — convergiu antes para um resultado pior)";

        return $"Convergência mais rápida: {quem} {unidadeVariada}, pico na geração {menor} contra {maior} da mais lenta{observacao}.";
    }

    /// <summary>Formata a taxa de mutação para uso como rótulo de valor variado/travado.</summary>
    public static string FormatarTaxa(double taxa) => taxa.ToString("0.###", CultureInfo.CurrentCulture);
}
