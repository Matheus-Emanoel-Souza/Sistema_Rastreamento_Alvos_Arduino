using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Retrato completo do melhor genoma a cada 1.000 gerações de treinamento: pontuação, decisões
/// (acertos/erros/fugas) e o genoma inteiro serializado, para poder recarregar e reproduzir
/// exatamente aquele comportamento depois. Gravado só por inserção — nunca atualizado nem
/// sobrescrito por gerações seguintes (ver
/// <see cref="Repositories.IInstantaneoComportamentoRepository"/>) — é precisamente esse
/// histórico comparativo, marco a marco, que permite estudar caso a caso se houve evolução real
/// ou só oscilação de ruído.
/// </summary>
public class InstantaneoComportamentoMelhorGene
{
    public int Id { get; set; }

    /// <summary>Geração em que este marco foi registrado (1000, 2000, 3000...).</summary>
    public int GeracaoMarco { get; set; }

    public double Fitness { get; set; }

    public int AlvosAbatidos { get; set; }
    public int AlvosEscapados { get; set; }

    /// <summary>Disparos autorizados que erraram por sorte (ver CalculadoraErroDisparo) — distinto de fuga (nem chegou a disparar).</summary>
    public int TirosErrados { get; set; }

    /// <summary>Abatidos / (Abatidos + TirosErrados) — precisão de quem chegou a disparar. <c>null</c> se nenhum disparo foi tentado.</summary>
    public double? TaxaDeAcerto { get; set; }

    /// <summary>Abatidos / (Abatidos + Escapados) — cobertura real contra os alvos que apareceram no episódio. <c>null</c> se nenhum alvo apareceu.</summary>
    public double? TaxaDeCobertura { get; set; }

    /// <summary>Genoma completo (pesos + bias) serializado em JSON — permite recarregar e reproduzir exatamente este comportamento mais tarde.</summary>
    public string GenomaJson { get; set; } = string.Empty;

    public DateTime DataHora { get; set; }
}
