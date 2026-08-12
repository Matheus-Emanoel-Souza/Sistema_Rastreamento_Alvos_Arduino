using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;

namespace RadarTorres.App.Services;

/// <summary>
/// Camada de segurança e execução do acionamento demonstrativo (laser de baixa potência /
/// indicador luminoso / simulação — nunca armamento real, conforme escopo do projeto).
/// Concentra a regra de negócio mais sensível do sistema: nenhum comando de acionamento é
/// enviado ao Arduino sem antes passar por <see cref="Authorize"/>.
/// </summary>
/// <remarks>
/// Também é o único ponto do sistema por onde toda tentativa de acionamento passa — por isso
/// é aqui, e não na ViewModel, que cada tentativa (autorizada/bloqueada, executada ou com
/// erro) é gravada no histórico de auditoria <c>acoes_realizadas</c> (Requisito 5).
/// </remarks>
public sealed class FireControlService : IFireControlService
{
    private static readonly TimeSpan FiringVisualDuration = TimeSpan.FromSeconds(1.5);

    private readonly ILoggingService _logger;
    private readonly IAcaoRealizadaRepository _acaoRepository;
    private readonly IAuthService _authService;
    private readonly Dispatcher _dispatcher;

    public FireControlService(
        ILoggingService logger,
        IAcaoRealizadaRepository acaoRepository,
        IAuthService authService,
        Dispatcher? dispatcher = null)
    {
        _logger = logger;
        _acaoRepository = acaoRepository;
        _authService = authService;
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

    public async Task<bool> TryFireAsync(Target target, ISerialCommunicationService? serialService, bool simulationMode, double minSafetyDistanceMeters, OrigemAcao origem)
    {
        FireAuthorizationResult authorization = Authorize(target, minSafetyDistanceMeters);

        if (!authorization.Authorized)
        {
            _logger.Warning(authorization.Reason);
            RegistrarAcao(target, origem, ResultadoAcao.Cancelada, authorization.Reason);
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
                RegistrarAcao(target, origem, ResultadoAcao.Erro, "Falha ao enviar comando pela porta serial.");
                return false;
            }
            // A confirmação real (ACK) chega de forma assíncrona via MessageReceived e é
            // tratada pela MainViewModel; aqui apenas registramos o envio do comando.
        }

        RegistrarAcao(target, origem, ResultadoAcao.Executada, null);

        // Mantém o feedback visual de "disparando" por um curto período antes de voltar a "selecionada".
        _ = ResetFiringStateAfterDelay(tower);

        return true;
    }

    private void RegistrarAcao(Target target, OrigemAcao origem, ResultadoAcao resultado, string? observacao)
    {
        try
        {
            _acaoRepository.Add(new AcaoRealizada
            {
                Dispositivo = target.SelectedTower?.Name ?? "—",
                TipoAcao = "Acionamento demonstrativo",
                X = target.X,
                Y = target.Y,
                Z = null,
                DataHora = DateTime.Now,
                UsuarioResponsavel = origem == OrigemAcao.Manual ? _authService.CurrentUser?.Login : null,
                Origem = origem,
                Resultado = resultado,
                Observacao = observacao
            });
        }
        catch (Exception ex)
        {
            // Falha ao gravar auditoria não deve derrubar o acionamento em si — só registra no console.
            _logger.Error($"Não foi possível gravar o registro de auditoria da ação: {ex.Message}");
        }
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
