namespace RadarTorres.App.Configuration;

/// <summary>
/// Preferências da aba "Configurações do Arduino" (caminho do CLI, último sketch, FQBN,
/// porta/baud rate e preferências do console), persistidas em
/// <c>%LocalAppData%\RadarTorres\arduino-settings.json</c> — ver <see cref="Services.IArduinoSettingsRepository"/>.
/// </summary>
/// <remarks>
/// Diferente de <see cref="AppSettings"/> (lido de <c>appsettings.json</c> na pasta de
/// instalação, somente leitura em tempo de execução) e de <c>PreferenciasUsuario</c>
/// (por usuário do sistema, em CSV), estas são preferências de máquina/instalação do
/// RadarTorres relacionadas a uma ferramenta externa (Arduino CLI) — por isso vivem em
/// <c>%LocalAppData%</c>, gravável sem privilégios de administrador, e não em
/// <c>C:\Program Files\...</c> nem misturadas às tabelas CSV de domínio.
/// </remarks>
public sealed class ArduinoCliSettings
{
    /// <summary>Caminho salvo pelo usuário para <c>arduino-cli.exe</c> (Procurar). Vazio até ser configurado ou detectado.</summary>
    public string? CliPath { get; set; }

    /// <summary>Último sketch (.ino) selecionado para compilação.</summary>
    public string? LastSketchPath { get; set; }

    /// <summary>FQBN da última placa selecionada.</summary>
    public string? SelectedFqbn { get; set; }

    /// <summary>Última porta COM usada (ambiente/monitor serial desta aba).</summary>
    public string? LastPort { get; set; }

    public int BaudRate { get; set; } = 9600;

    public bool ConsoleAutoScroll { get; set; } = true;

    public bool ConsoleShowTimestamps { get; set; } = true;
}
