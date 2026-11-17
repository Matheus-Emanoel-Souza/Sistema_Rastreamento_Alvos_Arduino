using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using RadarTorres.App.Data;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cobertura do histórico de ACONTECIMENTOS da tela "Treinamento do Controlador" (ver
/// SqliteEventoTreinamentoRepository). O ponto do registro é sobreviver ao fechamento do
/// aplicativo: um treinamento que roda a noite inteira e morre por exceção só deixa a razão da
/// parada aqui.
/// </summary>
/// <remarks>Usa um arquivo SQLite temporário via <c>SqliteConnectionFactory.UsarBancoDeTeste</c> — mesmo isolamento de <see cref="SqliteHistoricoTreinamentoRepositoryTests"/>.</remarks>
public sealed class SqliteEventoTreinamentoRepositoryTests : IDisposable
{
    private readonly string _pasta;
    private readonly SqliteEventoTreinamentoRepository _repositorio = new();

    public SqliteEventoTreinamentoRepositoryTests()
    {
        _pasta = FotosDeTeste.CriarPasta();
        SqliteConnectionFactory.UsarBancoDeTeste(Path.Combine(_pasta, "teste.db"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        FotosDeTeste.Limpar(_pasta);
    }

    private static EventoTreinamento Evento(
        string mensagem,
        int? geracao = 1,
        TipoEventoTreinamento tipo = TipoEventoTreinamento.Informacao,
        string? execucaoId = null) => new()
        {
            Mensagem = mensagem,
            Geracao = geracao,
            Tipo = tipo,
            DataHora = DateTime.Now,
            ExecucaoId = execucaoId ?? "legada",
        };

    [Fact]
    public void Registrar_PreservaMensagemGeracaoETipo()
    {
        _repositorio.Registrar(Evento("Treinamento interrompido por erro: falta memória.", geracao: 1240, tipo: TipoEventoTreinamento.Falha));

        var eventos = _repositorio.ObterUltimos();

        Assert.Single(eventos);
        Assert.Equal("Treinamento interrompido por erro: falta memória.", eventos[0].Mensagem);
        Assert.Equal(1240, eventos[0].Geracao);
        Assert.Equal(TipoEventoTreinamento.Falha, eventos[0].Tipo);
    }

    /// <summary>
    /// A tela mostra o mais recente no topo, então o repositório devolve nessa mesma ordem — ao
    /// contrário do histórico por geração, que é cronológico. Se a ordem se inverter, a tela passa
    /// a abrir mostrando o evento mais antigo como se fosse o último acontecimento.
    /// </summary>
    [Fact]
    public void ObterUltimos_DevolveDoMaisRecenteParaOMaisAntigo()
    {
        _repositorio.Registrar(Evento("primeiro"));
        _repositorio.Registrar(Evento("segundo"));
        _repositorio.Registrar(Evento("terceiro"));

        var eventos = _repositorio.ObterUltimos();

        Assert.Equal(["terceiro", "segundo", "primeiro"], [eventos[0].Mensagem, eventos[1].Mensagem, eventos[2].Mensagem]);
    }

    /// <summary>
    /// Eventos que não pertencem a geração nenhuma ("treinamento iniciado") precisam continuar
    /// sem geração ao voltar do banco: zero ali seria lido como a geração 0, que é a população
    /// inicial — um acontecimento real e diferente.
    /// </summary>
    [Fact]
    public void Registrar_SemGeracao_PreservaNuloEmVezDeZero()
    {
        _repositorio.Registrar(Evento("Treinamento iniciado.", geracao: null));

        var eventos = _repositorio.ObterUltimos();

        Assert.Null(eventos[0].Geracao);
        Assert.Equal("—", eventos[0].GeracaoTexto);
    }

    [Fact]
    public void Registrar_AoExcederOLimite_DescartaOsMaisAntigosPrimeiro()
    {
        for (int i = 1; i <= 1005; i++) _repositorio.Registrar(Evento($"evento {i}", geracao: i));

        var eventos = _repositorio.ObterUltimos(2000);

        Assert.Equal(1000, eventos.Count);
        Assert.Equal(1005, eventos[0].Geracao);  // mais recente no topo
        Assert.Equal(6, eventos[^1].Geracao);    // os 5 primeiros foram descartados
    }

    /// <summary>
    /// ExecucaoId diferencia eventos da sessão de treinamento atual dos de uma execução anterior
    /// (ver EventoTreinamento.EhSessaoAtual, computado pela ViewModel, não pelo repositório). Sem
    /// valor explícito, usa "legada" (registros gravados antes deste campo existir).
    /// </summary>
    [Fact]
    public void Registrar_PreservaExecucaoId_OuUsaLegadaPorPadrao()
    {
        _repositorio.Registrar(Evento("com execucao", execucaoId: "abc123"));
        _repositorio.Registrar(Evento("sem execucao"));

        var eventos = _repositorio.ObterUltimos();

        Assert.Equal("abc123", eventos.Single(e => e.Mensagem == "com execucao").ExecucaoId);
        Assert.Equal("legada", eventos.Single(e => e.Mensagem == "sem execucao").ExecucaoId);
    }

    [Fact]
    public void LimparTudo_RemoveTodosOsEventos()
    {
        _repositorio.Registrar(Evento("um"));
        _repositorio.Registrar(Evento("dois"));

        _repositorio.LimparTudo();

        Assert.Empty(_repositorio.ObterUltimos());
    }

    /// <summary>
    /// O tipo é gravado pelo NOME do membro. Este teste fixa isso: se alguém trocar a gravação
    /// para o valor numérico, reordenar o enum passaria a reinterpretar registros antigos — um
    /// "Falha" viraria "Informacao" silenciosamente.
    /// </summary>
    [Theory]
    [InlineData(TipoEventoTreinamento.Informacao)]
    [InlineData(TipoEventoTreinamento.Avanco)]
    [InlineData(TipoEventoTreinamento.Atencao)]
    [InlineData(TipoEventoTreinamento.Falha)]
    public void Registrar_PreservaCadaTipoDeEvento(TipoEventoTreinamento tipo)
    {
        _repositorio.Registrar(Evento("mensagem", tipo: tipo));

        Assert.Equal(tipo, _repositorio.ObterUltimos()[0].Tipo);
    }
}
