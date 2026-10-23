using RadarTorres.App.NeuralEvolution;

namespace RadarTorres.App.Services;

/// <summary>
/// Dá acesso à rede neural treinada embutida no executável (o genoma mais recentemente
/// exportado pela tela "Treinamento do Controlador" no momento do build — ver
/// Resources/ControladorTreinado/genoma-controlador.json e o target MSBuild
/// <c>EmbutirGenomaTreinado</c> no .csproj).
/// </summary>
public interface IControladorTreinadoProvider
{
    /// <summary>
    /// A rede carregada, ou <c>null</c> se nenhum genoma válido foi embutido ainda (instalação
    /// nova, nunca treinada) — nesse caso quem consome deve cair para a heurística pura.
    /// </summary>
    RedeNeural? Rede { get; }
}
