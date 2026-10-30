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

    public DateTime DataHora { get; set; }
}
