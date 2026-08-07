using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Camada de segurança e execução do acionamento demonstrativo (laser de baixa potência /
/// indicador luminoso / simulação — nunca armamento real, conforme escopo do projeto).
/// Concentra a regra de negócio mais sensível do sistema: nenhum comando de acionamento é
/// enviado ao Arduino sem antes passar por <see cref="Authorize"/>.
/// </summary>
public sealed class FireControlService : IFireControlService
{
    private static readonly TimeSpan FiringVisualDuration = TimeSpan.FromSeconds(1.5);

    private readonly ILoggingService _logger;
    private readonly Dispatcher _dispatcher;

    public FireControlService(ILoggingService logger, Dispatcher? dispatcher = null)
    {
        _logger = logger;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public FireAuthorizationResult Authorize(Target target, double minSafetyDistanceMeters)
    {
        if (!target.IsActive)
        {
            return new FireAuthorizationResult(false, "ALVO NÃO ESTÁ MAIS ATIVO");
        }

        if (target.SelectedTower is null)
        {
            return new FireAuthorizationResult(false, "NENHUMA TORRE SELECIONADA PARA ESTE ALVO");
        }

        if (target.Distance < minSafetyDistanceMeters)
        {
            return new FireAuthorizationResult(false, "ACIONAMENTO BLOQUEADO — ALVO DENTRO DA DISTÂNCIA MÍNIMA DE SEGURANÇA");
        }

        return new FireAuthorizationResult(true, "AUTORIZADO");
    }

    public async Task<bool> TryFireAsync(Target target, ISerialCommunicationService? serialService, bool simulationMode, double minSafetyDistanceMeters)
    {
        FireAuthorizationResult authorization = Authorize(target, minSafetyDistanceMeters);

        if (!authorization.Authorized)
        {
            _logger.Warning(authorization.Reason);
            return false;
        }

        Tower tower = target.SelectedTower!;
        tower.State = TowerState.Firing;

        string command = SerialProtocolParser.BuildFire(tower.Id, target.Id);

        if (simulationMode || serialService is null)
        {
            _logger.Info("Comando demonstrativo enviado (modo simulação)");
            _logger.Success("Confirmação simulada do Arduino recebida");
        }
        else
        {
            _logger.Info("Comando demonstrativo enviado");
            bool sent = await serialService.SendCommandAsync(command).ConfigureAwait(false);
            if (!sent)
            {
                tower.State = TowerState.Selected;
                return false;
            }
            // A confirmação real (ACK) chega de forma assíncrona via MessageReceived e é
            // tratada pela MainViewModel; aqui apenas registramos o envio do comando.
        }

        // Mantém o feedback visual de "disparando" por um curto período antes de voltar a "selecionada".
        _ = ResetFiringStateAfterDelay(tower);

        return true;
    }

    private async Task ResetFiringStateAfterDelay(Tower tower)
    {
        await Task.Delay(FiringVisualDuration).ConfigureAwait(false);
        RunOnDispatcher(() =>
        {
            if (tower.State == TowerState.Firing)
            {
                tower.State = TowerState.Selected;
            }
        });
    }

    private void RunOnDispatcher(Action action)
    {
        if (_dispatcher.CheckAccess()) action();
        else _dispatcher.Invoke(action);
    }
}
