using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using RadarTorres.App.Helpers;
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

    /// <summary>
    /// Perfil ativo guardado numa referência própria, em vez de procurado em
    /// <see cref="Perfis"/> a cada consulta.
    /// </summary>
    /// <remarks>
    /// A detecção consulta isto a cada rodada e, desde que ela roda em paralelo (card de
    /// Monitoramento + os quatro painéis da tela "Câmeras"), são várias threads consultando
    /// enquanto a tela "Perfis de Alvo" pode estar cadastrando ou removendo um perfil na thread
    /// de interface — percorrer a <see cref="ObservableCollection{T}"/> nessa situação lança
    /// "coleção modificada durante a enumeração". Ler um campo não tem esse problema.
    /// </remarks>
    private volatile PerfilAlvo? _perfilAtivo;

    public PerfilAlvo? PerfilAtivo => _perfilAtivo;

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
        _perfilAtivo = saved.FirstOrDefault(p => p.Selecionado);
    }

    public ResultadoPerfilAlvo Add(string nome, CorAlvo cor, FormatoAlvo formato, DimensoesAlvo dimensoes, IReadOnlyList<FotoReferenciaAlvo> fotos)
    {
        if (Validar(nome, formato, dimensoes, fotos, exigirMinimoDeFotos: true, out string? erro, out List<FotoReferenciaAlvo> fotosValidas, out DimensoesAlvo normalizadas))
        {
            var perfil = new PerfilAlvo
            {
                Id = _nextId++,
                Nome = nome.Trim(),
                Cor = cor,
                Formato = formato,
                TamanhoCm = normalizadas.TamanhoCm ?? 0,
                LarguraCm = normalizadas.LarguraCm,
                AlturaCm = normalizadas.AlturaCm,
                Fotos = fotosValidas
            };

            Perfis.Add(perfil);
            Persist();
            _logger.Info(
                $"Perfil de alvo criado: \"{perfil.Nome}\" ({perfil.Cor}/{perfil.Formato}, " +
                $"{perfil.DimensoesDescricao}, {perfil.TotalFotos} foto(s))");

            return ResultadoPerfilAlvo.Ok(perfil);
        }

        _logger.Warning($"Cadastro de perfil de alvo recusado: {erro}");
        return ResultadoPerfilAlvo.Falha(erro!);
    }

    /// <remarks>
    /// Altera o objeto JÁ presente em <see cref="Perfis"/> em vez de remover e inserir outro: o
    /// <see cref="PerfilAlvo.Id"/> é o que liga este alvo ao histórico de
    /// <see cref="ObjetoDetectado"/> (coluna <c>PerfilAlvoId</c>), então recriar o registro
    /// desvincularia silenciosamente todas as detecções anteriores. Também preserva a referência
    /// de objeto — a lista da tela, o perfil ativo e os rastreamentos em andamento continuam
    /// apontando para o mesmo perfil.
    /// </remarks>
    public ResultadoPerfilAlvo Atualizar(PerfilAlvo perfil, string nome, CorAlvo cor, FormatoAlvo formato, DimensoesAlvo dimensoes, IReadOnlyList<FotoReferenciaAlvo> fotos)
    {
        if (!Perfis.Contains(perfil))
        {
            return ResultadoPerfilAlvo.Falha("Este perfil não está mais cadastrado — recarregue a lista.");
        }

        // Mínimo de fotos vale no CADASTRO (decisão registrada em Docs/Projeto/LOG_SOLICITACOES.md):
        // na edição, perfis antigos (criados quando o sistema aceitava uma foto só) continuam
        // podendo ser corrigidos sem obrigar a refotografar o objeto na hora.
        if (!Validar(nome, formato, dimensoes, fotos, exigirMinimoDeFotos: false, out string? erro, out List<FotoReferenciaAlvo> fotosValidas, out DimensoesAlvo normalizadas))
        {
            _logger.Warning($"Atualização do perfil de alvo \"{perfil.Nome}\" recusada: {erro}");
            return ResultadoPerfilAlvo.Falha(erro!);
        }

        string descricaoAnterior = $"{perfil.Nome} ({perfil.Cor}/{perfil.Formato}, {perfil.DimensoesDescricao}, {perfil.TotalFotos} foto(s))";

        perfil.Nome = nome.Trim();
        perfil.Cor = cor;
        perfil.Formato = formato;
        perfil.TamanhoCm = normalizadas.TamanhoCm ?? 0;
        perfil.LarguraCm = normalizadas.LarguraCm;
        perfil.AlturaCm = normalizadas.AlturaCm;
        perfil.Fotos = fotosValidas;

        Persist();
        _logger.Info(
            $"Perfil de alvo #{perfil.Id} atualizado: {descricaoAnterior} → " +
            $"{perfil.Nome} ({perfil.Cor}/{perfil.Formato}, {perfil.DimensoesDescricao}, {perfil.TotalFotos} foto(s))");

        return ResultadoPerfilAlvo.Ok(perfil);
    }

    public void Remove(PerfilAlvo perfil)
    {
        if (!Perfis.Remove(perfil)) return;

        // Remover o perfil ativo deixa o sistema SEM alvo — a detecção precisa enxergar isso na
        // rodada seguinte, não continuar travada num perfil que não está mais cadastrado.
        if (ReferenceEquals(_perfilAtivo, perfil)) _perfilAtivo = null;

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
        _perfilAtivo = perfil;

        Persist();
        _logger.Info($"Perfil de alvo \"{perfil.Nome}\" selecionado como ativo");
    }

    /// <summary>
    /// Regras comuns a cadastro e edição. Devolve as fotos já filtradas (só as válidas, sem
    /// caminhos repetidos) e as dimensões normalizadas conforme o formato — é aqui que
    /// <see cref="PerfilAlvo.TamanhoCm"/> recebe a maior dimensão de um retângulo, mantendo
    /// compatível quem só sabe ler esse campo.
    /// </summary>
    private static bool Validar(
        string nome,
        FormatoAlvo formato,
        DimensoesAlvo dimensoes,
        IReadOnlyList<FotoReferenciaAlvo> fotos,
        bool exigirMinimoDeFotos,
        out string? erro,
        out List<FotoReferenciaAlvo> fotosValidas,
        out DimensoesAlvo normalizadas)
    {
        fotosValidas = new List<FotoReferenciaAlvo>();
        normalizadas = new DimensoesAlvo(null, null, null);

        if (string.IsNullOrWhiteSpace(nome))
        {
            erro = "Informe um nome para o perfil.";
            return false;
        }

        if (formato == FormatoAlvo.Retangular)
        {
            if (dimensoes.LarguraCm is not > 0 || dimensoes.AlturaCm is not > 0)
            {
                erro = "Objeto retangular exige largura e altura maiores que zero, em centímetros.";
                return false;
            }

            normalizadas = new DimensoesAlvo(
                Math.Max(dimensoes.LarguraCm.Value, dimensoes.AlturaCm.Value),
                dimensoes.LarguraCm,
                dimensoes.AlturaCm);
        }
        else
        {
            if (dimensoes.TamanhoCm is not > 0)
            {
                erro = formato == FormatoAlvo.Redondo
                    ? "Informe o diâmetro do objeto em centímetros (maior que zero)."
                    : "Informe o lado do objeto em centímetros (maior que zero).";
                return false;
            }

            normalizadas = DimensoesAlvo.Unica(dimensoes.TamanhoCm.Value);
        }

        var caminhosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (FotoReferenciaAlvo foto in fotos)
        {
            if (!ValidacaoFotoAlvo.EhValida(foto.Caminho, out string motivo))
            {
                erro = $"Fotografia inválida: {motivo}";
                return false;
            }

            if (caminhosVistos.Add(foto.Caminho)) fotosValidas.Add(foto);
        }

        if (exigirMinimoDeFotos && fotosValidas.Count < PerfilAlvo.MinimoFotos)
        {
            int faltam = PerfilAlvo.MinimoFotos - fotosValidas.Count;
            erro = $"São necessárias no mínimo {PerfilAlvo.MinimoFotos} fotografias válidas — faltam {faltam}.";
            return false;
        }

        erro = null;
        return true;
    }

    private void Persist() => _repository.Save(Perfis.ToList());
}
