using System.Collections.Generic;

namespace RadarTorres.App.Services;

/// <summary>Um campo selecionável (eixo X ou métrica) de uma <see cref="FonteDadosDashboard"/>.</summary>
public sealed record CampoDashboard(string Chave, string Nome);

/// <summary>
/// Uma fonte de dados disponível na tela "Dashboards" — uma tabela já existente no banco (marcos
/// de fitness, instantâneos de comportamento, histórico de gerações, experimentos de
/// hiperparâmetros, experimentos de comparação de rede neural), com os campos que podem virar
/// eixo X ou métrica (ver <see cref="IFontesDeDadosDashboardService"/>). Não é uma tabela nova —
/// é só o catálogo de onde os dados já persistidos por outras telas podem ser lidos daqui.
/// </summary>
public sealed record FonteDadosDashboard(string Chave, string Nome, IReadOnlyList<CampoDashboard> CamposEixoX, IReadOnlyList<CampoDashboard> CamposMetrica);

/// <summary>Um ponto já resolvido (eixo X, valor Y) de uma série pedida a <see cref="IFontesDeDadosDashboardService.ObterSerie"/>.</summary>
public sealed record PontoSerieDashboard(string RotuloEixoX, double OrdinalEixoX, double ValorMetrica);
