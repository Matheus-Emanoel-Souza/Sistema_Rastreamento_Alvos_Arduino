using System;
using System.Collections.Generic;
using System.Drawing;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Identidade de um objeto acompanhado ao longo dos quadros de UMA câmera. É a "identidade da
/// ocorrência", deliberadamente separada da identidade do alvo cadastrado
/// (<see cref="PerfilAlvo.Id"/>): o mesmo alvo cadastrado visto por duas câmeras tem dois
/// rastreamentos e, portanto, duas ocorrências — uma por quadrante.
/// </summary>
public sealed class RastreioVisual
{
    /// <summary>Identidade estável deste acompanhamento, gravada em <see cref="ObjetoDetectado.RastreamentoId"/>.</summary>
    public string Id { get; } = Guid.NewGuid().ToString("N");

    public string CameraDispositivoId { get; init; } = string.Empty;

    public Quadrant Quadrante { get; init; }

    /// <summary>Perfil de alvo reconhecido neste acompanhamento (<c>null</c> se a origem não tem cadastro).</summary>
    public int? PerfilAlvoId { get; init; }

    /// <summary>Última caixa conhecida do objeto, em pixels do quadro — base da associação entre quadros consecutivos.</summary>
    public Rectangle UltimaCaixa { get; set; }

    public DateTime PrimeiraDeteccao { get; init; }

    public DateTime UltimaDeteccao { get; set; }

    /// <summary>Quantas rodadas consecutivas confirmaram este objeto (ver <see cref="Configuration.VisionSettings.ConfirmacoesParaRegistrarOcorrencia"/>).</summary>
    public int Confirmacoes { get; set; }

    /// <summary>Ocorrência já gravada para este acompanhamento, ou <c>null</c> enquanto ela não foi aberta.</summary>
    public int? ObjetoDetectadoId { get; set; }

    /// <summary>Última vez que o "continua sendo detectado" foi gravado no banco (controle de frequência).</summary>
    public DateTime UltimaAtualizacaoPersistida { get; set; }

    public bool Encerrado { get; set; }

    public override string ToString() =>
        $"Rastreio {Id[..8]} | câmera {CameraDispositivoId} | {Helpers.QuadrantHelper.ToDisplayLabel(Quadrante)} | perfil {PerfilAlvoId?.ToString() ?? "—"}";
}

/// <summary>
/// Mantém a identidade dos objetos reconhecidos pela detecção por câmera entre quadros
/// consecutivos. Existe porque <see cref="ITargetTrackingService"/> — o rastreamento que já havia
/// no projeto — resolve um problema diferente: lá o identificador do alvo <b>vem pronto</b> no
/// protocolo do Arduino, e basta indexar por ele. A câmera não fornece identificador nenhum; a
/// identidade tem de ser deduzida da continuidade espacial das caixas entre rodadas, que é o que
/// esta classe faz.
/// </summary>
/// <remarks>
/// Compartilhado por todas as câmeras que estiverem detectando ao mesmo tempo (registro
/// Singleton), com acesso sincronizado — cada câmera processa seus quadros na própria thread.
/// </remarks>
public interface IRastreamentoVisualService
{
    /// <summary>Acompanhamentos ainda válidos (não encerrados por tolerância nem por parada da câmera).</summary>
    IReadOnlyList<RastreioVisual> Ativos { get; }

    /// <summary>
    /// Associa a caixa detectada a um acompanhamento existente (mesma câmera, mesmo quadrante,
    /// mesmo perfil e caixa sobreposta) ou abre um novo, e conta a confirmação da rodada.
    /// </summary>
    RastreioVisual ObterOuCriar(string cameraDispositivoId, Quadrant quadrante, int? perfilAlvoId, Rectangle caixa, DateTime agora);

    /// <summary>
    /// Encerra os acompanhamentos sem detecção há mais que a tolerância configurada. Encerrar é
    /// só fechar a identidade — <b>nunca</b> marca abate.
    /// </summary>
    IReadOnlyList<RastreioVisual> EncerrarInativos(DateTime agora);

    /// <summary>Encerra um acompanhamento específico (ex.: a ocorrência dele foi abatida).</summary>
    void Encerrar(RastreioVisual rastreio, string motivo);

    /// <summary>Encerra todos os acompanhamentos de uma câmera — ela parou de transmitir ou trocou de dispositivo.</summary>
    void EncerrarDaCamera(string cameraDispositivoId, string motivo);
}
