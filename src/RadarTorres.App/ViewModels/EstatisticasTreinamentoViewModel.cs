using System;
using System.Collections.ObjectModel;
using System.Linq;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da tela "Estatísticas de Treinamento" (exclusiva do Administrador — ver
/// <see cref="IPermissionService"/>): evolução de longo prazo do fitness médio a cada 1.000
/// gerações (<see cref="IMarcoFitnessTreinamentoRepository"/>), separada da tela "Treinamento do
/// Controlador" porque ali o foco é acompanhar o treinamento RODANDO agora (geração a geração,
/// até 300 no histórico recente); aqui é analisar a tendência de longuíssimo prazo sem ruído
/// geração a geração.
/// </summary>
public sealed class EstatisticasTreinamentoViewModel : ViewModelBase
{
    private readonly IMarcoFitnessTreinamentoRepository _marcoFitnessRepository;
    private readonly ILoggingService _logger;

    public ObservableCollection<MarcoFitnessTreinamento> Marcos { get; } = new();

    public RelayCommand AtualizarCommand { get; }

    public EstatisticasTreinamentoViewModel(IMarcoFitnessTreinamentoRepository marcoFitnessRepository, ILoggingService logger)
    {
        _marcoFitnessRepository = marcoFitnessRepository;
        _logger = logger;

        AtualizarCommand = new RelayCommand(Carregar);
        Carregar();
    }

    private void Carregar()
    {
        try
        {
            Marcos.Clear();
            foreach (MarcoFitnessTreinamento marco in _marcoFitnessRepository.ObterTodos())
            {
                Marcos.Add(marco);
            }

            OnPropertyChanged(nameof(TemDados));
            OnPropertyChanged(nameof(UltimoMarco));
            OnPropertyChanged(nameof(FitnessMedioGeral));
            OnPropertyChanged(nameof(MelhorFitnessGeral));
            OnPropertyChanged(nameof(MensagemSemDados));
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível carregar os marcos de treinamento: {ex.Message}");
        }
    }

    public bool TemDados => Marcos.Count > 0;

    public string MensagemSemDados =>
        "Nenhum marco registrado ainda — o primeiro aparece ao completar a geração 1.000 na tela \"Treinamento do Controlador\" (um marco a cada 1.000 gerações).";

    public MarcoFitnessTreinamento? UltimoMarco => Marcos.Count > 0 ? Marcos[^1] : null;

    public double FitnessMedioGeral => Marcos.Count > 0 ? Marcos.Average(m => m.FitnessMedioDoBloco) : 0;

    public double MelhorFitnessGeral => Marcos.Count > 0 ? Marcos.Max(m => m.MelhorFitnessDoBloco) : 0;
}
