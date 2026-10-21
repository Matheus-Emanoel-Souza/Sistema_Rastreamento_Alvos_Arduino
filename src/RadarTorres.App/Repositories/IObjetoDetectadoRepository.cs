using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Histórico de objetos detectados. Somente inserção — cada detecção é um fato imutável.
/// </summary>
public interface IObjetoDetectadoRepository
{
    IReadOnlyList<ObjetoDetectado> GetAll();
    ObjetoDetectado Add(ObjetoDetectado objeto);
}
