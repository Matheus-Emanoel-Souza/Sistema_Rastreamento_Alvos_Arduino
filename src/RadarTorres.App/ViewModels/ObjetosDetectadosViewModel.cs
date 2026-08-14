using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da tela "Objetos Detectados": lista o histórico de detecções (Requisito 4) e
/// orquestra exportação (CSV/XML/PDF) e importação (CSV/XML, Requisito "importar também").
/// Nenhum diálogo de arquivo aqui — a ViewModel só pede um caminho através dos eventos
/// <see cref="ExportRequested"/>/<see cref="ImportRequested"/>; quem mostra
/// <c>SaveFileDialog</c>/<c>OpenFileDialog</c> e devolve o caminho escolhido é
/// <see cref="Views.ObjetosDetectadosView"/> (mesmo padrão de diálogo de arquivo já usado em
/// <c>ArduinoSettingsViewModel</c>/<c>ArduinoSettingsView</c>).
/// </summary>
public sealed class ObjetosDetectadosViewModel : ViewModelBase
{
    private readonly IObjetoDetectadoRepository _repository;
    private readonly IObjetoDetectadoExportService _exportService;
    private readonly ILoggingService _logger;
    private readonly IAuthService _authService;
    private readonly IPermissionService _permissionService;

    public ObservableCollection<ObjetoDetectado> Itens { get; } = new();

    /// <summary>Importar grava permanentemente no CSV do sistema — mesma regra de "quem pode
    /// alterar o estado do sistema" já usada no resto do app (Visualizador é somente-consulta).
    /// Exportar é só leitura, então fica liberado para qualquer perfil.</summary>
    public bool PodeImportar => _permissionService.PodeExecutarAcoes(_authService.CurrentUser?.Perfil ?? PerfilUsuario.Visualizador);

    public bool NaoPodeImportar => !PodeImportar;

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

    /// <summary>Mesma ideia de <see cref="ExportRequested"/>, mas para um <c>OpenFileDialog</c>
    /// ("csv"/"xml" apenas — sem importação de PDF).</summary>
    public event EventHandler<string>? ImportRequested;

    public RelayCommand ExportCommand { get; }
    public RelayCommand ImportCommand { get; }

    public ObjetosDetectadosViewModel(
        IObjetoDetectadoRepository repository,
        IObjetoDetectadoExportService exportService,
        ILoggingService logger,
        IAuthService authService,
        IPermissionService permissionService)
    {
        _repository = repository;
        _exportService = exportService;
        _logger = logger;
        _authService = authService;
        _permissionService = permissionService;

        ExportCommand = new RelayCommand(formato => ExportRequested?.Invoke(this, (string)formato!));
        ImportCommand = new RelayCommand(formato => ImportRequested?.Invoke(this, (string)formato!), _ => PodeImportar);

        _authService.SessionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PodeImportar));
            OnPropertyChanged(nameof(NaoPodeImportar));
            RelayCommand.RaiseCanExecuteChangedForAll();
        };
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

    /// <summary>Chamado pela View depois que o usuário escolheu o arquivo no diálogo aberto em
    /// resposta a <see cref="ImportRequested"/>. As linhas lidas viram registros novos e
    /// permanentes no CSV do sistema — cada <see cref="IObjetoDetectadoRepository.Add"/> atribui
    /// um Id novo, o Id do arquivo importado é descartado (evita colisão com o histórico já
    /// existente).</summary>
    public void ImportFrom(string formato, string filePath)
    {
        if (!PodeImportar)
        {
            SetStatus("Seu perfil não permite importar registros.", success: false);
            return;
        }

        try
        {
            List<ObjetoDetectado> lidos = formato switch
            {
                "csv" => _exportService.ImportCsv(filePath),
                "xml" => _exportService.ImportXml(filePath),
                _ => []
            };

            foreach (ObjetoDetectado item in lidos)
            {
                Itens.Add(_repository.Add(item));
            }

            SetStatus($"{lidos.Count} registro(s) importado(s) de {filePath}", success: true);
            _logger.Success($"Objetos Detectados importado ({formato.ToUpperInvariant()}): {lidos.Count} registro(s) de {filePath}");
        }
        catch (Exception ex)
        {
            SetStatus($"Falha ao importar: {ex.Message}", success: false);
            _logger.Error($"Falha ao importar Objetos Detectados ({formato.ToUpperInvariant()}): {ex.Message}");
        }
    }

    private void SetStatus(string message, bool success)
    {
        StatusMessage = message;
        StatusIsSuccess = success;
    }
}
