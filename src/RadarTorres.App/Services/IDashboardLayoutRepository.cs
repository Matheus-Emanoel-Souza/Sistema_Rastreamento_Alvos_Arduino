using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Persiste o layout (posição/tamanho/visibilidade/ordem) dos cards de um
/// <c>DashboardCanvas</c>, definido livremente pelo usuário via arraste/redimensionamento/
/// ocultação. A chave do dicionário retornado é o <c>DashboardCard.CardId</c> de cada card.
///
/// Cada layout é identificado por uma <paramref name="layoutKey"/> própria — combinação de
/// tela ("painel-principal", "monitoramento", ...) e usuário — porque o layout é pessoal por
/// usuário e independente entre telas (Requisitos "salvo individualmente por usuário" e "mesmo
/// padrão de flexibilização... em todas as telas").
/// </summary>
public interface IDashboardLayoutRepository
{
    /// <summary>Retorna o layout salvo para <paramref name="layoutKey"/>, ou <c>null</c> se o
    /// usuário nunca salvou um layout pessoal para essa tela.</summary>
    Dictionary<string, DashboardCardLayout>? Load(string layoutKey);

    void Save(string layoutKey, Dictionary<string, DashboardCardLayout> layout);

    /// <summary>Remove o layout salvo de <paramref name="layoutKey"/>.</summary>
    void Clear(string layoutKey);

    /// <summary>Layout "padrão do sistema" de <paramref name="screen"/> (ex.: "monitoramento"),
    /// embutido no instalador (Resources/DefaultLayouts) — usado quando o usuário nunca salvou
    /// um layout pessoal e por "Retornar ao padrão do sistema". Retorna <c>null</c> se a tela
    /// não tiver um padrão embutido (cai no arranjo automático de DashboardCanvas).</summary>
    Dictionary<string, DashboardCardLayout>? LoadSystemDefault(string screen);
}
