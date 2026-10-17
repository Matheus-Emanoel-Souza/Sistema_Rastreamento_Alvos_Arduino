using System.Collections.Generic;

namespace RadarTorres.App.Models;

/// <summary>
/// Um trecho dos Termos de Uso já classificado pelo tipo (título, seção, parágrafo…). O
/// documento oficial é um arquivo de texto puro — o mesmo que o instalador exibe como página de
/// licença —, e esta classificação é o que permite apresentá-lo com hierarquia visual na tela e
/// no PDF sem manter uma segunda cópia do conteúdo em outro formato.
/// </summary>
/// <remarks>
/// São tipos distintos (e não um enum dentro de um único tipo) porque o WPF casa
/// <c>DataTemplate</c> por <c>DataType</c>: cada bloco ganha seu template sem precisar de um
/// seletor em código. O PDF usa a mesma lista de blocos, o que mantém tela e arquivo sempre
/// idênticos (ver <see cref="Services.ITermosDeUsoService"/>).
/// </remarks>
public abstract record BlocoTermos;

/// <summary>Título do documento, no topo da primeira página.</summary>
public sealed record TituloTermos(string Texto) : BlocoTermos;

/// <summary>Linha de vigência ("Versão X — vigente a partir de …").</summary>
public sealed record VersaoTermos(string Texto) : BlocoTermos;

/// <summary>Cabeçalho de cláusula ("1. IDENTIFICAÇÃO E NATUREZA ACADÊMICA").</summary>
public sealed record SecaoTermos(string Numero, string Titulo) : BlocoTermos;

/// <summary>
/// Parágrafo comum. <paramref name="Numero"/> é o prefixo numérico quando existe ("1.1."), que
/// a tela e o PDF destacam do restante do texto.
/// </summary>
public sealed record ParagrafoTermos(string? Numero, string Texto, bool Destaque = false) : BlocoTermos;

/// <summary>Item de lista — alínea ("a)") ou item do resumo final ("[X]").</summary>
public sealed record ItemListaTermos(string Marcador, string Texto) : BlocoTermos;

/// <summary>
/// Os Termos de Uso prontos para exibição: o texto integral como está no arquivo oficial e a
/// mesma coisa dividida em blocos para a apresentação formatada.
/// </summary>
public sealed record DocumentoTermos(
    string Titulo,
    string Versao,
    string TextoIntegral,
    IReadOnlyList<BlocoTermos> Blocos);
