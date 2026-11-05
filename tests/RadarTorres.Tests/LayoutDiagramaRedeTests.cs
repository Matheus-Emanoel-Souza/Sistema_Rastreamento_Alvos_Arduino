using RadarTorres.App.ViewModels;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobertura do dimensionamento do diagrama da rede na tela "Treinamento do Controlador" (ver
/// TreinamentoControladorViewModel.CalcularLayoutDoDiagrama).
/// </summary>
/// <remarks>
/// O diagrama tinha tamanho fixo e uma única coluna de ocultos, herdados da rede 7-8-2 de
/// topologia constante do método anterior. Com NEAT a quantidade de ocultos é resultado da
/// evolução: o que estes testes fixam é que nenhum neurônio volte a ficar mais perto do vizinho
/// do que o próprio diâmetro — era isso que os fazia se sobrepor e sumir da área visível.
/// </remarks>
public sealed class LayoutDiagramaRedeTests
{
    /// <summary>Maior diâmetro que um neurônio oculto assume (o recém-surgido, destacado) — nenhum espaçamento pode ficar abaixo disso.</summary>
    private const double MaiorDiametroDeOculto = 17.0;

    private const int EntradasComBias = 8;
    private const int Saidas = 2;

    /// <summary>Distância vertical entre dois ocultos consecutivos da mesma coluna, no layout calculado.</summary>
    private static double EspacamentoEntreOcultos(int totalOcultos)
    {
        TreinamentoControladorViewModel.LayoutDiagrama layout =
            TreinamentoControladorViewModel.CalcularLayoutDoDiagrama(EntradasComBias, Saidas, totalOcultos);

        int naColuna = layout.PorColunaOculta;
        if (naColuna < 2) return double.MaxValue; // uma coluna com um nó só não tem vizinho

        double primeiro = TreinamentoControladorViewModel.PosicaoVerticalNaColuna(0, naColuna, layout.Altura);
        double segundo = TreinamentoControladorViewModel.PosicaoVerticalNaColuna(1, naColuna, layout.Altura);
        return segundo - primeiro;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(12)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(200)]
    public void CalcularLayout_NenhumOcultoFicaMaisPertoDoVizinhoQueOProprioDiametro(int totalOcultos)
    {
        Assert.True(
            EspacamentoEntreOcultos(totalOcultos) >= MaiorDiametroDeOculto,
            $"Com {totalOcultos} ocultos o espaçamento ficou menor que o diâmetro de um neurônio — eles se sobrepõem.");
    }

    /// <summary>
    /// Entradas e saídas têm rótulo fixo logo abaixo do círculo, então precisam de mais folga que
    /// os ocultos; se a altura encolher, os rótulos passam a colidir entre si.
    /// </summary>
    [Fact]
    public void CalcularLayout_ReservaEspacoParaOsRotulosDeEntradaESaida()
    {
        TreinamentoControladorViewModel.LayoutDiagrama layout =
            TreinamentoControladorViewModel.CalcularLayoutDoDiagrama(EntradasComBias, Saidas, totalOcultos: 0);

        double primeira = TreinamentoControladorViewModel.PosicaoVerticalNaColuna(0, EntradasComBias, layout.Altura);
        double segunda = TreinamentoControladorViewModel.PosicaoVerticalNaColuna(1, EntradasComBias, layout.Altura);

        Assert.True(segunda - primeira >= 32.0, "entradas consecutivas precisam caber círculo + rótulo.");
    }

    /// <summary>
    /// Acima de certo ponto o diagrama para de crescer para baixo e passa a usar mais colunas: uma
    /// tira vertical de centenas de neurônios é rolável, mas não deixa a arquitetura ser
    /// compreendida de relance.
    /// </summary>
    [Fact]
    public void CalcularLayout_ComMuitosOcultos_DistribuiEmVariasColunasEmVezDeEsticarIndefinidamente()
    {
        TreinamentoControladorViewModel.LayoutDiagrama poucos =
            TreinamentoControladorViewModel.CalcularLayoutDoDiagrama(EntradasComBias, Saidas, totalOcultos: 10);
        TreinamentoControladorViewModel.LayoutDiagrama muitos =
            TreinamentoControladorViewModel.CalcularLayoutDoDiagrama(EntradasComBias, Saidas, totalOcultos: 300);

        Assert.Equal(1, poucos.ColunasOcultas);
        Assert.True(muitos.ColunasOcultas > 1);
        Assert.True(muitos.Altura <= 700.0, "a altura precisa parar de crescer quando entram novas colunas.");
        // Mais colunas exigem mais largura, senão elas se empilhariam no mesmo x.
        Assert.True(muitos.Largura > poucos.Largura);
    }

    /// <summary>Rede sem nenhum oculto (as redes mínimas com que o NEAT começa) continua válida e sem coluna intermediária.</summary>
    [Fact]
    public void CalcularLayout_SemOcultos_NaoCriaColunaIntermediaria()
    {
        TreinamentoControladorViewModel.LayoutDiagrama layout =
            TreinamentoControladorViewModel.CalcularLayoutDoDiagrama(EntradasComBias, Saidas, totalOcultos: 0);

        Assert.Equal(0, layout.ColunasOcultas);
        Assert.Equal(0, layout.PorColunaOculta);
        Assert.True(layout.Largura >= 620.0);
        Assert.True(layout.Altura >= 300.0);
    }

    /// <summary>
    /// Todos os ocultos precisam caber nas colunas calculadas. Se <c>ColunasOcultas *
    /// PorColunaOculta</c> ficasse abaixo do total, os últimos nós seriam posicionados fora do
    /// diagrama — exatamente o sintoma de "neurônio que não aparece".
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(19)]
    [InlineData(64)]
    [InlineData(137)]
    [InlineData(500)]
    public void CalcularLayout_AsColunasComportamTodosOsOcultos(int totalOcultos)
    {
        TreinamentoControladorViewModel.LayoutDiagrama layout =
            TreinamentoControladorViewModel.CalcularLayoutDoDiagrama(EntradasComBias, Saidas, totalOcultos);

        Assert.True(layout.ColunasOcultas * layout.PorColunaOculta >= totalOcultos);
    }

    /// <summary>A posição vertical tem que ficar dentro da altura do diagrama — fora dela, o nó simplesmente não é visto.</summary>
    [Theory]
    [InlineData(3)]
    [InlineData(25)]
    [InlineData(120)]
    public void PosicaoVerticalNaColuna_FicaDentroDaAlturaDoDiagrama(int totalOcultos)
    {
        TreinamentoControladorViewModel.LayoutDiagrama layout =
            TreinamentoControladorViewModel.CalcularLayoutDoDiagrama(EntradasComBias, Saidas, totalOcultos);

        for (int i = 0; i < layout.PorColunaOculta; i++)
        {
            double y = TreinamentoControladorViewModel.PosicaoVerticalNaColuna(i, layout.PorColunaOculta, layout.Altura);
            Assert.InRange(y, 0.0, layout.Altura);
        }
    }
}
