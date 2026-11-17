using RadarTorres.App.NeuralEvolution;
using Xunit;

namespace RadarTorres.Tests;

public class CurriculoTreinamentoTests
{
    [Fact]
    public void ComecaNaEtapaFacil()
    {
        var curriculo = new CurriculoTreinamento(limiarFitness: 100, geracoesEstaveisNecessarias: 3, tetoGeracoesPorEtapa: 10);

        Assert.Equal(EtapaCurriculo.Facil, curriculo.EtapaAtual);
    }

    [Fact]
    public void NaoAvancaComUmUnicoPicoIsolado()
    {
        var curriculo = new CurriculoTreinamento(limiarFitness: 100, geracoesEstaveisNecessarias: 3, tetoGeracoesPorEtapa: 10);

        curriculo.RegistrarGeracao(150); // pico isolado, 1 de 1
        curriculo.RegistrarGeracao(50);  // volta abaixo do limiar — reinicia a contagem

        Assert.Equal(EtapaCurriculo.Facil, curriculo.EtapaAtual);
    }

    [Fact]
    public void AvancaApenasAposGeracoesConsecutivasAcimaDoLimiar()
    {
        var curriculo = new CurriculoTreinamento(limiarFitness: 100, geracoesEstaveisNecessarias: 3, tetoGeracoesPorEtapa: 10);

        curriculo.RegistrarGeracao(150);
        curriculo.RegistrarGeracao(150);
        Assert.Equal(EtapaCurriculo.Facil, curriculo.EtapaAtual); // só 2 de 3

        EtapaCurriculo etapa = curriculo.RegistrarGeracao(150); // 3ª consecutiva

        Assert.Equal(EtapaCurriculo.Medio, etapa);
        Assert.Equal(EtapaCurriculo.Medio, curriculo.EtapaAtual);
    }

    [Fact]
    public void AvancaPeloTetoDeSegurancaMesmoSemAtingirOLimiar()
    {
        var curriculo = new CurriculoTreinamento(limiarFitness: 1000, geracoesEstaveisNecessarias: 3, tetoGeracoesPorEtapa: 5);

        for (int i = 0; i < 4; i++) curriculo.RegistrarGeracao(10); // nunca passa do limiar
        Assert.Equal(EtapaCurriculo.Facil, curriculo.EtapaAtual);

        EtapaCurriculo etapa = curriculo.RegistrarGeracao(10); // 5ª geração — estoura o teto

        Assert.Equal(EtapaCurriculo.Medio, etapa);
    }

    [Fact]
    public void NuncaRetrocedeDepoisDeAvancar()
    {
        var curriculo = new CurriculoTreinamento(limiarFitness: 100, geracoesEstaveisNecessarias: 2, tetoGeracoesPorEtapa: 10);

        curriculo.RegistrarGeracao(150);
        curriculo.RegistrarGeracao(150); // avança para Medio
        Assert.Equal(EtapaCurriculo.Medio, curriculo.EtapaAtual);

        curriculo.RegistrarGeracao(0); // fitness ruim na nova etapa — não deve voltar para Facil

        Assert.Equal(EtapaCurriculo.Medio, curriculo.EtapaAtual);
    }

    [Fact]
    public void ParaDeAvancarAoAlcancarDificilMesmoRegistrandoMaisGeracoes()
    {
        var curriculo = new CurriculoTreinamento(limiarFitness: 100, geracoesEstaveisNecessarias: 1, tetoGeracoesPorEtapa: 1);

        curriculo.RegistrarGeracao(150); // Facil -> Medio
        curriculo.RegistrarGeracao(150); // Medio -> Dificil
        Assert.Equal(EtapaCurriculo.Dificil, curriculo.EtapaAtual);

        EtapaCurriculo etapa = curriculo.RegistrarGeracao(150);

        Assert.Equal(EtapaCurriculo.Dificil, etapa);
    }

    [Theory]
    [InlineData(100, 0, 10)]
    [InlineData(100, 3, 2)]
    public void ConstrutorRejeitaParametrosInvalidos(double limiar, int geracoesEstaveis, int tetoGeracoes)
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            new CurriculoTreinamento(limiar, geracoesEstaveis, tetoGeracoes));
    }

    [Fact]
    public void AplicarEtapaFacilReduzDificuldadeEZeraCurvatura()
    {
        var baseParams = new ParametrosTreinamento();

        ParametrosTreinamento facil = baseParams.AplicarEtapa(EtapaCurriculo.Facil);

        Assert.True(facil.IntervaloSpawnMinSegundos > baseParams.IntervaloSpawnMinSegundos);
        Assert.True(facil.VelocidadeMaximaAlvoMetrosPorSegundo < baseParams.VelocidadeMaximaAlvoMetrosPorSegundo);
        Assert.Equal(0.0, facil.VariacaoMaximaDirecaoGrausPorSegundo);
    }

    [Fact]
    public void AplicarEtapaMedioNaoAlteraParametrosDeFabrica()
    {
        var baseParams = new ParametrosTreinamento();

        ParametrosTreinamento medio = baseParams.AplicarEtapa(EtapaCurriculo.Medio);

        Assert.Equal(baseParams, medio);
    }

    [Fact]
    public void AplicarEtapaDificilAumentaRitmoECurvaturaSemMudarVelocidadeMaxima()
    {
        var baseParams = new ParametrosTreinamento();

        ParametrosTreinamento dificil = baseParams.AplicarEtapa(EtapaCurriculo.Dificil);

        Assert.True(dificil.IntervaloSpawnMinSegundos < baseParams.IntervaloSpawnMinSegundos);
        Assert.True(dificil.VariacaoMaximaDirecaoGrausPorSegundo > baseParams.VariacaoMaximaDirecaoGrausPorSegundo);
        Assert.Equal(baseParams.VelocidadeMaximaAlvoMetrosPorSegundo, dificil.VelocidadeMaximaAlvoMetrosPorSegundo);
    }
}
