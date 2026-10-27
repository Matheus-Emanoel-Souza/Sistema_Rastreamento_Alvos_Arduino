using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Histórico de comportamento por geração da tela "Treinamento do Controlador". Buffer
/// circular limitado (ver implementação SQLite): ao ultrapassar o limite, o registro mais
/// antigo é descartado primeiro.
/// </summary>
public interface IHistoricoTreinamentoRepository
{
    /// <summary>Grava um novo registro e, se o total ultrapassar o limite, descarta o(s) mais antigo(s).</summary>
    void Registrar(HistoricoGeracaoTreinamento registro);

    /// <summary>Devolve os registros mais recentes, em ordem cronológica (mais antigo primeiro).</summary>
    IReadOnlyList<HistoricoGeracaoTreinamento> ObterUltimos(int quantidade = 300);

    /// <summary>Remove todos os registros — usado no reset manual do treinamento (tela Configurações).</summary>
    void LimparTudo();
}
