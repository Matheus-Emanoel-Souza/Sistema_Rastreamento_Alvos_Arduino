using System;

namespace RadarTorres.App.Models;

/// <summary>Como um <see cref="DashboardGrafico"/> deve ser desenhado.</summary>
public enum TipoGraficoDashboard
{
    Linha,
    Barra
}

/// <summary>
/// Um gráfico configurado pelo usuário na tela "Dashboards" (seção "Dados") — qual fonte de dados,
/// qual campo vira o eixo X, qual campo vira o valor plotado (Y) e como desenhar. Persistido para
/// o usuário não precisar reconfigurar o mesmo gráfico toda vez que abrir a tela (ver
/// <see cref="Repositories.IDashboardGraficoRepository"/> e
/// <see cref="Services.IFontesDeDadosDashboardService"/>, que resolve <see cref="FonteChave"/>/
/// <see cref="CampoEixoXChave"/>/<see cref="CampoMetricaChave"/> nos dados reais).
/// </summary>
public sealed class DashboardGrafico
{
    public int Id { get; set; }

    public required string Titulo { get; set; }

    /// <summary>Chave da fonte de dados (ver <see cref="Services.FonteDadosDashboard.Chave"/>) — ex.: "MarcosFitness".</summary>
    public required string FonteChave { get; set; }

    /// <summary>Chave do campo usado como eixo X — ex.: "GeracaoMarco".</summary>
    public required string CampoEixoXChave { get; set; }

    /// <summary>Chave do campo numérico plotado no eixo Y — ex.: "FitnessMedioDoBloco".</summary>
    public required string CampoMetricaChave { get; set; }

    public TipoGraficoDashboard TipoGrafico { get; set; } = TipoGraficoDashboard.Linha;

    /// <summary>Ordem de exibição na tela — menor primeiro. Não reaproveita o <see cref="Id"/> porque reordenar não deveria reescrever identidade.</summary>
    public int Ordem { get; set; }

    public DateTime DataCriacao { get; set; } = DateTime.Now;
}
