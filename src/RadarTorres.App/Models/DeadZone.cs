using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RadarTorres.App.Models;

/// <summary>
/// Forma geométrica de uma zona morta: quadrante inteiro ou faixa (anel) de distância a
/// partir da base, independente de quadrante. Ver <see cref="DeadZone"/>.
/// </summary>
public enum DeadZoneType
{
    /// <summary>Bloqueia todo um quadrante (Q1-Q4) — usa <see cref="DeadZone.Quadrant"/>.</summary>
    Quadrant,

    /// <summary>Bloqueia uma faixa de distância da base (m), em qualquer direção — usa
    /// <see cref="DeadZone.MinDistance"/>/<see cref="DeadZone.MaxDistance"/>.</summary>
    DistanceRange
}

/// <summary>
/// Área onde alvos são deliberadamente ignorados pelo algoritmo de seleção de torre e pelo
/// controle de acionamento (ex.: área de pessoal em solo, estrutura própria, zona de
/// exclusão administrativa) — mas continuam visíveis/rastreados no radar normalmente, só não
/// recebem torre nem podem ser acionados enquanto estiverem dentro da zona.
/// </summary>
/// <remarks>
/// Só o campo <see cref="Enabled"/> é editável após a criação (ligar/desligar sem perder a
/// configuração) — trocar tipo/quadrante/faixa de uma zona existente é removê-la e criar
/// outra, para manter o formulário e a validação simples. Persistida por
/// <see cref="Services.IDeadZoneRepository"/>, avaliada por <see cref="Services.IDeadZoneService"/>.
/// </remarks>
public sealed class DeadZone : INotifyPropertyChanged
{
    private bool _enabled = true;

    public int Id { get; init; }

    /// <summary>Nome livre dado pelo administrador (ex.: "Pátio de manutenção").</summary>
    public string Name { get; init; } = string.Empty;

    public DeadZoneType Type { get; init; }

    /// <summary>Quadrante bloqueado. Só relevante quando <see cref="Type"/> é <see cref="DeadZoneType.Quadrant"/>.</summary>
    public Quadrant Quadrant { get; init; }

    /// <summary>Distância mínima (m) da faixa bloqueada. Só relevante quando <see cref="Type"/> é <see cref="DeadZoneType.DistanceRange"/>.</summary>
    public double MinDistance { get; init; }

    /// <summary>Distância máxima (m) da faixa bloqueada. Só relevante quando <see cref="Type"/> é <see cref="DeadZoneType.DistanceRange"/>.</summary>
    public double MaxDistance { get; init; }

    /// <summary>Zona desativada continua salva, mas não bloqueia nada nem é desenhada no radar.</summary>
    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    /// <summary>Descrição amigável usada na lista de zonas e nas mensagens de bloqueio.</summary>
    public string Description => Type == DeadZoneType.Quadrant
        ? $"Quadrante {Quadrant}"
        : $"Faixa {MinDistance:0.00}–{MaxDistance:0.00} m";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() => $"{Name} [{Description}]{(Enabled ? "" : " (desativada)")}";
}
