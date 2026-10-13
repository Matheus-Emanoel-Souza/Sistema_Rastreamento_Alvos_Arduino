using System.Collections.Generic;

namespace RadarTorres.App.Configuration;

/// <summary>
/// Modelo raiz de configuração, associado por binding ao arquivo <c>appsettings.json</c>
/// através de <c>Microsoft.Extensions.Configuration</c> (ver App.xaml.cs). Centraliza tudo
/// que o enunciado pede para ser "facilmente alterável": baud rate, porta padrão, distâncias
/// mínima/máxima, timeout de alvos, frequência do radar e a lista de torres.
/// </summary>
public sealed class AppSettings
{
    public SerialSettings SerialSettings { get; set; } = new();
    public RadarSettings RadarSettings { get; set; } = new();
    public List<TowerDefinition> Towers { get; set; } = new();
    public SimulationSettings SimulationSettings { get; set; } = new();
    public VisionSettings VisionSettings { get; set; } = new();
}

public sealed class SerialSettings
{
    public string DefaultPort { get; set; } = "COM4";
    public int DefaultBaudRate { get; set; } = 9600;
    public int ReadTimeoutMs { get; set; } = 2000;
    public int WriteTimeoutMs { get; set; } = 1000;
    public int ReconnectAttempts { get; set; } = 3;

    /// <summary>
    /// Tempo máximo (ms) sem receber nenhum byte antes de o watchdog de conexão
    /// considerar a comunicação perdida e disparar o evento de desconexão.
    /// </summary>
    public int ConnectionWatchdogMs { get; set; } = 3000;

    /// <summary>Lista de baud rates comuns oferecida no combo da UI.</summary>
    public static readonly int[] CommonBaudRates = { 9600, 19200, 38400, 57600, 115200 };
}

public sealed class RadarSettings
{
    public double MaxDetectionDistanceMeters { get; set; } = 2.5;
    public double MinSafetyDistanceMeters { get; set; } = 1.5;
    public int TargetTimeoutMs { get; set; } = 5000;
    public int RefreshRateMs { get; set; } = 150;
    public int DistanceRingCount { get; set; } = 8;
}

/// <summary>Configuração declarativa de uma torre, lida do JSON e usada para instanciar <see cref="Models.Tower"/>.</summary>
public sealed class TowerDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class SimulationSettings
{
    public int DefaultTargetCount { get; set; } = 3;
    public int GenerationIntervalMs { get; set; } = 800;
    public int MaxSimultaneousTargets { get; set; } = 8;
}

/// <summary>
/// Parâmetros do card "Detecção por Câmera" (Monitoramento) — ver
/// <see cref="Services.IObjectDetectionService"/>/<see cref="Services.IAlvoVisualService"/>.
/// Todos os limiares abaixo são valores de partida (não calibrados com câmera/objeto reais) —
/// ajuste aqui depois de testar com o hardware físico, sem precisar recompilar.
/// </summary>
public sealed class VisionSettings
{
    /// <summary>Confiança mínima (0–1) de uma caixa do YOLO para ser considerada candidata a alvo.</summary>
    public double ConfiancaMinima { get; set; } = 0.5;

    /// <summary>Intervalo mínimo (ms) entre duas rodadas de detecção — a inferência roda bem mais lenta que a taxa de quadros da câmera.</summary>
    public int IntervaloProcessamentoMs { get; set; } = 500;

    /// <summary>
    /// Campo de visão horizontal da câmera, em graus — usado para estimar a distância do alvo
    /// a partir do tamanho da caixa detectada (ver <see cref="Services.AlvoVisualService"/>).
    /// 60° é um valor típico de webcam comum; meça o campo de visão real da câmera usada para
    /// uma estimativa mais precisa.
    /// </summary>
    public double CameraFovHorizontalGraus { get; set; } = 60.0;

    /// <summary>Razão largura/altura (sempre ≥ 1) da caixa acima da qual o formato é considerado "Retangular" em vez de "Quadrado"/"Redondo".</summary>
    public double LimiarAspectoRetangular { get; set; } = 1.4;

    /// <summary>Fração de pixels da caixa que precisam bater com a cor do perfil para o formato ser classificado como "Quadrado" (preenche o retângulo quase todo) em vez de "Redondo" (preenche menos, por causa dos cantos vazios de um círculo inscrito).</summary>
    public double LimiarPreenchimentoQuadrado { get; set; } = 0.80;

    /// <summary>Fração mínima de pixels da caixa que precisam bater com a cor do perfil para a detecção não ser descartada por cor (objeto considerado "não é dessa cor" abaixo disso).</summary>
    public double LimiarPreenchimentoMinimo { get; set; } = 0.30;

    /// <summary>Tempo mínimo (segundos) entre dois alertas sonoros/por voz consecutivos, para não disparar a cada rodada de detecção enquanto o alvo continuar visível.</summary>
    public int DebounceAlertaSegundos { get; set; } = 5;

    /// <summary>
    /// Similaridade mínima (0–1) entre a foto de referência de um perfil e uma caixa candidata
    /// do quadro ao vivo para considerar que é o mesmo objeto (ver
    /// <see cref="Services.AlvoVisualService"/>). Só usado quando o perfil ativo tem foto de
    /// referência — sem foto, a detecção cai de volta para cor/formato. Comparação é por cor/
    /// textura média numa grade 8×8 da região, bem sensível a diferença de iluminação entre a
    /// foto de cadastro e a cena ao vivo — um valor de partida, quase certamente precisa ser
    /// calibrado para baixo (mais tolerante) depois de testar com o objeto físico real.
    /// </summary>
    public double LimiarSimilaridadeFoto { get; set; } = 0.65;
}
