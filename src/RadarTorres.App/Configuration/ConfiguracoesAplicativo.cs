using RadarTorres.App.Models;

namespace RadarTorres.App.Configuration;

/// <summary>Como a janela principal deve abrir.</summary>
public enum ModoAberturaJanela
{
    /// <summary>Abre no tamanho definido em <see cref="ConfiguracoesAplicativo.LarguraJanela"/>/<see cref="ConfiguracoesAplicativo.AlturaJanela"/>.</summary>
    TamanhoPersonalizado,

    /// <summary>Abre maximizada, ocupando a tela toda.</summary>
    Maximizada,

    /// <summary>Abre no tamanho em que a janela estava quando o aplicativo foi fechado da última vez.</summary>
    LembrarUltimoTamanho
}

/// <summary>
/// Preferências do aplicativo nesta MÁQUINA, válidas antes e independentemente de qualquer
/// login — tamanho da janela, idioma inicial, tema inicial e comportamentos gerais. Gravadas em
/// JSON por <see cref="Services.IConfiguracoesAplicativoRepository"/>.
/// </summary>
/// <remarks>
/// Distinta de <see cref="PreferenciasUsuario"/>, que é por conta de usuário e fica no banco:
/// aquelas só podem ser lidas DEPOIS da autenticação, e a janela principal já precisa existir e
/// estar dimensionada antes da tela de login aparecer. Por isso o que afeta a abertura do
/// aplicativo mora aqui, num arquivo local lido na inicialização.
/// </remarks>
public sealed class ConfiguracoesAplicativo
{
    // ------------------------------------------------------------------ Janela

    public ModoAberturaJanela ModoAbertura { get; set; } = ModoAberturaJanela.TamanhoPersonalizado;

    /// <summary>Largura desejada da janela, em pixels independentes de dispositivo.</summary>
    public double LarguraJanela { get; set; } = 1450;

    public double AlturaJanela { get; set; } = 860;

    /// <summary>
    /// Último tamanho/estado observado ao fechar o aplicativo, usado por
    /// <see cref="ModoAberturaJanela.LembrarUltimoTamanho"/>. Gravado automaticamente, não
    /// editado pelo usuário na tela de Configurações.
    /// </summary>
    public double? UltimaLargura { get; set; }

    public double? UltimaAltura { get; set; }

    public bool UltimaMaximizada { get; set; }

    // ------------------------------------------------------------------ Aparência e idioma

    /// <summary>Idioma com que o aplicativo abre ("pt-BR" ou "en-US").</summary>
    public string Idioma { get; set; } = "pt-BR";

    public TemaPreferido Tema { get; set; } = TemaPreferido.Escuro;

    // ------------------------------------------------------------------ Comportamento

    /// <summary>
    /// Se a detecção por câmera deve religar sozinha ao abrir a tela de Monitoramento. Desligar
    /// é útil em máquina com webcam compartilhada com outro programa (reunião, gravação), em
    /// que ocupar a câmera automaticamente atrapalha.
    /// </summary>
    public bool IniciarCameraAutomaticamente { get; set; } = true;

    /// <summary>Se o contorno das regiões avaliadas aparece sobre o vídeo da detecção.</summary>
    public bool MostrarCaixasDeteccao { get; set; } = true;

    /// <summary>Se o alerta sonoro/por voz toca ao identificar um alvo.</summary>
    public bool AlertaSonoroHabilitado { get; set; } = true;

    /// <summary>
    /// Se cada execução gera o relatório de desempenho em disco
    /// (ver <see cref="Services.IPerformanceMonitor"/>). Configurável porque é instrumentação de
    /// diagnóstico: quem só quer usar o sistema pode desligar e não acumular arquivos.
    /// </summary>
    public bool GerarRelatorioDesempenho { get; set; } = true;

    // ------------------------------------------------------------------ Heurísticas do controlador

