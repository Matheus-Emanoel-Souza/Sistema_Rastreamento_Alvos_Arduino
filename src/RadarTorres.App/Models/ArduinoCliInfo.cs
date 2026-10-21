namespace RadarTorres.App.Models;

/// <summary>
/// Resultado da tentativa de localizar o executável <c>arduino-cli.exe</c> no computador do
/// usuário (Seção "Ambiente Arduino" da aba Configurações do Arduino).
/// </summary>
public sealed class ArduinoCliInfo
{
    public bool Found { get; init; }

    public string? ExecutablePath { get; init; }

    /// <summary>Versão relatada por <c>arduino-cli version</c>, quando foi possível executá-lo.</summary>
    public string? Version { get; init; }

    /// <summary>De onde o caminho foi obtido — exibido ao usuário para transparência da detecção.</summary>
    public ArduinoCliSource Source { get; init; }

    public static ArduinoCliInfo NotFound { get; } = new() { Found = false, Source = ArduinoCliSource.NaoEncontrado };
}

/// <summary>Origem do caminho do Arduino CLI encontrado, na ordem em que a detecção é tentada.</summary>
public enum ArduinoCliSource
{
    NaoEncontrado,

    /// <summary>Caminho previamente salvo pelo usuário (persistido em arduino-settings.json).</summary>
    ConfiguracaoSalva,

    /// <summary>Cópia local na pasta de instalação do RadarTorres.</summary>
    PastaDoAplicativo,

    /// <summary>Encontrado em algum diretório listado na variável de ambiente PATH.</summary>
    VariavelPath,

    /// <summary>Encontrado em um local comum de instalação (ex.: Arduino IDE 2.x, WinGet).</summary>
    LocalComumDeInstalacao
}
