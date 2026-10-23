using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Histórico de auditoria das mudanças de grau de perigosidade de alvos (ver
/// <see cref="Helpers.ClassificadorPerigoAlvo"/>). Propositalmente expõe apenas
/// <see cref="GetAll"/> e <see cref="Add"/> — mesmo critério de <see cref="IAcaoRealizadaRepository"/>:
/// registro de auditoria não é alterado nem removido por usuários comuns.
/// </summary>
public interface IClassificacaoPerigoRepository
{
    IReadOnlyList<ClassificacaoPerigoAlvo> GetAll();
    ClassificacaoPerigoAlvo Add(ClassificacaoPerigoAlvo classificacao);
}
