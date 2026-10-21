using System;
using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Histórico de ocorrências de detecção. A inserção continua sendo a operação principal — cada
/// ocorrência é um fato —, mas a ocorrência tem ciclo de vida: avança
/// <see cref="ObjetoDetectado.DataHoraUltimaDeteccao"/> enquanto o alvo segue sendo acompanhado e
/// muda de <see cref="EstadoOcorrencia"/> por eventos explícitos, cada mudança registrada no
/// histórico de transições. Nada aqui apaga ou reescreve o fato original.
/// </summary>
public interface IObjetoDetectadoRepository
{
    IReadOnlyList<ObjetoDetectado> GetAll();

    ObjetoDetectado? GetById(int id);

    ObjetoDetectado Add(ObjetoDetectado objeto);

    /// <summary>
    /// Insere a ocorrência apenas se ainda não existir uma com o mesmo
    /// (<see cref="ObjetoDetectado.RastreamentoId"/>, <see cref="ObjetoDetectado.Quadrante"/>);
    /// caso exista, devolve a que já está gravada sem inserir nada.
    /// </summary>
    /// <remarks>
    /// É a defesa final contra duplicidade: a decisão de "é a mesma ocorrência" é tomada antes,
    /// pelo rastreamento, mas as câmeras processam em paralelo (uma thread por câmera) e duas
    /// podem decidir criar a mesma ocorrência ao mesmo tempo. Resolver isso no banco, com índice
    /// único + inserção condicional, é o que torna o resultado independente de ordem de chegada.
    /// </remarks>
    ObjetoDetectado AdicionarSeNaoExistir(ObjetoDetectado objeto);

    /// <summary>Marca que a mesma ocorrência continua sendo detectada agora (sem criar registro novo).</summary>
    void RegistrarDeteccaoContinuada(int id, DateTime quando);

    /// <summary>
    /// Muda o estado da ocorrência e registra a transição no histórico. Devolve <c>false</c> se a
    /// ocorrência não existe ou se já estava neste estado.
    /// </summary>
    bool AtualizarEstado(int id, EstadoOcorrencia estado, string? usuario, string? observacao);

    /// <summary>Transições de estado de uma ocorrência, da mais antiga para a mais recente.</summary>
    IReadOnlyList<OcorrenciaEstadoTransicao> ObterHistoricoEstados(int objetoDetectadoId);
}