    /// <summary>
    /// Evita trocar a torre escolhida para um alvo quando a nova candidata vencedora está
    /// praticamente empatada com a torre já comprometida (ver
    /// <see cref="Services.TowerSelectionService"/>) — sem isto, um alvo perto da fronteira
    /// entre duas torres pode fazer a seleção oscilar entre elas a cada leitura do sensor,
    /// reenviando comandos de mira (pan) sem necessidade real.
    /// </summary>
    public bool HeuristicaAntiOscilacaoAtiva { get; set; } = true;

    /// <summary>
    /// Permite que um alvo mais urgente (mais perto da distância mínima de segurança) "roube"
    /// uma torre já comprometida com outro alvo bem menos urgente, em vez de ficar sem nenhuma
    /// torre disponível enquanto a torre menos crítica continua ocupada.
    /// </summary>
    public bool HeuristicaPriorizacaoPorUrgenciaAtiva { get; set; } = true;

    /// <summary>
    /// Descarta uma torre candidata se a linha entre ela e o alvo atravessar uma zona morta
    /// ativa — mesmo que o alvo em si não esteja dentro de nenhuma zona (hoje só isso é
    /// checado). Evita escolher uma torre cujo "tiro" passaria por uma área que o sistema foi
    /// configurado para nunca atingir.
    /// </summary>
    public bool HeuristicaCorredorDeTiroAtiva { get; set; } = true;

    /// <summary>
    /// Entre candidatas praticamente empatadas, prefere a torre usada com menos frequência
    /// nesta sessão, em vez de sempre a mesma — distribui o desgaste entre torres equivalentes.
    /// </summary>
    public bool HeuristicaBalanceamentoDeCargaAtiva { get; set; } = true;

    /// <summary>
    /// Calcula o ângulo de mira para onde o alvo estará um instante à frente (com base na
    /// velocidade estimada), em vez de para a posição atual dele — só afeta o ângulo de mira,
    /// nunca qual torre é escolhida.
    /// </summary>
    public bool HeuristicaPredicaoDeTrajetoriaAtiva { get; set; } = true;

    // ------------------------------------------------------------------ Disparo

    /// <summary>Piso absoluto de <see cref="LimiarConfiancaDisparoPercentual"/> — por mais que o
    /// usuário tente baixar mais que isto na tela, o valor salvo não desce daqui (ver
    /// <see cref="Services.ConfiguracoesAplicativoRepository"/>).</summary>
    public const double ConfiancaMinimaDisparoPercentual = 40;

    /// <summary>
    /// Certeza mínima (%) de que a câmera realmente identificou um alvo para que
    /// <see cref="Services.FireControlService"/> autorize o disparo automático — junto com "fora
    /// de zona morta" e "modo Disparar", é um dos três únicos critérios que valem, independente
    /// da distância até o alvo. Nunca pode ficar abaixo de <see cref="ConfiancaMinimaDisparoPercentual"/>.
    /// </summary>
    public double LimiarConfiancaDisparoPercentual { get; set; } = 70;

    // Nota: "registros por página" NÃO é oferecido aqui de propósito. O campo existe em
    // PreferenciasUsuario desde antes, mas nenhuma tela de histórico pagina de fato — expor o
    // controle daria a impressão de um ajuste que não produz efeito algum. Quando a paginação
    // for implementada, o lugar dela continua sendo PreferenciasUsuario (é preferência por
    // conta de usuário, não da máquina).

    /// <summary>Valores de fábrica — usados pelo botão "Restaurar padrões".</summary>
    public static ConfiguracoesAplicativo Padrao() => new();

    /// <summary>
    /// Cópia independente, para a tela de Configurações poder editar livremente e só aplicar no
    /// "Salvar" — sem isso, mexer em um controle já alteraria o objeto em uso pelo aplicativo e
    /// "Cancelar" não teria o que desfazer.
    /// </summary>
    public ConfiguracoesAplicativo Clonar() => (ConfiguracoesAplicativo)MemberwiseClone();
}
