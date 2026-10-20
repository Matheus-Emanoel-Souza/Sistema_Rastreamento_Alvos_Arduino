using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="ITermosDeUsoService"/>: lê o arquivo oficial embutido, classifica
/// o texto em blocos e gera o PDF a partir desses MESMOS blocos.
/// </summary>
/// <remarks>
/// <para>
/// O documento é texto puro com uma estrutura regular (título entre réguas de "=", cláusulas
/// entre réguas de "-", parágrafos numerados "N.N.", alíneas "a)" e itens "[X]" no resumo
/// final), e as linhas vêm quebradas em ~78 colunas para caber na página de licença do
/// instalador. O parser desfaz essa quebra e devolve parágrafos inteiros — é o que permite a
/// tela e o PDF reaproveitarem a largura disponível em vez de herdarem a quebra do arquivo.
/// </para>
/// <para>
/// A leitura e a análise acontecem uma única vez (<see cref="_documento"/>): o conteúdo está
/// embutido no executável e não muda em tempo de execução.
/// </para>
/// </remarks>
public sealed class TermosDeUsoService : ITermosDeUsoService
{
    /// <summary>Nome lógico do recurso embutido (definido no .csproj, apontando para installer\TERMOS_DE_USO.txt).</summary>
    private const string RecursoTermos = "RadarTorres.App.Resources.TERMOS_DE_USO.txt";

    private static readonly Regex RegexSecao = new(@"^(\d+)\.\s+(\S.*)$", RegexOptions.Compiled);
    private static readonly Regex RegexParagrafoNumerado = new(@"^(\d+\.\d+\.)\s+(\S.*)$", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex RegexAlinea = new(@"^\s{2,}([a-z]\))\s+(\S.*)$", RegexOptions.Compiled);
    private static readonly Regex RegexItemResumo = new(@"^\s{1,}(\[[xX ]\])\s+(\S.*)$", RegexOptions.Compiled);

    private readonly ILoggingService _logger;
    private DocumentoTermos? _documento;

    public TermosDeUsoService(ILoggingService logger)
    {
        _logger = logger;
    }

    public string NomeArquivoSugerido => "RadarTorres_Termos_de_Uso.pdf";

    public DocumentoTermos Obter() => _documento ??= Analisar(LerTextoOficial());

    // ---------------------------------------------------------------- Leitura e análise

    private string LerTextoOficial()
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(RecursoTermos);

        if (stream is null)
        {
            // Não deveria acontecer (o recurso é embutido em tempo de compilação), mas a tela
            // precisa continuar abrindo e dizendo o que houve em vez de quebrar.
            _logger.Error($"Recurso dos Termos de Uso não encontrado no executável ({RecursoTermos}).");
            return "Não foi possível carregar os Termos de Uso desta instalação.";
        }

        using var leitor = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return leitor.ReadToEnd();
    }

