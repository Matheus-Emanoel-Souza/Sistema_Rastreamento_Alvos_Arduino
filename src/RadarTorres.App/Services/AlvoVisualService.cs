using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using RadarTorres.App.Configuration;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="IAlvoVisualService"/>. Quando o perfil ativo tem foto de
/// referência (<see cref="PerfilAlvo.CaminhoFotoReferencia"/>), cada caixa candidata do quadro
/// ao vivo é comparada DIRETAMENTE com a foto (cor/textura média numa grade 8×8 da região —
/// ver <see cref="ExtrairAssinatura"/>/<see cref="CalcularSimilaridade"/>), não só cor/formato
/// — mais fiel ao objeto específico cadastrado. Sem foto, cai de volta para a heurística
/// anterior (cor dominante + proporção da caixa), sem quebrar perfis já cadastrados sem foto.
/// Nenhuma biblioteca externa de visão computacional — mesmo racional já documentado em
/// RadarTorres.App.csproj. São aproximações deliberadas — ver limiares configuráveis em
/// <see cref="VisionSettings"/>, pensados para serem recalibrados com câmera e objeto reais.
/// </summary>
public sealed class AlvoVisualService : IAlvoVisualService
{
    private const int GradeAssinatura = 8;
    private const int BytesPorPixel = 3;

    private readonly IObjectDetectionService _detectionService;
    private readonly IPerfilAlvoService _perfilAlvoService;
    private readonly ILoggingService _logger;

    private string? _caminhoFotoCacheada;
    private float[]? _assinaturaFotoCacheada;

    public AlvoVisualService(IObjectDetectionService detectionService, IPerfilAlvoService perfilAlvoService, ILoggingService logger)
    {
        _detectionService = detectionService;
        _perfilAlvoService = perfilAlvoService;
        _logger = logger;
    }

    public AlvoVisualAvaliacao AvaliarFrame(Bitmap frame)
    {
        PerfilAlvo? perfil = _perfilAlvoService.PerfilAtivo;
        if (perfil is null) return new AlvoVisualAvaliacao(0, 0, false, null);

        VisionSettings config = AppConfig.Current.VisionSettings;
        IReadOnlyList<VisionDetection> deteccoes = _detectionService.Detect(frame);

        float[]? assinaturaReferencia = ObterAssinaturaReferencia(perfil);
        bool usandoFoto = assinaturaReferencia is not null;
        double melhorPontuacao = 0;

        foreach (VisionDetection deteccao in deteccoes.OrderByDescending(d => d.Score))
        {
            double pontuacao;
            bool bate;

            if (usandoFoto)
            {
                var caixa = new Rectangle(deteccao.BoxXPx, deteccao.BoxYPx, deteccao.BoxWidthPx, deteccao.BoxHeightPx);
                float[] assinaturaCandidata = ExtrairAssinatura(frame, caixa);
                pontuacao = CalcularSimilaridade(assinaturaReferencia!, assinaturaCandidata);
                bate = pontuacao >= config.LimiarSimilaridadeFoto;
            }
            else
            {
                double fracaoCor = CalcularFracaoCor(frame, deteccao, perfil.Cor);
                pontuacao = fracaoCor;
                bate = fracaoCor >= config.LimiarPreenchimentoMinimo
                    && ClassificarFormato(deteccao, fracaoCor, config) == perfil.Formato;
            }

            melhorPontuacao = Math.Max(melhorPontuacao, pontuacao);
            if (!bate) continue;

            double distanciaMetros = EstimarDistanciaMetros(deteccao, perfil.TamanhoCm, frame.Width, config.CameraFovHorizontalGraus);
            return new AlvoVisualAvaliacao(deteccoes.Count, melhorPontuacao, usandoFoto, new AlvoVisualResultado(perfil, deteccao, distanciaMetros));
        }

        return new AlvoVisualAvaliacao(deteccoes.Count, melhorPontuacao, usandoFoto, null);
    }

    // ---------------------------------------------------------------- Comparação com a foto de referência

