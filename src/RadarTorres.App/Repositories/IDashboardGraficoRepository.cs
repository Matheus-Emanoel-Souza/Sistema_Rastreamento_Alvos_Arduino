using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>Gráficos configurados pelo usuário na tela "Dashboards" (ver <see cref="DashboardGrafico"/>). Poucas linhas — um por gráfico montado, não um por consulta executada.</summary>
public interface IDashboardGraficoRepository
{
    /// <summary>Grava um gráfico novo e devolve com o <see cref="DashboardGrafico.Id"/> preenchido.</summary>
    DashboardGrafico Registrar(DashboardGrafico card);

    /// <summary>Todos os gráficos configurados, em ordem de exibição (<see cref="DashboardGrafico.Ordem"/>).</summary>
    IReadOnlyList<DashboardGrafico> ObterTodos();

    /// <summary>Remove um gráfico pelo Id.</summary>
    void Remover(int id);
}
