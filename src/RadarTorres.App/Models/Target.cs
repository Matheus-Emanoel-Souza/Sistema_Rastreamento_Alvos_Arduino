using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RadarTorres.App.Models;

/// <summary>
/// Representa um alvo detectado pelos sensores do Arduino. Uma instância de <see cref="Target"/>
/// é criada na primeira vez que um determinado <see cref="Id"/> é reportado e é apenas
/// <b>atualizada</b> (nunca recriada) enquanto o mesmo ID continuar sendo informado — ver
/// <see cref="Services.TargetTrackingService"/>.
/// </summary>
/// <remarks>
/// Implementa <see cref="INotifyPropertyChanged"/> para permitir data-binding direto no WPF
/// (o <see cref="Views.RadarControl"/> e os painéis de status reagem a mudanças de posição,
/// quadrante e torre selecionada sem que a coleção precise ser recriada).
/// </remarks>
public class Target : INotifyPropertyChanged
{
    private double _angle;
    private double _distance;
    private double _x;
    private double _y;
    private Quadrant _quadrant;
    private bool _isActive = true;
    private bool _isSelected;
    private Tower? _selectedTower;
    private DateTime _lastUpdate;

    /// <summary>Identificador único do alvo, atribuído pelo Arduino (campo ID do protocolo).</summary>
    public int Id { get; init; }

    /// <summary>Ângulo em graus (0-360), medido no sentido horário a partir do Norte (0°).</summary>
    public double Angle
    {
        get => _angle;
        set => SetField(ref _angle, value);
    }

    /// <summary>Distância do alvo até a base, em metros.</summary>
    public double Distance
    {
        get => _distance;
        set => SetField(ref _distance, value);
    }

    /// <summary>Posição cartesiana X (metros) relativa à base, calculada a partir de Angle/Distance.</summary>
    public double X
    {
        get => _x;
        set => SetField(ref _x, value);
    }

    /// <summary>Posição cartesiana Y (metros) relativa à base, calculada a partir de Angle/Distance.</summary>
    public double Y
    {
        get => _y;
        set => SetField(ref _y, value);
    }

    /// <summary>Quadrante em que o alvo se encontra (ver <see cref="Helpers.QuadrantHelper"/>).</summary>
    public Quadrant Quadrant
    {
        get => _quadrant;
        set => SetField(ref _quadrant, value);
    }

    /// <summary>Instante em que o alvo foi detectado pela primeira vez.</summary>
    public DateTime Timestamp { get; init; }

    /// <summary>Instante da última leitura recebida para este alvo. Usado para detectar timeout.</summary>
    public DateTime LastUpdate
    {
        get => _lastUpdate;
        set => SetField(ref _lastUpdate, value);
    }

    /// <summary>
    /// Indica se o alvo ainda está sendo detectado. É definido como <c>false</c> pelo
    /// serviço de rastreamento quando o tempo desde <see cref="LastUpdate"/> excede o timeout
    /// configurado; a UI usa esse valor para removê-lo do radar.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set => SetField(ref _isActive, value);
    }

    /// <summary>Indica se este é o alvo atualmente selecionado/em foco na interface.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    /// <summary>Torre demonstrativa escolhida pelo <see cref="Services.TowerSelectionService"/> para este alvo (se houver).</summary>
    public Tower? SelectedTower
    {
        get => _selectedTower;
        set => SetField(ref _selectedTower, value);
    }

    /// <summary>Distância entre a torre selecionada e o alvo, em metros (0 se nenhuma torre selecionada).</summary>
    public double DistanceToSelectedTower { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() =>
        $"Alvo #{Id:D2} | Ang={Angle:0.0}° Dist={Distance:0.00}m | X={X:0.00} Y={Y:0.00} | {Quadrant}";
}
