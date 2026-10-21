using System.Collections.ObjectModel;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da tela "Histórico de Modos" (Requisito 6 — auditoria de troca de modo). Só
/// leitura: os registros são gravados por <see cref="MainViewModel"/> (setter de
/// <c>CurrentMode</c>), nunca por esta tela.
/// </summary>
public sealed class HistoricoModosViewModel : ViewModelBase
{
    private readonly IModoAtualTorreRepository _repository;

    public ObservableCollection<ModoAtualTorre> Itens { get; } = new();

    public HistoricoModosViewModel(IModoAtualTorreRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Recarrega a lista a partir do repositório — chamado pela View no <c>Loaded</c>
    /// (mesmo padrão de <c>ObjetosDetectadosView</c>: a View é Singleton e só sai/volta da
    /// árvore visual, nunca é reconstruída).</summary>
    public void Reload()
    {
        Itens.Clear();
        foreach (ModoAtualTorre item in _repository.GetAll())
        {
            Itens.Add(item);
        }
    }
}
