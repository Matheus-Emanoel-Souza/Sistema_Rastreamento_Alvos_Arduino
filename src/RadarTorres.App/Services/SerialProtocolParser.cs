using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RadarTorres.App.Services;

/// <summary>
/// Interpreta (parse) linhas de texto recebidas via serial do Arduino e as converte em
/// objetos <see cref="SerialMessage"/> fortemente tipados, além de construir as mensagens
/// de comando enviadas no sentido PC → Arduino. É a única classe do projeto que conhece
/// o formato textual do protocolo — todo o resto do sistema trabalha apenas com os tipos
/// de <see cref="SerialMessage"/>, o que permite trocar o protocolo no futuro (ex.: para
/// JSON ou binário) alterando apenas este arquivo.
///
/// Formato geral de uma mensagem: <c>TIPO;CHAVE1=VALOR1;CHAVE2=VALOR2;...</c>
/// Ver <c>Docs/Tecnica/COMUNICACAO_ARDUINO.md</c> para a especificação completa.
/// </summary>
public static class SerialProtocolParser
{
    private const double MinAngle = 0.0;
    private const double MaxAngle = 360.0;

    /// <summary>
    /// Tenta interpretar uma linha bruta recebida pela porta serial.
    /// Nunca lança exceção para entradas malformadas: nesse caso retorna <c>true</c> com uma
    /// <see cref="ErrorMessage"/> (dados semanticamente inválidos, ex. NaN/ângulo fora de faixa)
    /// ou uma <see cref="UnknownMessage"/> (linha que não corresponde a nenhum formato conhecido).
    /// </summary>
    public static bool TryParse(string? rawLine, out SerialMessage message)
    {
        string line = (rawLine ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(line))
        {
            message = new UnknownMessage(rawLine ?? string.Empty);
            return false;
        }

        string[] parts = line.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string header = parts[0].ToUpperInvariant();

        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < parts.Length; i++)
        {
            int eq = parts[i].IndexOf('=');
            if (eq <= 0) continue;
            fields[parts[i][..eq].Trim()] = parts[i][(eq + 1)..].Trim();
        }

