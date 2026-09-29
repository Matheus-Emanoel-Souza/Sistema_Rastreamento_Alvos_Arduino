using System.Collections.Generic;
using System.Collections.ObjectModel;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// Linha de exibição da tela "Histórico de Modos": um <see cref="ModoAtualTorre"/> com o login
/// do usuário já resolvido a partir de <see cref="ModoAtualTorre.UsuarioId"/> — a tabela em si
/// só guarda o Id (ver <c>Docs/Documentos_Entregaveis/Banco_de_Dados/schema.sql</c>).
/// </summary>
public sealed class ModoAtualTorreDisplay
{
    public int Id { get; init; }
    public string ModoAnterior { get; init; } = string.Empty;
    public string NovoModo { get; init; } = string.Empty;
    public string? UsuarioLogin { get; init; }
    public System.DateTime DataHora { get; init; }
}

/// <summary>
/// ViewModel da tela "Histórico de Modos" (Requisito 6 — auditoria de troca de modo). Só
/// leitura: os registros são gravados por <see cref="MainViewModel"/> (setter de
/// <c>CurrentMode</c>), nunca por esta tela.
/// </summary>
public sealed class HistoricoModosViewModel : ViewModelBase
{
    private readonly IModoAtualTorreRepository _repository;
    private readonly IUsuarioRepository _usuarioRepository;

    public ObservableCollection<ModoAtualTorreDisplay> Itens { get; } = new();

    public HistoricoModosViewModel(IModoAtualTorreRepository repository, IUsuarioRepository usuarioRepository)
    {
        _repository = repository;
        _usuarioRepository = usuarioRepository;
    }

    /// <summary>Recarrega a lista a partir do repositório — chamado pela View no <c>Loaded</c>
    /// (mesmo padrão de <c>ObjetosDetectadosView</c>: a View é Singleton e só sai/volta da
    /// árvore visual, nunca é reconstruída).</summary>
    public void Reload()
    {
        Dictionary<int, string> loginsPorId = new();
        foreach (Usuario usuario in _usuarioRepository.GetAll())
        {
            loginsPorId[usuario.Id] = usuario.Login;
        }

        Itens.Clear();
        foreach (ModoAtualTorre item in _repository.GetAll())
        {
            Itens.Add(new ModoAtualTorreDisplay
            {
                Id = item.Id,
                ModoAnterior = item.ModoAnterior,
                NovoModo = item.NovoModo,
                UsuarioLogin = item.UsuarioId.HasValue && loginsPorId.TryGetValue(item.UsuarioId.Value, out string? login) ? login : null,
                DataHora = item.DataHora
            });
        }
    }
}
