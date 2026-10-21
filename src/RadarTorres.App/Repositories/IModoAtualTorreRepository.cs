using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>Histórico de auditoria de trocas de modo do sistema. Somente inserção.</summary>
public interface IModoAtualTorreRepository
{
    IReadOnlyList<ModoAtualTorre> GetAll();
    ModoAtualTorre Add(ModoAtualTorre alteracao);
}
