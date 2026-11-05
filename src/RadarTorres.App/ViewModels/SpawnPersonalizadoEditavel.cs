namespace RadarTorres.App.ViewModels;

/// <summary>
/// Uma linha da tabela "Cenário personalizado" da tela de treinamento — posição (metros, mesma
/// convenção X/Y do Canvas) e velocidade de UM alvo, definidos à mão pelo usuário em vez de
/// sorteados. Convertida para <see cref="NeuralEvolution.SpawnScriptado"/> só no momento de
/// simular (ver <see cref="TreinamentoControladorViewModel.SimularCenarioPersonalizado"/>).
/// </summary>
public sealed class SpawnPersonalizadoEditavel : ViewModelBase
{
    private string _nome = string.Empty;
    public string Nome { get => _nome; set => SetProperty(ref _nome, value); }

    private double _x = 8.0;
    public double X { get => _x; set => SetProperty(ref _x, value); }

    private double _y;
    public double Y { get => _y; set => SetProperty(ref _y, value); }

    private double _velocidadeMetrosPorSegundo = 1.0;
    public double VelocidadeMetrosPorSegundo { get => _velocidadeMetrosPorSegundo; set => SetProperty(ref _velocidadeMetrosPorSegundo, value); }

    private double _instanteSegundos;
    public double InstanteSegundos { get => _instanteSegundos; set => SetProperty(ref _instanteSegundos, value); }

    private double _alturaMetros;
    /// <summary>
    /// Altura (m) deste alvo, considerada apenas quando o eixo Z está ligado (ver
    /// <see cref="NeuralEvolution.ParametrosTreinamento.EixoZHabilitado"/>). É aqui que se monta
    /// deliberadamente o caso de um alvo alto o bastante para ser visto por todas as torres —
    /// nos cenários do catálogo a altura é fixa em 0, para eles continuarem reproduzíveis.
    /// </summary>
    public double AlturaMetros { get => _alturaMetros; set => SetProperty(ref _alturaMetros, value); }
}
