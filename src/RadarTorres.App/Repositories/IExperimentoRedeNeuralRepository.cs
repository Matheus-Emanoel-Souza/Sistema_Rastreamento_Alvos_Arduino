using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Resultados reais de baterias de treino+validação comparando versões da rede neural do
/// controlador (ver <see cref="ExperimentoRedeNeural"/>). Análogo a
/// <see cref="IExperimentoHiperparametrosRepository"/>, mas medindo métricas TÁTICAS (abates,
/// escapes, tempo de resposta, cooperação, trocas de alvo) em vez de só a curva de fitness.
/// </summary>
public interface IExperimentoRedeNeuralRepository
{
    void Registrar(ExperimentoRedeNeural experimento);

    /// <summary>Devolve todos os experimentos, em ordem de registro.</summary>
    IReadOnlyList<ExperimentoRedeNeural> ObterTodos();

    /// <summary>Devolve só os experimentos vinculados a uma versão de rede (ver <see cref="RedeNeural.Id"/>).</summary>
    IReadOnlyList<ExperimentoRedeNeural> ObterPorRedeNeural(int redeNeuralId);
}
