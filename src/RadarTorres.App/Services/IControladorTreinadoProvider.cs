using RadarTorres.App.NeuralEvolution;

namespace RadarTorres.App.Services;

/// <summary>
/// Dá acesso à rede neural treinada embutida no executável (a rede mais recentemente exportada
/// pela tela "Treinamento do Controlador" no momento do build — ver
/// Resources/ControladorTreinado/rede-controlador.net e o target MSBuild
/// <c>EmbutirRedeTreinada</c> no .csproj).
/// </summary>
/// <remarks>
/// O arquivo está no formato <c>.net</c> do SharpNEAT (lista de conexões + funções de ativação),
/// e não mais num JSON com arquitetura e vetor de pesos: como o NEAT evolui a TOPOLOGIA, não há
/// arquitetura conhecida de antemão para interpretar um vetor plano.
/// </remarks>
public interface IControladorTreinadoProvider
{
    /// <summary>
    /// A rede carregada, ou <c>null</c> se nenhuma rede válida foi embutida ainda (instalação
    /// nova, nunca treinada) — nesse caso quem consome deve cair para a heurística pura.
    /// </summary>
    RedeNeat? Rede { get; }

    /// <summary>
    /// Quantidade de conexões da rede carregada — a medida de complexidade do NEAT, usada no
    /// diagnóstico exibido ao usuário. <c>0</c> quando <see cref="Rede"/> é <c>null</c>.
    /// </summary>
    int QuantidadeConexoes { get; }
}
