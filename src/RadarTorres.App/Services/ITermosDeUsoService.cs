using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Entrega os Termos de Uso vigentes para apresentação na tela e para exportação em PDF.
/// </summary>
/// <remarks>
/// <b>Fonte única da verdade:</b> o conteúdo vem de <c>installer\TERMOS_DE_USO.txt</c> — o MESMO
/// arquivo que o Inno Setup exibe como página de licença durante a instalação (ver
/// <c>installer\RadarTorres.iss</c>) —, embutido no executável como recurso. Não existe uma
/// segunda cópia do texto no código, no XAML nem em banco: tela, PDF e instalador leem a mesma
/// redação, e alterar o arquivo basta para os três acompanharem.
/// </remarks>
public interface ITermosDeUsoService
{
    /// <summary>Os termos vigentes, com o texto integral e os blocos já classificados para exibição.</summary>
    DocumentoTermos Obter();

    /// <summary>
    /// Gera o PDF do documento integral no caminho informado. Lança se não for possível gravar
    /// (quem chama traduz em mensagem para o usuário).
    /// </summary>
    void ExportarPdf(string caminhoArquivo);

    /// <summary>Nome de arquivo sugerido no diálogo de salvamento.</summary>
    string NomeArquivoSugerido { get; }
}
