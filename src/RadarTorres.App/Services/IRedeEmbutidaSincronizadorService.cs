using System.Collections.Generic;
using System.Threading.Tasks;

namespace RadarTorres.App.Services;

/// <summary>
/// Envia o genoma da rede neural treinada ao Arduino (comandos <c>GENOMA;*</c>, ver
/// <see cref="SerialProtocolParser"/>) e pede validação de entradas (<c>REDE;ENTRADAS=</c>) —
/// SOMENTE diagnóstico/auditoria. Nunca participa da seleção de torre nem da autorização de
/// disparo, que continuam decididas inteiramente pelo app (ver
/// <c>Docs/Documentos_Entregaveis/Artigo/Como_a_IA_funciona_no_RadarTorres.MD</c>, Parte 3).
/// </summary>
public interface IRedeEmbutidaSincronizadorService
{
    /// <summary>
    /// Envia os 74 pesos do genoma atualmente embutido no app (um por comando, com
    /// <c>GENOMA;RESET</c> antes e <c>GENOMA;COMMIT</c> depois). Não faz nada (e avisa no log) se
    /// não houver controlador treinado embutido (<see cref="IControladorTreinadoProvider.Genoma"/>
    /// nulo).
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
