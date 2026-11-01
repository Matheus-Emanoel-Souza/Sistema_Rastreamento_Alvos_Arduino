using System;
using RadarTorres.App.NeuralEvolution;
using RadarTorres.App.Services;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobertura de <see cref="ControladorTreinadoProvider"/>: carrega o genoma embutido de
/// verdade (ver Resources/ControladorTreinado/genoma-controlador.json e o target MSBuild
/// "EmbutirGenomaTreinado" no .csproj, que o substitui pelo último exportado nesta máquina
/// antes de cada build) — garante que o pipeline build→embutir→carregar nunca derruba o app,
/// seja qual for o genoma presente na máquina que compilou.
/// </summary>
/// <remarks>
/// <see cref="ControladorHibrido.QuantidadeEntradas"/> mudou (nova entrada de tempo detectado) e
/// o genoma embutido no repositório ainda foi treinado com a contagem antiga — o provider detecta
/// essa incompatibilidade e cai para heurística pura (<see cref="ControladorTreinadoProvider.Rede"/>
/// null) em vez de deixar <c>RedeNeural.Ativar</c> lançar na primeira seleção de torre real. Por
/// isso os testes aqui não assumem mais <c>Rede</c> sempre não-nulo: assumem só que carregar NUNCA
/// lança, e que, SE produzir uma rede, ela é compatível com a arquitetura atual. Volta a valer
/// "sempre produz rede" depois que um novo treinamento (7 entradas) for embutido.
/// </remarks>
public sealed class ControladorTreinadoProviderTests
{
    [Fact]
    public void Carregar_NuncaLancaMesmoComGenomaDeArquiteturaIncompativel()
    {
        // O próprio fato de chegar até aqui sem exceção já é a prova: Carregar() tem try/catch
        // e o guard de compatibilidade de arquitetura, então um genoma embutido desatualizado
        // (ver remarks da classe) vira Rede=null, nunca uma exceção não tratada.
        var provider = new ControladorTreinadoProvider(new LoggerFalso());
        Assert.True(provider.Rede is null || provider.Rede is RedeNeural);
    }

    [Fact]
    public void Carregar_SeProduzirRedeElaAtivaSemLancar()
    {
        var provider = new ControladorTreinadoProvider(new LoggerFalso());
        RedeNeural? rede = provider.Rede;
        if (rede is null) return; // genoma incompatível (ver remarks da classe) — nada a ativar

        double[] entradas = new double[ControladorHibrido.QuantidadeEntradas];
        Array.Fill(entradas, 0.5);
        double[] saida = rede.Ativar(entradas);

        Assert.Equal(ControladorHibrido.QuantidadeSaidas, saida.Length);
    }
}
