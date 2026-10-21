using System.Collections.Generic;
using System.Collections.ObjectModel;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// Linha de exibição da tela "Ações Realizadas": uma <see cref="AcaoRealizada"/> com as
/// coordenadas do alvo já resolvidas via <see cref="AcaoRealizada.ObjetoDetectadoId"/> — a
/// tabela em si não guarda X/Y/Z (ver comentário no modelo), quem precisa exibir a posição
/// consulta <c>objetos_detectados</c> por esse Id, e é isso que <see cref="AcoesRealizadasViewModel.Reload"/>
/// faz uma vez ao carregar a lista, não a cada célula da grade.
/// </summary>
public sealed class AcaoRealizadaDisplay
{
    public int Id { get; init; }
    public string TorreAcao { get; init; } = string.Empty;
    public int? ObjetoDetectadoId { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public System.DateTime DataHora { get; init; }
    public string? UsuarioResponsavel { get; init; }
    public OrigemAcao Origem { get; init; }
    public ResultadoAcao Resultado { get; init; }
    public string? Observacao { get; init; }
}

/// <summary>
/// ViewModel da tela "Ações Realizadas" (Requisito 5 — auditoria de acionamentos). Só leitura:
/// os registros são gravados por <see cref="Services.FireControlService"/>, nunca por esta
/// tela. A persistência (SQLite com fallback para CSV) fica inteiramente dentro de
/// <see cref="ResilientAcaoRealizadaRepository"/> — esta ViewModel só enxerga a interface.
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

        Itens.Clear();
        foreach (AcaoRealizada acao in _repository.GetAll())
        {
            ObjetoDetectado? objeto = acao.ObjetoDetectadoId.HasValue && objetosPorId.TryGetValue(acao.ObjetoDetectadoId.Value, out ObjetoDetectado? encontrado)
                ? encontrado
                : null;

            Itens.Add(new AcaoRealizadaDisplay
            {
                Id = acao.Id,
                TorreAcao = acao.TorreAcao,
                ObjetoDetectadoId = acao.ObjetoDetectadoId,
                X = objeto?.X,
                Y = objeto?.Y,
                DataHora = acao.DataHora,
                UsuarioResponsavel = acao.UsuarioResponsavel,
                Origem = acao.Origem,
                Resultado = acao.Resultado,
                Observacao = acao.Observacao
            });
        }
    }
}
