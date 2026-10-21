using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Registro persistido de um objeto/alvo detectado pelos sensores — a "foto" de uma detecção
/// no instante em que ela ocorreu, distinta do <see cref="Target"/> em memória (que representa
/// o alvo *vivo*, atualizado a cada leitura enquanto ativo no radar). Um <see cref="Target"/>
/// gera um <see cref="ObjetoDetectado"/> quando é detectado pela primeira vez
/// (<see cref="Services.ITargetTrackingService.TargetCreated"/>).
/// </summary>
public class ObjetoDetectado
{
    public int Id { get; set; }

    /// <summary>Tipo/classificação do objeto (hoje sempre "Alvo genérico" — sensor atual não classifica).</summary>
    public string Tipo { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    /// <summary>
    /// Coordenada de altura. <c>null</c> hoje porque os sensores do Arduino são 2D
    /// (ângulo + distância); campo mantido para sensores 3D futuros.
    /// </summary>
    public double? Z { get; set; }

    /// <summary>Quadrante/região em texto (ex.: "Q1"), ver <see cref="Quadrant"/>.</summary>
    public string Quadrante { get; set; } = string.Empty;

    public DateTime DataHora { get; set; }

    /// <summary>Sensor/dispositivo responsável pela detecção (ex.: "Arduino", "Simulador").</summary>
    public string Dispositivo { get; set; } = string.Empty;

    /// <summary>Nível de confiança da identificação (0-1), quando disponível. <c>null</c> se não medido.</summary>
    public double? NivelConfianca { get; set; }

    public string? Observacao { get; set; }

    /// <summary>Caminho/URL de uma imagem de referência, quando existir (não há câmera no projeto hoje).</summary>
    public string? ReferenciaImagem { get; set; }
}
