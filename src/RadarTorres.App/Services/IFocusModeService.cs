using System.Threading;
using System.Threading.Tasks;

namespace RadarTorres.App.Services;

/// <summary>Resultado de uma tentativa de entrar no modo foco, para exibir feedback ao usuário.</summary>
public sealed record FocusModeResultado(
    int ProcessosFechados,
    int ProcessosComFalha,
    bool RedeDesativada,
    string? AvisoRede);

/// <summary>
/// Modo foco: fecha janelas de aplicativos em segundo plano e tenta isolar a máquina da
/// rede (modo avião ou, se não for possível, desativação dos adaptadores de rede), para que
/// o computador fique dedicado ao RadarTorres durante uma demonstração/operação.
/// </summary>
public interface IFocusModeService
{
    /// <summary>Verdadeiro entre uma chamada a <see cref="EnterFocusModeAsync"/> bem-sucedida e a saída via <see cref="ExitFocusModeAsync"/>.</summary>
    bool IsActive { get; }

    Task<FocusModeResultado> EnterFocusModeAsync(CancellationToken cancellationToken = default);

    /// <summary>Reverte o que é reversível (rede). As janelas fechadas não são reabertas.</summary>
    Task ExitFocusModeAsync(CancellationToken cancellationToken = default);
}
