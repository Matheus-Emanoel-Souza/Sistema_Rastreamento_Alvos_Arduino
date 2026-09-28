using System;
using System.Collections.ObjectModel;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da tela "Objetos Detectados": lista o histórico de detecções (Requisito 4) e
/// orquestra exportação (CSV/XML/PDF). Sem importação de propósito — as informações desta
/// tabela só podem ser geradas pelo próprio sistema rodando (detecção real ou simulada), nunca
/// por um arquivo externo carregado pelo usuário. Nenhum diálogo de arquivo aqui — a ViewModel
/// só pede um caminho através do evento <see cref="ExportRequested"/>; quem mostra o
/// <c>SaveFileDialog</c> e devolve o caminho escolhido é <see cref="Views.ObjetosDetectadosView"/>
/// (mesmo padrão de diálogo de arquivo já usado em <c>ArduinoSettingsViewModel</c>/<c>ArduinoSettingsView</c>).
/// </summary>
public sealed class ObjetosDetectadosViewModel : ViewModelBase
{
    private readonly IObjetoDetectadoRepository _repository;
    private readonly IObjetoDetectadoExportService _exportService;
    private readonly ILoggingService _logger;

    public ObservableCollection<ObjetoDetectado> Itens { get; } = new();

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private bool _statusIsSuccess;
    public bool StatusIsSuccess
    {
        get => _statusIsSuccess;
        private set => SetProperty(ref _statusIsSuccess, value);
    }

    /// <summary>Pede à View para mostrar um <c>SaveFileDialog</c> apropriado ao formato
    /// ("csv"/"xml"/"pdf") e, se o usuário confirmar, chamar <see cref="ExportTo"/> de volta.</summary>
    public event EventHandler<string>? ExportRequested;

    public RelayCommand ExportCommand { get; }
    public RelayCommand ClearCommand { get; }

    public ObjetosDetectadosViewModel(
        IObjetoDetectadoRepository repository,
        IObjetoDetectadoExportService exportService,
        ILoggingService logger)
    {
        _repository = repository;
        _exportService = exportService;
        _logger = logger;

        ExportCommand = new RelayCommand(formato => ExportRequested?.Invoke(this, (string)formato!));
        ClearCommand = new RelayCommand(_ => ClearList());
    }

    /// <summary>Recarrega a lista a partir do CSV — chamado pela View no <c>Loaded</c> (mesmo
    /// padrão de <c>MonitoramentoView.LoadLayoutForCurrentUser</c>: <c>Loaded</c> refaz a cada
    /// navegação porque a View é Singleton e só sai/volta da árvore visual, nunca é reconstruída).</summary>
    public void Reload()
    {
        Itens.Clear();
        foreach (ObjetoDetectado item in _repository.GetAll())
        {
            Itens.Add(item);
        }
    }

    /// <summary>Chamado pela View depois que o usuário escolheu onde salvar no diálogo aberto em
    /// resposta a <see cref="ExportRequested"/>.</summary>
    public void ExportTo(string formato, string filePath)
    {
        try
        {
            switch (formato)
            {
                case "csv": _exportService.ExportCsv(Itens, filePath); break;
                case "xml": _exportService.ExportXml(Itens, filePath); break;
                case "pdf": _exportService.ExportPdf(Itens, filePath); break;
                default: return;
            }

            SetStatus($"{Itens.Count} registro(s) exportado(s) para {filePath}", success: true);
            _logger.Success($"Objetos Detectados exportado ({formato.ToUpperInvariant()}): {Itens.Count} registro(s) em {filePath}");
        }
        catch (Exception ex)
        {
            SetStatus($"Falha ao exportar: {ex.Message}", success: false);
            _logger.Error($"Falha ao exportar Objetos Detectados ({formato.ToUpperInvariant()}): {ex.Message}");
        }
    }

    /// <summary>Esvazia apenas a lista exibida em tela (não altera o CSV). Serve para separar
    /// visualmente detecções de sessões diferentes; os registros continuam no arquivo e voltam
    /// a aparecer no próximo <see cref="Reload"/>.</summary>
    private void ClearList()
    {
        Itens.Clear();
        SetStatus("Lista limpa (os registros continuam salvos no arquivo).", success: true);
    }

    private void SetStatus(string message, bool success)
    {
        StatusMessage = message;
        StatusIsSuccess = success;
    }
}
