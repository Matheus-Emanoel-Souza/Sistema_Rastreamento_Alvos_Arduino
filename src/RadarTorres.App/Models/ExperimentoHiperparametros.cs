using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Um resultado real (não simulado) de uma configuração de hiperparâmetros efetivamente treinada —
/// tamanho de população × neurônios na camada oculta — usado para justificar tecnicamente a escolha
/// desses valores no artigo (ver <see cref="Repositories.IExperimentoHiperparametrosRepository"/>).
/// A qual série comparativa a linha pertence (variação da camada oculta ou variação da população)
/// NÃO é gravada: é derivada dos próprios valores na tela de Estatísticas
/// (<c>EstatisticasTreinamentoViewModel</c>), para que qualquer execução — inclusive as já
/// registradas antes desta análise — entre automaticamente na comparação em que se encaixa.
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
    /// Tempo de parede gasto para rodar as <see cref="Geracoes"/> gerações desta configuração.
    /// <c>null</c> nos registros gravados antes de a medição existir — a tela mostra "—" nesses
    /// casos em vez de inventar um valor.
    /// </summary>
    public double? TempoSegundos { get; set; }
}
