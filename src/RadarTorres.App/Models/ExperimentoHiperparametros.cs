using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Um resultado real (não simulado) de uma configuração de hiperparâmetros efetivamente treinada —
/// tamanho de população × tamanho da rede × taxa de mutação — usado para justificar
/// tecnicamente a escolha desses valores no artigo (ver
/// <see cref="Repositories.IExperimentoHiperparametrosRepository"/>).
/// A qual série comparativa a linha pertence NÃO é gravada: é derivada dos próprios valores na
/// tela de Estatísticas (<c>EstatisticasTreinamentoViewModel</c>), para que qualquer execução —
/// inclusive as já registradas antes desta análise — entre automaticamente na comparação em que
/// se encaixa.
/// </summary>
public sealed class ExperimentoHiperparametros
{
    public int Id { get; set; }
    public int TamanhoPopulacao { get; set; }
    public int Geracoes { get; set; }
    public int NeuroniosOcultos { get; set; }
    public double FitnessFinal { get; set; }
    public DateTime DataHora { get; set; }

    /// <summary>
    /// Probabilidade de mutação. O SIGNIFICADO depende de <see cref="Metodo"/>: em registros
    /// <see cref="MetodoAlgoritmoGenetico"/> é a probabilidade de cada gene sofrer mutação; em
    /// registros <see cref="MetodoNeat"/> é a probabilidade de mutação ESTRUTURAL (acrescentar
    /// um nó), que é o knob equivalente no NEAT. <c>null</c> nos registros gravados antes de
    /// este campo existir — a tela os trata como o padrão do sistema e marca o valor como
    /// presumido, em vez de descartar a execução.
    /// </summary>
    public double? TaxaMutacao { get; set; }

    /// <summary>
    /// Qual método evolutivo produziu esta linha. Existe porque a migração para o SharpNEAT
    /// mudou o significado de <see cref="NeuroniosOcultos"/> (passou a guardar o teto de
    /// complexidade, em conexões) e de <see cref="TaxaMutacao"/>: comparar uma execução antiga
    /// com uma nova como se fossem a mesma série produziria uma tabela errada no artigo.
    /// <c>null</c> nos registros anteriores a esta coluna — todos eles são, por construção,
    /// <see cref="MetodoAlgoritmoGenetico"/>.
    /// </summary>
    public string? Metodo { get; set; }

    /// <summary>Algoritmo genético próprio, rede de topologia fixa 7-8-2 (até outubro de 2026).</summary>
    public const string MetodoAlgoritmoGenetico = "GA";

    /// <summary>NEAT via SharpNEAT, topologia evoluída.</summary>
    public const string MetodoNeat = "NEAT";

    /// <summary>
    /// Tempo de parede gasto para rodar as <see cref="Geracoes"/> gerações desta configuração.
    /// <c>null</c> nos registros gravados antes de a medição existir — a tela mostra "—" nesses
    /// casos em vez de inventar um valor.
    /// </summary>
    public double? TempoSegundos { get; set; }

    /// <summary>
    /// Primeira geração em que <see cref="FitnessFinal"/> foi atingido — a métrica de VELOCIDADE
    /// de evolução: duas configurações podem terminar no mesmo fitness, mas a que chegou na
    /// geração 40 evoluiu mais rápido que a que chegou na 280. <c>null</c> em registros
    /// anteriores à medição.
    /// </summary>
    public int? GeracaoDoPico { get; set; }

    /// <summary>Melhor fitness na geração 25 — primeiro ponto da curva de convergência. <c>null</c> se não medido.</summary>
    public double? FitnessGeracao25 { get; set; }

    /// <summary>Melhor fitness na geração 50. <c>null</c> se não medido.</summary>
    public double? FitnessGeracao50 { get; set; }

    /// <summary>Melhor fitness na geração 100. <c>null</c> se não medido.</summary>
    public double? FitnessGeracao100 { get; set; }

    /// <summary>Melhor fitness na geração 200. <c>null</c> se não medido.</summary>
    public double? FitnessGeracao200 { get; set; }
}
