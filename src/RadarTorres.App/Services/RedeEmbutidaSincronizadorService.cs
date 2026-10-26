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

    public async Task<bool> SincronizarGenomaAsync()
    {
        double[]? genoma = _controladorTreinado.Genoma;
        if (genoma is null)
        {
            _logger.Warning("Nenhum controlador treinado embutido no app — nada para sincronizar com o Arduino.");
            return false;
        }

        if (genoma.Length != RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao))
        {
            _logger.Error($"Genoma embutido tem {genoma.Length} genes, esperado {RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao)} — sincronização abortada.");
            return false;
        }

        _logger.Info($"Sincronizando rede embutida com o Arduino ({genoma.Length} pesos)...");

        if (!await _serial.SendCommandAsync(SerialProtocolParser.BuildGenomaReset()).ConfigureAwait(false))
        {
            _logger.Error("Falha ao enviar GENOMA;RESET — sincronização abortada.");
            return false;
        }

        for (int indice = 0; indice < genoma.Length; indice++)
        {
            string comando = SerialProtocolParser.BuildGenomaIndice(indice, genoma[indice]);
            if (!await _serial.SendCommandAsync(comando).ConfigureAwait(false))
            {
                _logger.Error($"Falha ao enviar peso {indice} de {genoma.Length} — sincronização abortada (genoma anterior do Arduino permanece em uso).");
                return false;
            }
        }

        bool commitEnviado = await _serial.SendCommandAsync(SerialProtocolParser.BuildGenomaCommit()).ConfigureAwait(false);
        if (!commitEnviado)
        {
            _logger.Error("Falha ao enviar GENOMA;COMMIT — o Arduino permanece com o genoma anterior (nenhum peso parcial é ativado).");
            return false;
        }

        _logger.Success("Genoma enviado ao Arduino — aguardando ACK;CMD=GENOMA;COMMIT;STATUS=OK no console de eventos para confirmar a ativação.");
        return true;
    }

    public async Task<bool> ValidarEntradasAsync(IReadOnlyList<double> entradas)
    {
        if (_controladorTreinado.Rede is not { } rede)
        {
            _logger.Warning("Nenhum controlador treinado embutido no app — não há o que validar contra o Arduino.");
            return false;
        }

        double[] saidaDoApp = rede.Ativar(entradas.ToArray());
        _logger.Info(
            $"Rede embutida — valor calculado pelo app: ajuste={saidaDoApp[0]:0.000000}, confiança={saidaDoApp[1]:0.000000}. " +
            "Enviando as mesmas entradas ao Arduino para comparação (resposta chega como STATUS;REDE_AJUSTE=...;REDE_CONFIANCA=... no console de eventos).");

        return await _serial.SendCommandAsync(SerialProtocolParser.BuildRedeEntradas(entradas)).ConfigureAwait(false);
    }
}
