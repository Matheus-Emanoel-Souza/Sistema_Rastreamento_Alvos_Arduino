using System;
using System.Collections.Generic;
using System.Diagnostics;
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
/// referência (<see cref="PerfilAlvo.Fotos"/>, cada uma recortada no objeto que o usuário marcou
/// em <see cref="FotoReferenciaAlvo.Recorte"/>), cada caixa candidata do quadro
/// ao vivo é comparada DIRETAMENTE com a foto (ver <see cref="ExtrairAssinatura"/>/
/// <see cref="CalcularSimilaridade"/>). Sem foto, cai de volta para a heurística de cor
/// dominante + proporção da caixa, sem quebrar perfis já cadastrados sem foto.
/// Nenhuma biblioteca externa de visão computacional — mesmo racional já documentado em
/// RadarTorres.App.csproj. São aproximações deliberadas — ver limiares configuráveis em
/// <see cref="VisionSettings"/>, pensados para serem recalibrados com câmera e objeto reais.
/// </summary>
/// <remarks>
/// <para>
/// Três decisões aqui existem por causa de falhas concretas observadas em teste, e vale manter
/// o motivo registrado para não serem "simplificadas" de volta:
/// </para>
/// <list type="number">
/// <item>
/// A região do objeto dentro de cada foto de referência vem da marcação do usuário quando existe e,
/// sem ela, é escolhida pela COR cadastrada no perfil — nunca pela confiança do detector
/// (<see cref="EncontrarAreaDoObjeto"/>). Pegar a caixa de maior
/// score fazia o perfil guardar a assinatura da pessoa que segurava o objeto — o tiny-YOLOv3
/// pontua "person" muito acima de um objeto comum, e a classe COCO é ignorada por este pipeline.
/// </item>
/// <item>
/// A similaridade usa interseção de histograma matiz/saturação (ver <see cref="IndiceHistograma"/>),
/// não só diferença média de cor numa grade. A diferença média dividida por 255 comprimia
/// qualquer par de imagens reais na faixa 0,69–0,88, então nenhum limiar nessa escala conseguia
/// separar "é o alvo" de "não é".
/// </item>
/// <item>
/// Quando o detector não propõe nenhuma caixa com a cor do perfil, as regiões candidatas são
/// propostas por segmentação de cor (<see cref="PropostasPorCor"/>). O tiny-YOLOv3 só dispara
/// nas 80 classes COCO — sem esse caminho, qualquer alvo fora dessa lista era indetectável por
/// construção, por mais que os limiares fossem afrouxados.
/// </item>
/// </list>
/// </remarks>
public sealed class AlvoVisualService : IAlvoVisualService
{
    /// <summary>Lado da grade espacial comparada entre foto e candidato (8×8 células de cor média).</summary>
    private const int GradeAssinatura = 8;

    /// <summary>Lado da reamostragem de onde grade e histograma são extraídos de uma só vez. Múltiplo de <see cref="GradeAssinatura"/>.</summary>
    private const int AmostraLado = 32;

    private const int BytesPorPixel = 3;

    private const int MatizBins = 12;
    private const int SaturacaoBins = 4;

    /// <summary>Baldes do histograma: matiz × saturação, mais um balde final para pixels sem cor definida (cinza/branco/preto).</summary>
    private const int HistogramaBins = (MatizBins * SaturacaoBins) + 1;

    /// <summary>
    /// Diferença média de cor (0–255) a partir da qual a parcela espacial da similaridade zera.
    /// 128 e não 255 de propósito: dividir por 255 deixava duas imagens sem relação nenhuma já
    /// pontuando ~0,7, inutilizando a faixa útil do limiar.
    /// </summary>
    private const double EscalaDiferencaGrade = 128.0;

    /// <summary>
    /// Largura do quadro de trabalho: o quadro ao vivo é reduzido UMA vez por rodada para esta
    /// largura e tudo (segmentação, fração de cor, assinatura) é medido sobre ele.
    /// </summary>
    /// <remarks>
    /// Medido nesta máquina: contar a fração de cor de uma caixa em resolução cheia custava
    /// ~13 ms POR CANDIDATO, contra ~0,2 ms sobre o quadro reduzido — e a fração medida muda
    /// menos de 2 pontos percentuais (70,7% contra 72,0% no mesmo caderno). Com vários
    /// candidatos por quadro, é a diferença entre dezenas de milissegundos e praticamente nada.
    /// 320px ainda deixa um alvo que ocupe 1/3 do quadro com ~100px de lado, de sobra para uma
    /// assinatura 32×32.
    /// </remarks>
    private const int LarguraTrabalho = 320;

