using System.Collections.ObjectModel;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Gerencia os perfis de alvo (cor, formato e tamanho que a detecção por visão computacional
/// deve travar) e qual deles está ativo no momento.
/// </summary>
public interface IPerfilAlvoService
{
    ObservableCollection<PerfilAlvo> Perfis { get; }

    /// <summary>Perfil atualmente ativo (o que a detecção deve travar agora), ou <c>null</c> se nenhum.</summary>
    PerfilAlvo? PerfilAtivo { get; }

    /// <summary>Cria e persiste um novo perfil de alvo.</summary>
    PerfilAlvo Add(string nome, CorAlvo cor, FormatoAlvo formato, double tamanhoCm);

    /// <summary>Remove definitivamente um perfil.</summary>
    void Remove(PerfilAlvo perfil);

    /// <summary>
    /// Marca o perfil informado como o único ativo (desmarca qualquer outro) e persiste a
    /// mudança.
    /// </summary>
    void SelecionarAtivo(PerfilAlvo perfil);

    /// <summary>Associa o caminho de uma foto de referência já salva em disco a este perfil.</summary>
    void DefinirFotoReferencia(PerfilAlvo perfil, string caminhoArquivo);
}
