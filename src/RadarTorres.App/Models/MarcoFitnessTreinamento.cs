using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Um marco de treinamento: fitness médio acumulado no bloco de 500 gerações que terminou em
/// <see cref="GeracaoMarco"/> (500, 1.000, 1.500...). Diferente de
/// <see cref="HistoricoGeracaoTreinamento"/> (buffer circular de só as últimas 300 gerações),
/// esta tabela nunca descarta linhas — um marco por 500 gerações é pouco volume mesmo em
/// treinamentos muito longos, e é exatamente o dado que o painel de evolução de longo prazo
/// precisa (ver <see cref="ViewModels.EstatisticasTreinamentoViewModel"/>).
/// </summary>
public class MarcoFitnessTreinamento
{
    public int Id { get; set; }

    /// <summary>Geração em que o bloco de 500 terminou (500, 1000, 1500...).</summary>
    public int GeracaoMarco { get; set; }

    /// <summary>Média do fitness médio de cada geração dentro deste bloco de 500.</summary>
    public double FitnessMedioDoBloco { get; set; }

    /// <summary>Melhor fitness já visto (máximo, não média) dentro deste bloco — contexto do "teto" alcançado no período.</summary>
    public double MelhorFitnessDoBloco { get; set; }

    public DateTime DataHora { get; set; }
}