    /// <summary>Converte o texto oficial na lista de blocos exibíveis.</summary>
    internal static DocumentoTermos Analisar(string textoIntegral)
    {
        string[] linhas = textoIntegral.Replace("\r\n", "\n").Split('\n');

        var blocos = new List<BlocoTermos>();
        var acumulado = new List<string>();

        string titulo = "Termos de Uso";
        string versao = string.Empty;
        bool tituloDefinido = false;
        bool aguardandoSecao = false;

        foreach (string linhaBruta in linhas)
        {
            string linha = linhaBruta.TrimEnd();

            if (EhRegua(linha))
            {
                Fechar();
                // Régua de "-" anuncia um cabeçalho de cláusula na linha seguinte; a de "=" só
                // emoldura o título do documento.
                aguardandoSecao = linha.TrimStart().StartsWith('-');
                continue;
            }

            if (linha.Trim().Length == 0)
            {
                Fechar();
                continue;
            }

            // Alínea e item do resumo são blocos de uma linha lógica própria: fecham o que vinha
            // antes para não serem absorvidos pelo parágrafo anterior.
            if (RegexAlinea.IsMatch(linhaBruta) || RegexItemResumo.IsMatch(linhaBruta))
            {
                if (acumulado.Count > 0 && !EhContinuacaoDeItem(linhaBruta)) Fechar();
            }

            acumulado.Add(linhaBruta);
        }

        Fechar();

        return new DocumentoTermos(titulo, versao, textoIntegral, blocos);

        void Fechar()
        {
            if (acumulado.Count == 0) return;

            string primeira = acumulado[0];
            string texto = string.Join(" ", acumulado.Select(l => l.Trim())).Trim();
            acumulado.Clear();

            if (!tituloDefinido)
            {
                titulo = texto;
                tituloDefinido = true;
                blocos.Add(new TituloTermos(texto));
                return;
            }

            if (texto.StartsWith("Versão ", StringComparison.OrdinalIgnoreCase))
            {
                versao = texto;
                blocos.Add(new VersaoTermos(texto));
                return;
            }

            if (aguardandoSecao && RegexSecao.Match(texto) is { Success: true } secao)
            {
                aguardandoSecao = false;
                blocos.Add(new SecaoTermos(secao.Groups[1].Value, secao.Groups[2].Value));
                return;
            }

            if (RegexItemResumo.Match(primeira) is { Success: true } item)
            {
                blocos.Add(new ItemListaTermos(item.Groups[1].Value, RemoverMarcador(texto, item.Groups[1].Value)));
                return;
            }

            if (RegexAlinea.Match(primeira) is { Success: true } alinea)
            {
                blocos.Add(new ItemListaTermos(alinea.Groups[1].Value, RemoverMarcador(texto, alinea.Groups[1].Value)));
                return;
            }

            if (RegexParagrafoNumerado.Match(texto) is { Success: true } numerado)
            {
                blocos.Add(new ParagrafoTermos(numerado.Groups[1].Value, numerado.Groups[2].Value));
                return;
            }

            // Linha curta, inteiramente em maiúsculas: é um aviso em destaque do documento
            // (ex.: "LEIA ATENTAMENTE ANTES DE INSTALAR OU UTILIZAR.").
            bool destaque = texto.Length <= 120 && texto == texto.ToUpperInvariant() && texto.Any(char.IsLetter);
            blocos.Add(new ParagrafoTermos(null, texto, destaque));
        }

        bool EhContinuacaoDeItem(string linha) =>
            acumulado.Count > 0
            && (RegexAlinea.IsMatch(acumulado[0]) || RegexItemResumo.IsMatch(acumulado[0]))
            && !RegexAlinea.IsMatch(linha)
            && !RegexItemResumo.IsMatch(linha);
    }

    private static bool EhRegua(string linha)
    {
        string conteudo = linha.Trim();
        return conteudo.Length >= 10
            && (conteudo.All(c => c == '=') || conteudo.All(c => c == '-'));
    }

    private static string RemoverMarcador(string texto, string marcador)
    {
        string semMarcador = texto.TrimStart();
        return semMarcador.StartsWith(marcador, StringComparison.Ordinal)
            ? semMarcador[marcador.Length..].TrimStart()
            : semMarcador;
    }

    // ---------------------------------------------------------------- PDF

    private const double MargemEsquerda = 56;
    private const double MargemDireita = 56;
    private const double MargemSuperior = 54;
    private const double MargemInferior = 56;

