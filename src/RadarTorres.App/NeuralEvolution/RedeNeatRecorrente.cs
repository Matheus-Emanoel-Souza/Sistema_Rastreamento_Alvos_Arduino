using System;
using System.Collections.Generic;
using SharpNeat;
using SharpNeat.Evaluation;
using SharpNeat.Neat.Genome;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// Variante de <see cref="RedeNeat"/> com estado interno ISOLADO POR CONTEXTO (na prática, por
/// torre — ver <see cref="ControladorHibridoV2"/>): em vez de uma única caixa-preta compartilhada
/// por todas as decisões do episódio, decodifica uma caixa-preta INDEPENDENTE para cada id de
/// contexto na primeira vez que ele aparece, e reaproveita essa mesma instância (sem reset) em
/// todas as ativações seguintes daquele contexto — cada torre "lembra" só da própria sequência de
/// decisões, sem se misturar com a de outra torre.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que isto existe fora do laço evolutivo.</b> O SharpNEAT decodifica e entrega UMA
/// caixa-preta já pronta por genoma avaliado (<c>IPhenomeEvaluator&lt;IBlackBox&lt;double&gt;&gt;.Evaluate</c>,
/// ver <see cref="AvaliadorAmbienteV2"/>) — o genoma em si não é exposto ao avaliador, só o
/// resultado decodificado. Isolar estado por torre exige decodificar várias caixas-pretas
/// independentes do MESMO genoma, o que só é possível tendo o genoma (e o decodificador) em mãos,
/// não só a caixa-preta final. Por isso esta classe guarda genoma + decodificador (ver
/// <see cref="TreinadorNeat.CriarRedeRecorrente"/>) em vez de envolver uma caixa-preta já pronta
/// como <see cref="RedeNeat"/> faz.
/// </para>
/// <para>
/// <b>Consequência prática:</b> esta classe serve para REPRODUZIR/AVALIAR um genoma já escolhido
/// (replay na tela, ou uma avaliação isolada da bateria comparativa — ver
/// <see cref="AmbienteTreinamento.ExecutarEpisodioRapidoV2(RedeNeatRecorrente,int?,TipoCenarioTreinamento)"/>),
/// não para a SELEÇÃO durante o treinamento em si: dentro do laço evolutivo do SharpNEAT
/// (<see cref="TreinadorNeat.EvoluirGeracao"/>), a recorrência disponível é a mais simples — uma
/// única caixa-preta cíclica por episódio, reiniciada a cada episódio novo (ver
/// <see cref="RedeNeat.Reiniciar"/> usado por <see cref="AmbienteTreinamento.ExecutarEpisodioRapidoV2(RedeNeat,int?,TipoCenarioTreinamento)"/>) —
/// ela compartilha estado entre as decisões de torres diferentes dentro do mesmo passo, uma
/// simplificação documentada em vez de isolamento perfeito por torre. Isolar por torre DENTRO do
/// treinamento exigiria um avaliador que opere sobre o genoma (não a caixa-preta) — o SharpNEAT
/// 4.1 não expõe essa extensão no esquema de avaliação por caixa-preta usado aqui.
/// </para>
/// </remarks>
public sealed class RedeNeatRecorrente
{
    private readonly NeatGenome<double> _genoma;
    private readonly IGenomeDecoder<NeatGenome<double>, IBlackBox<double>> _decodificador;
    private readonly Dictionary<int, IBlackBox<double>> _caixasPorContexto = new();

    public RedeNeatRecorrente(NeatGenome<double> genoma, IGenomeDecoder<NeatGenome<double>, IBlackBox<double>> decodificador)
    {
        _genoma = genoma;
        _decodificador = decodificador;
    }

    /// <summary>Ativa a caixa-preta do contexto <paramref name="idContexto"/> (decodificando-a, se for a primeira vez), SEM resetar — o estado persiste para a próxima ativação do MESMO contexto.</summary>
    public double[] Ativar(int idContexto, double[] entradas)
    {
        if (!_caixasPorContexto.TryGetValue(idContexto, out IBlackBox<double>? caixa))
        {
            caixa = _decodificador.Decode(_genoma);
            _caixasPorContexto[idContexto] = caixa;
        }

        Span<double> nosDeEntrada = caixa.Inputs.Span;
        nosDeEntrada[0] = 1.0;
        for (int i = 0; i < entradas.Length; i++)
        {
            nosDeEntrada[i + 1] = entradas[i];
        }

        caixa.Activate();

        Span<double> nosDeSaida = caixa.Outputs.Span;
        var saidas = new double[nosDeSaida.Length];
        nosDeSaida.CopyTo(saidas);
        return saidas;
    }

    /// <summary>Descarta o estado (e a caixa-preta) de um contexto — chamado quando o alvo/torre associado deixa de existir, para a próxima vez que o id for reaproveitado (outro episódio) começar do zero.</summary>
    public void ReiniciarContexto(int idContexto) => _caixasPorContexto.Remove(idContexto);

    /// <summary>Descarta TODOS os contextos — chamado no início de cada episódio novo, para garantir que nenhuma memória de um episódio anterior vaze para este (ver observações da classe).</summary>
    public void ReiniciarTudo() => _caixasPorContexto.Clear();
}
