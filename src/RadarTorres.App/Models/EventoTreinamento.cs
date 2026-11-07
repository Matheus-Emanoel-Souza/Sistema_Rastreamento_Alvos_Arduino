using System;
using System.ComponentModel;

namespace RadarTorres.App.Models;

/// <summary>Natureza de um <see cref="EventoTreinamento"/> — define a cor da linha na tela (ver Converters/TipoEventoTreinamentoToBrushConverter).</summary>
/// <remarks>
/// Os valores são gravados no banco pelo NOME (ver <see cref="Repositories.IEventoTreinamentoRepository"/>),
/// não pelo número: renomear um membro quebraria a leitura de registros antigos, mas reordená-los
/// não. Membros novos podem ser acrescentados ao fim sem afetar o que já foi gravado.
/// </remarks>
public enum TipoEventoTreinamento
{
    /// <summary>Acontecimento normal do fluxo (início, parada, marco gravado).</summary>
    Informacao,

    /// <summary>Progresso real do aprendizado (novo melhor fitness, meta alcançada).</summary>
    Avanco,

    /// <summary>Algo que merece atenção, mas não interrompe o treinamento (troca para simplificação).</summary>
    Atencao,

    /// <summary>Falha — o treinamento parou ou uma operação não pôde ser concluída.</summary>
    Falha
}

/// <summary>
/// Uma linha do histórico de execução da tela de treinamento: o que aconteceu, em que geração e
/// quando.
/// </summary>
/// <remarks>
/// <para>
/// Existe porque esses acontecimentos (início, parada, falha na thread de fundo, novo recorde de
/// fitness, troca de fase da regulação de complexidade, marco gravado) só iam para o
/// <see cref="Services.ILoggingService"/> — ou seja, ficavam invisíveis para quem está olhando a
/// tela justamente no momento em que ocorrem.
/// </para>
/// <para>
/// É persistido (ver <see cref="Repositories.IEventoTreinamentoRepository"/>) porque um
/// treinamento pode rodar por horas sem ninguém olhando: se ele falhar durante a madrugada, a
/// mensagem do erro é justamente o que se precisa no dia seguinte — e, só em memória, ela morreria
/// junto com o aplicativo. O histórico por geração guarda os NÚMEROS; este guarda o que aconteceu.
/// </para>
/// </remarks>
public sealed class EventoTreinamento : INotifyPropertyChanged
{
    public int Id { get; set; }

    public required string Mensagem { get; init; }

    /// <summary>Geração em que o evento ocorreu, ou <c>null</c> quando ele não pertence a uma geração específica (ex.: "treinamento iniciado").</summary>
    public int? Geracao { get; init; }

    public TipoEventoTreinamento Tipo { get; init; } = TipoEventoTreinamento.Informacao;

    public DateTime DataHora { get; init; } = DateTime.Now;

    /// <summary>
    /// Identificador da sessão de treinamento (um <see cref="Guid"/> novo a cada "Iniciar" — ver
    /// <see cref="ViewModels.TreinamentoControladorViewModel"/>) que gerou este evento — mesmo
    /// conceito já usado em <see cref="MarcoFitnessTreinamento.ExecucaoId"/>. Permite diferenciar
    /// "o que aconteceu agora" de "o que aconteceu numa execução anterior" ao reabrir a tela.
    /// Registros gravados antes deste campo existir usam <c>"legada"</c> (ver migração em
    /// <see cref="Repositories.SqliteEventoTreinamentoRepository"/>).
    /// </summary>
    public string ExecucaoId { get; init; } = "legada";

    private bool _ehSessaoAtual;

    /// <summary>
    /// Se este evento pertence à sessão de treinamento ATUALMENTE aberta na tela — calculado pela
    /// ViewModel ao carregar/registrar (comparando com o <see cref="ExecucaoId"/> da sessão em
    /// curso), não persistido no banco: é um estado de apresentação, não um fato histórico.
    /// Notifica <see cref="PropertyChanged"/> porque muda DEPOIS do item já estar na tela (um novo
    /// "Iniciar" torna os eventos anteriores "de uma sessão passada" sem recriar a lista) — mesmo
    /// padrão já usado em <see cref="Tower"/>/<see cref="Target"/> para propriedades mutáveis.
    /// </summary>
    public bool EhSessaoAtual
    {
        get => _ehSessaoAtual;
        set
        {
            if (_ehSessaoAtual == value) return;
            _ehSessaoAtual = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EhSessaoAtual)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Rótulo da geração para a tela — traço quando o evento não tem geração associada.</summary>
    public string GeracaoTexto => Geracao.HasValue ? Geracao.Value.ToString("N0") : "—";
}
