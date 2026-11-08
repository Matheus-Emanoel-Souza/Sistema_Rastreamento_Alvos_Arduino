using System;
using System.Collections.ObjectModel;
using System.Linq;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// Tela "Dashboards" (seção "Dados"): o usuário escolhe uma fonte de dados já existente no banco
/// (marcos/instantâneos de treinamento, histórico de gerações, experimentos de hiperparâmetros,
/// experimentos de comparação de rede neural), um campo para o eixo X, um campo para a métrica e
/// o tipo de gráfico — e monta um dashboard próprio, gráfico a gráfico, sem escrever SQL. Os
/// gráficos montados ficam salvos (<see cref="IDashboardGraficoRepository"/>) e recarregam sozinhos
/// na próxima abertura da tela.
/// </summary>
public sealed class DashboardsViewModel : ViewModelBase
{
    private readonly IDashboardGraficoRepository _cardRepository;
    private readonly IFontesDeDadosDashboardService _fontesService;
    private readonly ILoggingService _logger;

    public ObservableCollection<FonteDadosDashboard> Fontes { get; } = new();
    public ObservableCollection<DashboardGraficoViewModel> Cards { get; } = new();

    public ObservableCollection<CampoDashboard> CamposEixoXDisponiveis { get; } = new();
    public ObservableCollection<CampoDashboard> CamposMetricaDisponiveis { get; } = new();

    public TipoGraficoDashboard[] TiposGraficoDisponiveis { get; } = [TipoGraficoDashboard.Linha, TipoGraficoDashboard.Barra];

    private FonteDadosDashboard? _fonteSelecionada;
    public FonteDadosDashboard? FonteSelecionada
    {
        get => _fonteSelecionada;
        set
        {
            if (!SetProperty(ref _fonteSelecionada, value)) return;

            CamposEixoXDisponiveis.Clear();
            CamposMetricaDisponiveis.Clear();
            if (value is null) return;

            foreach (CampoDashboard campo in value.CamposEixoX) CamposEixoXDisponiveis.Add(campo);
            foreach (CampoDashboard campo in value.CamposMetrica) CamposMetricaDisponiveis.Add(campo);

            CampoEixoXSelecionado = CamposEixoXDisponiveis.FirstOrDefault();
            CampoMetricaSelecionado = CamposMetricaDisponiveis.FirstOrDefault();
            AtualizarTituloSugerido();
        }
    }

    private CampoDashboard? _campoEixoXSelecionado;
    public CampoDashboard? CampoEixoXSelecionado
    {
        get => _campoEixoXSelecionado;
        set { if (SetProperty(ref _campoEixoXSelecionado, value)) AtualizarTituloSugerido(); }
    }

    private CampoDashboard? _campoMetricaSelecionado;
    public CampoDashboard? CampoMetricaSelecionado
    {
        get => _campoMetricaSelecionado;
        set { if (SetProperty(ref _campoMetricaSelecionado, value)) AtualizarTituloSugerido(); }
    }

    private TipoGraficoDashboard _tipoGraficoSelecionado = TipoGraficoDashboard.Linha;
    public TipoGraficoDashboard TipoGraficoSelecionado
    {
        get => _tipoGraficoSelecionado;
        set => SetProperty(ref _tipoGraficoSelecionado, value);
    }

    private string _tituloNovoCard = string.Empty;
    public string TituloNovoCard
    {
        get => _tituloNovoCard;
        set => SetProperty(ref _tituloNovoCard, value);
    }

    private void AtualizarTituloSugerido()
    {
        if (CampoMetricaSelecionado is null || CampoEixoXSelecionado is null) return;
        TituloNovoCard = $"{CampoMetricaSelecionado.Nome} por {CampoEixoXSelecionado.Nome}";
    }

    private string? _statusMensagem;
    public string? StatusMensagem
    {
        get => _statusMensagem;
        set => SetProperty(ref _statusMensagem, value);
    }

