using RadarTorres.App.NeuralEvolution;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobertura de <see cref="AlvoTreinamento.Certeza"/> — regressão da causa raiz da estagnação
/// investigada: antes a certeza era sorteada uma ÚNICA vez no nascimento (uniforme 0-1) e nunca
/// mudava, então ~70% dos alvos nasciam permanentemente abaixo do limiar padrão de disparo
/// (0,70) e nunca podiam ser abatidos, não importa a qualidade do genoma — um piso de fuga fixo
/// que dominava o fitness e escondia a diferença real entre genomas bons e ruins. Agora a
/// certeza CRESCE com o tempo de observação (ver AmbienteTreinamento.TalvezNascerAlvo), então
/// nenhum alvo fica preso para sempre — só demora mais ou menos.
/// </summary>
public sealed class AlvoTreinamentoTests
{
    [Fact]
    public void Certeza_NoNascimento_EIgualACertezaBase()
    {
        var alvo = new AlvoTreinamento { Id = 1, CertezaBase = 0.35, TaxaCrescimentoCertezaPorSegundo = 0.1 };

        Assert.Equal(0.35, alvo.Certeza, precision: 10);
    }

    [Fact]
    public void Certeza_CrescePassoAPassoComOTempoDeVida()
    {
        var alvo = new AlvoTreinamento { Id = 1, CertezaBase = 0.2, TaxaCrescimentoCertezaPorSegundo = 0.1 };

        alvo.TempoDeVidaSegundos = 1;
        Assert.Equal(0.3, alvo.Certeza, precision: 10);

        alvo.TempoDeVidaSegundos = 2;
        Assert.Equal(0.4, alvo.Certeza, precision: 10);
    }

    [Fact]
    public void Certeza_NuncaUltrapassaUm_MesmoComMuitoTempoDeVida()
    {
        var alvo = new AlvoTreinamento { Id = 1, CertezaBase = 0.9, TaxaCrescimentoCertezaPorSegundo = 0.5, TempoDeVidaSegundos = 100 };

        Assert.Equal(1.0, alvo.Certeza, precision: 10);
    }

    /// <summary>
    /// Mesmo no PIOR caso sorteado por <see cref="AmbienteTreinamento.TalvezNascerAlvo"/>
    /// (CertezaBase e taxa de crescimento mínimas), o alvo tem que conseguir cruzar o limiar
    /// padrão de disparo (0,70) dentro da duração normal de um episódio — nenhum alvo pode ficar
    /// estruturalmente impossível de abater.
    /// </summary>
    [Fact]
    public void Certeza_NoPiorCasoSorteado_UltrapassaOLimiarPadraoDentroDoEpisodio()
    {
        const double piorCertezaBase = 0.20;
        const double piorTaxaCrescimento = 0.08;
        var alvo = new AlvoTreinamento
        {
            Id = 1,
            CertezaBase = piorCertezaBase,
            TaxaCrescimentoCertezaPorSegundo = piorTaxaCrescimento,
            TempoDeVidaSegundos = 10
        };

        Assert.True(alvo.Certeza >= 0.70, $"Certeza ficou em {alvo.Certeza}, não ultrapassou o limiar padrão mesmo no pior caso.");
    }
}
