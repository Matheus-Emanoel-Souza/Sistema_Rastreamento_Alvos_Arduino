using System.Collections.ObjectModel;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da tela "Ações Realizadas" (Requisito 5 — auditoria de acionamentos). Só leitura:
/// os registros são gravados por <see cref="Services.FireControlService"/>, nunca por esta
/// tela. A persistência (SQLite com fallback para CSV) fica inteiramente dentro de
/// <see cref="ResilientAcaoRealizadaRepository"/> — esta ViewModel só enxerga a interface.
/// </summary>
public sealed class AcoesRealizadasViewModel : ViewModelBase
{
    private readonly IAcaoRealizadaRepository _repository;

    public ObservableCollection<AcaoRealizada> Itens { get; } = new();

    public AcoesRealizadasViewModel(IAcaoRealizadaRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Recarrega a lista a partir do repositório — chamado pela View no <c>Loaded</c>
    /// (mesmo padrão de <c>ObjetosDetectadosView</c>: a View é Singleton e só sai/volta da
    /// árvore visual, nunca é reconstruída).</summary>
    public void Reload()
    {
        Itens.Clear();
        foreach (AcaoRealizada item in _repository.GetAll())
        {
            Itens.Add(item);
        }
    }
}
