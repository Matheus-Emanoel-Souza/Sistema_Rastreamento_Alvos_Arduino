using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Resultados reais da análise experimental de hiperparâmetros (tamanho de população × neurônios
/// na camada oculta) da tela de Treinamento do Controlador. Sem limite/buffer circular — ao
/// contrário de <see cref="IHistoricoTreinamentoRepository"/>, aqui cada linha é um experimento
/// completo (não uma geração), poucos registros no total, todos relevantes para a comparação.
/// </summary>
public interface IExperimentoHiperparametrosRepository
{
    /// <summary>Grava o resultado de um experimento concluído.</summary>
    void Registrar(ExperimentoHiperparametros experimento);

    /// <summary>Devolve todos os experimentos registrados, em ordem cronológica (mais antigo primeiro).</summary>
    IReadOnlyList<ExperimentoHiperparametros> ObterTodos();

    /// <summary>Apaga um experimento pelo Id — usado para descartar uma execução interrompida/inválida sem zerar a tabela inteira.</summary>
    void Remover(int id);
}
