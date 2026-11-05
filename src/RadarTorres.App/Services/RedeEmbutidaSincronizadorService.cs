using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RadarTorres.App.NeuralEvolution;

namespace RadarTorres.App.Services;

/// <inheritdoc cref="IRedeEmbutidaSincronizadorService"/>
public sealed class RedeEmbutidaSincronizadorService : IRedeEmbutidaSincronizadorService
{
    private readonly ISerialCommunicationService _serial;
    private readonly IControladorTreinadoProvider _controladorTreinado;
    private readonly ILoggingService _logger;

    public RedeEmbutidaSincronizadorService(
        ISerialCommunicationService serial,
        IControladorTreinadoProvider controladorTreinado,
        ILoggingService logger)
    {
        _serial = serial;
        _controladorTreinado = controladorTreinado;
        _logger = logger;
    }

    /// <summary>
    /// <para>
    /// INDISPONÍVEL desde a migração para o SharpNEAT. O firmware do Arduino
    /// (<c>RadarTorres_Firmware.ino</c>) embute uma rede de topologia FIXA de 74 genes
    /// (<c>REDE_QTD_GENES</c>), e o protocolo <c>GENOMA;INDICE=n;VALOR=x</c> transmite
    /// exatamente esse vetor plano, posição a posição.
    /// </para>
    /// <para>
    /// O NEAT evolui a topologia junto com os pesos: a rede treinada tem um número de neurônios
    /// e um conjunto de conexões que mudam de execução para execução, e não existe vetor de 74
    /// posições que a represente. Enviar os pesos "na ordem" produziria uma rede diferente da
    /// treinada no Arduino — pior que não sincronizar, porque passaria despercebido.
    /// </para>
    /// <para>
    /// Para reativar isto é preciso ou (a) estender o protocolo e o firmware para receber a
    /// lista de conexões (origem, destino, peso) de uma topologia arbitrária, ou (b) manter o
    /// Arduino rodando só a heurística, que é o comportamento atual enquanto nada for
    /// sincronizado.
    /// </para>
    /// </summary>
    public Task<bool> SincronizarGenomaAsync()
    {
        _logger.Error(
            "Sincronização de genoma com o Arduino indisponível: o firmware espera um vetor fixo de 74 pesos " +
            "(rede 7-8-2), e a rede treinada pelo SharpNEAT tem topologia variável — não há vetor equivalente. " +
            "O Arduino continua operando com a rede/heurística já gravada nele.");

        return Task.FromResult(false);
    }

    public async Task<bool> ValidarEntradasAsync(IReadOnlyList<double> entradas)
    {
        if (_controladorTreinado.Rede is not { } rede)
        {
            _logger.Warning("Nenhum controlador treinado embutido no app — não há o que validar contra o Arduino.");
            return false;
        }

        double[] saidaDoApp = rede.Ativar(entradas.ToArray());
        _logger.Warning(
            "Atenção: o app usa a rede NEAT treinada e o Arduino usa a rede fixa de 74 pesos gravada no firmware — " +
            "desde a migração para o SharpNEAT as duas NÃO são mais a mesma rede (ver SincronizarGenomaAsync), " +
            "então divergência nos valores abaixo é esperada e não indica defeito.");
        _logger.Info(
            $"Rede embutida — valor calculado pelo app: ajuste={saidaDoApp[0]:0.000000}, confiança={saidaDoApp[1]:0.000000}. " +
            "Enviando as mesmas entradas ao Arduino para comparação (resposta chega como STATUS;REDE_AJUSTE=...;REDE_CONFIANCA=... no console de eventos).");

        return await _serial.SendCommandAsync(SerialProtocolParser.BuildRedeEntradas(entradas)).ConfigureAwait(false);
    }
}
