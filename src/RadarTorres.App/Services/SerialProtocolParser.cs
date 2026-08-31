using System;
using System.Collections.Generic;
using System.Globalization;

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

    public static string BuildSystemOn() => "SYSTEM;ON";

    public static string BuildSystemOff() => "SYSTEM;OFF";

    public static string BuildModeDetection() => "MODE;DETECTION";

    public static string BuildModeAuto() => "MODE;AUTO";

    public static string BuildSetMinDistance(double meters) =>
        $"SET;MIN_DISTANCE={meters.ToString("0.00", CultureInfo.InvariantCulture)}";

    public static string BuildSetMaxDistance(double meters) =>
        $"SET;MAX_DISTANCE={meters.ToString("0.00", CultureInfo.InvariantCulture)}";

    public static string BuildFire(int towerId, int targetId) =>
        $"FIRE;TOWER={towerId};TARGET={targetId}";
}
