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

    /// <summary>
    /// Identificador da sessão de treinamento (um <see cref="Guid"/> novo a cada "Iniciar"
    /// — ver TreinamentoControladorViewModel) que gravou este marco. A numeração de gerações do
    /// NEAT reinicia em 0 a cada sessão (ver TreinadorNeat.EvoluirGeracao), então duas sessões
    /// diferentes podem legitimamente produzir o MESMO <see cref="GeracaoMarco"/> (ex.: 500) —
    /// sem este campo, a segunda gravação colidia com a primeira no índice único e falhava com
    /// "UNIQUE constraint failed". Registros gravados antes deste campo existir usam
    /// <c>"legada"</c> (ver migração em SqliteMarcoFitnessTreinamentoRepository).
    /// </summary>
    public string ExecucaoId { get; set; } = "legada";
}
