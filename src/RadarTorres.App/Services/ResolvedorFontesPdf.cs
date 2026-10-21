using System;
using System.Collections.Concurrent;
using System.IO;
using PdfSharp.Fonts;

namespace RadarTorres.App.Services;

/// <summary>
/// Fornece ao PDFsharp os arquivos de fonte usados nos PDFs gerados pelo aplicativo.
/// </summary>
/// <remarks>
/// <para>
/// O PDFsharp 6 não acompanha fonte nenhuma e não consulta o sistema por conta própria: sem um
/// <see cref="IFontResolver"/> registrado, qualquer <c>new XFont("Verdana", …)</c> lança
/// <c>InvalidOperationException: No appropriate font found for family name</c>. Era o caso aqui —
/// a exportação de "Objetos Detectados" em PDF falhava por isso, e o mesmo teria acontecido com
/// o PDF dos Termos de Uso.
/// </para>
/// <para>
/// As fontes são lidas de <c>%WINDIR%\Fonts</c>, o que é seguro neste projeto por ele ser
/// exclusivamente Windows (<c>net9.0-windows</c>, <c>RuntimeIdentifiers=win-x64</c>) e por
/// Verdana, Segoe UI e Arial fazerem parte da instalação padrão do sistema desde o Windows 7.
/// Quando a fonte pedida não existir, cai para a primeira disponível da lista em vez de derrubar
/// a exportação — um PDF em Arial é melhor do que nenhum PDF.
/// </para>
/// </remarks>
public sealed class ResolvedorFontesPdf : IFontResolver
{
    /// <summary>Famílias aceitas, em ordem de preferência quando a pedida não estiver instalada.</summary>
    private static readonly (string Familia, string Regular, string Negrito, string Italico, string NegritoItalico)[] Familias =
    [
        ("verdana", "verdana.ttf", "verdanab.ttf", "verdanai.ttf", "verdanaz.ttf"),
        ("segoe ui", "segoeui.ttf", "segoeuib.ttf", "segoeuii.ttf", "segoeuiz.ttf"),
        ("arial", "arial.ttf", "arialbd.ttf", "ariali.ttf", "arialbi.ttf"),
    ];

    private static readonly ConcurrentDictionary<string, byte[]> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Trava = new();
    private static bool _registrado;

    /// <summary>
    /// Registra este resolvedor no PDFsharp. Idempotente — pode ser chamado por qualquer serviço
    /// que vá gerar PDF, sem coordenação entre eles.
    /// </summary>
    public static void Registrar()
    {
        if (_registrado) return;

        lock (Trava)
        {
            if (_registrado) return;

            // Só define se ninguém já tiver definido: trocar um resolvedor em uso invalidaria as
            // fontes já resolvidas pelo PDFsharp no mesmo processo.
            GlobalFontSettings.FontResolver ??= new ResolvedorFontesPdf();
            _registrado = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        string pedida = familyName.Trim().ToLowerInvariant();

        foreach ((string familia, string regular, string negrito, string italico, string negritoItalico) in Familias)
        {
            if (familia != pedida) continue;

            string arquivo = (isBold, isItalic) switch
            {
                (true, true) => negritoItalico,
                (true, false) => negrito,
                (false, true) => italico,
                _ => regular
            };

            // Itálico/negrito-itálico podem não existir para a família (Verdana Itálico existe,
            // mas nem toda instalação traz todos os arquivos): cai para o regular da mesma
            // família antes de trocar de família.
            if (Existe(arquivo)) return new FontResolverInfo(arquivo);
            if (Existe(regular)) return new FontResolverInfo(regular);
        }

        // Família desconhecida ou ausente: usa a primeira que estiver instalada.
        foreach ((string _, string regular, string negrito, string italico, string negritoItalico) in Familias)
        {
            string arquivo = (isBold, isItalic) switch
            {
                (true, true) => negritoItalico,
                (true, false) => negrito,
                (false, true) => italico,
                _ => regular
            };

            if (Existe(arquivo)) return new FontResolverInfo(arquivo);
            if (Existe(regular)) return new FontResolverInfo(regular);
        }

        return null;
    }

    public byte[]? GetFont(string faceName) =>
        Cache.GetOrAdd(faceName, nome =>
        {
            string caminho = CaminhoDaFonte(nome);
            return File.Exists(caminho) ? File.ReadAllBytes(caminho) : Array.Empty<byte>();
        }) is { Length: > 0 } bytes ? bytes : null;

    private static bool Existe(string arquivo) => File.Exists(CaminhoDaFonte(arquivo));

    private static string CaminhoDaFonte(string arquivo) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), arquivo);
}
