using System.Collections.Generic;

namespace RadarTorres.App.Models;

/// <summary>
/// Uma placa/FQBN selecionável no combo "Placa" da aba Configurações do Arduino.
/// </summary>
/// <param name="Fqbn">Identificador completo da placa (ex.: "arduino:avr:uno"), usado literalmente no comando <c>arduino-cli compile --fqbn</c>.</param>
/// <param name="DisplayName">Nome amigável exibido na interface (ex.: "Arduino Uno").</param>
public sealed record ArduinoBoardOption(string Fqbn, string DisplayName)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// Lista curada de placas comuns, sempre disponível no combo mesmo sem o Arduino CLI
/// instalado ou sem nenhum "core" baixado — evita depender de acesso à rede/downloads
/// silenciosos apenas para preencher a interface (ver Docs/COMUNICACAO_ARDUINO.md).
/// O botão "Atualizar placas e portas" complementa esta lista com o resultado de
/// <c>arduino-cli board listall</c> quando o CLI está disponível e possui cores instalados.
/// </summary>
public static class ArduinoBoardCatalog
{
    public static IReadOnlyList<ArduinoBoardOption> DefaultBoards { get; } =
    [
        new ArduinoBoardOption("arduino:avr:uno", "Arduino Uno"),
        new ArduinoBoardOption("arduino:avr:nano", "Arduino Nano"),
        new ArduinoBoardOption("arduino:avr:mega", "Arduino Mega 2560"),
        new ArduinoBoardOption("arduino:avr:leonardo", "Arduino Leonardo"),
        new ArduinoBoardOption("esp32:esp32:esp32", "ESP32 Dev Module"),
        new ArduinoBoardOption("esp8266:esp8266:nodemcuv2", "NodeMCU 1.0 (ESP-12E)"),
    ];
}
