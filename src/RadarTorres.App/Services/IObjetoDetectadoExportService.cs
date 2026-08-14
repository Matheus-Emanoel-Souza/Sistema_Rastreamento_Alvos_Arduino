using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Exporta/importa a lista de <see cref="ObjetoDetectado"/> exibida na tela "Objetos
/// Detectados" em arquivo, nos formatos CSV, XML e (só exportação) PDF.
/// </summary>
/// <remarks>
/// Deliberadamente livre de qualquer referência a WPF/XAML (diálogos de arquivo são
/// responsabilidade da View, mesmo princípio já usado em <c>ArduinoSettingsView</c>) — só
/// recebe/devolve o caminho do arquivo já escolhido pelo usuário.
/// </remarks>
public interface IObjetoDetectadoExportService
{
    void ExportCsv(IEnumerable<ObjetoDetectado> itens, string filePath);
    void ExportXml(IEnumerable<ObjetoDetectado> itens, string filePath);

    /// <summary>Relatório em PDF (só leitura — não existe importação de PDF, ver
    /// <c>Docs/ARQUITETURA.md</c>: um PDF não guarda estrutura de dados confiável para reler).</summary>
    void ExportPdf(IEnumerable<ObjetoDetectado> itens, string filePath);

    /// <summary>Lê um arquivo CSV no mesmo formato de <see cref="ExportCsv"/> (mesmas colunas,
    /// mesma ordem — inclusive um arquivo exportado por este mesmo serviço). Não grava nada;
    /// quem persiste os itens lidos é o chamador (ver <c>ObjetosDetectadosViewModel.ImportFrom</c>).</summary>
    List<ObjetoDetectado> ImportCsv(string filePath);

    /// <summary>Lê um arquivo XML no mesmo formato de <see cref="ExportXml"/>.</summary>
    List<ObjetoDetectado> ImportXml(string filePath);
}
