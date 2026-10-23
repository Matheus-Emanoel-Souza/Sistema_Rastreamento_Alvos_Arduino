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
    public DateTime DataHora { get; set; }
}
