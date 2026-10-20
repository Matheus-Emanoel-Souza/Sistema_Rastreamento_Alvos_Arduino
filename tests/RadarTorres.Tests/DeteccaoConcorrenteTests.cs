using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using RadarTorres.App.Models;
using RadarTorres.App.Services;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Detecção rodando em paralelo (card de Monitoramento + os quatro painéis da tela "Câmeras"):
/// os serviços envolvidos são Singleton e passaram a ter até cinco chamadores simultâneos, o que
/// transformou estado compartilhado antes inofensivo em condição de corrida.
/// </summary>
public sealed class DeteccaoConcorrenteTests : IDisposable
{
    private readonly string _pasta = FotosDeTeste.CriarPasta();

    public void Dispose() => FotosDeTeste.Limpar(_pasta);

    /// <summary>Detector que não propõe nada: o teste exercita a comparação com as fotos, não o YOLO.</summary>
    private sealed class DetectorVazio : IObjectDetectionService
    {
        public IReadOnlyList<VisionDetection> Detect(Bitmap frame) => Array.Empty<VisionDetection>();
    }

    private static Bitmap QuadroAoVivo(int largura = 320, int altura = 240)
    {
        var frame = new Bitmap(largura, altura, PixelFormat.Format24bppRgb);
        using Graphics g = Graphics.FromImage(frame);
        g.Clear(Color.FromArgb(20, 30, 25));
        g.FillEllipse(Brushes.Red, largura / 3, altura / 3, largura / 4, altura / 4);
        return frame;
    }

    private PerfilAlvo PerfilComFotos(PerfilAlvoService servico, string nome, int quantidade = 3)
    {
        List<FotoReferenciaAlvo> fotos = Enumerable.Range(0, quantidade)
            .Select(_ => new FotoReferenciaAlvo { Caminho = FotosDeTeste.FotoValida(_pasta), CapturadaEm = DateTime.Now })
            .ToList();

        ResultadoPerfilAlvo resultado = servico.Add(nome, CorAlvo.Vermelho, FormatoAlvo.Redondo, DimensoesAlvo.Unica(8), fotos);
        Assert.True(resultado.Sucesso, resultado.Erro);
        return resultado.Perfil!;
    }

    /// <summary>
    /// Cinco "câmeras" avaliando quadros ao mesmo tempo contra o mesmo perfil. O cache de
    /// assinaturas das fotos é compartilhado; antes de ser trocado numa única referência, uma
    /// thread podia enxergar a chave nova junto da lista antiga.
    /// </summary>
    [Fact]
    public void Avaliar_quadros_em_paralelo_nao_quebra_nem_mistura_o_cache_de_assinaturas()
    {
        var logger = new LoggerFalso();
        var perfilService = new PerfilAlvoService(new RepositorioPerfisEmMemoria(), logger);
        PerfilAlvo perfil = PerfilComFotos(perfilService, "Bola vermelha");
        perfilService.SelecionarAtivo(perfil);

        var alvoVisual = new AlvoVisualService(new DetectorVazio(), perfilService, logger);
        var erros = new ConcurrentBag<Exception>();
        var usouFoto = new ConcurrentBag<bool>();

        Parallel.For(0, 5, _ =>
        {
            try
            {
                for (int rodada = 0; rodada < 10; rodada++)
                {
                    using Bitmap frame = QuadroAoVivo();
                    AlvoVisualAvaliacao avaliacao = alvoVisual.AvaliarFrame(frame);
                    usouFoto.Add(avaliacao.UsouFotoReferencia);
                }
            }
            catch (Exception ex)
            {
                erros.Add(ex);
            }
        });

        Assert.Empty(erros);

        // Todas as rodadas enxergaram as fotos do perfil: nenhuma pegou um cache meio construído
        // (que apareceria aqui como uma avaliação sem foto de referência).
        Assert.Equal(50, usouFoto.Count);
        Assert.All(usouFoto, Assert.True);
    }

    /// <summary>
    /// Trocar o perfil ativo enquanto as câmeras avaliam quadros: consultar o perfil ativo não
    /// pode percorrer a coleção observável que a tela está alterando.
    /// </summary>
    [Fact]
    public void Trocar_o_perfil_ativo_durante_a_deteccao_nao_derruba_as_camaras()
    {
        var logger = new LoggerFalso();
        var perfilService = new PerfilAlvoService(new RepositorioPerfisEmMemoria(), logger);
        PerfilAlvo primeiro = PerfilComFotos(perfilService, "Alvo A");
        perfilService.SelecionarAtivo(primeiro);

        var alvoVisual = new AlvoVisualService(new DetectorVazio(), perfilService, logger);
        var erros = new ConcurrentBag<Exception>();
        using var pararCadastro = new CancellationTokenSource();

        // Thread de "interface": cadastra, ativa e remove perfis o tempo todo.
        Task tela = Task.Run(() =>
        {
            try
            {
                int indice = 0;
                while (!pararCadastro.IsCancellationRequested)
                {
                    PerfilAlvo novo = PerfilComFotos(perfilService, $"Alvo {indice++}");
                    perfilService.SelecionarAtivo(novo);
                    perfilService.Remove(novo);
                }
            }
            catch (Exception ex)
            {
                erros.Add(ex);
            }
        });

        Parallel.For(0, 4, _ =>
        {
            try
            {
                for (int rodada = 0; rodada < 15; rodada++)
                {
                    using Bitmap frame = QuadroAoVivo();
                    alvoVisual.AvaliarFrame(frame);
                }
            }
            catch (Exception ex)
            {
                erros.Add(ex);
            }
        });

        pararCadastro.Cancel();
        tela.Wait(TimeSpan.FromSeconds(30));

        Assert.Empty(erros);
    }

    /// <summary>Remover o perfil ativo deixa a detecção sem alvo imediatamente — não preso ao perfil excluído.</summary>
    [Fact]
    public void Remover_o_perfil_ativo_zera_o_perfil_da_deteccao()
    {
        var perfilService = new PerfilAlvoService(new RepositorioPerfisEmMemoria(), new LoggerFalso());
        PerfilAlvo perfil = PerfilComFotos(perfilService, "Alvo");
        perfilService.SelecionarAtivo(perfil);

        Assert.Same(perfil, perfilService.PerfilAtivo);

        perfilService.Remove(perfil);

        Assert.Null(perfilService.PerfilAtivo);
    }

    /// <summary>Perfil marcado como ativo no arquivo salvo volta como ativo ao abrir o aplicativo.</summary>
    [Fact]
    public void Perfil_ativo_salvo_e_restaurado_na_abertura()
    {
        var repositorio = new RepositorioPerfisEmMemoria();
        var primeiro = new PerfilAlvoService(repositorio, new LoggerFalso());
        PerfilAlvo perfil = PerfilComFotos(primeiro, "Alvo");
        primeiro.SelecionarAtivo(perfil);

        // Nova instância lendo o mesmo repositório (equivale a reabrir o aplicativo).
        var segundo = new PerfilAlvoService(repositorio, new LoggerFalso());

        Assert.NotNull(segundo.PerfilAtivo);
        Assert.Equal(perfil.Id, segundo.PerfilAtivo!.Id);
    }
}
