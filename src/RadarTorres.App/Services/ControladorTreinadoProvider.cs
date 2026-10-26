using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using RadarTorres.App.NeuralEvolution;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="IControladorTreinadoProvider"/>: lê o JSON embutido como
/// <c>EmbeddedResource</c> (mesmo padrão de <c>DashboardLayoutRepository.LoadSystemDefault</c>
/// para o layout padrão) e monta a <see cref="RedeNeural"/> a partir dele. Carregado uma única
/// vez — a rede não muda em tempo de execução, só numa próxima build/instalação.
/// </summary>
public sealed class ControladorTreinadoProvider : IControladorTreinadoProvider
{
    private const string LogicalName = "RadarTorres.App.Resources.ControladorTreinado.genoma-controlador.json";

    public RedeNeural? Rede { get; }
    public double[]? Genoma { get; }

    public ControladorTreinadoProvider(ILoggingService logger)
    {
        (Rede, Genoma) = Carregar(logger);
    }

    private static (RedeNeural? Rede, double[]? Genoma) Carregar(ILoggingService logger)
    {
        try
        {
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(LogicalName);
            if (stream is null)
            {
                logger.Warning("Nenhum controlador treinado embutido encontrado — usando heurística pura.");
                return (null, null);
            }

            GenomaExportado? payload = JsonSerializer.Deserialize<GenomaExportado>(stream);
            if (payload is null || payload.Arquitetura.Length == 0 || payload.Genoma.Length == 0)
            {
                logger.Warning("Controlador treinado embutido está vazio ou corrompido — usando heurística pura.");
                return (null, null);
            }

            var rede = new RedeNeural(payload.Arquitetura, payload.Genoma);
            logger.Info($"Controlador treinado carregado (geração {payload.Geracao}, melhor fitness {payload.MelhorFitness:0.0}).");
            return (rede, payload.Genoma);
        }
        catch (Exception ex)
        {
            logger.Warning($"Não foi possível carregar o controlador treinado embutido: {ex.Message}. Usando heurística pura.");
            return (null, null);
        }
    }

    /// <summary>Mesmo formato gravado por <c>TreinamentoControladorViewModel.ExportarMelhorGenoma</c>.</summary>
    private sealed class GenomaExportado
    {
        public int[] Arquitetura { get; set; } = [];
        public double[] Genoma { get; set; } = [];
        public int Geracao { get; set; }
        public double MelhorFitness { get; set; }
    }
}
