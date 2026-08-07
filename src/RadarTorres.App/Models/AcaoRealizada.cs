using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Registro de auditoria de uma ação de acionamento demonstrativo (ex.: "Torre 1 —
/// acionamento — coordenadas (X, Y, Z)"). Gravado por <see cref="Services.FireControlService"/>
/// a cada tentativa (autorizada e executada, bloqueada ou com erro) — nunca editável ou
/// removível por usuários comuns (ver <see cref="Repositories.IAcaoRealizadaRepository"/>,
/// que propositalmente não expõe Update/Delete).
/// </summary>
public class AcaoRealizada
{
    public int Id { get; set; }

    /// <summary>Nome da torre/dispositivo responsável (ex.: "Torre 1").</summary>
    public string Dispositivo { get; set; } = string.Empty;

    /// <summary>Tipo da ação (hoje sempre "Acionamento demonstrativo").</summary>
    public string TipoAcao { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    /// <summary>Ver <see cref="ObjetoDetectado.Z"/> — nulo enquanto não houver sensor 3D.</summary>
    public double? Z { get; set; }

    public DateTime DataHora { get; set; }

    /// <summary>Login do usuário que originou a ordem, quando manual. <c>null</c> se automática.</summary>
    public string? UsuarioResponsavel { get; set; }

    public OrigemAcao Origem { get; set; }

    public ResultadoAcao Resultado { get; set; }

    /// <summary>Observação livre ou mensagem de erro (ex.: motivo do bloqueio de segurança).</summary>
    public string? Observacao { get; set; }
}
