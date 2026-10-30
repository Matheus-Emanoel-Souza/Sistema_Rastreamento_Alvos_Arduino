using System;

namespace RadarTorres.App.ViewModels;

/// <summary>Natureza de um <see cref="EventoTreinamento"/> — define a cor com que a linha aparece no painel.</summary>
public enum TipoEventoTreinamento
{
    /// <summary>Acontecimento normal do fluxo (início, parada, marco gravado).</summary>
    Informacao,

    /// <summary>Progresso real do aprendizado (novo melhor fitness, meta alcançada).</summary>
    Avanco,

    /// <summary>Algo que merece atenção, mas não interrompe o treinamento (estagnação prolongada, troca para simplificação).</summary>
    Atencao,

    /// <summary>Falha — o treinamento parou ou uma operação não pôde ser concluída.</summary>
    Falha
}

/// <summary>
/// Uma linha do histórico de execução da tela de treinamento: o que aconteceu, em que geração e
/// quando. Existe porque esses acontecimentos (início, parada, falha na thread de fundo, novo
/// recorde de fitness, troca de fase da regulação de complexidade, marco gravado) só iam para o
/// <see cref="Services.ILoggingService"/> — ou seja, ficavam invisíveis para quem está olhando a
/// tela justamente no momento em que ocorrem.
/// </summary>
public sealed class EventoTreinamento
{
    public required string Mensagem { get; init; }

    /// <summary>Geração em que o evento ocorreu, ou <c>null</c> quando ele não pertence a uma geração específica (ex.: "treinamento iniciado").</summary>
    public int? Geracao { get; init; }

    public TipoEventoTreinamento Tipo { get; init; } = TipoEventoTreinamento.Informacao;

    public DateTime DataHora { get; init; } = DateTime.Now;

    /// <summary>Rótulo da geração para a tela — traço quando o evento não tem geração associada.</summary>
    public string GeracaoTexto => Geracao.HasValue ? Geracao.Value.ToString("N0") : "—";

    /// <summary>Cor da linha conforme <see cref="Tipo"/> — mesma paleta já usada nos outros cards da tela.</summary>
    public string Cor => Tipo switch
    {
        TipoEventoTreinamento.Avanco => "#4ADE80",
        TipoEventoTreinamento.Atencao => "#FBBF24",
        TipoEventoTreinamento.Falha => "#F87171",
        _ => "#94A3B8"
    };
}
