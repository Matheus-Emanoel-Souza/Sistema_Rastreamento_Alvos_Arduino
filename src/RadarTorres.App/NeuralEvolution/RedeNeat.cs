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
    private readonly bool _manterEstadoEntreAtivacoes;

    /// <summary>Quantidade de nós de entrada da caixa-preta do SharpNEAT, já incluindo o bias — ver observações sobre a convenção de índices acima.</summary>
    public static int QuantidadeEntradasComBias => ControladorHibrido.QuantidadeEntradas + 1;

    /// <summary>Entradas reais que esta instância aceita (sem o bias) — lido direto da caixa-preta, então vale tanto para o contrato V1 (<see cref="ControladorHibrido"/>, 7 entradas) quanto para o V2 (<see cref="ControladorHibridoV2"/>, mais entradas).</summary>
    public int QuantidadeEntradas => _caixaPreta.Inputs.Length - 1;

    /// <summary>Saídas desta instância — 2 para V1, 3 para V2 (ver <see cref="QuantidadeEntradas"/>).</summary>
    public int QuantidadeSaidas => _caixaPreta.Outputs.Length;

    /// <param name="caixaPreta">Rede já decodificada pelo SharpNEAT.</param>
    /// <param name="manterEstadoEntreAtivacoes">
    /// <c>false</c> (padrão) reseta a caixa-preta ao final de CADA <see cref="Ativar"/> — é o que
    /// garante determinismo por decisão (mesma entrada → mesma saída), premissa de
    /// <see cref="ExtratorDeRegras"/> e da comparação app↔Arduino, e o único comportamento usado
    /// pelo controlador V1. <c>true</c> deixa o estado interno (relevante só em rede CÍCLICA —
    /// ver <see cref="TreinadorNeat"/>) acumular de uma ativação para a próxima dentro do mesmo
    /// episódio — quem liga isto é responsável por chamar <see cref="Reiniciar"/> no início de
    /// cada episódio novo, ou o estado de um episódio vaza para o seguinte.
    /// </param>
    public RedeNeat(IBlackBox<double> caixaPreta, bool manterEstadoEntreAtivacoes = false)
    {
        ArgumentNullException.ThrowIfNull(caixaPreta);
        _caixaPreta = caixaPreta;
        _manterEstadoEntreAtivacoes = manterEstadoEntreAtivacoes;
    }

    /// <summary>
    /// Propaga as entradas e devolve as saídas. Mesma assinatura da antiga <c>RedeNeural.Ativar</c>
    /// — é isso que permite <see cref="ControladorHibrido"/>, <see cref="ExtratorDeRegras"/> e
    /// <c>TowerSelectionService</c> seguirem inalterados na lógica.
    /// </summary>
    /// <remarks>
    /// Por padrão (<see cref="_manterEstadoEntreAtivacoes"/> falso) <see cref="IBlackBox{T}.Reset"/>
    /// é chamado ao final de cada ativação, porque o NEAT pode produzir redes CÍCLICAS (com estado
    /// interno entre ativações) e cada decisão do controlador V1 é independente — resetar garante
    /// que a mesma entrada sempre produza a mesma saída. Em redes acíclicas (configuração padrão)
    /// o reset é barato e não muda nada. Com o modo recorrente ligado, o reset é responsabilidade
    /// de quem chama (ver <see cref="Reiniciar"/>), não mais automático aqui.
    /// </remarks>
    public double[] Ativar(double[] entradas)
    {
        ArgumentNullException.ThrowIfNull(entradas);

        if (entradas.Length != QuantidadeEntradas)
        {
            throw new ArgumentException(
                $"Esperadas {QuantidadeEntradas} entradas, recebidas {entradas.Length}.",
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

        if (!_manterEstadoEntreAtivacoes) _caixaPreta.Reset();
        return saidas;
    }

    /// <summary>
    /// Zera o estado interno da caixa-preta — chamado automaticamente a cada <see cref="Ativar"/>
    /// quando o modo recorrente está desligado, e precisa ser chamado manualmente (uma vez por
    /// episódio, antes do primeiro passo) quando está ligado — ver
    /// <see cref="AmbienteTreinamento.ExecutarEpisodioRapidoV2"/>.
    /// </summary>
    public void Reiniciar() => _caixaPreta.Reset();
}
