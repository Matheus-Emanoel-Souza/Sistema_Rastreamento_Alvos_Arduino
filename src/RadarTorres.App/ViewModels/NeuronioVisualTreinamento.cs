namespace RadarTorres.App.ViewModels;

/// <summary>Um neurônio do diagrama da rede neural, já em coordenadas de tela.</summary>
public sealed class NeuronioVisualTreinamento : ViewModelBase
{
    public int Camada { get; init; }
    public int Indice { get; init; }

    public double CanvasX { get; init; }
    public double CanvasY { get; init; }
    public double Raio { get; init; } = 14;

    private string _cor = "#CCECFF";
    public string Cor { get => _cor; set => SetProperty(ref _cor, value); }

    private string _rotulo = string.Empty;
    public string Rotulo { get => _rotulo; set => SetProperty(ref _rotulo, value); }
}
