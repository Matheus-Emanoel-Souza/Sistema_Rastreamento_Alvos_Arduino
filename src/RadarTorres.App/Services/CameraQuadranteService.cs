using System;
using System.Collections.Generic;
using System.Linq;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="ICameraQuadranteService"/>. Mantém as associações em memória
/// (quatro linhas, no máximo) e regrava a do dispositivo alterado — a detecção consulta o
/// quadrante a cada ocorrência registrada e não pode depender de ida ao banco nesse caminho.
/// </summary>
/// <remarks>
/// Sem referência a WPF, igual a <see cref="PerfilAlvoService"/>/<see cref="ZonaMortaService"/>.
/// A lista é protegida por <c>lock</c> porque a leitura acontece nas threads de detecção (uma por
/// câmera) enquanto a escrita vem da thread de interface.
/// </remarks>
public sealed class CameraQuadranteService : ICameraQuadranteService
{
    private readonly ICameraQuadranteRepository _repository;
    private readonly ILoggingService _logger;
    private readonly object _gate = new();

    private List<CameraQuadrante> _associacoes;

    public CameraQuadranteService(ICameraQuadranteRepository repository, ILoggingService logger)
    {
        _repository = repository;
        _logger = logger;
        _associacoes = _repository.GetAll().ToList();
    }

    public IReadOnlyList<CameraQuadrante> Associacoes
    {
        get { lock (_gate) return _associacoes.ToList(); }
    }

    public event EventHandler? AssociacoesAlteradas;

    public Quadrant ObterQuadrante(string? dispositivoId)
    {
        if (string.IsNullOrEmpty(dispositivoId)) return Quadrant.None;

        lock (_gate)
        {
            return _associacoes.FirstOrDefault(a => a.DispositivoId == dispositivoId)?.Quadrante ?? Quadrant.None;
        }
    }

    public CameraQuadrante? ObterCameraDoQuadrante(Quadrant quadrante)
    {
        lock (_gate)
        {
            return _associacoes.FirstOrDefault(a => a.Quadrante == quadrante);
        }
    }

    public ResultadoAtribuicaoQuadrante Atribuir(CameraDevice camera, Quadrant quadrante)
    {
        if (camera is null || string.IsNullOrWhiteSpace(camera.Id))
        {
            return ResultadoAtribuicaoQuadrante.Falha("Selecione uma câmera antes de definir o quadrante.");
        }

        try
        {
            if (quadrante == Quadrant.None)
            {
                _repository.Remover(camera.Id);
                Recarregar();
                _logger.Info($"Câmera \"{camera.Name}\" não monitora mais nenhum quadrante.");
                return ResultadoAtribuicaoQuadrante.Ok();
            }

            if (quadrante is not (Quadrant.Q1 or Quadrant.Q2 or Quadrant.Q3 or Quadrant.Q4))
            {
                return ResultadoAtribuicaoQuadrante.Falha("Quadrante inválido — use Q1, Q2, Q3 ou Q4.");
            }

            CameraQuadrante? ocupante = ObterCameraDoQuadrante(quadrante);

            _repository.Salvar(new CameraQuadrante
            {
                DispositivoId = camera.Id,
                Nome = camera.Name,
                Quadrante = quadrante,
                AtualizadoEm = DateTime.Now
            });

            Recarregar();

            if (ocupante is not null && ocupante.DispositivoId != camera.Id)
            {
                _logger.Warning(
                    $"Quadrante {QuadrantHelper.ToDisplayLabel(quadrante)} transferido de \"{ocupante.Nome}\" " +
                    $"para \"{camera.Name}\" — um quadrante é monitorado por uma única câmera.");
            }
            else
            {
                _logger.Info($"Câmera \"{camera.Name}\" passou a monitorar o quadrante {QuadrantHelper.ToDisplayLabel(quadrante)}.");
            }

            return ResultadoAtribuicaoQuadrante.Ok();
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível salvar o quadrante da câmera \"{camera.Name}\": {ex.Message}");
            return ResultadoAtribuicaoQuadrante.Falha($"Não foi possível salvar: {ex.Message}");
        }
    }

    private void Recarregar()
    {
        List<CameraQuadrante> atuais = _repository.GetAll().ToList();
        lock (_gate) _associacoes = atuais;
        AssociacoesAlteradas?.Invoke(this, EventArgs.Empty);
    }
}
