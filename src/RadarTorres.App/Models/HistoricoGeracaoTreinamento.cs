using System;

namespace RadarTorres.App.Models;

/// <summary>Um registro de "como a última geração se comportou" na tela de Treinamento do Controlador (ver IHistoricoTreinamentoRepository).</summary>
public sealed class HistoricoGeracaoTreinamento
{
    public int Id { get; set; }
    public int Geracao { get; set; }
    public double MelhorFitness { get; set; }
    public double FitnessMedio { get; set; }
    public int AlvosAbatidos { get; set; }
    public int AlvosEscapados { get; set; }

    /// <summary>
    /// Número de conexões do campeão desta geração. Só existe a partir do NEAT: no algoritmo
    /// genético anterior a topologia era fixa (7-8-2), então a complexidade era a mesma em toda
    /// geração e não havia nada para registrar. É <c>null</c> nas linhas gravadas antes desta
    /// coluna existir (ver <see cref="Repositories.SqliteHistoricoTreinamentoRepository"/>).
    /// </summary>
    public double? ComplexidadeMelhor { get; set; }

    /// <summary>Em quantas espécies a população estava dividida nesta geração — <c>null</c> nas linhas anteriores à coluna.</summary>
    public int? QuantidadeEspecies { get; set; }

    /// <summary>
    /// Execução de treinamento (clique em "Iniciar") à qual esta geração pertence — mesma chave
    /// de <see cref="ExecucaoTreinamento.ExecucaoId"/> e das demais tabelas de neuroevolução.
    /// Sem ela, duas execuções seguidas se misturavam no histórico, porque a numeração de
    /// gerações do NEAT reinicia em 0 a cada "Iniciar". Linhas gravadas antes desta coluna
    /// existir ficam com <c>'legada'</c>.
    /// </summary>
    public string ExecucaoId { get; set; } = "legada";

    public DateTime DataHora { get; set; }
}