    /// <remarks>
    /// Gerado com PDFsharp, que o projeto já usa para exportar "Objetos Detectados" — nenhuma
    /// dependência nova. O texto é quebrado em linhas aqui (e não com <c>XTextFormatter</c>)
    /// porque é preciso saber a altura consumida a cada linha para decidir a quebra de página no
    /// lugar certo; o formatador desenha, mas não informa o quanto ocupou.
    /// </remarks>
    public void ExportarPdf(string caminhoArquivo)
    {
        DocumentoTermos termos = Obter();

        // Sem isto o PDFsharp 6 lança ao criar a primeira XFont (ver ResolvedorFontesPdf).
        ResolvedorFontesPdf.Registrar();

        var documento = new PdfDocument();
        documento.Info.Title = termos.Titulo;
        documento.Info.Subject = termos.Versao;
        documento.Info.Author = "RadarTorres";

        var fonteTitulo = new XFont("Verdana", 16, XFontStyleEx.Bold);
        var fonteVersao = new XFont("Verdana", 9, XFontStyleEx.Italic);
        var fonteSecao = new XFont("Verdana", 11, XFontStyleEx.Bold);
        var fonteNumero = new XFont("Verdana", 9, XFontStyleEx.Bold);
        var fonteCorpo = new XFont("Verdana", 9, XFontStyleEx.Regular);
        var fonteRodape = new XFont("Verdana", 8, XFontStyleEx.Regular);

        PdfPage pagina = null!;
        XGraphics? gfx = null;
        double y = 0;
        double larguraUtil = 0;

        NovaPagina();

        foreach (BlocoTermos bloco in termos.Blocos)
        {
            switch (bloco)
            {
                case TituloTermos titulo:
                    Desenhar(titulo.Texto, fonteTitulo, XBrushes.Black, 0, 22, espacoAntes: 0, espacoDepois: 6);
                    break;

                case VersaoTermos versao:
                    Desenhar(versao.Texto, fonteVersao, XBrushes.Gray, 0, 13, espacoAntes: 0, espacoDepois: 14);
                    break;

                case SecaoTermos secao:
                    // Cabeçalho de cláusula nunca fica órfão no pé da página.
                    GarantirEspaco(60);
                    Desenhar($"{secao.Numero}. {secao.Titulo}", fonteSecao, XBrushes.Black, 0, 15, espacoAntes: 14, espacoDepois: 8);
                    break;

                case ItemListaTermos item:
                    Desenhar($"{item.Marcador} {item.Texto}", fonteCorpo, XBrushes.Black, 18, 13, espacoAntes: 0, espacoDepois: 6);
                    break;

                case ParagrafoTermos paragrafo:
                    string texto = paragrafo.Numero is null ? paragrafo.Texto : $"{paragrafo.Numero} {paragrafo.Texto}";
                    XFont fonte = paragrafo.Destaque ? fonteNumero : fonteCorpo;
                    Desenhar(texto, fonte, XBrushes.Black, 0, 13, espacoAntes: 0, espacoDepois: 8);
                    break;
            }
        }

        // O passe de rodapés abre um XGraphics novo em cada página; o PDFsharp recusa isso
        // enquanto o anterior da mesma página não tiver sido liberado.
        gfx?.Dispose();

        DesenharRodapes(documento, fonteRodape, termos);
        documento.Save(caminhoArquivo);

        void NovaPagina()
        {
            gfx?.Dispose();
            pagina = documento.AddPage();
            gfx = XGraphics.FromPdfPage(pagina);
            larguraUtil = pagina.Width.Point - MargemEsquerda - MargemDireita;
            y = MargemSuperior;
        }

        void GarantirEspaco(double altura)
        {
            if (y + altura > pagina.Height.Point - MargemInferior) NovaPagina();
        }

        void Desenhar(string texto, XFont fonte, XBrush pincel, double recuo, double alturaLinha, double espacoAntes, double espacoDepois)
        {
            y += espacoAntes;

            foreach (string linha in QuebrarEmLinhas(gfx!, texto, fonte, larguraUtil - recuo))
            {
                GarantirEspaco(alturaLinha);
                gfx!.DrawString(linha, fonte, pincel, new XRect(MargemEsquerda + recuo, y, larguraUtil - recuo, alturaLinha), XStringFormats.TopLeft);
                y += alturaLinha;
            }

            y += espacoDepois;
        }
    }

    /// <summary>Quebra o texto em linhas que cabem na largura informada, palavra a palavra.</summary>
    private static IEnumerable<string> QuebrarEmLinhas(XGraphics gfx, string texto, XFont fonte, double largura)
    {
        var linha = new StringBuilder();

        foreach (string palavra in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string tentativa = linha.Length == 0 ? palavra : $"{linha} {palavra}";

            if (linha.Length > 0 && gfx.MeasureString(tentativa, fonte).Width > largura)
            {
                yield return linha.ToString();
                linha.Clear().Append(palavra);
            }
            else
            {
                linha.Clear().Append(tentativa);
            }
        }

        if (linha.Length > 0) yield return linha.ToString();
    }

    /// <summary>
    /// Rodapé com "Página X de Y" em todas as páginas, desenhado depois que o documento inteiro
    /// existe — antes disso o total de páginas ainda não é conhecido.
    /// </summary>
    private static void DesenharRodapes(PdfDocument documento, XFont fonte, DocumentoTermos termos)
    {
        for (int indice = 0; indice < documento.PageCount; indice++)
        {
            PdfPage pagina = documento.Pages[indice];
            using XGraphics gfx = XGraphics.FromPdfPage(pagina, XGraphicsPdfPageOptions.Append);

            double y = pagina.Height.Point - MargemInferior + 18;
            double largura = pagina.Width.Point - MargemEsquerda - MargemDireita;

            gfx.DrawLine(XPens.LightGray, MargemEsquerda, y - 8, pagina.Width.Point - MargemDireita, y - 8);

            gfx.DrawString(termos.Versao, fonte, XBrushes.Gray,
                new XRect(MargemEsquerda, y, largura, 12), XStringFormats.TopLeft);

            gfx.DrawString($"Página {indice + 1} de {documento.PageCount}", fonte, XBrushes.Gray,
                new XRect(MargemEsquerda, y, largura, 12), XStringFormats.TopRight);
        }
    }
}