    /// <summary>Teto de regiões propostas por cor num quadro, para não inundar a comparação com respingos do fundo.</summary>
    private const int MaxRegioesPorCor = 5;

    /// <summary>
    /// Confiança atribuída a uma região proposta por cor. Abaixo de qualquer caixa real do
    /// detector de propósito: na ordenação de candidatos, proposta de cor é último recurso.
    /// </summary>
    private const double ScoreRegiaoPorCor = 0.2;

    private readonly IObjectDetectionService _detectionService;
    private readonly IPerfilAlvoService _perfilAlvoService;
    private readonly ILoggingService _logger;

    /// <summary>Chave + assinaturas numa única referência, trocada de uma vez (ver <see cref="ObterAssinaturasReferencia"/>).</summary>
    private sealed record CacheAssinaturas(string Chave, IReadOnlyList<AssinaturaVisual> Assinaturas);

    private CacheAssinaturas? _cacheAssinaturas;
    private readonly object _travaCache = new();

    public AlvoVisualService(IObjectDetectionService detectionService, IPerfilAlvoService perfilAlvoService, ILoggingService logger)
    {
        _detectionService = detectionService;
        _perfilAlvoService = perfilAlvoService;
        _logger = logger;
    }

    public AlvoVisualAvaliacao AvaliarFrame(Bitmap frame)
    {
        PerfilAlvo? perfil = _perfilAlvoService.PerfilAtivo;
        if (perfil is null)
        {
            return new AlvoVisualAvaliacao(0, 0, false, null, Array.Empty<AlvoVisualCandidato>(), TemposRodada.Zero);
        }

        VisionSettings config = AppConfig.Current.VisionSettings;
        long inicioRodada = Stopwatch.GetTimestamp();

        long marco = Stopwatch.GetTimestamp();
        using var trabalho = FrameTrabalho.Criar(frame);
        double preparoMs = Decorrido(marco);

        marco = Stopwatch.GetTimestamp();
        IReadOnlyList<AssinaturaVisual> assinaturasReferencia = ObterAssinaturasReferencia(perfil, config);
        double referenciaMs = Decorrido(marco);

        bool usandoFoto = assinaturasReferencia.Count > 0;

        IReadOnlyList<Candidato> candidatos = ObterCandidatos(
            frame, trabalho, perfil, config, out double segmentacaoMs, out double detectorMs, out bool detectorExecutou);

        long inicioComparacao = Stopwatch.GetTimestamp();

        var avaliados = new List<AlvoVisualCandidato>(candidatos.Count);
        AlvoVisualCandidato? melhorAceito = null;
        double melhorPontuacao = 0;

        foreach (Candidato candidato in candidatos)
        {
            Rectangle caixaOriginal = trabalho.ParaOriginal(candidato.Caixa);
            var deteccao = new VisionDetection(
                caixaOriginal.X, caixaOriginal.Y, caixaOriginal.Width, caixaOriginal.Height, candidato.Score);

            double pontuacao;
            bool bate;

            if (usandoFoto)
            {
                AssinaturaVisual assinaturaCandidata = ExtrairAssinatura(trabalho.Bitmap, candidato.Caixa, config);

                // Melhor similaridade entre TODAS as fotos do perfil: cada foto e o mesmo objeto
                // visto de outro angulo/iluminacao, entao basta UMA bater. Exigir media (ou
                // todas) faria a foto mais desfavoravel derrubar o reconhecimento inteiro --
                // justamente o oposto do motivo de haver varias.
                pontuacao = assinaturasReferencia.Max(a => CalcularSimilaridade(a, assinaturaCandidata, config));

                // A cor cadastrada entra como segunda prova mesmo havendo foto: é barata, já foi
                // informada pelo usuário e descarta de imediato o candidato que "parece" pela
                // distribuição de cor mas nem tem a cor do alvo em quantidade relevante.
                bate = pontuacao >= config.LimiarSimilaridadeFoto
                    && candidato.FracaoCor >= config.LimiarCorCandidato;
            }
            else
            {
                pontuacao = candidato.FracaoCor;
                bate = candidato.FracaoCor >= config.LimiarPreenchimentoMinimo
                    && ClassificarFormato(deteccao, candidato.FracaoCor, config) == perfil.Formato;
            }

            melhorPontuacao = Math.Max(melhorPontuacao, pontuacao);

            var avaliado = new AlvoVisualCandidato(deteccao, pontuacao, candidato.FracaoCor, bate, candidato.Origem);
            avaliados.Add(avaliado);

            // Avalia TODOS os candidatos e fica com o de maior pontuação, em vez de parar no
            // primeiro que passa: além de escolher de fato o melhor alvo quando há mais de um
            // objeto parecido em cena, é o que permite desenhar na tela tudo o que foi
            // considerado — com parada antecipada, o resto do quadro ficaria invisível.
            if (bate && (melhorAceito is null || pontuacao > melhorAceito.Pontuacao)) melhorAceito = avaliado;
        }

        AlvoVisualResultado? resultado = melhorAceito is null
            ? null
            : new AlvoVisualResultado(
                perfil,
                melhorAceito.Deteccao,
                EstimarDistanciaMetros(melhorAceito.Deteccao, perfil.MaiorDimensaoCm, frame.Width, config.CameraFovHorizontalGraus));

        var tempos = new TemposRodada(
            Decorrido(inicioRodada),
            preparoMs,
            referenciaMs,
            segmentacaoMs,
            detectorMs,
            Decorrido(inicioComparacao),
            detectorExecutou);

        return new AlvoVisualAvaliacao(avaliados.Count, melhorPontuacao, usandoFoto, resultado, avaliados, tempos);
    }

