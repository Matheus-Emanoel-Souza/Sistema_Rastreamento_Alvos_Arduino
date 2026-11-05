using System.Collections.Generic;
using System.Threading.Tasks;

namespace RadarTorres.App.Services;

/// <summary>
/// Envia o genoma da rede neural treinada ao Arduino (comandos <c>GENOMA;*</c>, ver
/// <see cref="SerialProtocolParser"/> — desativado, ver <see cref="SincronizarGenomaAsync"/>) e
/// pede validação de entradas (<c>REDE;ENTRADAS=</c>) —
/// SOMENTE diagnóstico/auditoria. Nunca participa da seleção de torre nem da autorização de
/// disparo, que continuam decididas inteiramente pelo app (ver
/// <c>Rede_Neural/Arquitetura/CONTROLADOR_NEUROEVOLUTIVO.md</c>, Parte 3).
/// </summary>
public interface IRedeEmbutidaSincronizadorService
{
    /// <summary>
    /// INDISPONÍVEL desde a migração para o SharpNEAT: sempre registra o motivo no log e devolve
    /// <c>false</c>. O protocolo <c>GENOMA;*</c> transmite um vetor fixo de 74 pesos, que
    /// pressupõe a rede 7-8-2 gravada no firmware; uma rede NEAT tem topologia variável e não
    /// pode ser representada assim. Ver a implementação para o que seria preciso para reativar.
    /// </summary>
    Task<bool> SincronizarGenomaAsync();

    /// <summary>
    /// Pede ao Arduino que calcule, de forma independente, a saída da rede embutida para estas 6
    /// entradas, e registra no log o valor calculado pelo próprio app logo antes de enviar — a
    /// resposta do Arduino (<see cref="RedeSaidaMessage"/>) chega depois, de forma assíncrona, pelo
    /// mesmo evento <see cref="ISerialCommunicationService.MessageReceived"/> já usado por todo o
    /// resto do protocolo, e é logada por quem a recebe (ver <c>MainViewModel</c>) para comparação
    /// manual com o valor já logado aqui.
    /// </summary>
    Task<bool> ValidarEntradasAsync(IReadOnlyList<double> entradas);
}
