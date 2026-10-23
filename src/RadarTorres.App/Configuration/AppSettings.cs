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

    /// <summary>
    /// Raio (m) do quadrado que cada câmera defende junto à base — uma câmera olha PARA FORA, em
    /// direção à borda do seu quadrante, nunca para dentro desta área. Uma detecção cujo X/Y
    /// calculado cai aqui dentro é descartada como ruído (ver
    /// <see cref="Services.OcorrenciaDeteccaoService.RegistrarDeteccao"/>), não um alvo de verdade
    /// se aproximando.
    /// </summary>
    public double RaioQuadradoDefendidoMetros { get; set; } = 0.5;
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
/// Todos os limiares abaixo são valores de partida — ajuste em <c>appsettings.json</c> depois
/// de testar com o hardware físico, sem precisar recompilar.
/// </summary>
/// <remarks>
/// ATENÇÃO: os valores inicializados aqui são apenas o fallback para chaves AUSENTES do
/// <c>appsettings.json</c> — toda chave presente no arquivo VENCE o valor do código
/// (<c>configuration.Bind</c> em <c>App.LoadAppSettings</c>). Já aconteceu de uma recalibração
/// feita só aqui não ter nenhum efeito em execução, porque o arquivo continuava com os números
/// antigos. Ao mudar um limiar, mude nos DOIS lugares — e confira o cabeçalho do relatório de
/// desempenho (<see cref="Services.IPerformanceMonitor"/>), que imprime os valores realmente
/// em vigor justamente para esse engano não passar despercebido de novo.
/// </remarks>
public sealed class VisionSettings
{
    /// <summary>
    /// Confiança mínima (0–1) de uma caixa do YOLO para ser considerada candidata a alvo.
    /// Baixo de propósito: o tiny-YOLOv3 é um modelo pequeno e raramente passa de 0,5 em
    /// objetos do dia a dia fora de cena ideal, e aqui a caixa é só uma *proposta de região* —
    /// quem decide se é o alvo é a comparação com o perfil em <see cref="Services.AlvoVisualService"/>,
    /// então vale deixar entrar candidato fraco e filtrar depois, em vez de perder o alvo aqui.
    /// </summary>
    public double ConfiancaMinima { get; set; } = 0.25;

    /// <summary>
    /// Intervalo mínimo (ms) entre duas rodadas de detecção. É um piso, não um alvo: uma rodada
    /// nunca começa enquanto a anterior não terminou (ver DeteccaoCameraViewModel), então o
    /// ritmo real é o maior entre este valor e o custo da rodada.
    /// </summary>
    /// <remarks>
    /// 120 ms só passou a fazer sentido depois de a rodada que resolve pela cor cair para ~27 ms
    /// (ver <see cref="Services.AlvoVisualService"/>) — com os ~700 ms que uma rodada custava
    /// antes, qualquer valor abaixo disso era ficção e o sistema respondia em torno de 1,5 s.
    /// Quando o detector ONNX precisa rodar, a rodada volta a custar ~480 ms e este piso deixa
    /// de ser o limitante, como deve ser.
    /// </remarks>
    public int IntervaloProcessamentoMs { get; set; } = 120;

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
    /// referência — sem foto, a detecção cai de volta para cor/formato.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Calibrado medindo a métrica atual (interseção de histograma matiz/saturação + grade
    /// espacial — ver <see cref="Services.AlvoVisualService"/>) sobre as fotos de referência
    /// reais do projeto: o mesmo caderno verde fotografado duas vezes, em ângulos e
    /// enquadramentos diferentes, pontuou <b>0,56</b>; o pior falso positivo da mesma cena
    /// (o rosto de quem segurava o caderno) ficou em <b>0,32</b>, seguido de cama 0,26,
    /// camiseta 0,22 e parede 0,14. 0,45 fica no meio dessa faixa livre, deixando folga dos
    /// dois lados para variação de iluminação ao vivo.
    /// </para>
    /// <para>
    /// A métrica anterior (diferença média de cor dividida por 255) comprimia qualquer par de
    /// imagens reais na faixa 0,57–0,86 — nos mesmos pares acima, rosto dava 0,80, cama 0,78 e
    /// camiseta 0,71, todos ACIMA do limiar de 0,65 que estava configurado. Era aprovação
    /// quase universal, não reconhecimento. Se mexer na métrica, recalibre este número junto.
    /// </para>
    /// </remarks>
    public double LimiarSimilaridadeFoto { get; set; } = 0.45;