    public RelayCommand AdicionarCommand { get; }
    public RelayCommand AtualizarTodosCommand { get; }

    public DashboardsViewModel(
        IDashboardGraficoRepository cardRepository,
        IFontesDeDadosDashboardService fontesService,
        ILoggingService logger)
    {
        _cardRepository = cardRepository;
        _fontesService = fontesService;
        _logger = logger;

        foreach (FonteDadosDashboard fonte in _fontesService.ObterFontes()) Fontes.Add(fonte);
        FonteSelecionada = Fontes.FirstOrDefault();

        AdicionarCommand = new RelayCommand(Adicionar, PodeAdicionar);
        AtualizarTodosCommand = new RelayCommand(AtualizarTodos);

        CarregarCardsSalvos();
    }

    private bool PodeAdicionar() =>
        FonteSelecionada is not null && CampoEixoXSelecionado is not null && CampoMetricaSelecionado is not null;

    /// <summary>Recarrega os gráficos já salvos de sessões anteriores — mesmo princípio de outras telas (dado persistido sobrevive ao fechamento do app).</summary>
    private void CarregarCardsSalvos()
    {
        try
        {
            foreach (DashboardGrafico card in _cardRepository.ObterTodos())
            {
                AdicionarCardNaTela(card);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível carregar os dashboards salvos: {ex.Message}");
            StatusMensagem = "Não foi possível carregar os dashboards salvos.";
        }
    }

    private void Adicionar()
    {
        if (!PodeAdicionar()) return;

        var card = new DashboardGrafico
        {
            Titulo = string.IsNullOrWhiteSpace(TituloNovoCard) ? "Gráfico" : TituloNovoCard,
            FonteChave = FonteSelecionada!.Chave,
            CampoEixoXChave = CampoEixoXSelecionado!.Chave,
            CampoMetricaChave = CampoMetricaSelecionado!.Chave,
            TipoGrafico = TipoGraficoSelecionado,
            Ordem = Cards.Count,
        };

        try
        {
            _cardRepository.Registrar(card);
            AdicionarCardNaTela(card);
            StatusMensagem = null;
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível salvar o dashboard '{card.Titulo}': {ex.Message}");
            StatusMensagem = $"Não foi possível salvar: {ex.Message}";
        }
    }

    private void AdicionarCardNaTela(DashboardGrafico card)
    {
        FonteDadosDashboard? fonte = Fontes.FirstOrDefault(f => f.Chave == card.FonteChave);
        string fonteNome = fonte?.Nome ?? card.FonteChave;
        string eixoXNome = fonte?.CamposEixoX.FirstOrDefault(c => c.Chave == card.CampoEixoXChave)?.Nome ?? card.CampoEixoXChave;
        string metricaNome = fonte?.CamposMetrica.FirstOrDefault(c => c.Chave == card.CampoMetricaChave)?.Nome ?? card.CampoMetricaChave;

        var pontos = _fontesService.ObterSerie(card.FonteChave, card.CampoEixoXChave, card.CampoMetricaChave);
        Cards.Add(new DashboardGraficoViewModel(card, fonteNome, eixoXNome, metricaNome, pontos, Remover));
    }

    private void Remover(int id)
    {
        try
        {
            _cardRepository.Remover(id);
            DashboardGraficoViewModel? card = Cards.FirstOrDefault(c => c.Id == id);
            if (card is not null) Cards.Remove(card);
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível remover o dashboard (Id {id}): {ex.Message}");
            StatusMensagem = $"Não foi possível remover: {ex.Message}";
        }
    }

    /// <summary>Reexecuta a consulta de todos os gráficos já montados — os dados-fonte podem ter mudado (ex.: um treinamento rodou mais gerações) desde que a tela abriu.</summary>
    private void AtualizarTodos()
    {
        foreach (DashboardGraficoViewModel card in Cards)
        {
            card.Pontos = _fontesService.ObterSerie(card.FonteChave, card.CampoEixoXChave, card.CampoMetricaChave);
        }
    }
}
