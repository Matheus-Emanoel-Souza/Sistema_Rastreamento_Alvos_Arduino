using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Exporta a lista de <see cref="ObjetoDetectado"/> exibida na tela "Objetos Detectados" em
/// arquivo, nos formatos CSV, XML e PDF. Sem importação de propósito — as informações desta
/// tabela só podem ser geradas pelo próprio sistema rodando (detecção real ou simulada).
/// </summary>
/// <remarks>
/// Deliberadamente livre de qualquer referência a WPF/XAML (diálogos de arquivo são
/// responsabilidade da View, mesmo princípio já usado em <c>ArduinoSettingsView</c>) — só
/// recebe o caminho do arquivo já escolhido pelo usuário.
/// </remarks>
public interface IObjetoDetectadoExportService
{
    void ExportCsv(IEnumerable<ObjetoDetectado> itens, string filePath);
    void ExportXml(IEnumerable<ObjetoDetectado> itens, string filePath);
    void ExportPdf(IEnumerable<ObjetoDetectado> itens, string filePath);
}
