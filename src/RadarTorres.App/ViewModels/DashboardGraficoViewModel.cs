using System.Collections.Generic;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// Um gráfico já resolvido (pontos prontos para desenhar) exibido na tela "Dashboards" — par de
/// apresentação do <see cref="DashboardGrafico"/> persistido, ver <see cref="DashboardsViewModel"/>.
/// </summary>
public sealed class DashboardGraficoViewModel : ViewModelBase
{
    public int Id { get; }
    public string Titulo { get; }
    public string FonteChave { get; }
    public string CampoEixoXChave { get; }
    public string CampoMetricaChave { get; }
    public string FonteNome { get; }
    public string EixoXNome { get; }
    public string MetricaNome { get; }
    public TipoGraficoDashboard TipoGrafico { get; }

    private IReadOnlyList<PontoSerieDashboard> _pontos;
    public IReadOnlyList<PontoSerieDashboard> Pontos
    {
        get => _pontos;
        set => SetProperty(ref _pontos, value);
    }

    public RelayCommand RemoverCommand { get; }

    public DashboardGraficoViewModel(
        DashboardGrafico card,
        string fonteNome,
        string eixoXNome,
        string metricaNome,
        IReadOnlyList<PontoSerieDashboard> pontos,
        System.Action<int> remover)
    {
        Id = card.Id;
        Titulo = card.Titulo;
        FonteChave = card.FonteChave;
        CampoEixoXChave = card.CampoEixoXChave;
        CampoMetricaChave = card.CampoMetricaChave;
        FonteNome = fonteNome;
        EixoXNome = eixoXNome;
        MetricaNome = metricaNome;
        TipoGrafico = card.TipoGrafico;
        _pontos = pontos;
        RemoverCommand = new RelayCommand(() => remover(Id));
    }
}
