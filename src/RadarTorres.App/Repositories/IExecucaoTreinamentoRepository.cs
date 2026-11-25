using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>Persistência das execuções de treinamento (TABEXE) — a entidade pai das tabelas de neuroevolução.</summary>
public interface IExecucaoTreinamentoRepository
{
    /// <summary>Abre uma execução (DataHoraFim NULL, encerramento EmAndamento). Chamado no "Iniciar".</summary>
    void Abrir(ExecucaoTreinamento execucao);

    /// <summary>
    /// Fecha a execução identificada por <paramref name="execucaoId"/>, gravando o instante do
    /// fim, o total de gerações, o melhor fitness e o motivo. Não faz nada se a execução não
    /// existir (execução iniciada por uma versão anterior do aplicativo, por exemplo).
    /// </summary>
    void Fechar(string execucaoId, int geracoesConcluidas, double? melhorFitness, EncerramentoExecucaoTreinamento encerramento);

    /// <summary>Execução em andamento (sem DataHoraFim), a mais recente; NULL quando não há nenhuma.</summary>
    ExecucaoTreinamento? ObterEmAndamento();

    IReadOnlyList<ExecucaoTreinamento> ObterUltimas(int quantidade = 50);

    /// <summary>
    /// Marca como <see cref="EncerramentoExecucaoTreinamento.Interrompida"/> toda execução que
    /// ficou aberta de uma execução ANTERIOR do aplicativo. Chamado uma vez na inicialização:
    /// sem isto, uma queda com o treinamento rodando deixaria a linha "em andamento" para sempre
    /// e o espelho seguinte a atribuiria dados de uma execução morta.
    /// </summary>
    int FecharPendentesDeExecucaoAnterior();
}
