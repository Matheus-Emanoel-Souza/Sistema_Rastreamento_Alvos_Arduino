using System.Collections.Generic;

namespace RadarTorres.App.Services;

/// <summary>
/// Catálogo das fontes de dados que a tela "Dashboards" pode plotar, e a execução da consulta que
/// resolve uma combinação fonte+eixoX+métrica em pontos prontos para desenhar. Reaproveita os
/// repositórios já existentes (<c>ObterTodos</c>/<c>ObterUltimos</c>) — não cria nenhuma tabela ou
/// consulta nova além do catálogo em si.
/// </summary>
public interface IFontesDeDadosDashboardService
{
    /// <summary>Todas as fontes disponíveis, com os campos selecionáveis de cada uma.</summary>
    IReadOnlyList<FonteDadosDashboard> ObterFontes();

    /// <summary>
    /// Resolve uma fonte+eixoX+métrica em pontos, já ordenados pelo eixo X. Lista vazia se a
    /// fonte não tiver nenhum dado ainda (ex.: nenhum treinamento rodado) — nunca lança por falta
    /// de dado, só por combinação de chaves inválida (erro de programação, não de uso).
    /// </summary>
    IReadOnlyList<PontoSerieDashboard> ObterSerie(string fonteChave, string campoEixoXChave, string campoMetricaChave);
}
