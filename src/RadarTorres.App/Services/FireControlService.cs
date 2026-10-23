using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using RadarTorres.App.Helpers;
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
/// é aqui, e não na ViewModel, que uma tentativa efetivamente executada é gravada no histórico
/// de auditoria <c>acoes_realizadas</c> (Requisito 5); tentativas bloqueadas ou com erro de
/// envio só geram log.
/// </remarks>
public sealed class FireControlService : IFireControlService
{
    private static readonly TimeSpan FiringVisualDuration = TimeSpan.FromSeconds(1.5);

    private readonly ILoggingService _logger;
    private readonly IAcaoRealizadaRepository _acaoRepository;
    private readonly IZonaMortaService _zonaMortaService;
    private readonly IOcorrenciaDeteccaoService _ocorrenciaService;
    private readonly IConfiguracoesAplicativoRepository _configuracoes;
    private readonly Dispatcher _dispatcher;

    public FireControlService(
        ILoggingService logger,
        IAcaoRealizadaRepository acaoRepository,
        IZonaMortaService zonaMortaService,
        IOcorrenciaDeteccaoService ocorrenciaService,
        IConfiguracoesAplicativoRepository configuracoes,
        Dispatcher? dispatcher = null)
    {
        _logger = logger;
        _acaoRepository = acaoRepository;
        _zonaMortaService = zonaMortaService;
        _ocorrenciaService = ocorrenciaService;
        _configuracoes = configuracoes;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public FireAuthorizationResult Authorize(Target target, Tower tower, SystemMode currentMode)
    {
        if (!target.IsActive)
        {
            return new FireAuthorizationResult(false, "ALVO NÃO ESTÁ MAIS ATIVO");
        }

        if (currentMode != SystemMode.Disparar)
        {
            return new FireAuthorizationResult(false, "ACIONAMENTO BLOQUEADO — MODO DO SISTEMA NÃO PERMITE DISPARO");
        }

        // Checado independente da torre: mesmo que uma torre já tivesse sido escolhida antes de
        // a zona ser criada/ativada, nenhum acionamento (manual ou automático) sai enquanto o
        // alvo estiver dentro de uma zona morta ativa — único critério ligado à posição do
        // alvo. A distância até o alvo NÃO bloqueia disparo.
        ZonaMorta? blockingZone = _zonaMortaService.FindBlockingZone(target);
        if (blockingZone is not null)
        {
            return new FireAuthorizationResult(false, $"ACIONAMENTO BLOQUEADO — ALVO EM ZONA MORTA ({blockingZone.Name} — {blockingZone.Description})");
        }

        double limiarPercentual = _configuracoes.Atual.LimiarConfiancaDisparoPercentual;
        double confiancaPercentual = target.Confidence * 100;
        if (confiancaPercentual < limiarPercentual)
        {
            return new FireAuthorizationResult(false, $"ACIONAMENTO BLOQUEADO — CERTEZA INSUFICIENTE ({confiancaPercentual:0}% < {limiarPercentual:0}%)");
        }

        if (!target.EngagingTowers.Any(t => t.Id == tower.Id))
        {
            return new FireAuthorizationResult(false, $"ACIONAMENTO BLOQUEADO — {tower.Name} SEM LINHA DE TIRO LIVRE PARA ESTE ALVO (OUTRO CANHÃO NA FRENTE OU FORA DE ALCANCE DE CÂMERA)");
        }

        return new FireAuthorizationResult(true, "AUTORIZADO");
    }

    public async Task<bool> TryFireAsync(Target target, Tower tower, ISerialCommunicationService? serialService, bool simulationMode, SystemMode currentMode)
    {
        FireAuthorizationResult authorization = Authorize(target, tower, currentMode);

        if (!authorization.Authorized)
        {
            _logger.Warning(authorization.Reason);
            return false;
        }

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
                _logger.Error("Falha ao enviar comando pela porta serial.");
                return false;
            }
            // A confirmação real (ACK) chega de forma assíncrona via MessageReceived e é
            // tratada pela MainViewModel; aqui apenas registramos o envio do comando.
        }

        // Comando sai de qualquer forma (é o disparo demonstrativo em si); se ACERTA o alvo é
        // probabilístico — alvo mais rápido tem mais chance de escapar de UM canhão, por isso
        // TowerSelectionService já autoriza mais de uma torre no mesmo alvo quando a velocidade
        // dele justifica ajuda (ver Target.EngagingTowers).
        double probabilidadeDeErro = CalculadoraErroDisparo.ProbabilidadeDeErro(target.SpeedMetersPerSecond);
        bool acertou = Random.Shared.NextDouble() >= probabilidadeDeErro;

        RegistrarAcao(tower, target.ObjetoDetectadoId, acertou, target.SpeedMetersPerSecond);

        if (acertou)
        {
            MarcarOcorrenciaPendenteDeAbate(target);
        }
        else
        {
            _logger.Warning($"{tower.Name} ERROU o disparo contra o alvo #{target.Id:D2} (velocidade {target.SpeedMetersPerSecond:0.00} m/s, chance de erro {probabilidadeDeErro:P0}).");
        }

        // Mantém o feedback visual de "disparando" por um curto período antes de voltar a "selecionada".
        _ = ResetFiringStateAfterDelay(tower);

        return true;
    }

    /// <summary>
    /// Acionamento efetivado é o evento confiável que coloca a ocorrência do alvo em
    /// <see cref="EstadoOcorrencia.AbatePendente"/> — o abate em si só se confirma por decisão de
    /// um operador na tela "Objetos Detectados". Esta é a ÚNICA porta de entrada automática para
    /// o ciclo de abate: desaparecer da imagem nunca abate nada.
    /// </summary>
    private void MarcarOcorrenciaPendenteDeAbate(Target target)
    {
        if (target.ObjetoDetectadoId is not { } ocorrenciaId) return;

        bool marcada = _ocorrenciaService.MarcarAbatePendente(
            ocorrenciaId,
            usuario: null,
            observacao: $"Acionamento demonstrativo executado contra o alvo #{target.Id:D2}. Aguardando confirmação de abate pelo operador.");

        if (marcada)
        {
            _logger.Warning($"Ocorrência #{ocorrenciaId} aguardando confirmação de abate na tela \"Objetos Detectados\".");
        }
    }

    private void RegistrarAcao(Tower tower, int? objetoDetectadoId, bool acertou, double velocidadeAlvoMetrosPorSegundo)
    {
        try
        {
            _acaoRepository.Add(new AcaoRealizada
            {
                TorreId = tower.Id,
                ObjetoDetectadoId = objetoDetectadoId,
                DataHora = DateTime.Now,
                Observacao = acertou
                    ? $"Acerto (velocidade do alvo: {velocidadeAlvoMetrosPorSegundo:0.00} m/s)."
                    : $"Erro (velocidade do alvo: {velocidadeAlvoMetrosPorSegundo:0.00} m/s).",
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
