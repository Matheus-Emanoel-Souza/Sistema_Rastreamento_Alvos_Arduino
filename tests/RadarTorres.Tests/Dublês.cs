using System.Collections.Generic;
using System.Collections.ObjectModel;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;

namespace RadarTorres.Tests;

/// <summary>
/// Dublês de teste escritos à mão (sem biblioteca de mock): as interfaces exercitadas têm poucos
/// membros e os testes precisam inspecionar o estado resultante, não verificar chamadas — uma
/// classe simples é mais legível aqui do que a configuração de um mock.
/// </summary>
internal sealed class LoggerFalso : ILoggingService
{
    public ObservableCollection<LogEntry> Entries { get; } = new();

    public List<string> Mensagens { get; } = new();

    public void Info(string message) => Mensagens.Add($"INFO {message}");

    public void Success(string message) => Mensagens.Add($"OK {message}");

    public void Warning(string message) => Mensagens.Add($"AVISO {message}");

    public void Error(string message) => Mensagens.Add($"ERRO {message}");

    public void Clear() => Mensagens.Clear();
}

/// <summary>Repositório de perfis em memória — o teste não deve tocar o JSON do usuário.</summary>
internal sealed class RepositorioPerfisEmMemoria : IPerfilAlvoRepository
{
    public List<PerfilAlvo> Gravados { get; private set; } = new();

    public int VezesQueGravou { get; private set; }

    public List<PerfilAlvo> Load() => Gravados;

    public void Save(List<PerfilAlvo> perfis)
    {
        VezesQueGravou++;
        Gravados = perfis;
    }
}

/// <summary>Associação câmera → quadrante fixa, informada pelo teste.</summary>
internal sealed class CameraQuadranteFalso : ICameraQuadranteService
{
    private readonly Dictionary<string, Quadrant> _porDispositivo = new();

    public IReadOnlyList<CameraQuadrante> Associacoes => _porDispositivo
        .Select(par => new CameraQuadrante { DispositivoId = par.Key, Nome = par.Key, Quadrante = par.Value })
        .ToList();

    public event EventHandler? AssociacoesAlteradas;

    public void Definir(string dispositivoId, Quadrant quadrante)
    {
        _porDispositivo[dispositivoId] = quadrante;
        AssociacoesAlteradas?.Invoke(this, EventArgs.Empty);
    }

    public Quadrant ObterQuadrante(string? dispositivoId) =>
        dispositivoId is not null && _porDispositivo.TryGetValue(dispositivoId, out Quadrant q) ? q : Quadrant.None;

    public CameraQuadrante? ObterCameraDoQuadrante(Quadrant quadrante) =>
        Associacoes.FirstOrDefault(a => a.Quadrante == quadrante);

    public ResultadoAtribuicaoQuadrante Atribuir(CameraDevice camera, Quadrant quadrante)
    {
        Definir(camera.Id, quadrante);
        return ResultadoAtribuicaoQuadrante.Ok();
    }
}

/// <summary>
/// Histórico de ocorrências em memória, com a MESMA regra de unicidade do SQLite
/// (RastreamentoId + Quadrante) — é o que permite testar a prevenção de duplicidade, inclusive
/// sob concorrência, sem depender de arquivo em disco.
/// </summary>
internal sealed class RepositorioOcorrenciasEmMemoria : IObjetoDetectadoRepository
{
    private readonly object _gate = new();
    private readonly List<ObjetoDetectado> _itens = new();
    private readonly List<OcorrenciaEstadoTransicao> _transicoes = new();
    private int _proximoId = 1;

    public int InsercoesRecusadasPorDuplicidade { get; private set; }

    public IReadOnlyList<ObjetoDetectado> GetAll()
    {
        lock (_gate) return _itens.ToList();
    }

    public ObjetoDetectado? GetById(int id)
    {
        lock (_gate) return _itens.FirstOrDefault(i => i.Id == id);
    }

    public ObjetoDetectado Add(ObjetoDetectado objeto)
    {
        lock (_gate)
        {
            objeto.Id = _proximoId++;
            _itens.Add(objeto);
            RegistrarTransicao(objeto.Id, objeto.Estado, null, "Ocorrência registrada.");
            return objeto;
        }
    }

    public ObjetoDetectado AdicionarSeNaoExistir(ObjetoDetectado objeto)
    {
        lock (_gate)
        {
            ObjetoDetectado? existente = _itens.FirstOrDefault(i =>
                i.RastreamentoId == objeto.RastreamentoId && i.Quadrante == objeto.Quadrante);

            if (existente is not null)
            {
                InsercoesRecusadasPorDuplicidade++;
                return existente;
            }

            objeto.Id = _proximoId++;
            _itens.Add(objeto);
            RegistrarTransicao(objeto.Id, objeto.Estado, null, "Ocorrência registrada.");
            return objeto;
        }
    }

    public void RegistrarDeteccaoContinuada(int id, DateTime quando)
    {
        lock (_gate)
        {
            ObjetoDetectado? item = _itens.FirstOrDefault(i => i.Id == id);
            if (item is not null) item.DataHoraUltimaDeteccao = quando;
        }
    }

    public bool AtualizarEstado(int id, EstadoOcorrencia estado, string? usuario, string? observacao)
    {
        lock (_gate)
        {
            ObjetoDetectado? item = _itens.FirstOrDefault(i => i.Id == id);
            if (item is null || item.Estado == estado) return false;

            item.Estado = estado;
            RegistrarTransicao(id, estado, usuario, observacao);
            return true;
        }
    }

    public IReadOnlyList<OcorrenciaEstadoTransicao> ObterHistoricoEstados(int objetoDetectadoId)
    {
        lock (_gate) return _transicoes.Where(t => t.ObjetoDetectadoId == objetoDetectadoId).ToList();
    }

    private void RegistrarTransicao(int objetoId, EstadoOcorrencia estado, string? usuario, string? observacao) =>
        _transicoes.Add(new OcorrenciaEstadoTransicao
        {
            Id = _transicoes.Count + 1,
            ObjetoDetectadoId = objetoId,
            Estado = estado,
            DataHora = DateTime.Now,
            Usuario = usuario,
            Observacao = observacao
        });
}
