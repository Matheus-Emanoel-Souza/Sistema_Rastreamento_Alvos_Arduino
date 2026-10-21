using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RadarTorres.App.Models;

/// <summary>
/// Representa uma torre demonstrativa/virtual posicionada ao redor da base.
/// A quantidade de torres não é fixa: instâncias são criadas a partir da seção
/// <c>Towers</c> do <c>appsettings.json</c> (ver <see cref="Configuration.AppSettings"/>),
/// portanto adicionar ou remover torres é apenas uma questão de editar a configuração.
/// </summary>
public class Tower : INotifyPropertyChanged
{
    private TowerState _state = TowerState.Idle;
    private bool _isAvailable = true;
    private double _distanceToTarget;

    /// <summary>Identificador único da torre.</summary>
    public int Id { get; init; }

    /// <summary>Nome de exibição da torre (ex.: "Torre 1").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Posição cartesiana X da torre, em metros, relativa à base.</summary>
    public double X { get; init; }

    /// <summary>Posição cartesiana Y da torre, em metros, relativa à base.</summary>
    public double Y { get; init; }

    /// <summary>
    /// Quadrante predominante de cobertura da torre. Usado pelo <see cref="Services.TowerSelectionService"/>
    /// para priorizar torres "naturais" daquele quadrante antes de considerar as demais.
    /// </summary>
    public Quadrant PreferredQuadrant { get; init; }

    /// <summary>Estado operacional atual (Idle, Selected, Firing, Unavailable, Offline).</summary>
    public TowerState State
    {
        get => _state;
        set => SetField(ref _state, value);
    }

    /// <summary>
    /// Indica se a torre pode ser escolhida no momento. Uma torre pode estar indisponível
    /// por estar em manutenção, desabilitada manualmente ou já ocupada com outro alvo.
    /// </summary>
    public bool IsAvailable
    {
        get => _isAvailable;
        set => SetField(ref _isAvailable, value);
    }

    /// <summary>
    /// Distância (m) calculada até o alvo mais recentemente avaliado por esta torre.
    /// É um valor transitório, recalculado a cada execução do algoritmo de seleção.
    /// </summary>
    public double DistanceToTarget
    {
        get => _distanceToTarget;
        set => SetField(ref _distanceToTarget, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() => $"{Name} (X={X:0.0}, Y={Y:0.0}) [{State}]";
}
