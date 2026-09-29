using System.Collections.Generic;
using System.Collections.ObjectModel;
using RadarTorres.App.Configuration;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// Linha de exibição da tela "Ações Realizadas": uma <see cref="AcaoRealizada"/> com a torre
/// (nome, resolvido a partir de <see cref="AcaoRealizada.TorreId"/> via
/// <c>AppConfig.Current.Towers</c>) e as coordenadas do alvo (via
/// <see cref="AcaoRealizada.ObjetoDetectadoId"/>) já resolvidas — a tabela em si não guarda
/// esses dados duplicados, é isso que <see cref="AcoesRealizadasViewModel.Reload"/> faz uma vez
/// ao carregar a lista, não a cada célula da grade.
/// </summary>
public sealed class AcaoRealizadaDisplay
{
    public int Id { get; init; }
    public int TorreId { get; init; }
    public string TorreNome { get; init; } = string.Empty;
    public int? ObjetoDetectadoId { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public System.DateTime DataHora { get; init; }
    public string? Observacao { get; init; }
}

/// <summary>
/// ViewModel da tela "Ações Realizadas" (Requisito 5 — auditoria de acionamentos). Só leitura:
/// os registros são gravados por <see cref="Services.FireControlService"/>, nunca por esta
/// tela. A persistência (SQLite) fica inteiramente dentro de
/// <see cref="SqliteAcaoRealizadaRepository"/> — esta ViewModel só enxerga a interface.
/// </summary>
public sealed class AcoesRealizadasViewModel : ViewModelBase
{
    private readonly IAcaoRealizadaRepository _repository;
    private readonly IObjetoDetectadoRepository _objetoRepository;

    public ObservableCollection<AcaoRealizadaDisplay> Itens { get; } = new();

    public AcoesRealizadasViewModel(IAcaoRealizadaRepository repository, IObjetoDetectadoRepository objetoRepository)
    {
        _repository = repository;
        _objetoRepository = objetoRepository;
    }

    /// <summary>Recarrega a lista a partir do repositório — chamado pela View no <c>Loaded</c>
    /// (mesmo padrão de <c>ObjetosDetectadosView</c>: a View é Singleton e só sai/volta da
    /// árvore visual, nunca é reconstruída).</summary>
    public void Reload()
    {
        Dictionary<int, ObjetoDetectado> objetosPorId = new();
        foreach (ObjetoDetectado objeto in _objetoRepository.GetAll())
        {
            objetosPorId[objeto.Id] = objeto;
        }

        Dictionary<int, string> torresPorId = new();
        foreach (TowerDefinition torre in AppConfig.Current.Towers)
        {
            torresPorId[torre.Id] = torre.Name;
        }

        Itens.Clear();
        foreach (AcaoRealizada acao in _repository.GetAll())
        {
            ObjetoDetectado? objeto = acao.ObjetoDetectadoId.HasValue && objetosPorId.TryGetValue(acao.ObjetoDetectadoId.Value, out ObjetoDetectado? encontrado)
                ? encontrado
                : null;

            Itens.Add(new AcaoRealizadaDisplay
            {
                Id = acao.Id,
                TorreId = acao.TorreId,
                TorreNome = torresPorId.TryGetValue(acao.TorreId, out string? nome) ? nome : $"Torre {acao.TorreId}",
                ObjetoDetectadoId = acao.ObjetoDetectadoId,
                X = objeto?.X,
                Y = objeto?.Y,
                DataHora = acao.DataHora,
                Observacao = acao.Observacao
            });
        }
    }
}
