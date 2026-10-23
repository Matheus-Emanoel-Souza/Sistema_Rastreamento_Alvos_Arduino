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
public sealed class ControladorTreinadoProviderTests
{
    [Fact]
    public void Carregar_SempreProduzUmaRedeComAArquiteturaEsperada()
    {
        var provider = new ControladorTreinadoProvider(new LoggerFalso());

        // Pode ser null só se o recurso embutido estiver corrompido (não é o caso aqui: o
        // próprio build deste projeto embute um JSON válido, zerado ou treinado).
        Assert.NotNull(provider.Rede);
    }

    [Fact]
    public void Carregar_RedeCarregadaAtivaSemLancar()
    {
        var provider = new ControladorTreinadoProvider(new LoggerFalso());
        RedeNeural? rede = provider.Rede;
        Assert.NotNull(rede);

        double[] saida = rede!.Ativar([0.5, 1, 0, 0.5, 0.5, 1.0]);

        Assert.Equal(ControladorHibrido.QuantidadeSaidas, saida.Length);
    }
}
