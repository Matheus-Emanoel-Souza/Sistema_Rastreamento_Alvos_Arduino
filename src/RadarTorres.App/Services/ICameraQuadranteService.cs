using System;
using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>Resultado de uma tentativa de atribuir quadrante a uma câmera.</summary>
public sealed record ResultadoAtribuicaoQuadrante(bool Sucesso, string? Erro)
{
    public static ResultadoAtribuicaoQuadrante Ok() => new(true, null);

    public static ResultadoAtribuicaoQuadrante Falha(string erro) => new(false, erro);
}

/// <summary>
/// Quem sabe qual câmera monitora qual quadrante. Consultado pela tela "Câmeras" (atribuição) e
/// pela detecção por visão (para rotular a ocorrência com o quadrante de quem a enxergou) — em
/// vez de cada ponto ler o repositório e repetir a validação da regra "uma câmera, um quadrante".
/// </summary>
public interface ICameraQuadranteService
{
    /// <summary>Todas as associações salvas, do quadrante 1 ao 4.</summary>
    IReadOnlyList<CameraQuadrante> Associacoes { get; }

    /// <summary>Disparado depois de qualquer atribuição/remoção, para as telas abertas se realinharem.</summary>
    event EventHandler? AssociacoesAlteradas;

    /// <summary>Quadrante da câmera informada, ou <see cref="Quadrant.None"/> se ela não tem quadrante atribuído.</summary>
    Quadrant ObterQuadrante(string? dispositivoId);

    /// <summary>Câmera responsável pelo quadrante, ou <c>null</c> se nenhuma foi atribuída.</summary>
    CameraQuadrante? ObterCameraDoQuadrante(Quadrant quadrante);

    /// <summary>
    /// Atribui o quadrante à câmera, transferindo-o de outra câmera se necessário.
    /// <see cref="Quadrant.None"/> remove a atribuição. Recusa quadrante fora de Q1..Q4 e
    /// dispositivo vazio.
    /// </summary>
    ResultadoAtribuicaoQuadrante Atribuir(CameraDevice camera, Quadrant quadrante);
}
