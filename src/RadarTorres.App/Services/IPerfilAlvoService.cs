using System.Collections.Generic;
using System.Collections.ObjectModel;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Dimensões físicas informadas no formulário, em centímetros. <see cref="TamanhoCm"/> é a
/// dimensão única de quadrado/redondo (lado/diâmetro); <see cref="LarguraCm"/>/<see cref="AlturaCm"/>
/// são as duas dimensões independentes de um objeto retangular. Agrupadas num tipo próprio para
/// a assinatura de <see cref="IPerfilAlvoService.Add"/>/<see cref="IPerfilAlvoService.Atualizar"/>
/// não crescer em três <c>double?</c> soltos, fáceis de trocar de ordem na chamada.
/// </summary>
public sealed record DimensoesAlvo(double? TamanhoCm, double? LarguraCm, double? AlturaCm)
{
    public static DimensoesAlvo Unica(double tamanhoCm) => new(tamanhoCm, null, null);

    public static DimensoesAlvo Retangulo(double larguraCm, double alturaCm) => new(null, larguraCm, alturaCm);
}

/// <summary>Resultado de uma tentativa de criar ou atualizar um perfil de alvo — mesmo padrão de <see cref="AuthResult"/>.</summary>
public sealed record ResultadoPerfilAlvo(bool Sucesso, string? Erro, PerfilAlvo? Perfil)
{
    public static ResultadoPerfilAlvo Ok(PerfilAlvo perfil) => new(true, null, perfil);

    public static ResultadoPerfilAlvo Falha(string erro) => new(false, erro, null);
}

/// <summary>
/// Gerencia os perfis de alvo (cor, formato, dimensões e fotografias que a detecção por visão
/// computacional deve travar) e qual deles está ativo no momento.
/// </summary>
/// <remarks>
/// Toda validação de negócio mora aqui, não na tela: nome obrigatório, dimensões coerentes com o
/// formato e o mínimo de <see cref="PerfilAlvo.MinimoFotos"/> fotografias válidas no cadastro. A
/// tela desabilita o botão pelos mesmos critérios, mas é esta camada que recusa de fato — é o
/// equivalente, num aplicativo desktop, à validação de servidor que não pode ser contornada
/// chamando a API direto.
/// </remarks>
public interface IPerfilAlvoService
{
    ObservableCollection<PerfilAlvo> Perfis { get; }

    /// <summary>Perfil atualmente ativo (o que a detecção deve travar agora), ou <c>null</c> se nenhum.</summary>
    PerfilAlvo? PerfilAtivo { get; }

    /// <summary>
    /// Cria e persiste um novo perfil de alvo. Exige no mínimo <see cref="PerfilAlvo.MinimoFotos"/>
    /// fotografias válidas (ver <see cref="Helpers.ValidacaoFotoAlvo"/>) — recusado caso contrário.
    /// </summary>
    ResultadoPerfilAlvo Add(string nome, CorAlvo cor, FormatoAlvo formato, DimensoesAlvo dimensoes, IReadOnlyList<FotoReferenciaAlvo> fotos);

    /// <summary>
    /// Atualiza um perfil JÁ cadastrado, preservando seu <see cref="PerfilAlvo.Id"/> (e portanto
    /// o vínculo com o histórico de <see cref="ObjetoDetectado"/> daquele alvo). Nunca cria um
    /// registro novo: perfil que não está na coleção é recusado.
    /// </summary>
    ResultadoPerfilAlvo Atualizar(PerfilAlvo perfil, string nome, CorAlvo cor, FormatoAlvo formato, DimensoesAlvo dimensoes, IReadOnlyList<FotoReferenciaAlvo> fotos);

    /// <summary>Remove definitivamente um perfil.</summary>
    void Remove(PerfilAlvo perfil);

    /// <summary>
    /// Marca o perfil informado como o único ativo (desmarca qualquer outro) e persiste a
    /// mudança.
    /// </summary>
    void SelecionarAtivo(PerfilAlvo perfil);
}
