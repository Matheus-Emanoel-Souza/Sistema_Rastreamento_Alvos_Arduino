using System.Threading.Tasks;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>Resultado da checagem de segurança antes de um acionamento demonstrativo.</summary>
public sealed record FireAuthorizationResult(bool Authorized, string Reason);

public interface IFireControlService
{
    /// <summary>
    /// Verifica se o acionamento demonstrativo do alvo é permitido, aplicando a regra de
    /// segurança: se <c>distânciaAlvo &lt; distânciaMínima</c>, o acionamento NÃO é autorizado.
    /// </summary>
    FireAuthorizationResult Authorize(Target target, double minSafetyDistanceMeters);

    /// <summary>
    /// Executa (ou simula) o acionamento demonstrativo: valida segurança, envia o comando
    /// <c>FIRE;TOWER=x;TARGET=y</c> pela serial (ou apenas registra em modo de simulação) e
    /// atualiza o estado visual da torre para "Firing" temporariamente.
    /// </summary>
    /// <param name="origem">
    /// Se a ação foi disparada manualmente por um usuário ou automaticamente pelo sistema —
    /// gravado no histórico de auditoria (<c>acoes_realizadas</c>, Requisito 5).
    /// </param>
    /// <param name="usuarioResponsavel">
    /// Login de quem solicitou (só faz sentido quando <paramref name="origem"/> é
    /// <see cref="OrigemAcao.Manual"/>). Recebido como parâmetro em vez de lido de
    /// <c>IAuthService.CurrentUser</c> aqui dentro porque este serviço é Singleton (uma única
    /// conexão física com o Arduino, compartilhada por todas as janelas/sessões) enquanto a
    /// sessão de login é por janela — quem sabe qual usuário disparou é o chamador (<c>MainViewModel</c>,
    /// que é Scoped por janela).
    /// </param>
    Task<bool> TryFireAsync(Target target, ISerialCommunicationService? serialService, bool simulationMode, double minSafetyDistanceMeters, OrigemAcao origem, string? usuarioResponsavel = null);
}
