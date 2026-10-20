using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Situação de uma ocorrência de detecção. A ocorrência nasce <see cref="Detectado"/> e só sai
/// desse estado por um evento explícito do sistema — <b>nunca</b> por deixar de aparecer na
/// imagem: perder o objeto de vista é oclusão, troca de enquadramento ou falha momentânea do
/// detector, não abate (ver <see cref="Services.RastreamentoVisualService"/>).
/// </summary>
public enum EstadoOcorrencia
{
    /// <summary>Alvo detectado e ainda em acompanhamento (ou já encerrado sem abate).</summary>
    Detectado,

    /// <summary>
    /// Houve acionamento demonstrativo bem-sucedido contra este alvo
    /// (<see cref="Services.IFireControlService.TryFireAsync"/>), mas o abate ainda depende de
    /// confirmação do operador na tela "Objetos Detectados".
    /// </summary>
    AbatePendente,

    /// <summary>Abate confirmado por um operador. Estado final.</summary>
    Abatido
}

/// <summary>
/// Registro persistido de uma <b>ocorrência</b> de detecção — o fato "este alvo foi visto aqui,
/// por este dispositivo, neste instante". É distinto de duas outras coisas, e misturá-las é a
/// origem da duplicidade que este modelo evita:
/// <list type="bullet">
/// <item><see cref="PerfilAlvo"/> é o <b>cadastro</b> do alvo (fotos, cor, dimensões). Um
/// cadastro origina várias ocorrências — até uma por quadrante, ver
/// <see cref="Services.IOcorrenciaDeteccaoService"/>.</item>
/// <item><see cref="Target"/> é o alvo <b>vivo</b> do radar Arduino, em memória, atualizado a
/// cada leitura; <see cref="RastreamentoId"/> é o equivalente para a detecção por câmera.</item>
/// </list>
/// </summary>
/// <remarks>
/// Enquanto a mesma identidade de rastreamento continua sendo acompanhada no mesmo quadrante,
/// nenhuma linha nova é criada: só <see cref="DataHoraUltimaDeteccao"/> avança. É o que impede
/// uma linha por quadro (ou por alerta) para um alvo parado em frente à câmera.
/// </remarks>
public class ObjetoDetectado
{
    public int Id { get; set; }

    /// <summary>Tipo/classificação do objeto (nome do perfil de alvo na detecção por câmera; "Alvo genérico" no radar, que não classifica).</summary>
    public string Tipo { get; set; } = string.Empty;

    public double X { get; set; }

    public double Y { get; set; }

    /// <summary>
    /// Coordenada de altura. <c>null</c> hoje porque os sensores do Arduino são 2D
    /// (ângulo + distância); campo mantido para sensores 3D futuros.
    /// </summary>
    public double? Z { get; set; }

    /// <summary>
    /// Quadrante/região em texto (ex.: "Q1"), ver <see cref="Quadrant"/>. Na detecção por câmera
    /// é o quadrante ATRIBUÍDO à câmera que detectou (<see cref="CameraQuadrante"/>); no radar,
    /// o quadrante calculado da posição do alvo.
    /// </summary>
    public string Quadrante { get; set; } = string.Empty;

    public DateTime DataHora { get; set; }

    /// <summary>Sensor/dispositivo responsável pela detecção (ex.: "Arduino", "Simulador", "Câmera").</summary>
    public string Dispositivo { get; set; } = string.Empty;

    /// <summary>Nível de confiança da identificação (0-1), quando disponível. <c>null</c> se não medido.</summary>
    public double? NivelConfianca { get; set; }

    public string? Observacao { get; set; }

    /// <summary>Caminho/URL de uma imagem de referência, quando existir.</summary>
    public string? ReferenciaImagem { get; set; }

    // ------------------------------------------------------------------ Vínculos e ciclo de vida

    /// <summary>
    /// <see cref="PerfilAlvo.Id"/> do alvo cadastrado que originou esta ocorrência, ou
    /// <c>null</c> para detecções que não vêm de um perfil (radar/simulador). É este vínculo —
    /// e não o nome em <see cref="Tipo"/> — que mantém o histórico associado ao alvo quando o
    /// cadastro é editado.
    /// </summary>
    public int? PerfilAlvoId { get; set; }

    /// <summary>Identificador do dispositivo de câmera que detectou (<see cref="CameraDevice.Id"/>), quando a origem é câmera.</summary>
    public string? CameraDispositivoId { get; set; }

    /// <summary>Nome da câmera que detectou, para a tela não depender de o dispositivo estar conectado.</summary>
    public string? CameraNome { get; set; }

    /// <summary>
    /// Identidade do rastreamento que gerou esta ocorrência — <b>a identidade da ocorrência, não
    /// a do alvo cadastrado</b>. Mesmo objeto acompanhado continuamente pela mesma câmera
    /// mantém o mesmo valor; o mesmo alvo visto por outra câmera/quadrante recebe outro.
    /// <c>null</c> em registros do radar e nos criados antes deste mecanismo.
    /// </summary>
    public string? RastreamentoId { get; set; }

    public EstadoOcorrencia Estado { get; set; } = EstadoOcorrencia.Detectado;

    /// <summary>
    /// Última vez que este mesmo rastreamento foi reconhecido. Avança enquanto o alvo continua
    /// sendo acompanhado, em vez de gerar uma linha nova.
    /// </summary>
    public DateTime? DataHoraUltimaDeteccao { get; set; }
}

/// <summary>
/// Uma transição de <see cref="EstadoOcorrencia"/> de uma ocorrência (tabela
/// <c>OcorrenciaEstados</c>). Existe para o estado atual nunca ser a única informação disponível:
/// auditoria precisa saber quando a ocorrência foi aberta, quando o acionamento a deixou
/// pendente de abate e quem confirmou ou descartou o abate.
/// </summary>
public sealed class OcorrenciaEstadoTransicao
{
    public int Id { get; set; }

    public int ObjetoDetectadoId { get; set; }

    public EstadoOcorrencia Estado { get; set; }

    public DateTime DataHora { get; set; }

    /// <summary>Login do usuário responsável, quando a transição partiu de uma ação humana.</summary>
    public string? Usuario { get; set; }

    public string? Observacao { get; set; }
}
