using System;
using System.Collections.Generic;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Decorator de <see cref="IAcaoRealizadaRepository"/>: toda leitura/escrita tenta primeiro o
/// banco de dados (<see cref="SqliteAcaoRealizadaRepository"/>) e, se a operação falhar por
/// qualquer motivo (arquivo de banco bloqueado, disco cheio, permissão negada etc.), cai
/// automaticamente para o CSV (<see cref="CsvAcaoRealizadaRepository"/>) — o módulo continua
/// funcionando normalmente e nenhum acionamento deixa de ser registrado, só muda onde fica
/// gravado. A cada chamada tenta o banco de novo primeiro (sem estado "banco morto"
/// permanente), então uma reconexão é detectada sozinha na próxima ação.
/// </summary>
public sealed class ResilientAcaoRealizadaRepository : IAcaoRealizadaRepository
{
    private readonly SqliteAcaoRealizadaRepository _banco;
    private readonly CsvAcaoRealizadaRepository _fallback;
    private readonly ILoggingService _logger;
    private bool _avisouFallback;

    public ResilientAcaoRealizadaRepository(ILoggingService logger)
    {
        _banco = new SqliteAcaoRealizadaRepository();
        _fallback = new CsvAcaoRealizadaRepository();
        _logger = logger;
    }

    public IReadOnlyList<AcaoRealizada> GetAll()
    {
        try
        {
            IReadOnlyList<AcaoRealizada> itens = _banco.GetAll();
            _avisouFallback = false;
            return itens;
        }
        catch (Exception ex)
        {
            AvisarFallback(ex);
            return _fallback.GetAll();
        }
    }

    public AcaoRealizada Add(AcaoRealizada acao)
    {
        try
        {
            AcaoRealizada salva = _banco.Add(acao);
            _avisouFallback = false;
            return salva;
        }
        catch (Exception ex)
        {
            AvisarFallback(ex);
            return _fallback.Add(acao);
        }
    }

    private void AvisarFallback(Exception ex)
    {
        // Só loga uma vez seguida — evita spam no console de eventos se o banco continuar
        // indisponível durante vários acionamentos consecutivos.
        if (_avisouFallback) return;
        _avisouFallback = true;
        _logger.Warning($"Sem ligação com o banco de dados de Ações Realizadas — gravando localmente (CSV) até a próxima tentativa. Detalhe: {ex.Message}");
    }
}
