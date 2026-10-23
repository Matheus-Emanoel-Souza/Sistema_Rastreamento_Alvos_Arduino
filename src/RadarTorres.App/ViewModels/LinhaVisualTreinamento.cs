namespace RadarTorres.App.ViewModels;

/// <summary>Uma linha de mira/disparo (torre -> alvo) já em coordenadas de tela, para o Canvas da tela de treinamento.</summary>
public sealed class LinhaVisualTreinamento : ViewModelBase
{
    /// <summary>Id da torre dona desta linha — uma torre tem no máximo uma linha por vez.</summary>
    public int Id { get; init; }

    private double _x1;
    public double X1 { get => _x1; set => SetProperty(ref _x1, value); }

    private double _y1;
    public double Y1 { get => _y1; set => SetProperty(ref _y1, value); }

    private double _x2;
    public double X2 { get => _x2; set => SetProperty(ref _x2, value); }

    private double _y2;
    public double Y2 { get => _y2; set => SetProperty(ref _y2, value); }

    private double _espessura = 1.5;
    public double Espessura { get => _espessura; set => SetProperty(ref _espessura, value); }

    private string _cor = "#60A5FA";
    public string Cor { get => _cor; set => SetProperty(ref _cor, value); }
}
