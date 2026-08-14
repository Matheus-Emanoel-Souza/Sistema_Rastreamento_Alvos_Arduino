using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using RadarTorres.App.Data;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;

namespace RadarTorres.App.Services;

/// <summary>Implementação de <see cref="IObjetoDetectadoExportService"/>.</summary>
public sealed class ObjetoDetectadoExportService : IObjetoDetectadoExportService
{
    private static readonly XmlSerializer XmlSerializer =
        new(typeof(List<ObjetoDetectado>), new XmlRootAttribute("ObjetosDetectados") { ElementName = "ObjetosDetectados" });

    public void ExportCsv(IEnumerable<ObjetoDetectado> itens, string filePath)
    {
        // Reaproveita o mesmo mapeamento de colunas da persistência real (CsvObjetoDetectadoRepository)
        // — um arquivo exportado aqui é lido de volta por ImportCsv com o formato garantido idêntico.
        var store = new CsvTableStore<ObjetoDetectado>(filePath, CsvObjetoDetectadoRepository.BuildColumns());
        store.WriteAll(itens);
    }

    public List<ObjetoDetectado> ImportCsv(string filePath)
    {
        var store = new CsvTableStore<ObjetoDetectado>(filePath, CsvObjetoDetectadoRepository.BuildColumns());
        return store.ReadAll();
    }

    public void ExportXml(IEnumerable<ObjetoDetectado> itens, string filePath)
    {
        using var writer = new StreamWriter(filePath, append: false, Encoding.UTF8);
        XmlSerializer.Serialize(writer, itens.ToList());
    }

    public List<ObjetoDetectado> ImportXml(string filePath)
    {
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        return XmlSerializer.Deserialize(reader) as List<ObjetoDetectado> ?? [];
    }

    // ---------------------------------------------------------------- PDF (só exportação)

    private const double MarginLeft = 30;
    private const double MarginTop = 30;
    private const double LineHeight = 14;

    private static readonly (string Header, double Width, Func<ObjetoDetectado, string> Value)[] Columns =
    [
        ("Id", 30, o => o.Id.ToString()),
        ("Tipo", 95, o => o.Tipo),
        ("X (m)", 55, o => o.X.ToString("0.00")),
        ("Y (m)", 55, o => o.Y.ToString("0.00")),
        ("Quad.", 40, o => o.Quadrante),
        ("Data/Hora", 115, o => o.DataHora.ToString("dd/MM/yyyy HH:mm:ss")),
        ("Dispositivo", 80, o => o.Dispositivo),
        ("Observação", 140, o => string.IsNullOrEmpty(o.Observacao) ? "—" : o.Observacao),
    ];

    public void ExportPdf(IEnumerable<ObjetoDetectado> itens, string filePath)
    {
        List<ObjetoDetectado> lista = itens.ToList();

        var document = new PdfDocument();
        document.Info.Title = "Objetos Detectados";

        var titleFont = new XFont("Verdana", 16, XFontStyleEx.Bold);
        var subtitleFont = new XFont("Verdana", 9, XFontStyleEx.Regular);
        var headerFont = new XFont("Verdana", 9, XFontStyleEx.Bold);
        var bodyFont = new XFont("Verdana", 8, XFontStyleEx.Regular);

        PdfPage page = null!;
        XGraphics gfx = null!;
        double y = 0;
        double pageWidth = 0;
        double pageHeight = 0;

        void DrawRow(IReadOnlyList<string> values, XFont font, XBrush brush)
        {
            double x = MarginLeft;
            for (int i = 0; i < Columns.Length; i++)
            {
                gfx.DrawString(values[i], font, brush, new XRect(x, y, Columns[i].Width, LineHeight), XStringFormats.TopLeft);
                x += Columns[i].Width;
            }
            y += LineHeight;
        }

        void StartPage()
        {
            page = document.AddPage();
            page.Orientation = PdfSharp.PageOrientation.Landscape;
            gfx = XGraphics.FromPdfPage(page);
            pageWidth = page.Width.Point;
            pageHeight = page.Height.Point;
            y = MarginTop;

            gfx.DrawString("Objetos Detectados", titleFont, XBrushes.Black, new XPoint(MarginLeft, y + 14));
            y += 24;
            gfx.DrawString($"Exportado em {DateTime.Now:dd/MM/yyyy HH:mm:ss} — {lista.Count} registro(s)",
                subtitleFont, XBrushes.Gray, new XPoint(MarginLeft, y + 10));
            y += 20;

            DrawRow(Columns.Select(c => c.Header).ToArray(), headerFont, XBrushes.Black);
            y += 3;
            gfx.DrawLine(XPens.Black, MarginLeft, y, pageWidth - MarginLeft, y);
            y += 6;
        }

        StartPage();

        foreach (ObjetoDetectado item in lista)
        {
            if (y + LineHeight > pageHeight - MarginTop)
            {
                StartPage();
            }

            DrawRow(Columns.Select(c => c.Value(item)).ToArray(), bodyFont, XBrushes.Black);
        }

        document.Save(filePath);
    }
}