    private static double Decorrido(long desde) => Stopwatch.GetElapsedTime(desde).TotalMilliseconds;

    // ---------------------------------------------------------------- Quadro de trabalho

    /// <summary>
    /// O quadro reduzido uma única vez por rodada, com as escalas para converter caixas entre
    /// ele e o quadro original. Existe para a redução acontecer UMA vez e ser reaproveitada por
    /// segmentação, fração de cor e assinatura — antes cada etapa reduzia (ou lia em resolução
    /// cheia) por conta própria, repetindo o trabalho mais caro da rodada várias vezes.
    /// </summary>
    private sealed class FrameTrabalho : IDisposable
    {
        public Bitmap Bitmap { get; }

        private readonly double _escalaX;
        private readonly double _escalaY;

        private FrameTrabalho(Bitmap bitmap, double escalaX, double escalaY)
        {
            Bitmap = bitmap;
            _escalaX = escalaX;
            _escalaY = escalaY;
        }

        public static FrameTrabalho Criar(Bitmap frame)
        {
            int largura = Math.Min(LarguraTrabalho, frame.Width);
            int altura = Math.Max(1, (int)Math.Round(frame.Height * (largura / (double)frame.Width)));

            Bitmap reduzido = new(largura, altura, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(reduzido))
            {
                // Bilinear simples: a imagem está encolhendo para ~1/4 da largura e o destino é
                // máscara de cor e histograma, não exibição — reamostragem de alta qualidade
                // custa caro e não muda o veredito.
                g.InterpolationMode = InterpolationMode.Bilinear;
                g.DrawImage(frame, 0, 0, largura, altura);
            }

            return new FrameTrabalho(reduzido, frame.Width / (double)largura, frame.Height / (double)altura);
        }

        public Rectangle ParaOriginal(Rectangle caixa) => new(
            (int)(caixa.X * _escalaX),
            (int)(caixa.Y * _escalaY),
            Math.Max(1, (int)(caixa.Width * _escalaX)),
            Math.Max(1, (int)(caixa.Height * _escalaY)));

        public Rectangle ParaReduzido(Rectangle caixa) => new(
            (int)(caixa.X / _escalaX),
            (int)(caixa.Y / _escalaY),
            Math.Max(1, (int)(caixa.Width / _escalaX)),
            Math.Max(1, (int)(caixa.Height / _escalaY)));

        public void Dispose() => Bitmap.Dispose();
    }

    // ---------------------------------------------------------------- Candidatos

    /// <summary>Caixa candidata em coordenadas do quadro de trabalho, já com a fração de cor medida.</summary>
    private sealed record Candidato(Rectangle Caixa, double FracaoCor, double Score, OrigemCandidato Origem);

