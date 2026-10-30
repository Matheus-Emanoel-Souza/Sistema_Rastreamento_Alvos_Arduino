using System;
using SharpNeat;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// Adaptador fino sobre a rede neural do SharpNEAT (<see cref="IBlackBox{T}"/>), para que o
/// resto do projeto continue falando em "entradas → saídas" sem conhecer a API do framework.
///
/// Substitui a antiga <c>RedeNeural</c> (feedforward fully-connected de topologia fixa,
/// implementada à mão). A diferença essencial não é de implementação, é de método: com NEAT a
/// topologia NÃO é mais escolhida por nós — o número de neurônios ocultos e quais conexões
/// existem são resultado da evolução, não parâmetro de entrada. Por isso aqui não existe mais
/// nada como <c>TamanhoGenoma(int[] camadas)</c>: o genoma é um grafo, não um vetor de pesos de
/// tamanho previsível.
/// </summary>
/// <remarks>
/// Convenção de entradas do SharpNEAT (ver <c>XorEvaluator</c> no repositório do framework): o
/// nó de índice 0 é o BIAS e precisa ser escrito como 1,0 manualmente a cada ativação; as
/// entradas reais começam no índice 1. Por isso a caixa-preta tem
/// <see cref="ControladorHibrido.QuantidadeEntradas"/> + 1 nós de entrada, e
/// <see cref="Ativar"/> recebe só as entradas reais — quem chama nunca lida com o bias.
/// </remarks>
public sealed class RedeNeat
{
    private readonly IBlackBox<double> _caixaPreta;

    /// <summary>Quantidade de nós de entrada da caixa-preta do SharpNEAT, já incluindo o bias — ver observações sobre a convenção de índices acima.</summary>
    public static int QuantidadeEntradasComBias => ControladorHibrido.QuantidadeEntradas + 1;

    public RedeNeat(IBlackBox<double> caixaPreta)
    {
        ArgumentNullException.ThrowIfNull(caixaPreta);

        if (caixaPreta.Inputs.Length != QuantidadeEntradasComBias)
        {
            throw new ArgumentException(
                $"A rede tem {caixaPreta.Inputs.Length} nós de entrada, esperado {QuantidadeEntradasComBias} ({ControladorHibrido.QuantidadeEntradas} entradas + bias).",
                nameof(caixaPreta));
        }

        if (caixaPreta.Outputs.Length != ControladorHibrido.QuantidadeSaidas)
        {
            throw new ArgumentException(
                $"A rede tem {caixaPreta.Outputs.Length} saídas, esperado {ControladorHibrido.QuantidadeSaidas}.",
                nameof(caixaPreta));
        }

        _caixaPreta = caixaPreta;
    }

    /// <summary>
    /// Propaga as entradas e devolve as saídas. Mesma assinatura da antiga <c>RedeNeural.Ativar</c>
    /// — é isso que permite <see cref="ControladorHibrido"/>, <see cref="ExtratorDeRegras"/> e
    /// <c>TowerSelectionService</c> seguirem inalterados na lógica.
    /// </summary>
    /// <remarks>
    /// <see cref="IBlackBox{T}.Reset"/> é chamado ao final de cada ativação porque o NEAT pode
    /// produzir redes CÍCLICAS (com estado interno entre ativações). Cada decisão do controlador
    /// é independente — resetar garante que a mesma entrada sempre produza a mesma saída, que é
    /// a premissa de <see cref="ExtratorDeRegras"/> e da comparação app↔Arduino. Em redes
    /// acíclicas (a configuração padrão aqui) o reset é barato e não muda nada.
    /// </remarks>
    public double[] Ativar(double[] entradas)
    {
        ArgumentNullException.ThrowIfNull(entradas);

        if (entradas.Length != ControladorHibrido.QuantidadeEntradas)
        {
            throw new ArgumentException(
                $"Esperadas {ControladorHibrido.QuantidadeEntradas} entradas, recebidas {entradas.Length}.",
                nameof(entradas));
        }

        Span<double> nosDeEntrada = _caixaPreta.Inputs.Span;
        nosDeEntrada[0] = 1.0; // bias (convenção do SharpNEAT)
        for (int i = 0; i < entradas.Length; i++)
        {
            nosDeEntrada[i + 1] = entradas[i];
        }

        _caixaPreta.Activate();

        Span<double> nosDeSaida = _caixaPreta.Outputs.Span;
        var saidas = new double[nosDeSaida.Length];
        nosDeSaida.CopyTo(saidas);

        _caixaPreta.Reset();
        return saidas;
    }
}
