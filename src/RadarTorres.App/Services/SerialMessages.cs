namespace RadarTorres.App.Services;

/// <summary>Tipo de mensagem recebida do Arduino, conforme o protocolo descrito em COMUNICACAO_ARDUINO.md.</summary>
public enum SerialMessageType
{
    Target,
    Status,
    Ack,
    Error,
    RedeSaida,
    Unknown
}

/// <summary>
/// Representa, de forma tipada, uma mensagem já decodificada recebida via serial (ou gerada
/// pelo simulador). É a base comum retornada por <see cref="SerialProtocolParser"/>;
/// use o <see cref="Type"/> para saber qual subtipo (<see cref="TargetMessage"/>,
/// <see cref="StatusMessage"/>, <see cref="AckMessage"/> ou <see cref="ErrorMessage"/>) esperar.
/// </summary>
public abstract record SerialMessage(SerialMessageType Type, string RawLine);

/// <summary>Mensagem <c>TARGET;ID=..;ANGLE=..;DIST=..</c> — leitura de um alvo.</summary>
public sealed record TargetMessage(int Id, double Angle, double Distance, string RawLine)
    : SerialMessage(SerialMessageType.Target, RawLine);

/// <summary>Mensagem <c>STATUS;SYSTEM=..</c> — status geral reportado pelo Arduino.</summary>
public sealed record StatusMessage(string SystemStatus, string RawLine)
    : SerialMessage(SerialMessageType.Status, RawLine);

/// <summary>Mensagem <c>ACK;CMD=..</c> — confirmação de recebimento de um comando enviado pelo PC.</summary>
public sealed record AckMessage(string Command, string RawLine)
    : SerialMessage(SerialMessageType.Ack, RawLine);

/// <summary>Mensagem <c>ERROR;REASON=..</c> — erro reportado pelo próprio firmware.</summary>
public sealed record ErrorMessage(string Reason, string RawLine)
    : SerialMessage(SerialMessageType.Error, RawLine);

/// <summary>
/// Mensagem <c>STATUS;REDE_AJUSTE=..;REDE_CONFIANCA=..</c> — resposta da rede neural embutida no
/// Arduino (ver <c>REDE;ENTRADAS=</c> em <see cref="SerialProtocolParser.BuildRedeEntradas"/>) a
/// um pedido de cálculo. Só diagnóstico: nunca aciona nada no firmware por conta própria (ver
/// <c>Docs/Documentos_Entregaveis/Artigo/Como_a_IA_funciona_no_RadarTorres.MD</c>, Parte 3).
/// </summary>
public sealed record RedeSaidaMessage(double AjustePontuacao, double ConfiancaDisparo, string RawLine)
    : SerialMessage(SerialMessageType.RedeSaida, RawLine);

/// <summary>Mensagem que não corresponde a nenhum formato conhecido do protocolo.</summary>
public sealed record UnknownMessage(string RawLine)
    : SerialMessage(SerialMessageType.Unknown, RawLine);
