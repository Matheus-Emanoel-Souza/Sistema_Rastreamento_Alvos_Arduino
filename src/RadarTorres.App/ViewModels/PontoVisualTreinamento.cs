namespace RadarTorres.App.ViewModels;

/// <summary>Um ponto (torre ou alvo) já convertido para coordenadas de tela, para o Canvas da tela de treinamento.</summary>
public sealed class PontoVisualTreinamento : ViewModelBase
{
    public int Id { get; init; }

    private double _canvasX;
    public double CanvasX { get => _canvasX; set => SetProperty(ref _canvasX, value); }

    private double _canvasY;
    public double CanvasY { get => _canvasY; set => SetProperty(ref _canvasY, value); }

    private double _tamanho = 10;
    public double Tamanho { get => _tamanho; set => SetProperty(ref _tamanho, value); }

    private string _cor = "#60A5FA";
    public string Cor { get => _cor; set => SetProperty(ref _cor, value); }

    private bool _destacado;
    /// <summary>
    /// Marca o alvo que está alto o bastante para ser enxergado por TODAS as torres (eixo Z
    /// ligado — ver <see cref="NeuralEvolution.AlvoTreinamento.VisivelPorTodasAsTorres"/>). A
    /// altura muda o comportamento da simulação, então precisa aparecer na tela, não só no número.
    /// </summary>
    public bool Destacado { get => _destacado; set => SetProperty(ref _destacado, value); }

    /// <summary>
    /// Ângulo (bearing, 0°-360°) para onde esta torre está apontando agora — só usado pelo cano
    /// do canhão desenhado no Canvas (ver <see cref="Views.TreinamentoControladorView"/>); gira
    /// junto com o campo de visão (<see cref="SetorVisualTreinamento"/>) da mesma torre.
    /// </summary>
    private double _anguloGraus;
    public double AnguloGraus { get => _anguloGraus; set => SetProperty(ref _anguloGraus, value); }
}
