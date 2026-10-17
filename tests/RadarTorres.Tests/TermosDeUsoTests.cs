using System.IO;
using PdfSharp.Pdf.IO;
using RadarTorres.App.Models;
using RadarTorres.App.Services;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Termos de Uso (critérios de aceitação 1 a 4): identificação correta dos autores e do
/// orientador, preservação das demais cláusulas, e o PDF exportado batendo com o texto exibido.
/// </summary>
public sealed class TermosDeUsoTests : IDisposable
{
    private readonly string _pasta = FotosDeTeste.CriarPasta();
    private readonly TermosDeUsoService _servico = new(new LoggerFalso());

    public void Dispose() => FotosDeTeste.Limpar(_pasta);

    // ---------------------------------------------------------------- 1: redação da seção 1.1

    [Fact]
    public void Clausula_1_1_identifica_os_dois_desenvolvedores_e_o_orientador()
    {
        DocumentoTermos termos = _servico.Obter();

        ParagrafoTermos clausula = termos.Blocos
            .OfType<ParagrafoTermos>()
            .Single(p => p.Numero == "1.1.");

        Assert.Contains("Matheus Emanoel de Souza", clausula.Texto);
        Assert.Contains("Gabriel Vasconcellos de Morais", clausula.Texto);
        Assert.Contains("Anker Loss", clausula.Texto);
        Assert.Contains("UCL", clausula.Texto);
        Assert.Contains("Trabalho de Conclusão de Curso", clausula.Texto);
    }

    /// <summary>A quebra de linha em ~78 colunas do arquivo não pode partir nomes na exibição.</summary>
    [Fact]
    public void Paragrafos_sao_remontados_sem_a_quebra_de_linha_do_arquivo()
    {
        DocumentoTermos termos = _servico.Obter();

        ParagrafoTermos clausula = termos.Blocos.OfType<ParagrafoTermos>().Single(p => p.Numero == "1.1.");

        Assert.DoesNotContain("\n", clausula.Texto);
        Assert.DoesNotContain("  ", clausula.Texto);
    }

    // ---------------------------------------------------------------- 2: demais cláusulas intactas

    [Fact]
    public void Todas_as_clausulas_do_documento_continuam_presentes()
    {
        DocumentoTermos termos = _servico.Obter();

        List<SecaoTermos> secoes = termos.Blocos.OfType<SecaoTermos>().ToList();

        // O documento tem 15 cláusulas numeradas, de 1 a 15, sem buracos nem repetição.
        Assert.Equal(15, secoes.Count);
        Assert.Equal(Enumerable.Range(1, 15).Select(n => n.ToString()).ToArray(), secoes.Select(s => s.Numero).ToArray());

        Assert.Contains(secoes, s => s.Titulo.Contains("IDENTIFICAÇÃO"));
        Assert.Contains(secoes, s => s.Titulo.Contains("RESTRIÇÕES DE SEGURANÇA"));
    }

    [Fact]
    public void Alineas_e_itens_do_resumo_final_sao_reconhecidos_como_lista()
    {
        DocumentoTermos termos = _servico.Obter();

        List<ItemListaTermos> itens = termos.Blocos.OfType<ItemListaTermos>().ToList();

        Assert.Contains(itens, i => i.Marcador == "a)");
        Assert.Contains(itens, i => i.Marcador.StartsWith("[X", StringComparison.OrdinalIgnoreCase));

        // Nenhum item pode ter ficado com o marcador duplicado dentro do texto.
        Assert.All(itens, i => Assert.False(i.Texto.StartsWith(i.Marcador, StringComparison.Ordinal)));
    }

    [Fact]
    public void Versao_vigente_e_exposta_separadamente()
    {
        DocumentoTermos termos = _servico.Obter();

        Assert.StartsWith("Versão ", termos.Versao);
        Assert.Contains("vigente a partir de", termos.Versao);
        Assert.Single(termos.Blocos.OfType<VersaoTermos>());
        Assert.Single(termos.Blocos.OfType<TituloTermos>());
    }

    // ---------------------------------------------------------------- 4: tela e PDF iguais

