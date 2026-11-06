using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// Torre dentro de um episódio de treinamento. Posição fixa durante todo o episódio (mesma
/// premissa de <see cref="Tower"/> no sistema real) — só muda se está livre para receber um
/// novo alvo.
/// </summary>
public sealed class TorreTreinamento
{
    public int Id { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public Quadrant QuadrantePreferido { get; init; }

    /// <summary>Id do alvo atualmente comprometido com esta torre, ou <c>null</c> se livre.</summary>
    public int? AlvoComprometidoId { get; set; }

    public bool Livre => AlvoComprometidoId is null;

    /// <summary>
    /// Instante (<see cref="EstadoEpisodio.TempoDecorridoSegundos"/>) do último disparo desta
    /// torre, ou <c>null</c> se ainda não disparou neste episódio. Usado só para a tela destacar
    /// visualmente "disparando" por uma janela curta — ver <see cref="ControladorHibrido.DecidirDisparo"/>.
    /// </summary>
    public double? DisparadoEmSegundos { get; set; }

    /// <summary>Posição do alvo no instante do último disparo — guardada porque o alvo abatido é removido do episódio no mesmo passo em que dispara.</summary>
    public double UltimoAlvoDisparoX { get; set; }
    public double UltimoAlvoDisparoY { get; set; }

    /// <summary>Soma do tempo (s), ao longo do episódio, em que esta torre esteve comprometida com algum alvo (<see cref="Livre"/> == false) — usada só ao fim do episódio para penalizar desequilíbrio entre torres (ver <see cref="ParametrosTreinamento.PenalidadeDesequilibrioTorresPorSegundo"/>), não influencia nenhuma decisão durante o episódio.</summary>
    public double TempoComprometidoSegundos { get; set; }

    /// <summary>
    /// Instante (<see cref="EstadoEpisodio.TempoDecorridoSegundos"/>) de cada novo compromisso
    /// desta torre com um alvo — usado só pelo controlador V2 (<see cref="ControladorHibridoV2"/>)
    /// para alimentar a entrada "trocas recentes de alvo" (ver <see cref="ContarTrocasRecentes"/>).
    /// Fila (não lista) porque só a ponta mais antiga é descartada, nunca o meio.
    /// </summary>
    public Queue<double> InstantesDeNovoCompromisso { get; } = new();

    /// <summary>
    /// Registra que esta torre acabou de assumir um alvo NOVO neste instante — chamado pela
    /// seleção de torre (V2) toda vez que <see cref="AlvoComprometidoId"/> passa a apontar para um
    /// alvo diferente do anterior. Não existe no fluxo V1: a thrash de troca de alvo por torre é
    /// uma entrada nova da V2 (ver <see cref="ControladorHibridoV2.RotulosEntradas"/>), então só o
    /// código do ambiente V2 escreve aqui — o controlador V1, intocado, nunca lê nem escreve isto.
    /// </summary>
    public void RegistrarNovoCompromisso(double tempoAtualSegundos) => InstantesDeNovoCompromisso.Enqueue(tempoAtualSegundos);

    /// <summary>
    /// Quantos novos compromissos esta torre assumiu dentro de <paramref name="janelaSegundos"/>
    /// até <paramref name="tempoAtualSegundos"/> — descarta (de vez, não é só leitura) os instantes
    /// fora da janela, então chamar isto já funciona como a limpeza periódica da fila.
    /// </summary>
    public int ContarTrocasRecentes(double tempoAtualSegundos, double janelaSegundos)
    {
        while (InstantesDeNovoCompromisso.Count > 0 && tempoAtualSegundos - InstantesDeNovoCompromisso.Peek() > janelaSegundos)
        {
            InstantesDeNovoCompromisso.Dequeue();
        }

        return InstantesDeNovoCompromisso.Count;
    }
}
