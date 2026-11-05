using System;
using System.Collections.Generic;
using SharpNeat.IO.Models;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// Calcula a ativação de CADA nó de uma rede NEAT — inclusive os ocultos — propagando o grafo do
/// campeão à mão, nó por nó.
///
/// Existe por um motivo específico: a caixa-preta do SharpNEAT (<see cref="SharpNeat.IBlackBox{T}"/>,
/// adaptada em <see cref="RedeNeat"/>) expõe apenas entradas e saídas, então a migração para o
/// framework fez o diagrama da tela de treinamento perder a cor por ativação dos neurônios
/// ocultos que a rede própria anterior mostrava. Em vez de estimar um número que o framework não
/// fornece, aqui a propagação é REFEITA sobre a mesma topologia e os mesmos pesos: o valor de cada
/// oculto é o valor real que ele assume naquela ativação.
/// </summary>
/// <remarks>
/// Só funciona em rede ACÍCLICA com ativação TanH — exatamente a configuração usada no projeto
/// (ver <see cref="TreinadorNeat.Aciclica"/> e <see cref="TreinadorNeat.FuncaoAtivacao"/>). Fora
/// disso devolve <c>null</c>, e o diagrama volta a pintar os ocultos em cor neutra: é melhor não
/// mostrar valor nenhum do que mostrar um valor que a rede não produziria. A correção do cálculo é
/// verificável: as saídas obtidas aqui têm que bater com as de <see cref="RedeNeat.Ativar"/> para
/// a mesma entrada (ver AtivacoesRedeNeatTests).
/// </remarks>
public static class AtivacoesRedeNeat
{
    /// <summary>Código da função de ativação (como gravado no formato <c>.net</c> do SharpNEAT) que esta propagação sabe reproduzir.</summary>
    private const string CodigoAtivacaoSuportado = "TanH";

    /// <summary>
    /// Propaga <paramref name="entradasComBias"/> pelo grafo de <paramref name="modelo"/>.
    /// </summary>
    /// <param name="modelo">Topologia + pesos do campeão (ver <c>NeatGenomeConverter.ToNetFileModel</c>).</param>
    /// <param name="entradasComBias">
    /// Entradas na convenção de índices do SharpNEAT: posição 0 é o bias (1,0) e as entradas reais
    /// vêm depois — o mesmo vetor que <see cref="RedeNeat"/> escreve na caixa-preta.
    /// </param>
    /// <returns>Ativação por id de nó, ou <c>null</c> se a rede não for do tipo suportado (ver observações).</returns>
    public static IReadOnlyDictionary<int, double>? Calcular(NetFileModel modelo, double[] entradasComBias)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        ArgumentNullException.ThrowIfNull(entradasComBias);

        if (!modelo.IsAcyclic) return null;
        if (entradasComBias.Length != modelo.InputCount) return null;

        foreach (ActivationFnLine ativacao in modelo.ActivationFns)
        {
            if (!string.Equals(ativacao.Code, CodigoAtivacaoSuportado, StringComparison.OrdinalIgnoreCase)) return null;
        }

        int primeiroIdOculto = modelo.InputCount + modelo.OutputCount;

        // Soma ponderada que chega em cada nó, e as conexões que saem dele. Entradas e saídas
        // existem sempre (o framework reserva os ids), ocultos só se aparecerem em alguma conexão.
        var somaDeEntrada = new Dictionary<int, double>();
        var grauDeEntrada = new Dictionary<int, int>();
        var conexoesDeSaida = new Dictionary<int, List<(int Destino, double Peso)>>();

        for (int id = 0; id < primeiroIdOculto; id++)
        {
            somaDeEntrada[id] = 0.0;
            grauDeEntrada[id] = 0;
        }

        foreach (ConnectionLine conexao in modelo.Connections)
        {
            if (!somaDeEntrada.ContainsKey(conexao.SourceId))
            {
                somaDeEntrada[conexao.SourceId] = 0.0;
                grauDeEntrada[conexao.SourceId] = 0;
            }

            if (!somaDeEntrada.ContainsKey(conexao.TargetId))
            {
                somaDeEntrada[conexao.TargetId] = 0.0;
                grauDeEntrada[conexao.TargetId] = 0;
            }

            grauDeEntrada[conexao.TargetId]++;

            if (!conexoesDeSaida.TryGetValue(conexao.SourceId, out List<(int, double)>? saindo))
            {
                saindo = new List<(int, double)>();
                conexoesDeSaida[conexao.SourceId] = saindo;
            }

            saindo.Add((conexao.TargetId, conexao.Weight));
        }

        var ativacoes = new Dictionary<int, double>(somaDeEntrada.Count);

        // Ordenação topológica (Kahn): um nó só é ativado depois que TODAS as conexões que chegam
        // nele já contribuíram para sua soma. Os nós de entrada entram na fila direto — não têm
        // conexões de chegada nem função de ativação, apenas repassam o valor recebido.
        var fila = new Queue<int>();

        for (int id = 0; id < modelo.InputCount; id++)
        {
            ativacoes[id] = entradasComBias[id];
            fila.Enqueue(id);
        }

        foreach ((int id, int grau) in grauDeEntrada)
        {
            if (id >= modelo.InputCount && grau == 0) fila.Enqueue(id);
        }

        int processados = 0;

        while (fila.Count > 0)
        {
            int id = fila.Dequeue();
            processados++;

            if (!ativacoes.TryGetValue(id, out double valor))
            {
                // Nó oculto ou de saída: a soma ponderada já está completa neste ponto, então é
                // agora que a função de ativação se aplica (mesma regra do SharpNEAT, que ativa
                // todos os nós menos os de entrada).
                valor = Math.Tanh(somaDeEntrada[id]);
                ativacoes[id] = valor;
            }

            if (!conexoesDeSaida.TryGetValue(id, out List<(int Destino, double Peso)>? saindo)) continue;

            foreach ((int destino, double peso) in saindo)
            {
                somaDeEntrada[destino] += valor * peso;
                if (--grauDeEntrada[destino] == 0) fila.Enqueue(destino);
            }
        }

        // Sobrou nó sem processar = existe ciclo. Não deveria acontecer com IsAcyclic, mas é
        // barato garantir em vez de devolver ativações pela metade.
        if (processados != somaDeEntrada.Count) return null;

        // Saídas sem nenhuma conexão de chegada ficam de fora do laço acima quando o grafo as
        // deixa isoladas — valem TanH(0) = 0, o mesmo que o framework produz.
        for (int i = 0; i < modelo.OutputCount; i++)
        {
            int id = modelo.InputCount + i;
            if (!ativacoes.ContainsKey(id)) ativacoes[id] = Math.Tanh(somaDeEntrada[id]);
        }

        return ativacoes;
    }
}
