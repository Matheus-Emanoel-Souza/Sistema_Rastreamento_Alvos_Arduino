using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Histórico de auditoria de ações de acionamento. Propositalmente expõe apenas
/// <see cref="GetAll"/> e <see cref="Add"/> — sem Update/Delete — porque registros de
/// auditoria não devem ser alterados nem removidos por usuários comuns (Requisito 5).
/// </summary>
public interface IAcaoRealizadaRepository
{
    IReadOnlyList<AcaoRealizada> GetAll();
    AcaoRealizada Add(AcaoRealizada acao);
}
