using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Registro de auditoria de um acionamento demonstrativo efetivamente executado. Gravado por
/// <see cref="Services.FireControlService"/> só quando o comando chega a ser enviado com
/// sucesso (tentativas bloqueadas/com erro só geram log, não linha aqui — ver
/// <see cref="Repositories.IAcaoRealizadaRepository"/>, que propositalmente não expõe
/// Update/Delete). Espelha a tabela acadêmica <c>acoes_realizadas</c> (ID_ACAO, ID_TORRE,
/// ID_OBJETO, DH_ACAO, OBSERVACAO) descrita em
/// <c>Docs/Documentos_Entregaveis/Banco_de_Dados/schema.sql</c>.
/// </summary>
public class AcaoRealizada
{
    public int Id { get; set; }

    /// <summary>Id da torre que executou a ação (ver <see cref="Tower.Id"/>).</summary>
    public int TorreId { get; set; }

    /// <summary>
    /// Referência ao <see cref="ObjetoDetectado"/> que originou esta ação — é a chave para as
    /// coordenadas do alvo (X/Y/Z), que não são duplicadas aqui; quem precisa da posição
    /// consulta <see cref="Repositories.IObjetoDetectadoRepository"/> por este Id (ver
    /// <see cref="ViewModels.AcoesRealizadasViewModel"/>, que já resolve isso para exibição).
    /// <c>null</c> se o alvo não tinha objeto detectado persistido no momento da ação.
    /// </summary>
    public int? ObjetoDetectadoId { get; set; }

    public DateTime DataHora { get; set; }

    public string? Observacao { get; set; }
}
