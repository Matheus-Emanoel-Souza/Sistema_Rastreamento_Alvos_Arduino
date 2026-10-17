using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
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
/// <remarks>
/// Cada linha é uma OCORRÊNCIA de detecção — não um alvo cadastrado. O mesmo alvo
/// (<see cref="ObjetoDetectado.PerfilAlvoId"/>) aparece em até quatro linhas, uma por quadrante,
/// cada uma com a câmera responsável e o próprio estado. Esta tela é também onde o ciclo de abate
/// se fecha: o acionamento demonstrativo deixa a ocorrência em
/// <see cref="EstadoOcorrencia.AbatePendente"/> e é aqui que um operador confirma o abate ou o
/// descarta — nenhum automatismo faz isso, por decisão de negócio registrada em
/// <c>Docs/Projeto/LOG_SOLICITACOES.md</c>.
/// </remarks>
public sealed class ObjetosDetectadosViewModel : ViewModelBase
{
    private readonly IObjetoDetectadoRepository _repository;
    private readonly IObjetoDetectadoExportService _exportService;
    private readonly IAuthService _authService;
    private readonly IPermissionService _permissionService;
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

    /// <summary>Confirma o abate da ocorrência selecionada (só habilitado em AbatePendente).</summary>
    public RelayCommand ConfirmarAbateCommand { get; }

    /// <summary>Descarta o abate: a ocorrência volta a Detectado, com a transição registrada.</summary>
    public RelayCommand DescartarAbateCommand { get; }

    public ObjetosDetectadosViewModel(
        IObjetoDetectadoRepository repository,
        IObjetoDetectadoExportService exportService,
        IAuthService authService,
        IPermissionService permissionService,
        ILoggingService logger)
    {
        _repository = repository;
        _exportService = exportService;
        _authService = authService;
        _permissionService = permissionService;
        _logger = logger;

        ExportCommand = new RelayCommand(formato => ExportRequested?.Invoke(this, (string)formato!));
        ClearCommand = new RelayCommand(_ => ClearList());
        ConfirmarAbateCommand = new RelayCommand(_ => AlterarEstado(EstadoOcorrencia.Abatido), _ => PodeDecidirAbate);
        DescartarAbateCommand = new RelayCommand(_ => AlterarEstado(EstadoOcorrencia.Detectado), _ => PodeDecidirAbate);
    }

    // ---------------------------------------------------------------- Seleção / ciclo de abate

    private ObjetoDetectado? _selecionado;
    public ObjetoDetectado? Selecionado
    {
        get => _selecionado;
        set
        {
            if (!SetProperty(ref _selecionado, value)) return;

            OnPropertyChanged(nameof(PodeDecidirAbate));
            AtualizarHistorico();
            RelayCommand.RaiseCanExecuteChangedForAll();
        }
    }

    /// <summary>
    /// Só perfis que podem executar ações decidem abate, e só quando a ocorrência selecionada
    /// está efetivamente pendente — mesma checagem aplicada no acionamento (Requisito 7/11).
    /// </summary>
    public bool PodeDecidirAbate =>
        Selecionado is { Estado: EstadoOcorrencia.AbatePendente }
        && _permissionService.PodeExecutarAcoes(_authService.CurrentUser?.Perfil ?? PerfilUsuario.Visualizador);

    private string? _historicoSelecionado;

    /// <summary>Histórico de transições de estado da ocorrência selecionada, pronto para exibição.</summary>
    public string? HistoricoSelecionado
    {
        get => _historicoSelecionado;
        private set => SetProperty(ref _historicoSelecionado, value);
    }

    private void AtualizarHistorico()
    {
        if (Selecionado is null)
        {
            HistoricoSelecionado = null;
            return;
        }

        try
        {
            var texto = new StringBuilder();
            foreach (OcorrenciaEstadoTransicao transicao in _repository.ObterHistoricoEstados(Selecionado.Id))
            {
                texto.Append(transicao.DataHora.ToString("dd/MM/yyyy HH:mm:ss"))
                     .Append(" — ")
                     .Append(transicao.Estado);

                if (!string.IsNullOrWhiteSpace(transicao.Usuario)) texto.Append(" (").Append(transicao.Usuario).Append(')');
                if (!string.IsNullOrWhiteSpace(transicao.Observacao)) texto.Append(": ").Append(transicao.Observacao);

                texto.AppendLine();
            }

            HistoricoSelecionado = texto.Length > 0 ? texto.ToString().TrimEnd() : "Sem transições registradas para esta ocorrência.";
        }
        catch (Exception ex)
        {
            HistoricoSelecionado = $"Não foi possível carregar o histórico desta ocorrência: {ex.Message}";
        }
    }

    private void AlterarEstado(EstadoOcorrencia novoEstado)
    {
        if (Selecionado is not { } ocorrencia) return;
        if (!PodeDecidirAbate) return;

        string? usuario = _authService.CurrentUser?.Login;
        string observacao = novoEstado == EstadoOcorrencia.Abatido
            ? "Abate confirmado pelo operador."
            : "Abate descartado pelo operador — ocorrência volta a apenas detectada.";

        try
        {
            if (!_repository.AtualizarEstado(ocorrencia.Id, novoEstado, usuario, observacao))
            {
                SetStatus("A ocorrência já não estava pendente de confirmação — a lista foi recarregada.", success: false);
                Reload();
                return;
            }

            ocorrencia.Estado = novoEstado;

            SetStatus($"Ocorrência #{ocorrencia.Id}: {observacao}", success: true);
            _logger.Warning($"Ocorrência #{ocorrencia.Id} — {observacao} (usuário: {usuario ?? "—"})");

            // Recarrega para a grade refletir o estado persistido e reposiciona a seleção na
            // mesma ocorrência, para o histórico atualizado continuar visível.
            int id = ocorrencia.Id;
            Reload();
            Selecionado = Itens.FirstOrDefault(i => i.Id == id);
        }
        catch (Exception ex)
        {
            SetStatus($"Não foi possível atualizar o estado: {ex.Message}", success: false);
            _logger.Error($"Falha ao atualizar o estado da ocorrência #{ocorrencia.Id}: {ex.Message}");
        }
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

        OnPropertyChanged(nameof(PodeDecidirAbate));
        RelayCommand.RaiseCanExecuteChangedForAll();
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
