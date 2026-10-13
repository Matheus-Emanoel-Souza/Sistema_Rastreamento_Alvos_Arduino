using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="IPerfilAlvoService"/>. Deliberadamente livre de qualquer
/// referência a WPF/XAML, mesmo princípio de <see cref="ZonaMortaService"/> — pura lógica de
/// negócio, testável isoladamente. Carrega os perfis salvos uma única vez na construção e
/// grava o arquivo inteiro a cada mudança (poucos perfis, poucas mudanças — I/O irrelevante).
/// </summary>
public sealed class PerfilAlvoService : IPerfilAlvoService
{
    private readonly IPerfilAlvoRepository _repository;
    private readonly ILoggingService _logger;
    private int _nextId;

    public ObservableCollection<PerfilAlvo> Perfis { get; } = new();

    public PerfilAlvo? PerfilAtivo => Perfis.FirstOrDefault(p => p.Selecionado);

    public PerfilAlvoService(IPerfilAlvoRepository repository, ILoggingService logger)
    {
        _repository = repository;
        _logger = logger;

        List<PerfilAlvo> saved = _repository.Load();
        foreach (PerfilAlvo perfil in saved)
        {
            Perfis.Add(perfil);
        }
        _nextId = saved.Count > 0 ? saved.Max(p => p.Id) + 1 : 1;
    }

    public PerfilAlvo Add(string nome, CorAlvo cor, FormatoAlvo formato, double tamanhoCm)
    {
        var perfil = new PerfilAlvo
        {
            Id = _nextId++,
            Nome = nome,
            Cor = cor,
            Formato = formato,
            TamanhoCm = tamanhoCm
        };

        Perfis.Add(perfil);
        Persist();
        _logger.Info($"Perfil de alvo criado: \"{perfil.Nome}\" ({perfil.Cor}/{perfil.Formato}, {perfil.TamanhoCm:0.0} cm)");
        return perfil;
    }

    public void Remove(PerfilAlvo perfil)
    {
        if (!Perfis.Remove(perfil)) return;

        Persist();
        _logger.Info($"Perfil de alvo \"{perfil.Nome}\" removido");
    }

    public void SelecionarAtivo(PerfilAlvo perfil)
    {
        if (!Perfis.Contains(perfil)) return;
        if (perfil.Selecionado) return;

        foreach (PerfilAlvo outro in Perfis)
        {
            if (outro.Selecionado) outro.Selecionado = false;
        }
        perfil.Selecionado = true;

        Persist();
        _logger.Info($"Perfil de alvo \"{perfil.Nome}\" selecionado como ativo");
    }

    public void DefinirFotoReferencia(PerfilAlvo perfil, string caminhoArquivo)
    {
        if (!Perfis.Contains(perfil)) return;

        perfil.CaminhoFotoReferencia = caminhoArquivo;
        Persist();
    }

    private void Persist() => _repository.Save(Perfis.ToList());
}
