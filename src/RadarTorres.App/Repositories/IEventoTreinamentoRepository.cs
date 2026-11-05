using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Histórico de ACONTECIMENTOS da tela "Treinamento do Controlador" — início, parada, falha, novo
/// recorde de fitness, troca de fase da regulação de complexidade, marco gravado, rede exportada.
/// Buffer circular limitado (ver implementação SQLite): ao ultrapassar o limite, o registro mais
/// antigo é descartado primeiro.
/// </summary>
/// <remarks>
/// Complementa <see cref="IHistoricoTreinamentoRepository"/> em vez de duplicá-lo: aquele guarda
/// os NÚMEROS de cada geração (fitness, abates, complexidade, espécies); este guarda o que
/// aconteceu e quando. Um treinamento que roda a noite inteira e morre por exceção deixa, no
/// primeiro, uma série que simplesmente para — e só aqui a razão da parada sobrevive ao
/// fechamento do aplicativo.
/// </remarks>
public interface IEventoTreinamentoRepository
{
    /// <summary>Grava um novo evento e, se o total ultrapassar o limite, descarta o(s) mais antigo(s).</summary>
    void Registrar(EventoTreinamento evento);

    /// <summary>Devolve os eventos mais recentes, do MAIS NOVO para o mais antigo — a mesma ordem em que a tela os exibe.</summary>
    IReadOnlyList<EventoTreinamento> ObterUltimos(int quantidade = 200);

    /// <summary>Remove todos os eventos — usado no reset manual do treinamento (tela Configurações).</summary>
    void LimparTudo();
}
