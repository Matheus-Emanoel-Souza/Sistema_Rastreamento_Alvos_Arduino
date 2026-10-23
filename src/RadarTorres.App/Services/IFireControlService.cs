using System.Threading.Tasks;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>Resultado da checagem de segurança antes de um acionamento demonstrativo.</summary>
public sealed record FireAuthorizationResult(bool Authorized, string Reason);

public interface IFireControlService
{
    /// <summary>
    /// Verifica se o acionamento demonstrativo de <paramref name="tower"/> contra o alvo é
    /// permitido. Critérios (nesta ordem): alvo ativo, modo do sistema é Disparar, alvo fora de
    /// zona morta, certeza da câmera (<see cref="Target.Confidence"/>) acima do limiar
    /// configurado, e <paramref name="tower"/> entre as torres autorizadas a engajar este alvo
    /// (<see cref="Target.EngagingTowers"/> — já exclui quem tem outro canhão na frente do
    /// tiro). A distância até o alvo NÃO é critério: fora de zona morta, qualquer distância é
    /// autorizada.
    /// </summary>
    FireAuthorizationResult Authorize(Target target, Tower tower, SystemMode currentMode);

    /// <summary>
    /// Executa (ou simula) o acionamento demonstrativo de <paramref name="tower"/>: valida
    /// segurança, envia o comando <c>FIRE;TOWER=x;TARGET=y</c> pela serial (ou apenas registra em
    /// modo de simulação) e atualiza o estado visual da torre para "Firing" temporariamente. Só
    /// grava em <c>acoes_realizadas</c> (Requisito 5) quando o comando é efetivamente enviado com
    /// sucesso — tentativas bloqueadas ou com erro de envio só geram log
    /// (<see cref="ILoggingService"/>), sem linha na tabela (schema acadêmico não tem coluna
    /// de resultado para distingui-las).
    /// </summary>
    Task<bool> TryFireAsync(Target target, Tower tower, ISerialCommunicationService? serialService, bool simulationMode, SystemMode currentMode);
}
