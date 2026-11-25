using System;
using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>Persistência dos espelhos periódicos do sistema (TABESP).</summary>
public interface IEspelhoSistemaRepository
{
    /// <summary>
    /// Fotografa o estado atual do sistema e grava uma linha. Os agregados vêm das próprias
    /// tabelas no mesmo banco, não de serviços em memória — assim o espelho reflete o que está
    /// PERSISTIDO, que é o que alguém consegue auditar depois.
    /// </summary>
    /// <returns>O espelho gravado.</returns>
    EspelhoSistema Capturar();

    IReadOnlyList<EspelhoSistema> ObterUltimos(int quantidade = 200);

    /// <summary>Espelhos de uma janela de tempo, em ordem cronológica — base para gráficos de evolução.</summary>
    IReadOnlyList<EspelhoSistema> ObterPorPeriodo(DateTime inicio, DateTime fim);
}
