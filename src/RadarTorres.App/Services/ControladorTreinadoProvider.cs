using System;
using System.IO;
using System.Reflection;
using RadarTorres.App.NeuralEvolution;
using SharpNeat;
using SharpNeat.IO;
using SharpNeat.IO.Models;
using SharpNeat.NeuralNets.IO;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="IControladorTreinadoProvider"/>: lê o arquivo <c>.net</c>
/// embutido como <c>EmbeddedResource</c> (mesmo padrão de
/// <c>DashboardLayoutRepository.LoadSystemDefault</c> para o layout padrão) e monta a
/// <see cref="RedeNeat"/> a partir dele, usando o leitor do próprio SharpNEAT. Carregado uma
/// única vez — a rede não muda em tempo de execução, só numa próxima build/instalação.
/// </summary>
public sealed class ControladorTreinadoProvider : IControladorTreinadoProvider
{
    private const string LogicalName = "RadarTorres.App.Resources.ControladorTreinado.rede-controlador.net";

    public RedeNeat? Rede { get; }
    public int QuantidadeConexoes { get; }

    public ControladorTreinadoProvider(ILoggingService logger)
    {
        (Rede, QuantidadeConexoes) = Carregar(logger);
    }

    private static (RedeNeat? Rede, int QuantidadeConexoes) Carregar(ILoggingService logger)
    {
        try
        {
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(LogicalName);
            if (stream is null)
            {
                logger.Warning("Nenhum controlador treinado embutido encontrado — usando heurística pura.");
                return (null, 0);
            }

            NetFileModel modelo = NetFile.Load(stream);

            if (modelo.Connections.Count == 0)
            {
                logger.Warning("Controlador treinado embutido não tem nenhuma conexão — usando heurística pura.");
                return (null, 0);
            }

            // Rede treinada com uma contagem de entradas diferente da atual (ex.: embutida antes
            // de ControladorHibrido.QuantidadeEntradas mudar) não é compatível — ConstruirEntradas
            // do sistema real sempre produz o número ATUAL de entradas, então ativar uma rede
            // antiga com esse vetor lançaria exceção na primeira seleção de torre. Cai para
            // heurística pura em vez de travar o app; a rede precisa ser retreinada.
            if (modelo.InputCount != RedeNeat.QuantidadeEntradasComBias || modelo.OutputCount != ControladorHibrido.QuantidadeSaidas)
            {
                logger.Warning(
                    $"Controlador treinado embutido tem {modelo.InputCount} entradas e {modelo.OutputCount} saídas, mas a versão atual usa {RedeNeat.QuantidadeEntradasComBias} e {ControladorHibrido.QuantidadeSaidas} — incompatível. Usando heurística pura até um novo treinamento ser embutido.");
                return (null, 0);
            }

            IBlackBox<double> caixaPreta = NeuralNetConverter.ToNeuralNet(
                modelo,
                enableHardwareAcceleratedNeuralNets: false,
                enableHardwareAcceleratedActivationFunctions: false);

            var rede = new RedeNeat(caixaPreta);
            logger.Info($"Controlador treinado carregado (rede NEAT com {modelo.Connections.Count} conexões, {(modelo.IsAcyclic ? "acíclica" : "cíclica")}).");
            return (rede, modelo.Connections.Count);
        }
        catch (Exception ex)
        {
            logger.Warning($"Não foi possível carregar o controlador treinado embutido: {ex.Message}. Usando heurística pura.");
            return (null, 0);
        }
    }
}
