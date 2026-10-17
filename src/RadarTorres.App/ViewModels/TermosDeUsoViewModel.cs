using System;
using System.Threading.Tasks;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da janela "Termos de Uso": entrega os blocos já classificados do documento vigente
/// para exibição e coordena a exportação em PDF.
/// </summary>
/// <remarks>
/// Nenhum diálogo de arquivo aqui — a ViewModel só pede um caminho através de
/// <see cref="SalvarPdfSolicitado"/>; quem mostra o <c>SaveFileDialog</c> e devolve o caminho é
/// a View, mesmo padrão já usado em <see cref="ObjetosDetectadosViewModel"/>.
/// </remarks>
public sealed class TermosDeUsoViewModel : ViewModelBase
{
    private readonly ITermosDeUsoService _termosService;
    private readonly ILoggingService _logger;

    public TermosDeUsoViewModel(ITermosDeUsoService termosService, ILoggingService logger)
    {
        _termosService = termosService;
        _logger = logger;

        Documento = _termosService.Obter();
        BaixarPdfCommand = new RelayCommand(_ => SalvarPdfSolicitado?.Invoke(this, _termosService.NomeArquivoSugerido), _ => !EstaGerandoPdf);
    }

    /// <summary>Documento vigente: título, versão e os blocos na ordem em que devem ser lidos.</summary>
    public DocumentoTermos Documento { get; }

    /// <summary>
    /// Pede à View um <c>SaveFileDialog</c> com o nome sugerido; se o usuário confirmar, ela
    /// chama <see cref="ExportarPdfAsync"/> de volta com o caminho escolhido.
    /// </summary>
    public event EventHandler<string>? SalvarPdfSolicitado;

    public RelayCommand BaixarPdfCommand { get; }

    private bool _estaGerandoPdf;

    /// <summary>Enquanto verdadeiro, o botão fica desabilitado e exibe o rótulo de progresso — também impede gerar duas vezes por cliques repetidos.</summary>
    public bool EstaGerandoPdf
    {
        get => _estaGerandoPdf;
        private set
        {
            if (!SetProperty(ref _estaGerandoPdf, value)) return;

            OnPropertyChanged(nameof(TextoBotaoPdf));
            RelayCommand.RaiseCanExecuteChangedForAll();
        }
    }

    public string TextoBotaoPdf => EstaGerandoPdf ? "Gerando o PDF…" : "Baixar Termos de Uso em PDF";

    private string? _mensagemStatus;
    public string? MensagemStatus
    {
        get => _mensagemStatus;
        private set => SetProperty(ref _mensagemStatus, value);
    }

    private bool _statusSucesso;
    public bool StatusSucesso
    {
        get => _statusSucesso;
        private set => SetProperty(ref _statusSucesso, value);
    }

    /// <summary>
    /// Gera o PDF fora da thread de interface — o documento tem várias páginas e a medição de
    /// texto do PDFsharp não é instantânea; travar a janela durante a geração daria a impressão
    /// de que o botão não funcionou.
    /// </summary>
    public async Task ExportarPdfAsync(string caminhoArquivo)
    {
        if (EstaGerandoPdf) return;

        EstaGerandoPdf = true;
        MensagemStatus = null;

        try
        {
            await Task.Run(() => _termosService.ExportarPdf(caminhoArquivo));

            StatusSucesso = true;
            MensagemStatus = $"Termos de Uso salvos em {caminhoArquivo}";
            _logger.Success($"Termos de Uso exportados em PDF: {caminhoArquivo}");
        }
        catch (Exception ex)
        {
            StatusSucesso = false;
            MensagemStatus = $"Não foi possível gerar o PDF: {ex.Message}";
            _logger.Error($"Falha ao exportar os Termos de Uso em PDF: {ex.Message}");
        }
        finally
        {
            EstaGerandoPdf = false;
        }
    }
}
