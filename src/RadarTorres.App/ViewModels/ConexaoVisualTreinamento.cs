namespace RadarTorres.App.ViewModels;

/// <summary>Uma conexão (peso) entre dois neurônios de camadas adjacentes, já em coordenadas de tela.</summary>
public sealed class ConexaoVisualTreinamento : ViewModelBase
{
    public double X1 { get; init; }
    public double Y1 { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }

    private string _cor = "#9CA3AF";
    public string Cor { get => _cor; set => SetProperty(ref _cor, value); }

    private double _espessura = 1.0;
    public double Espessura { get => _espessura; set => SetProperty(ref _espessura, value); }
}