    /// <summary>
    /// Assinatura visual (grade 8×8 de cor média) da foto de referência do perfil, cacheada por
    /// caminho de arquivo — recarregar/reprocessar a foto a cada quadro (a cada ~500ms) seria
    /// trabalho repetido à toa, já que a foto não muda enquanto o perfil continuar o mesmo.
    /// </summary>
    private float[]? ObterAssinaturaReferencia(PerfilAlvo perfil)
    {
        string? caminho = perfil.CaminhoFotoReferencia;
        if (string.IsNullOrEmpty(caminho) || !File.Exists(caminho)) return null;

        if (caminho == _caminhoFotoCacheada) return _assinaturaFotoCacheada;

        try
        {
            using var referencia = new Bitmap(caminho);
            Rectangle area = EncontrarAreaDoObjeto(referencia);
            float[] assinatura = ExtrairAssinatura(referencia, area);

            _caminhoFotoCacheada = caminho;
            _assinaturaFotoCacheada = assinatura;
            return assinatura;
        }
        catch (Exception ex)
        {
            _logger.Warning($"Não foi possível processar a foto de referência de \"{perfil.Nome}\" para comparação: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Tenta achar o objeto DENTRO da própria foto de referência (rodando a mesma detecção
    /// usada no quadro ao vivo) para não comparar o candidato contra a foto inteira (que inclui
    /// fundo). Sem nenhuma detecção na foto, assume que o usuário centralizou o objeto ao tirar
    /// a foto (a tela "Perfis de Alvo" mostra o preview ao vivo exatamente para isso) e usa um
    /// recorte central.
    /// </summary>
    private Rectangle EncontrarAreaDoObjeto(Bitmap referencia)
    {
        VisionDetection? melhor = _detectionService.Detect(referencia).OrderByDescending(d => d.Score).FirstOrDefault();
        if (melhor is not null)
        {
            return new Rectangle(melhor.BoxXPx, melhor.BoxYPx, melhor.BoxWidthPx, melhor.BoxHeightPx);
        }

        int largura = (int)(referencia.Width * 0.6);
        int altura = (int)(referencia.Height * 0.6);
        return new Rectangle((referencia.Width - largura) / 2, (referencia.Height - altura) / 2, largura, altura);
    }

    /// <summary>
    /// Reduz a região informada a uma grade fixa <see cref="GradeAssinatura"/>×<see cref="GradeAssinatura"/>
    /// de cor média (BGR) — uma "impressão digital" barata de cor/textura grosseira da região,
    /// comparável entre a foto de referência e qualquer caixa candidata do quadro ao vivo
    /// independente do tamanho original de cada uma. Usa <see cref="Graphics.DrawImage"/> (não
    /// <c>LockBits</c> direto na região original) de propósito — já reamostra para o destino
    /// sempre do tamanho exato da grade, sem o risco de ler além do buffer de um recorte que
    /// já causou um crash fatal nesta mesma classe (ver <see cref="CalcularFracaoCor"/>).
    /// </summary>
    private static float[] ExtrairAssinatura(Bitmap imagem, Rectangle area)
    {
        Rectangle recorteOrigem = Rectangle.Intersect(area, new Rectangle(0, 0, imagem.Width, imagem.Height));
        var assinatura = new float[GradeAssinatura * GradeAssinatura * BytesPorPixel];

        if (recorteOrigem.Width <= 0 || recorteOrigem.Height <= 0) return assinatura;

        using Bitmap amostra = new(GradeAssinatura, GradeAssinatura, PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(amostra))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.DrawImage(imagem, new Rectangle(0, 0, GradeAssinatura, GradeAssinatura), recorteOrigem, GraphicsUnit.Pixel);
        }

        BitmapData data = amostra.LockBits(
            new Rectangle(0, 0, GradeAssinatura, GradeAssinatura), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            byte[] linha = new byte[data.Stride];
            int idx = 0;

            for (int y = 0; y < GradeAssinatura; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, linha, 0, data.Stride);

                for (int x = 0; x < GradeAssinatura; x++)
                {
                    int offset = x * BytesPorPixel;
                    assinatura[idx++] = linha[offset];
                    assinatura[idx++] = linha[offset + 1];
                    assinatura[idx++] = linha[offset + 2];
                }
            }
        }
        finally
        {
            amostra.UnlockBits(data);
        }

        return assinatura;
    }

    /// <summary>Similaridade 0–1 entre duas assinaturas (1 = idêntica) — 1 menos a diferença
    /// média absoluta por canal, normalizada pela faixa de um byte (0–255).</summary>
    private static double CalcularSimilaridade(float[] referencia, float[] candidata)
    {
        double somaDiferencas = 0;
        for (int i = 0; i < referencia.Length; i++)
        {
            somaDiferencas += Math.Abs(referencia[i] - candidata[i]);
        }

        double diferencaMedia = somaDiferencas / referencia.Length;
        return 1.0 - (diferencaMedia / 255.0);
    }

    // ---------------------------------------------------------------- Fallback cor/formato (sem foto)

    private static FormatoAlvo ClassificarFormato(VisionDetection deteccao, double fracaoCor, VisionSettings config)
    {
        double maior = Math.Max(deteccao.BoxWidthPx, deteccao.BoxHeightPx);
        double menor = Math.Max(1, Math.Min(deteccao.BoxWidthPx, deteccao.BoxHeightPx));
        double aspecto = maior / menor;

        if (aspecto >= config.LimiarAspectoRetangular) return FormatoAlvo.Retangular;

        return fracaoCor >= config.LimiarPreenchimentoQuadrado ? FormatoAlvo.Quadrado : FormatoAlvo.Redondo;
    }

    private static double CalcularFracaoCor(Bitmap frame, VisionDetection deteccao, CorAlvo cor)
    {
        Rectangle area = Rectangle.Intersect(
            new Rectangle(deteccao.BoxXPx, deteccao.BoxYPx, deteccao.BoxWidthPx, deteccao.BoxHeightPx),
            new Rectangle(0, 0, frame.Width, frame.Height));

        if (area.Width <= 0 || area.Height <= 0) return 0;

        BitmapData data = frame.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            // IMPORTANTE: quando o PixelFormat pedido já bate com o do Bitmap de origem (é o
            // caso aqui), LockBits NÃO copia dados — Scan0/Stride continuam apontando para o
            // buffer ORIGINAL da imagem inteira (não um buffer novo só do recorte), com Scan0
            // já deslocado até o canto do recorte. Por isso cada linha só pode copiar os bytes
            // realmente pertencentes a ela (area.Width * BytesPorPixel) — copiar data.Stride
            // inteiro (a largura da imagem INTEIRA) a partir de um recorte lia além do fim do
            // buffer alocado na última linha da imagem, causando um crash fatal da CLR (acesso
            // de memória inválido), não uma exceção .NET normal.
            int totalPixels = area.Width * area.Height;
            int pixelsNaCor = 0;
            int bytesLinha = area.Width * BytesPorPixel;
            byte[] linha = new byte[bytesLinha];

            for (int y = 0; y < area.Height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, linha, 0, bytesLinha);

                for (int x = 0; x < area.Width; x++)
                {
                    int offset = x * BytesPorPixel;
                    byte b = linha[offset];
                    byte g = linha[offset + 1];
                    byte r = linha[offset + 2];

                    if (CorDominante(r, g, b) == cor) pixelsNaCor++;
                }
            }

            return (double)pixelsNaCor / totalPixels;
        }
        finally
        {
            frame.UnlockBits(data);
        }
    }

    private static CorAlvo CorDominante(byte r, byte g, byte b)
    {
        if (r >= g && r >= b) return CorAlvo.Vermelho;
        return g >= b ? CorAlvo.Verde : CorAlvo.Azul;
    }

    // ---------------------------------------------------------------- Distância

    /// <summary>
    /// Distância pelo modelo de câmera pinhole: um objeto de tamanho real conhecido
    /// (<paramref name="tamanhoRealCm"/>, maior dimensão) ocupando
    /// <see cref="VisionDetection.BoxWidthPx"/>/<see cref="VisionDetection.BoxHeightPx"/>
    /// pixels (o maior dos dois, correspondendo à mesma dimensão medida) numa imagem de
    /// <paramref name="larguraImagemPx"/> pixels de largura, com o campo de visão horizontal
    /// informado em <paramref name="fovHorizontalGraus"/>.
    /// </summary>
    private static double EstimarDistanciaMetros(VisionDetection deteccao, double tamanhoRealCm, int larguraImagemPx, double fovHorizontalGraus)
    {
        double tamanhoAparentePx = Math.Max(deteccao.BoxWidthPx, deteccao.BoxHeightPx);
        if (tamanhoAparentePx <= 0) return 0;

        double fovRad = fovHorizontalGraus * Math.PI / 180.0;
        double distanciaCm = (tamanhoRealCm * larguraImagemPx) / (2.0 * tamanhoAparentePx * Math.Tan(fovRad / 2.0));

        return distanciaCm / 100.0;
    }
}
