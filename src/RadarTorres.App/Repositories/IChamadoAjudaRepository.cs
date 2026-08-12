using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>Chamados de ajuda abertos pelos usuários. Suporta atualização (resolução pelo administrador).</summary>
public interface IChamadoAjudaRepository
{
    IReadOnlyList<ChamadoAjuda> GetAll();
    ChamadoAjuda Add(ChamadoAjuda chamado);
    void Update(ChamadoAjuda chamado);
}
