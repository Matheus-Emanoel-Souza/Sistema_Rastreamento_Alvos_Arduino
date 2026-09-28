namespace RadarTorres.App.Models;

/// <summary>
/// Modo de operação do sistema, conforme selecionado pelo usuário no painel de controle —
/// modelo tipo semáforo (Requisito "3 modos: vermelho/amarelo/verde"). Cada modo habilita um
/// subconjunto de comportamentos automáticos; a cor de cada um é decidida pela View (ver
/// <c>Converters/SystemModeToLabelConverter</c> e <c>MonitoramentoView.xaml</c>), não faz parte
/// do domínio.
/// </summary>
public enum SystemMode
{
    /// <summary>Verde — Ligado apenas: alvos são detectados e exibidos, sem seleção de torre nem acionamento.</summary>
    LigadoApenas = 0,

    /// <summary>Amarelo — Acompanhar alvos: localização + seleção automática da torre mais próxima/adequada, sem acionamento.</summary>
    AcompanharAlvos = 1,

    /// <summary>Vermelho — Disparar: localização + seleção automática + acionamento demonstrativo automático (laser/indicador).</summary>
    Disparar = 2
}

/// <summary>Estado da conexão serial com o Arduino (ou com o simulador).</summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Error
}

/// <summary>Estado operacional de uma torre demonstrativa.</summary>
public enum TowerState
{
    /// <summary>Torre livre, pronta para ser selecionada.</summary>
    Idle,

    /// <summary>Torre foi escolhida para o alvo ativo no momento.</summary>
    Selected,

    /// <summary>Torre está executando o acionamento demonstrativo (laser/indicador).</summary>
    Firing,

    /// <summary>Torre fora de operação (manutenção, falha, desabilitada manualmente).</summary>
    Unavailable,

    /// <summary>Torre sem comunicação/offline.</summary>
    Offline
}

/// <summary>
/// Quadrante cartesiano em relação à base (origem), conforme convenção adotada no projeto:
/// Q1 = X positivo / Y positivo · Q2 = X negativo / Y positivo ·
/// Q3 = X negativo / Y negativo · Q4 = X positivo / Y negativo.
/// </summary>
public enum Quadrant
{
    /// <summary>Alvo exatamente sobre a base (X=0, Y=0) — sem quadrante definido.</summary>
    None,
    Q1,
    Q2,
    Q3,
    Q4
}

/// <summary>Origem de uma leitura/comando: hardware real ou simulador interno.</summary>
public enum DataSource
{
    Serial,
    Simulation
}
