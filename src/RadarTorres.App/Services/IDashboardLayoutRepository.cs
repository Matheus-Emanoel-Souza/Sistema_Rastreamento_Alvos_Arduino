using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Persiste o layout (posição/tamanho) dos cards do painel principal, definido livremente
/// pelo usuário via arraste/redimensionamento. A chave do dicionário é o
/// <c>DashboardCard.CardId</c> de cada card.
/// </summary>
public interface IDashboardLayoutRepository
{
    /// <summary>Retorna o layout salvo, ou <c>null</c> se nunca foi salvo (primeira execução).</summary>
    Dictionary<string, DashboardCardLayout>? Load();

    void Save(Dictionary<string, DashboardCardLayout> layout);

    /// <summary>Remove o layout salvo (usado por "Restaurar layout padrão").</summary>
    void Clear();
}
