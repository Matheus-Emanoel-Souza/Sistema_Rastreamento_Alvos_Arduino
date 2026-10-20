using System.Drawing;
using RadarTorres.App.Models;
using RadarTorres.App.Services;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Identidade dos objetos acompanhados entre quadros (critérios 6, 7 e 9): o que mantém a mesma
/// ocorrência, o que abre outra, e por quanto tempo a identidade sobrevive sem detecção.
/// </summary>
public sealed class RastreamentoVisualServiceTests
{
    private readonly RastreamentoVisualService _servico = new(new LoggerFalso());

    private static ConfiguracaoDeTeste Config(int toleranciaSegundos = 10, double iou = 0.2) =>
        new(v =>
        {
            v.ToleranciaPerdaRastreamentoSegundos = toleranciaSegundos;
            v.LimiarIoURastreamento = iou;
        });

    [Fact]
    public void Caixa_sobreposta_na_mesma_camera_e_o_mesmo_acompanhamento()
    {
        using var _ = Config();
        DateTime agora = DateTime.Now;

        RastreioVisual primeiro = _servico.ObterOuCriar("cam", Quadrant.Q1, 1, new Rectangle(100, 100, 80, 80), agora);
        RastreioVisual segundo = _servico.ObterOuCriar("cam", Quadrant.Q1, 1, new Rectangle(104, 98, 82, 78), agora.AddMilliseconds(120));

        Assert.Same(primeiro, segundo);
        Assert.Equal(2, segundo.Confirmacoes);
        Assert.Single(_servico.Ativos);
    }

    [Fact]
    public void Caixa_sem_sobreposicao_abre_outro_acompanhamento()
    {
        using var _ = Config();
        DateTime agora = DateTime.Now;

        RastreioVisual primeiro = _servico.ObterOuCriar("cam", Quadrant.Q1, 1, new Rectangle(0, 0, 50, 50), agora);
        RastreioVisual segundo = _servico.ObterOuCriar("cam", Quadrant.Q1, 1, new Rectangle(500, 400, 50, 50), agora);

        Assert.NotSame(primeiro, segundo);
        Assert.NotEqual(primeiro.Id, segundo.Id);
        Assert.Equal(2, _servico.Ativos.Count);
    }

    [Fact]
    public void Caixa_que_cresce_muito_mantem_a_identidade_pelo_centro()
    {
        using var _ = Config();
        DateTime agora = DateTime.Now;

        // Objeto se aproximando: a caixa triplica de lado, mas o centro continua dentro da anterior.
        RastreioVisual primeiro = _servico.ObterOuCriar("cam", Quadrant.Q1, 1, new Rectangle(100, 100, 40, 40), agora);
        RastreioVisual segundo = _servico.ObterOuCriar("cam", Quadrant.Q1, 1, new Rectangle(60, 60, 120, 120), agora.AddMilliseconds(120));

        Assert.Same(primeiro, segundo);
    }

    [Fact]
    public void Cameras_diferentes_nunca_compartilham_acompanhamento()
    {
        using var _ = Config();
        DateTime agora = DateTime.Now;

        RastreioVisual q1 = _servico.ObterOuCriar("cam-1", Quadrant.Q1, 1, new Rectangle(100, 100, 80, 80), agora);
        RastreioVisual q2 = _servico.ObterOuCriar("cam-2", Quadrant.Q2, 1, new Rectangle(100, 100, 80, 80), agora);

        Assert.NotSame(q1, q2);
        Assert.Equal(Quadrant.Q1, q1.Quadrante);
        Assert.Equal(Quadrant.Q2, q2.Quadrante);
    }

    [Fact]
    public void Perfis_diferentes_na_mesma_camera_sao_acompanhamentos_distintos()
    {
        using var _ = Config();
        DateTime agora = DateTime.Now;

        RastreioVisual alvoA = _servico.ObterOuCriar("cam", Quadrant.Q1, 1, new Rectangle(100, 100, 80, 80), agora);
        RastreioVisual alvoB = _servico.ObterOuCriar("cam", Quadrant.Q1, 2, new Rectangle(100, 100, 80, 80), agora);

        Assert.NotSame(alvoA, alvoB);
    }

    [Fact]
    public void Acompanhamento_sobrevive_a_perda_dentro_da_tolerancia()
    {
        using var _ = Config(toleranciaSegundos: 30);
        DateTime agora = DateTime.Now;

        RastreioVisual original = _servico.ObterOuCriar("cam", Quadrant.Q1, 1, new Rectangle(100, 100, 80, 80), agora);

        // 20 segundos sem detecção, abaixo da tolerância de 30s.
        IReadOnlyList<RastreioVisual> encerrados = _servico.EncerrarInativos(agora.AddSeconds(20));

        Assert.Empty(encerrados);
        Assert.Single(_servico.Ativos);

        RastreioVisual retorno = _servico.ObterOuCriar("cam", Quadrant.Q1, 1, new Rectangle(100, 100, 80, 80), agora.AddSeconds(20));
        Assert.Same(original, retorno);
    }

    [Fact]
    public void Acompanhamento_e_encerrado_passada_a_tolerancia()
    {
        using var _ = Config(toleranciaSegundos: 10);
        DateTime agora = DateTime.Now;

        _servico.ObterOuCriar("cam", Quadrant.Q1, 1, new Rectangle(100, 100, 80, 80), agora);

        IReadOnlyList<RastreioVisual> encerrados = _servico.EncerrarInativos(agora.AddSeconds(11));

        Assert.Single(encerrados);
        Assert.Empty(_servico.Ativos);

        // Encerrar é só fechar a identidade: nada aqui afirma abate (ver EstadoOcorrencia).
        Assert.True(encerrados[0].Encerrado);
    }

    [Fact]
    public void EncerrarDaCamera_afeta_somente_a_camera_informada()
    {
        using var _ = Config();
        DateTime agora = DateTime.Now;

        _servico.ObterOuCriar("cam-1", Quadrant.Q1, 1, new Rectangle(10, 10, 40, 40), agora);
        _servico.ObterOuCriar("cam-2", Quadrant.Q2, 1, new Rectangle(10, 10, 40, 40), agora);

        _servico.EncerrarDaCamera("cam-1", "teste");

        Assert.Single(_servico.Ativos);
        Assert.Equal("cam-2", _servico.Ativos[0].CameraDispositivoId);
    }

    [Fact]
    public void Acesso_concorrente_nao_corrompe_a_lista_de_acompanhamentos()
    {
        using var _ = Config();
        DateTime agora = DateTime.Now;

        // Quatro "câmeras" em paralelo, cada uma na sua região do quadro.
        Parallel.For(0, 4, indice =>
        {
            for (int rodada = 0; rodada < 50; rodada++)
            {
                _servico.ObterOuCriar(
                    $"cam-{indice}",
                    Quadrant.Q1,
                    1,
                    new Rectangle(indice * 150, 100, 60, 60),
                    agora.AddMilliseconds(rodada * 120));
            }
        });

        Assert.Equal(4, _servico.Ativos.Count);
        Assert.Equal(4, _servico.Ativos.Select(r => r.CameraDispositivoId).Distinct().Count());
    }
}
