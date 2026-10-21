using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using RadarTorres.App.Configuration;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="IRastreamentoVisualService"/>. Associação por continuidade
/// espacial: a caixa desta rodada pertence ao acompanhamento cuja última caixa ela mais
/// sobrepõe, desde que seja a mesma câmera, o mesmo quadrante e o mesmo perfil de alvo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que IoU e não "a caixa mais próxima".</b> Centro próximo acontece o tempo todo entre
/// objetos distintos num mesmo quadro (dois objetos da mesma cor lado a lado). A interseção sobre
/// união leva tamanho em conta, então duas caixas só casam se ocuparem aproximadamente a mesma
/// região — e ainda há um resgate por "centro dentro da caixa anterior", que cobre o caso real de
/// a caixa mudar muito de tamanho quando o objeto se aproxima.
/// </para>
/// <para>
/// <b>Tolerância.</b> Um acompanhamento sobrevive
/// <see cref="VisionSettings.ToleranciaPerdaRastreamentoSegundos"/> sem nenhuma detecção. É o que
/// faz oclusão (alguém passa na frente), saída e volta ao campo de visão e falha momentânea do
/// detector não virarem ocorrências novas. Passado esse tempo, a identidade é fechada — e
/// fechar identidade NUNCA é abate: o estado da ocorrência continua
/// <see cref="EstadoOcorrencia.Detectado"/>, e só um evento explícito do sistema o altera.
/// </para>
/// <para>
/// <b>Concorrência.</b> Uma thread por câmera chama estes métodos simultaneamente. Toda a
/// manipulação da lista acontece sob um único <c>lock</c>: as listas são curtas (poucos
/// acompanhamentos por câmera) e a seção crítica é só aritmética de retângulos, então a contenção
/// é irrelevante perto do custo de uma rodada de detecção.
/// </para>
/// </remarks>
public sealed class RastreamentoVisualService : IRastreamentoVisualService
{
    private readonly ILoggingService _logger;
    private readonly object _gate = new();
    private readonly List<RastreioVisual> _rastreios = new();

    public RastreamentoVisualService(ILoggingService logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<RastreioVisual> Ativos
    {
        get { lock (_gate) return _rastreios.Where(r => !r.Encerrado).ToList(); }
    }

    public RastreioVisual ObterOuCriar(string cameraDispositivoId, Quadrant quadrante, int? perfilAlvoId, Rectangle caixa, DateTime agora)
    {
        lock (_gate)
        {
            RastreioVisual? melhor = null;
            double melhorIoU = 0;

            foreach (RastreioVisual candidato in _rastreios)
            {
                if (candidato.Encerrado) continue;
                if (candidato.CameraDispositivoId != cameraDispositivoId) continue;
                if (candidato.Quadrante != quadrante) continue;
                if (candidato.PerfilAlvoId != perfilAlvoId) continue;

                double iou = CalcularIoU(candidato.UltimaCaixa, caixa);
                bool centroDentro = candidato.UltimaCaixa.Contains(Centro(caixa)) || caixa.Contains(Centro(candidato.UltimaCaixa));

                if (iou < AppConfig.Current.VisionSettings.LimiarIoURastreamento && !centroDentro) continue;
                if (iou < melhorIoU) continue;

                melhorIoU = iou;
                melhor = candidato;
            }

            if (melhor is not null)
            {
                melhor.UltimaCaixa = caixa;
                melhor.UltimaDeteccao = agora;
                melhor.Confirmacoes++;
                return melhor;
            }

            var novo = new RastreioVisual
            {
                CameraDispositivoId = cameraDispositivoId,
                Quadrante = quadrante,
                PerfilAlvoId = perfilAlvoId,
                UltimaCaixa = caixa,
                PrimeiraDeteccao = agora,
                UltimaDeteccao = agora,
                Confirmacoes = 1
            };

            _rastreios.Add(novo);
            return novo;
        }
    }

    public IReadOnlyList<RastreioVisual> EncerrarInativos(DateTime agora)
    {
        int toleranciaSegundos = AppConfig.Current.VisionSettings.ToleranciaPerdaRastreamentoSegundos;
        var encerrados = new List<RastreioVisual>();

        lock (_gate)
        {
            foreach (RastreioVisual rastreio in _rastreios)
            {
                if (rastreio.Encerrado) continue;
                if ((agora - rastreio.UltimaDeteccao).TotalSeconds < toleranciaSegundos) continue;

                rastreio.Encerrado = true;
                encerrados.Add(rastreio);
            }

            _rastreios.RemoveAll(r => r.Encerrado);
        }

        foreach (RastreioVisual rastreio in encerrados)
        {
            _logger.Info(
                $"Acompanhamento encerrado por ausência de detecção há mais de {toleranciaSegundos}s " +
                $"({QuadrantHelper.ToDisplayLabel(rastreio.Quadrante)}, ocorrência " +
                $"{rastreio.ObjetoDetectadoId?.ToString() ?? "não aberta"}). Não implica abate.");
        }

        return encerrados;
    }

    public void Encerrar(RastreioVisual rastreio, string motivo)
    {
        lock (_gate)
        {
            if (rastreio.Encerrado) return;
            rastreio.Encerrado = true;
            _rastreios.Remove(rastreio);
        }

        _logger.Info($"Acompanhamento da ocorrência {rastreio.ObjetoDetectadoId?.ToString() ?? "(não aberta)"} encerrado: {motivo}");
    }

    public void EncerrarDaCamera(string cameraDispositivoId, string motivo)
    {
        List<RastreioVisual> afetados;

        lock (_gate)
        {
            afetados = _rastreios.Where(r => r.CameraDispositivoId == cameraDispositivoId && !r.Encerrado).ToList();
            foreach (RastreioVisual rastreio in afetados) rastreio.Encerrado = true;
            _rastreios.RemoveAll(r => r.Encerrado);
        }

        if (afetados.Count > 0)
        {
            _logger.Info($"{afetados.Count} acompanhamento(s) encerrado(s) nesta câmera: {motivo}. Não implica abate.");
        }
    }

    private static Point Centro(Rectangle caixa) => new(caixa.X + (caixa.Width / 2), caixa.Y + (caixa.Height / 2));

    /// <summary>Interseção sobre união de duas caixas (0 = nenhuma sobreposição, 1 = idênticas).</summary>
    private static double CalcularIoU(Rectangle a, Rectangle b)
    {
        Rectangle interseccao = Rectangle.Intersect(a, b);
        if (interseccao.Width <= 0 || interseccao.Height <= 0) return 0;

        double areaInterseccao = (double)interseccao.Width * interseccao.Height;
        double areaUniao = ((double)a.Width * a.Height) + ((double)b.Width * b.Height) - areaInterseccao;

        return areaUniao <= 0 ? 0 : areaInterseccao / areaUniao;
    }
}