    /// <summary>
    /// Peso (0–1) da interseção de histograma matiz/saturação na similaridade final; o restante
    /// vai para a grade espacial 8×8. Pesa mais no histograma porque ele não depende de onde o
    /// objeto está na caixa nem de estar girado — a grade 8×8 sozinha desalinha assim que o
    /// objeto é segurado num ângulo diferente do da foto de cadastro.
    /// </summary>
    public double PesoHistogramaCor { get; set; } = 0.7;

    /// <summary>
    /// Fração mínima de pixels de uma caixa candidata que precisam ter a cor cadastrada no
    /// perfil para ela ser aceita como alvo, mesmo quando a comparação com a foto passa. Serve
    /// de segunda prova barata contra falso positivo — e aproveita a cor do perfil, que antes
    /// era simplesmente ignorada sempre que o perfil tinha foto.
    /// </summary>
    public double LimiarCorCandidato { get; set; } = 0.25;

    /// <summary>
    /// Saturação mínima (0–1) para um pixel ter cor considerada "definida". Abaixo disso o
    /// pixel é cinza/branco/preto e não conta para nenhuma <see cref="Models.CorAlvo"/> —
    /// sem esse corte, parede clara e pele contavam como "Vermelho" e inflavam a fração de cor.
    /// </summary>
    public double SaturacaoMinimaCor { get; set; } = 0.25;

    /// <summary>Brilho mínimo (0–1) para um pixel ter cor considerada "definida" (descarta sombra/preto).</summary>
    public double BrilhoMinimoCor { get; set; } = 0.15;

    /// <summary>
    /// Área mínima, como fração do quadro, de uma região proposta por segmentação de cor
    /// (usada quando o YOLO não propõe nenhuma caixa com a cor do perfil — ver
    /// <see cref="Services.AlvoVisualService"/>). Evita propor respingos de cor do fundo.
    /// </summary>
    public double AreaMinimaRegiaoCor { get; set; } = 0.005;

    // ---------------------------------------------------------------- Rastreamento de ocorrências

    /// <summary>
    /// Quantas rodadas consecutivas de detecção o mesmo objeto precisa ser reconhecido antes de
    /// abrir uma ocorrência no histórico (ver <see cref="Services.RastreamentoVisualService"/>).
    /// </summary>
    /// <remarks>
    /// 2 e não 1 de propósito: a pontuação do reconhecimento oscila entre quadros, e um único
    /// quadro no limite do limiar geraria uma ocorrência que não se repete na rodada seguinte —
    /// exatamente o tipo de registro que suja o histórico. Com duas rodadas (~240 ms no intervalo
    /// padrão) o custo em latência é irrelevante e o falso positivo isolado desaparece.
    /// </remarks>
    public int ConfirmacoesParaRegistrarOcorrencia { get; set; } = 2;

    /// <summary>
    /// Por quantos segundos sem nenhuma detecção um rastreamento continua valendo — tolerância a
    /// oclusão, objeto saindo e voltando ao campo de visão e falha momentânea do detector.
    /// Enquanto vale, o objeto que reaparece volta para a MESMA ocorrência; depois disso, o
    /// rastreamento é encerrado e um reaparecimento gera uma ocorrência nova.
    /// </summary>
    /// <remarks>
    /// Encerrar o rastreamento NÃO significa abate: o estado da ocorrência continua
    /// <see cref="Models.EstadoOcorrencia.Detectado"/>. Abate só por evento explícito — ver
    /// <see cref="Models.EstadoOcorrencia"/>.
    /// </remarks>
    public int ToleranciaPerdaRastreamentoSegundos { get; set; } = 10;

    /// <summary>
    /// Interseção sobre união mínima entre a caixa deste quadro e a última caixa conhecida de um
    /// rastreamento para considerar que é o mesmo objeto. Baixo de propósito (0,2): a caixa muda
    /// bastante de tamanho conforme o objeto se aproxima ou gira, e perder a identidade por isso
    /// criaria ocorrências repetidas do mesmo alvo.
    /// </summary>
    public double LimiarIoURastreamento { get; set; } = 0.2;

    /// <summary>
    /// Intervalo mínimo (segundos) entre duas gravações de "continua sendo detectado" da mesma
    /// ocorrência. Evita um UPDATE no banco a cada rodada de um alvo parado em frente à câmera,
    /// sem perder a noção de até quando o acompanhamento durou.
    /// </summary>
    public int IntervaloAtualizacaoAcompanhamentoSegundos { get; set; } = 5;
}