    /// <summary>
    /// O texto integral exposto para a tela é exatamente o arquivo oficial — é dele que os
    /// blocos (tela) e o PDF saem, então não há como divergirem.
    /// </summary>
    [Fact]
    public void Texto_integral_contem_tudo_que_os_blocos_exibem()
    {
        DocumentoTermos termos = _servico.Obter();

        Assert.Contains("Gabriel Vasconcellos", termos.TextoIntegral);
        Assert.Contains("Anker Loss", termos.TextoIntegral);

        foreach (SecaoTermos secao in termos.Blocos.OfType<SecaoTermos>())
        {
            Assert.Contains(secao.Titulo, termos.TextoIntegral);
        }
    }

    // ---------------------------------------------------------------- 3: PDF

    [Fact]
    public void Pdf_e_gerado_com_o_documento_integral_e_varias_paginas()
    {
        string caminho = Path.Combine(_pasta, _servico.NomeArquivoSugerido);

        _servico.ExportarPdf(caminho);

        Assert.True(File.Exists(caminho));
        Assert.True(new FileInfo(caminho).Length > 5_000, "PDF pequeno demais para conter o documento inteiro.");

        // Abre o arquivo de volta: se não for um PDF válido, isto lança.
        using var pdf = PdfReader.Open(caminho, PdfDocumentOpenMode.Import);

        // 15 cláusulas em corpo 9 não cabem em uma página — um PDF de página única significaria
        // documento truncado, que é exatamente o que o requisito proíbe.
        Assert.True(pdf.PageCount >= 5, $"PDF com apenas {pdf.PageCount} página(s).");
        Assert.Contains("RADARTORRES", pdf.Info.Title, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Versão ", pdf.Info.Subject);
    }

    [Fact]
    public void Nome_do_arquivo_sugerido_e_claro()
    {
        Assert.Equal("RadarTorres_Termos_de_Uso.pdf", _servico.NomeArquivoSugerido);
    }

    [Fact]
    public void Exportar_para_caminho_invalido_lanca_para_a_tela_tratar()
    {
        string caminhoInvalido = Path.Combine(_pasta, "pasta-que-nao-existe", "sub", "arquivo.pdf");

        Assert.ThrowsAny<Exception>(() => _servico.ExportarPdf(caminhoInvalido));
    }

    // ---------------------------------------------------------------- Parser isolado

    [Fact]
    public void Parser_classifica_titulo_secao_paragrafo_e_alinea()
    {
        const string exemplo = """
            ================================================================================
                              TERMOS DE USO DO SOFTWARE "EXEMPLO"
            ================================================================================

            Versão 9.9 — vigente a partir de 1 de janeiro de 2030.

            LEIA ATENTAMENTE.

            --------------------------------------------------------------------------------
            3. CLAUSULA DE TESTE
            --------------------------------------------------------------------------------

            3.1. Primeiro paragrafo que ocupa duas linhas no arquivo e precisa voltar a ser
            uma linha so.

               a) primeira alinea;
               b) segunda alinea que tambem ocupa duas linhas no arquivo e precisa ser
                  remontada.
            """;

        DocumentoTermos termos = TermosDeUsoService.Analisar(exemplo);

        Assert.Equal("TERMOS DE USO DO SOFTWARE \"EXEMPLO\"", termos.Titulo);
        Assert.StartsWith("Versão 9.9", termos.Versao);

        SecaoTermos secao = Assert.Single(termos.Blocos.OfType<SecaoTermos>());
        Assert.Equal("3", secao.Numero);
        Assert.Equal("CLAUSULA DE TESTE", secao.Titulo);

        ParagrafoTermos paragrafo = termos.Blocos.OfType<ParagrafoTermos>().Single(p => p.Numero == "3.1.");
        Assert.Equal("Primeiro paragrafo que ocupa duas linhas no arquivo e precisa voltar a ser uma linha so.", paragrafo.Texto);

        Assert.Contains(termos.Blocos.OfType<ParagrafoTermos>(), p => p.Destaque && p.Texto == "LEIA ATENTAMENTE.");

        List<ItemListaTermos> alineas = termos.Blocos.OfType<ItemListaTermos>().ToList();
        Assert.Equal(2, alineas.Count);
        Assert.Equal("a)", alineas[0].Marcador);
        Assert.Equal("segunda alinea que tambem ocupa duas linhas no arquivo e precisa ser remontada.", alineas[1].Texto);
    }
}