        switch (header)
        {
            case "TARGET":
                return TryBuildTargetMessage(fields, line, out message);

            case "STATUS":
                if (fields.TryGetValue("REDE_AJUSTE", out string? redeAjusteStr) &&
                    fields.TryGetValue("REDE_CONFIANCA", out string? redeConfiancaStr))
                {
                    return TryBuildRedeSaidaMessage(redeAjusteStr, redeConfiancaStr, line, out message);
                }
                fields.TryGetValue("SYSTEM", out string? systemStatus);
                message = new StatusMessage(systemStatus ?? "UNKNOWN", line);
                return true;

            case "ACK":
                fields.TryGetValue("CMD", out string? cmd);
                message = new AckMessage(cmd ?? string.Empty, line);
                return true;

            case "ERROR":
                fields.TryGetValue("REASON", out string? reason);
                message = new ErrorMessage(reason ?? "DESCONHECIDO", line);
                return true;

            default:
                message = new UnknownMessage(line);
                return false;
        }
    }

    private static bool TryBuildRedeSaidaMessage(string ajusteStr, string confiancaStr, string rawLine, out SerialMessage message)
    {
        if (!double.TryParse(ajusteStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double ajuste) || double.IsNaN(ajuste) || double.IsInfinity(ajuste))
        {
            message = new ErrorMessage($"REDE_AJUSTE INVÁLIDO/NaN ('{ajusteStr}')", rawLine);
            return true;
        }

        if (!double.TryParse(confiancaStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double confianca) || double.IsNaN(confianca) || double.IsInfinity(confianca))
        {
            message = new ErrorMessage($"REDE_CONFIANCA INVÁLIDO/NaN ('{confiancaStr}')", rawLine);
            return true;
        }

        message = new RedeSaidaMessage(ajuste, confianca, rawLine);
        return true;
    }

    private static bool TryBuildTargetMessage(Dictionary<string, string> fields, string rawLine, out SerialMessage message)
    {
        if (!fields.TryGetValue("ID", out string? idStr) ||
            !fields.TryGetValue("ANGLE", out string? angleStr) ||
            !fields.TryGetValue("DIST", out string? distStr))
        {
            message = new ErrorMessage("MENSAGEM TARGET INCOMPLETA", rawLine);
            return true;
        }

        if (!int.TryParse(idStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
        {
            message = new ErrorMessage($"ID DE ALVO INVÁLIDO ('{idStr}')", rawLine);
            return true;
        }

        if (!double.TryParse(angleStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double angle) || double.IsNaN(angle) || double.IsInfinity(angle))
        {
            message = new ErrorMessage($"ÂNGULO INVÁLIDO/NaN PARA ALVO {id} ('{angleStr}')", rawLine);
            return true;
        }

        if (!double.TryParse(distStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double distance) || double.IsNaN(distance) || double.IsInfinity(distance))
        {
            message = new ErrorMessage($"DISTÂNCIA INVÁLIDA/NaN PARA ALVO {id} ('{distStr}')", rawLine);
            return true;
        }

        if (angle < MinAngle || angle >= MaxAngle)
        {
            message = new ErrorMessage($"ÂNGULO FORA DA FAIXA 0-360° PARA ALVO {id} ({angle})", rawLine);
            return true;
        }

        if (distance < 0)
        {
            message = new ErrorMessage($"DISTÂNCIA NEGATIVA PARA ALVO {id} ({distance})", rawLine);
            return true;
        }

        message = new TargetMessage(id, angle, distance, rawLine);
        return true;
    }

    // ---------- Construção de comandos PC -> Arduino ----------

    public static string BuildModeDetection() => "MODE;DETECTION";

    public static string BuildModeAuto() => "MODE;AUTO";

    public static string BuildSetMinDistance(double meters) =>
        $"SET;MIN_DISTANCE={meters.ToString("0.00", CultureInfo.InvariantCulture)}";

    public static string BuildSetMaxDistance(double meters) =>
        $"SET;MAX_DISTANCE={meters.ToString("0.00", CultureInfo.InvariantCulture)}";

    public static string BuildFire(int towerId, int targetId) =>
        $"FIRE;TOWER={towerId};TARGET={targetId}";

    /// <summary>
    /// Comando que reposiciona fisicamente (eixo PAN) a torre indicada para o ângulo informado —
    /// mesma convenção de bússola de <c>TARGET.ANGLE</c> (0°-360°, 0°=Norte, sentido horário).
    /// Enviado pelo <see cref="Services.TowerSelectionService"/> sempre que uma torre é designada
    /// para um alvo, inclusive quando está cobrindo um alvo de outro quadrante.
    /// </summary>
    public static string BuildAim(int towerId, double angleDegrees) =>
        $"AIM;TOWER={towerId};ANGLE={angleDegrees.ToString("0.0", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Rede neural embutida no Arduino (diagnóstico, nunca aciona nada — ver
    /// <c>Docs/Documentos_Entregaveis/Artigo/Como_a_IA_funciona_no_RadarTorres.MD</c>, Parte 3).
    /// Zera o buffer de carregamento de um genoma novo antes de enviar os 74 pesos.
    /// </summary>
    public static string BuildGenomaReset() => "GENOMA;RESET";

    /// <summary>Envia UM peso/viés do genoma (mesma ordem de <see cref="NeuralEvolution.RedeNeural"/>).</summary>
    public static string BuildGenomaIndice(int indice, double valor) =>
        $"GENOMA;INDICE={indice};VALOR={valor.ToString("0.000000", CultureInfo.InvariantCulture)}";

    /// <summary>Ativa o genoma recebido — o Arduino só aceita se os 74 índices já tiverem chegado.</summary>
    public static string BuildGenomaCommit() => "GENOMA;COMMIT";

    /// <summary>
    /// Pede ao Arduino que calcule, de forma independente, a saída da rede embutida para estas
    /// <see cref="NeuralEvolution.ControladorHibrido.QuantidadeEntradas"/> entradas (mesma ordem de
    /// <see cref="NeuralEvolution.ControladorHibrido.ConstruirEntradas(double,double,bool,bool,double,double,double,double,double,double)"/>).
    /// </summary>
    public static string BuildRedeEntradas(IReadOnlyList<double> entradas)
    {
        if (entradas.Count != NeuralEvolution.ControladorHibrido.QuantidadeEntradas)
        {
            throw new ArgumentException($"Esperadas {NeuralEvolution.ControladorHibrido.QuantidadeEntradas} entradas, recebidas {entradas.Count}.", nameof(entradas));
        }

        string valores = string.Join(';', entradas.Select(e => e.ToString("0.000000", CultureInfo.InvariantCulture)));
        return $"REDE;ENTRADAS={valores}";
    }
}