    /// <summary>
    /// Regiões a avaliar no quadro. A segmentação por cor roda PRIMEIRO e, quando acha uma
    /// mancha com a cor do perfil, o detector ONNX nem chega a rodar.
    /// </summary>
    /// <remarks>
    /// A ordem é invertida em relação ao que parece natural (detector primeiro, cor como
    /// resgate) por custo medido nesta máquina: uma rodada que resolve pela cor leva ~27 ms,
    /// contra ~480 ms quando o tiny-YOLOv3 roda (preparo da entrada + inferência). Como o alvo é
    /// cadastrado COM uma cor, o caminho barato resolve o caso normal e o detector fica para o
    /// caso em que a cor não encontra nada — que é exatamente quando a caixa de um objeto
    /// reconhecido vale o tempo gasto.
    /// </remarks>
    private IReadOnlyList<Candidato> ObterCandidatos(
        Bitmap frame,
        FrameTrabalho trabalho,
        PerfilAlvo perfil,
        VisionSettings config,
        out double segmentacaoMs,
        out double detectorMs,
        out bool detectorExecutou)
    {
        long marco = Stopwatch.GetTimestamp();

        List<Candidato> porCor = SegmentarPorCor(trabalho.Bitmap, perfil.Cor, config)
            .Select(r => new Candidato(r, CalcularFracaoCor(trabalho.Bitmap, r, perfil.Cor, config), ScoreRegiaoPorCor, OrigemCandidato.Cor))
            .ToList();

        segmentacaoMs = Decorrido(marco);

        if (porCor.Any(c => c.FracaoCor >= config.LimiarCorCandidato))
        {
            detectorMs = 0;
            detectorExecutou = false;
            return porCor.OrderByDescending(c => c.FracaoCor).ToList();
        }

        detectorExecutou = true;
        marco = Stopwatch.GetTimestamp();

        List<Candidato> doDetector = _detectionService.Detect(frame)
            .Select(d =>
            {
                Rectangle reduzida = trabalho.ParaReduzido(Caixa(d));
                return new Candidato(reduzida, CalcularFracaoCor(trabalho.Bitmap, reduzida, perfil.Cor, config), d.Score, OrigemCandidato.Detector);
            })
            .ToList();

        detectorMs = Decorrido(marco);

        // Sem mancha de cor convincente, a fração de cor continua sendo o melhor sinal para ordenar.
        return porCor.Concat(doDetector).OrderByDescending(c => c.FracaoCor).ToList();
    }

    private static Rectangle Caixa(VisionDetection deteccao) =>
        new(deteccao.BoxXPx, deteccao.BoxYPx, deteccao.BoxWidthPx, deteccao.BoxHeightPx);

    /// <summary>
    /// Propõe regiões candidatas segmentando a cor do perfil no quadro de trabalho: marca os
    /// pixels da cor e agrupa os vizinhos em componentes conectados, devolvendo a caixa de cada
    /// mancha grande o bastante. É o que permite travar num alvo que o modelo COCO nunca viu.
    /// Devolve caixas em coordenadas do quadro de trabalho.
    /// </summary>
    private static IReadOnlyList<Rectangle> SegmentarPorCor(Bitmap trabalho, CorAlvo cor, VisionSettings config)
    {
        int largura = trabalho.Width;
        int altura = trabalho.Height;
        var mascara = new bool[largura * altura];

        BitmapData data = trabalho.LockBits(
            new Rectangle(0, 0, largura, altura), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var pixels = new byte[data.Stride * altura];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

            for (int y = 0; y < altura; y++)
            {
                int baseLinha = y * data.Stride;
                for (int x = 0; x < largura; x++)
                {
                    int offset = baseLinha + (x * BytesPorPixel);
                    mascara[(y * largura) + x] =
                        CorDeterminada(pixels[offset + 2], pixels[offset + 1], pixels[offset], config) == cor;
                }
            }
        }
        finally
        {
            trabalho.UnlockBits(data);
        }

        int areaMinima = Math.Max(4, (int)(largura * altura * config.AreaMinimaRegiaoCor));
        return ComponentesConectados(mascara, largura, altura, areaMinima);
    }

    /// <summary>
    /// Agrupa pixels marcados em componentes conectados (vizinhança 4) e devolve a caixa
    /// envolvente dos maiores. Varredura iterativa com pilha explícita, não recursão — uma
    /// mancha de cor grande chega facilmente a milhares de pixels e estouraria a pilha da thread.
    /// </summary>
    private static List<Rectangle> ComponentesConectados(bool[] mascara, int largura, int altura, int areaMinima)
    {
        var visitado = new bool[mascara.Length];
        var pilha = new Stack<int>();
        var encontrados = new List<(Rectangle Caixa, int Area)>();

        for (int inicio = 0; inicio < mascara.Length; inicio++)
        {
            if (!mascara[inicio] || visitado[inicio]) continue;

            int minX = largura, maxX = -1, minY = altura, maxY = -1, area = 0;

            visitado[inicio] = true;
            pilha.Push(inicio);

            while (pilha.Count > 0)
            {
                int indice = pilha.Pop();
                int x = indice % largura;
                int y = indice / largura;

                area++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;

                if (x > 0) Empilhar(indice - 1);
                if (x < largura - 1) Empilhar(indice + 1);
                if (y > 0) Empilhar(indice - largura);
                if (y < altura - 1) Empilhar(indice + largura);
            }

            if (area >= areaMinima)
            {
                encontrados.Add((new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1), area));
            }
        }

