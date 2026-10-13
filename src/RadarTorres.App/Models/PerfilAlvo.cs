using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RadarTorres.App.Models;

/// <summary>
/// Descreve o que a detecção por visão computacional deve travar ao identificar um alvo: cor
/// (<see cref="Cor"/>), formato (<see cref="Formato"/>) e tamanho aproximado
/// (<see cref="TamanhoCm"/>, maior dimensão do objeto, em centímetros). O usuário pode salvar
/// vários perfis e escolher qual está <see cref="Selecionado"/> (ativo) a qualquer momento —
/// só um perfil fica selecionado por vez, ver <see cref="Services.IPerfilAlvoService.SelecionarAtivo"/>.
/// </summary>
/// <remarks>
/// Opcionalmente guarda o caminho de uma foto de referência (<see cref="CaminhoFotoReferencia"/>)
/// capturada de uma das câmeras do módulo "Câmeras", para tratar esse objeto manualmente depois
/// (ex.: conferir visualmente o que foi configurado, ou usar como material de apoio). A captura
/// em si não faz parte da detecção automática — é só um registro para consulta posterior.
/// </remarks>
public sealed class PerfilAlvo : INotifyPropertyChanged
{
    private bool _selecionado;
    private string? _caminhoFotoReferencia;

    public int Id { get; init; }

    /// <summary>Nome livre dado pelo usuário (ex.: "Bola vermelha pequena").</summary>
    public string Nome { get; init; } = string.Empty;

    public CorAlvo Cor { get; init; }

    public FormatoAlvo Formato { get; init; }

    /// <summary>Maior dimensão aproximada do objeto, em centímetros.</summary>
    public double TamanhoCm { get; init; }

    /// <summary>
    /// Se este é o perfil atualmente ativo (o que a detecção deve travar agora). Só um perfil
    /// fica <c>true</c> por vez — ver <see cref="Services.PerfilAlvoService.SelecionarAtivo"/>.
    /// </summary>
    public bool Selecionado
    {
        get => _selecionado;
        set => SetField(ref _selecionado, value);
    }

    /// <summary>
    /// Caminho absoluto da foto de referência capturada para este perfil, ou <c>null</c> se
    /// nenhuma foto foi tirada ainda.
    /// </summary>
    public string? CaminhoFotoReferencia
    {
        get => _caminhoFotoReferencia;
        set => SetField(ref _caminhoFotoReferencia, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override string ToString() =>
        $"{Nome} [{Cor}/{Formato}, {TamanhoCm:0.0} cm]{(Selecionado ? " (ativo)" : "")}";
}
