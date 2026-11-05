using System;
using System.Collections.Generic;
using RadarTorres.App.Models;
using RadarTorres.App.Services;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobertura do ponto de entrega dos dados da simulação para o sistema físico (ver
/// Services/TelemetriaSimulacaoService.cs).
/// </summary>
/// <remarks>
/// A integração com o Arduino ainda NÃO existe — nada aqui escreve na porta serial. O que estes
/// testes fixam é o contrato de que ela vai depender: o quadro mais recente fica disponível, os
/// assinantes são avisados, e um assinante com defeito não derruba a simulação que o alimenta.
/// </remarks>
public sealed class TelemetriaSimulacaoServiceTests
{
    private static QuadroTelemetriaSimulacao Quadro(double tempoSegundos, bool eixoZ = true) => new(
        TempoSimulacaoSegundos: tempoSegundos,
        DataHora: DateTime.Now,
        EixoZHabilitado: eixoZ,
        Alvos:
        [
            new AlvoTelemetria(
                Id: 1, X: 2.5, Y: -1.0, AlturaMetros: 1.2,
                VelocidadeMetrosPorSegundo: 1.5, Quadrante: Quadrant.Q4,
                VisivelPorTodasAsTorres: true, TorreComprometidaId: 3, Certeza: 0.8)
        ]);

    [Fact]
    public void UltimoQuadro_AntesDeQualquerPublicacao_EhNulo()
    {
        var servico = new TelemetriaSimulacaoService();

        Assert.Null(servico.UltimoQuadro);
    }

    [Fact]
    public void Publicar_GuardaOQuadroMaisRecenteEAvisaOsAssinantes()
    {
        var servico = new TelemetriaSimulacaoService();
        var recebidos = new List<QuadroTelemetriaSimulacao>();
        servico.QuadroPublicado += (_, quadro) => recebidos.Add(quadro);

        servico.Publicar(Quadro(1.0));
        servico.Publicar(Quadro(2.0));

        Assert.Equal(2, recebidos.Count);
        Assert.Equal(2.0, servico.UltimoQuadro!.TempoSimulacaoSegundos);
    }

    /// <summary>
    /// O quadro carrega os dois relógios: o do episódio, que ordena os acontecimentos, e o de
    /// parede, que permite verificar depois se a simulação de fato correu em tempo normal.
    /// </summary>
    [Fact]
    public void Publicar_PreservaOsDadosDoAlvoInclusiveAAltura()
    {
        var servico = new TelemetriaSimulacaoService();

        servico.Publicar(Quadro(3.5));

        AlvoTelemetria alvo = servico.UltimoQuadro!.Alvos[0];
        Assert.Equal(1.2, alvo.AlturaMetros);
        Assert.True(alvo.VisivelPorTodasAsTorres);
        Assert.Equal(3, alvo.TorreComprometidaId);
        Assert.Equal(3.5, servico.UltimoQuadro.TempoSimulacaoSegundos);
        Assert.True(servico.UltimoQuadro.EixoZHabilitado);
    }

    /// <summary>
    /// Quem assina este evento é, no futuro, a ponte serial com o hardware. Uma falha dela não
    /// pode interromper a simulação que a alimenta — a tela continua sendo a dona do laço.
    /// </summary>
    [Fact]
    public void Publicar_ComAssinanteQueFalha_NaoPropagaAExcecao()
    {
        var servico = new TelemetriaSimulacaoService();
        servico.QuadroPublicado += (_, _) => throw new InvalidOperationException("consumidor com defeito");

        servico.Publicar(Quadro(1.0));

        // O quadro é guardado antes de qualquer assinante ser chamado, então continua disponível.
        Assert.Equal(1.0, servico.UltimoQuadro!.TempoSimulacaoSegundos);
    }

    /// <summary>
    /// Com a simulação parada não há estado atual para entregar: manter o último quadro o faria
    /// passar por atual para quem estivesse lendo.
    /// </summary>
    [Fact]
    public void Limpar_DescartaOUltimoQuadro()
    {
        var servico = new TelemetriaSimulacaoService();
        servico.Publicar(Quadro(1.0));

        servico.Limpar();

        Assert.Null(servico.UltimoQuadro);
    }
}