        return encontrados
            .OrderByDescending(e => e.Area)
            .Take(MaxRegioesPorCor)
            .Select(e => e.Caixa)
            .ToList();

        void Empilhar(int vizinho)
        {
            if (!mascara[vizinho] || visitado[vizinho]) return;
            visitado[vizinho] = true;
            pilha.Push(vizinho);
        }
    }

    // ---------------------------------------------------------------- Comparação com a foto de referência

    /// <summary>Grade espacial de cor média + histograma matiz/saturação de uma região.</summary>
    private sealed record AssinaturaVisual(float[] Grade, float[] Histograma);

    /// <summary>
    /// <para>
    /// Uma assinatura visual por fotografia do perfil, cacheadas em conjunto por (cor + lista de
    /// caminhos + recorte de cada um) — recarregar e reprocessar as fotos a cada quadro seria
    /// trabalho repetido à toa, já que elas não mudam enquanto o perfil continuar o mesmo. Cor e
    /// recortes entram na chave porque são eles que decidem qual região de cada foto vira
    /// assinatura (ver <see cref="EncontrarAreaDoObjeto"/>): sem isso, marcar outro objeto —
    /// ou adicionar/remover uma foto na edição do perfil — continuaria usando as assinaturas
    /// antigas.
    /// </para>
    /// <para>
    /// <b>Concorrência:</b> desde que a detecção roda em paralelo (card de Monitoramento + os
    /// quatro painéis da tela "Câmeras"), até cinco threads chamam este método ao mesmo tempo.
    /// Chave e assinaturas vivem numa única referência trocada de uma vez — com dois campos
    /// separados, uma thread podia enxergar a chave NOVA junto da lista VELHA e comparar o
    /// quadro ao vivo com a assinatura do perfil anterior. A reconstrução acontece sob trava
    /// para não processar as mesmas fotos cinco vezes (nem repetir cinco vezes o mesmo aviso).
    /// </para>
    /// </summary>
    private IReadOnlyList<AssinaturaVisual> ObterAssinaturasReferencia(PerfilAlvo perfil, VisionSettings config)
    {
        List<FotoReferenciaAlvo> fotos = perfil.Fotos.Where(f => File.Exists(f.Caminho)).ToList();
        if (fotos.Count == 0) return Array.Empty<AssinaturaVisual>();

        string chave = $"{perfil.Cor}|" + string.Join(";", fotos.Select(f => $"{f.Caminho}@{f.Recorte}"));

        // Leitura sem trava: a referência é trocada de uma vez só, então ou se enxerga o cache
        // antigo inteiro, ou o novo inteiro — nunca a chave nova com a lista velha.
        if (_cacheAssinaturas is { } cache && cache.Chave == chave) return cache.Assinaturas;

        lock (_travaCache)
        {
            // Outra câmera pode ter reconstruído enquanto esta esperava a trava.
            if (_cacheAssinaturas is { } atual && atual.Chave == chave) return atual.Assinaturas;

            var assinaturas = new List<AssinaturaVisual>(fotos.Count);

            foreach (FotoReferenciaAlvo foto in fotos)
            {
                try
                {
                    using var referencia = new Bitmap(foto.Caminho);
                    Rectangle area = EncontrarAreaDoObjeto(referencia, perfil, foto, config);
                    assinaturas.Add(ExtrairAssinatura(referencia, area, config));
                }
                catch (Exception ex)
                {
                    // Uma foto ilegível não invalida as outras: segue com as que deram certo, e o
                    // aviso diz exatamente qual arquivo revisar.
                    _logger.Warning(
                        $"Não foi possível processar a fotografia \"{Path.GetFileName(foto.Caminho)}\" do perfil " +
                        $"\"{perfil.Nome}\" para comparação: {ex.Message}");
                }
            }

            _cacheAssinaturas = new CacheAssinaturas(chave, assinaturas);
            return assinaturas;
        }
    }

    /// <summary>
    /// Acha o objeto DENTRO da foto de referência para não comparar o candidato contra a foto
    /// inteira (que inclui fundo, e normalmente a pessoa que segurava o objeto).
    /// </summary>
    /// <remarks>
    /// Com <see cref="PerfilAlvo.RecorteFotoReferencia"/> preenchido, a região é a que o usuário
    /// marcou sobre a foto e nada mais é calculado. O resto deste método é o caminho automático,
    /// usado só por perfis sem marcação: a escolha aí é pela COR cadastrada no perfil, não pela
    /// confiança do detector. Pegar a caixa
    /// de maior score parece o óbvio e está errado aqui: o tiny-YOLOv3 pontua "person" bem acima
    /// de um objeto comum, e como este pipeline ignora a classe COCO, a foto de um objeto
    /// segurado na mão virava a assinatura do rosto/roupa de quem segurava — todo o
    /// reconhecimento passava a perseguir o alvo errado. Sem nenhuma região com a cor do perfil,
    /// assume que o usuário centralizou o objeto ao tirar a foto (a tela "Perfis de Alvo" mostra
    /// o preview ao vivo exatamente para isso) e usa um recorte central.
    /// </remarks>
    private Rectangle EncontrarAreaDoObjeto(Bitmap referencia, PerfilAlvo perfil, FotoReferenciaAlvo foto, VisionSettings config)
    {
        // Se o usuário marcou o objeto na foto, não há o que adivinhar: é exatamente esta a
        // região a observar. Toda a busca abaixo existe só para quem não marcou nada.
        if (foto.Recorte is { EhValido: true } recorte)
        {
            Rectangle marcada = Rectangle.Intersect(
                new Rectangle(recorte.X, recorte.Y, recorte.Largura, recorte.Altura),
                new Rectangle(0, 0, referencia.Width, referencia.Height));

            if (marcada.Width > 0 && marcada.Height > 0)
            {
                _logger.Info(
                    $"Perfil \"{perfil.Nome}\": usando o objeto marcado pelo usuário em " +
                    $"\"{Path.GetFileName(foto.Caminho)}\" ({marcada.Width}×{marcada.Height}px em x={marcada.X}, y={marcada.Y}).");
                return marcada;
            }

            // Recorte fora da foto (foto trocada por fora, por exemplo) — segue para a busca
            // automática em vez de devolver uma região vazia.
            _logger.Warning(
                $"Perfil \"{perfil.Nome}\": a marcação do objeto não cai dentro de \"{Path.GetFileName(foto.Caminho)}\". " +
                "Localizando o objeto automaticamente — remarque o objeto na tela \"Perfis de Alvo\".");
        }

        using var trabalho = FrameTrabalho.Criar(referencia);

        IEnumerable<Rectangle> candidatas = SegmentarPorCor(trabalho.Bitmap, perfil.Cor, config)
            .Select(trabalho.ParaOriginal)
            .Concat(_detectionService.Detect(referencia).Select(Caixa));

        Rectangle melhor = Rectangle.Empty;
        double melhorFracao = 0;

        foreach (Rectangle caixa in candidatas)
        {
            // Esta é a única medição da classe feita em resolução cheia: roda uma vez por perfil
            // (o resultado fica cacheado), não por quadro, então a precisão vale o custo.
            double fracao = CalcularFracaoCor(referencia, caixa, perfil.Cor, config);
            if (fracao <= melhorFracao) continue;

            melhorFracao = fracao;
            melhor = caixa;
        }

        if (melhorFracao >= config.LimiarCorCandidato)
        {
            _logger.Info(
                $"Perfil \"{perfil.Nome}\": objeto localizado em \"{Path.GetFileName(foto.Caminho)}\" em {melhor.Width}×{melhor.Height}px " +
                $"({melhorFracao:P0} de {perfil.Cor}).");
            return melhor;
        }

        _logger.Warning(
            $"Perfil \"{perfil.Nome}\": nenhuma região de \"{Path.GetFileName(foto.Caminho)}\" tem {perfil.Cor} suficiente " +
            $"(melhor: {melhorFracao:P0}). Usando o centro da foto — confira se a cor cadastrada bate com o objeto.");

        int largura = (int)(referencia.Width * 0.6);
        int altura = (int)(referencia.Height * 0.6);
        return new Rectangle((referencia.Width - largura) / 2, (referencia.Height - altura) / 2, largura, altura);
    }

    /// <summary>
    /// Reduz a região a uma amostra fixa <see cref="AmostraLado"/>² e extrai dela, de uma só
    /// passada, a grade <see cref="GradeAssinatura"/>×<see cref="GradeAssinatura"/> de cor média
    /// e o histograma matiz/saturação. Usa <see cref="Graphics.DrawImage"/> (não
    /// <c>LockBits</c> direto na região original) de propósito — já reamostra para o destino
    /// sempre do tamanho exato da amostra, sem o risco de ler além do buffer de um recorte que
    /// já causou um crash fatal nesta mesma classe (ver <see cref="CalcularFracaoCor"/>).
    /// </summary>
    private static AssinaturaVisual ExtrairAssinatura(Bitmap imagem, Rectangle area, VisionSettings config)
    {
        Rectangle recorteOrigem = Rectangle.Intersect(area, new Rectangle(0, 0, imagem.Width, imagem.Height));

        var grade = new float[GradeAssinatura * GradeAssinatura * BytesPorPixel];
        var histograma = new float[HistogramaBins];

        if (recorteOrigem.Width <= 0 || recorteOrigem.Height <= 0) return new AssinaturaVisual(grade, histograma);

        using Bitmap amostra = new(AmostraLado, AmostraLado, PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(amostra))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.DrawImage(imagem, new Rectangle(0, 0, AmostraLado, AmostraLado), recorteOrigem, GraphicsUnit.Pixel);
        }

        BitmapData data = amostra.LockBits(
            new Rectangle(0, 0, AmostraLado, AmostraLado), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var pixels = new byte[data.Stride * AmostraLado];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

            int ladoCelula = AmostraLado / GradeAssinatura;

            for (int y = 0; y < AmostraLado; y++)
            {
                int baseLinha = y * data.Stride;

                for (int x = 0; x < AmostraLado; x++)
                {
                    int offset = baseLinha + (x * BytesPorPixel);
                    byte b = pixels[offset];
                    byte g = pixels[offset + 1];
                    byte r = pixels[offset + 2];

                    int celula = (((y / ladoCelula) * GradeAssinatura) + (x / ladoCelula)) * BytesPorPixel;
                    grade[celula] += b;
                    grade[celula + 1] += g;
                    grade[celula + 2] += r;

                    histograma[IndiceHistograma(r, g, b, config)]++;
                }
            }

            int pixelsPorCelula = ladoCelula * ladoCelula;
            for (int i = 0; i < grade.Length; i++) grade[i] /= pixelsPorCelula;

            const int totalPixels = AmostraLado * AmostraLado;
            for (int i = 0; i < histograma.Length; i++) histograma[i] /= totalPixels;
        }
        finally
        {
            amostra.UnlockBits(data);
        }

        return new AssinaturaVisual(grade, histograma);
    }

    /// <summary>
    /// Balde do histograma de um pixel: matiz × saturação para pixel colorido, ou o balde final
    /// (sem cor definida) para cinza/branco/preto. Guardar os sem-cor num balde próprio em vez
    /// de descartá-los mantém a massa do histograma somando 1, de modo que a interseção entre
    /// dois histogramas continua sendo uma fração honesta de "quantos pixels combinam".
    /// </summary>
    private static int IndiceHistograma(byte r, byte g, byte b, VisionSettings config)
    {
        (double matiz, double saturacao, double valor) = ParaHsv(r, g, b);

        if (saturacao < config.SaturacaoMinimaCor || valor < config.BrilhoMinimoCor) return HistogramaBins - 1;

        int binMatiz = Math.Min(MatizBins - 1, (int)(matiz / 360.0 * MatizBins));
        int binSaturacao = Math.Min(SaturacaoBins - 1, (int)(saturacao * SaturacaoBins));
        return (binMatiz * SaturacaoBins) + binSaturacao;
    }

    /// <summary>
    /// Similaridade 0–1 entre duas assinaturas: interseção dos histogramas de cor (não depende
    /// de posição nem de rotação do objeto dentro da caixa) combinada com a diferença da grade
    /// espacial (preserva alguma noção de layout, para não casar com qualquer objeto da mesma cor).
    /// </summary>
    /// <remarks>
    /// Ambas as parcelas usam a faixa 0–1 de verdade. A versão anterior era só
    /// <c>1 − diferençaMédia/255</c>, e como a diferença média entre duas fotos reais quaisquer
    /// fica tipicamente entre 30 e 80, o resultado vivia preso entre 0,69 e 0,88: nenhum limiar
    /// nessa escala separava o alvo do resto da cena.
    /// </remarks>
    private static double CalcularSimilaridade(AssinaturaVisual referencia, AssinaturaVisual candidata, VisionSettings config)
    {
        double interseccao = 0;
        for (int i = 0; i < referencia.Histograma.Length; i++)
        {
            interseccao += Math.Min(referencia.Histograma[i], candidata.Histograma[i]);
        }

        double somaDiferencas = 0;
        for (int i = 0; i < referencia.Grade.Length; i++)
        {
            somaDiferencas += Math.Abs(referencia.Grade[i] - candidata.Grade[i]);
        }

        double diferencaMedia = somaDiferencas / referencia.Grade.Length;
        double similaridadeGrade = Math.Max(0, 1.0 - (diferencaMedia / EscalaDiferencaGrade));

        double peso = Math.Clamp(config.PesoHistogramaCor, 0, 1);
        return (peso * interseccao) + ((1 - peso) * similaridadeGrade);
    }

    // ---------------------------------------------------------------- Cor

    private static FormatoAlvo ClassificarFormato(VisionDetection deteccao, double fracaoCor, VisionSettings config)
    {
        double maior = Math.Max(deteccao.BoxWidthPx, deteccao.BoxHeightPx);
        double menor = Math.Max(1, Math.Min(deteccao.BoxWidthPx, deteccao.BoxHeightPx));
        double aspecto = maior / menor;

        if (aspecto >= config.LimiarAspectoRetangular) return FormatoAlvo.Retangular;

        return fracaoCor >= config.LimiarPreenchimentoQuadrado ? FormatoAlvo.Quadrado : FormatoAlvo.Redondo;
    }

    private static double CalcularFracaoCor(Bitmap frame, Rectangle caixa, CorAlvo cor, VisionSettings config)
    {
        Rectangle area = Rectangle.Intersect(caixa, new Rectangle(0, 0, frame.Width, frame.Height));

        if (area.Width <= 0 || area.Height <= 0) return 0;

        BitmapData data = frame.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            // IMPORTANTE: quando o PixelFormat pedido já bate com o do Bitmap de origem (é o
            // caso do quadro ao vivo), LockBits NÃO copia dados — Scan0/Stride continuam
            // apontando para o buffer ORIGINAL da imagem inteira (não um buffer novo só do
            // recorte), com Scan0 já deslocado até o canto do recorte. Por isso cada linha só
            // pode copiar os bytes realmente pertencentes a ela (area.Width * BytesPorPixel) —
            // copiar data.Stride inteiro (a largura da imagem INTEIRA) a partir de um recorte
            // lia além do fim do buffer alocado na última linha da imagem, causando um crash
            // fatal da CLR (acesso de memória inválido), não uma exceção .NET normal.
            int totalPixels = area.Width * area.Height;
            int pixelsNaCor = 0;
            int bytesLinha = area.Width * BytesPorPixel;
            var linha = new byte[bytesLinha];

            for (int y = 0; y < area.Height; y++)
            {
                Marshal.Copy(data.Scan0 + (y * data.Stride), linha, 0, bytesLinha);

                for (int x = 0; x < area.Width; x++)
                {
                    int offset = x * BytesPorPixel;
                    if (CorDeterminada(linha[offset + 2], linha[offset + 1], linha[offset], config) == cor) pixelsNaCor++;
                }
            }

            return (double)pixelsNaCor / totalPixels;
        }
        finally
        {
            frame.UnlockBits(data);
        }
    }

    /// <summary>
    /// Cor cadastrável do pixel, ou <c>null</c> quando ele não tem cor definida (cinza, branco,
    /// preto, sombra) ou tem um matiz fora das três cores do cadastro (amarelo, ciano, magenta).
    /// </summary>
    /// <remarks>
    /// Substitui uma classificação anterior que forçava TODO pixel a ser vermelho, verde ou azul
    /// (<c>r >= g &amp;&amp; r >= b</c> → vermelho). Parede clara, pele e camiseta branca caíam
    /// todas em "vermelho", então a fração de cor de qualquer região chegava perto de 1 e os
    /// limiares de preenchimento não filtravam nada.
    /// </remarks>
    private static CorAlvo? CorDeterminada(byte r, byte g, byte b, VisionSettings config)
    {
        (double matiz, double saturacao, double valor) = ParaHsv(r, g, b);

        if (saturacao < config.SaturacaoMinimaCor || valor < config.BrilhoMinimoCor) return null;

        if (matiz >= 330 || matiz < 20) return CorAlvo.Vermelho;
        if (matiz >= 70 && matiz < 170) return CorAlvo.Verde;
        if (matiz >= 180 && matiz < 270) return CorAlvo.Azul;

        return null;
    }

    /// <summary>Converte RGB em matiz (0–360), saturação (0–1) e valor/brilho (0–1).</summary>
    private static (double Matiz, double Saturacao, double Valor) ParaHsv(byte r, byte g, byte b)
    {
        double rn = r / 255.0, gn = g / 255.0, bn = b / 255.0;

        double max = Math.Max(rn, Math.Max(gn, bn));
        double min = Math.Min(rn, Math.Min(gn, bn));
        double delta = max - min;

        double matiz;
        if (delta <= 0) matiz = 0;
        else if (max == rn) matiz = 60 * ((((gn - bn) / delta) % 6 + 6) % 6);
        else if (max == gn) matiz = 60 * (((bn - rn) / delta) + 2);
        else matiz = 60 * (((rn - gn) / delta) + 4);

        double saturacao = max <= 0 ? 0 : delta / max;
        return (matiz, saturacao, max);
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
