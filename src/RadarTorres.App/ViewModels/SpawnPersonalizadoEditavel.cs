namespace RadarTorres.App.ViewModels;

/// <summary>
/// Uma linha da tabela "Cenário personalizado" da tela de treinamento — posição (metros, mesma
/// convenção X/Y do Canvas) e velocidade de UM alvo, definidos à mão pelo usuário em vez de
/// sorteados. Convertida para <see cref="NeuralEvolution.SpawnScriptado"/> só no momento de
/// simular (ver <see cref="TreinamentoControladorViewModel.SimularCenarioPersonalizado"/>).
/// </summary>
public sealed class SpawnPersonalizadoEditavel : ViewModelBase
{
    private double _x = 8.0;
    public double X { get => _x; set => SetProperty(ref _x, value); }

    private double _y;
    public double Y { get => _y; set => SetProperty(ref _y, value); }

    private double _velocidadeMetrosPorSegundo = 1.0;
    public double VelocidadeMetrosPorSegundo { get => _velocidadeMetrosPorSegundo; set => SetProperty(ref _velocidadeMetrosPorSegundo, value); }

    private double _instanteSegundos;
    public double InstanteSegundos { get => _instanteSegundos; set => SetProperty(ref _instanteSegundos, value); }
}
