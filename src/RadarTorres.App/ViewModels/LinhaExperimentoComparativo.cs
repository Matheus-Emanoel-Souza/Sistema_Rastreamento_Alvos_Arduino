using System;
using System.Globalization;
using RadarTorres.App.Models;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// Uma linha das tabelas comparativas de hiperparâmetros da tela "Estatísticas de Treinamento".
/// Envolve um <see cref="ExperimentoHiperparametros"/> real OU representa uma configuração do
/// plano que ainda NÃO foi executada (<see cref="Executado"/> = false) — nesse caso todas as
/// métricas aparecem como "—", nunca com valor estimado, para a tabela deixar explícito o que
/// foi de fato treinado e o que falta treinar.
/// </summary>
public sealed class LinhaExperimentoComparativo
{
    private readonly ExperimentoHiperparametros? _experimento;

    private LinhaExperimentoComparativo(
        int tamanhoPopulacao,
        int neuroniosOcultos,
        ExperimentoHiperparametros? experimento,
        bool ehBaseline)
    {
        TamanhoPopulacao = tamanhoPopulacao;
        NeuroniosOcultos = neuroniosOcultos;
        _experimento = experimento;
        EhBaseline = ehBaseline;
    }

    /// <summary>Linha de uma configuração efetivamente treinada.</summary>
    public static LinhaExperimentoComparativo Executada(ExperimentoHiperparametros experimento, bool ehBaseline) =>
        new(experimento.TamanhoPopulacao, experimento.NeuroniosOcultos, experimento, ehBaseline);

    /// <summary>Linha de uma configuração prevista no plano mas ainda sem execução registrada.</summary>
    public static LinhaExperimentoComparativo Pendente(int tamanhoPopulacao, int neuroniosOcultos, bool ehBaseline) =>
        new(tamanhoPopulacao, neuroniosOcultos, experimento: null, ehBaseline);

    public int TamanhoPopulacao { get; }
    public int NeuroniosOcultos { get; }

    /// <summary>A configuração de referência, compartilhada pelas duas séries (ver <see cref="Services.ConfiguracaoExperimento"/>).</summary>
    public bool EhBaseline { get; }

    public bool Executado => _experimento is not null;

    public double FitnessFinal => _experimento?.FitnessFinal ?? 0;

    /// <summary>Marcado pela ViewModel na linha de maior <see cref="FitnessFinal"/> da tabela.</summary>
    public bool MelhorFitness { get; set; }

    /// <summary>Marcado pela ViewModel na linha de maior <see cref="FitnessPorSegundo"/> da tabela (só entre as que têm tempo medido).</summary>
    public bool MelhorCustoBeneficio { get; set; }

    /// <summary>
    /// Fitness final por segundo de treinamento — a métrica de custo-benefício: uma configuração
    /// que chega quase ao mesmo fitness em metade do tempo é preferível. <c>null</c> quando o
    /// registro é anterior à medição de tempo (ou a configuração não foi executada).
    /// </summary>
    public double? FitnessPorSegundo =>
        _experimento?.TempoSegundos is > 0 ? _experimento.FitnessFinal / _experimento.TempoSegundos.Value : null;

    public string GeracoesTexto => _experimento is null ? "—" : _experimento.Geracoes.ToString(CultureInfo.CurrentCulture);

    public string FitnessFinalTexto => _experimento is null ? "—" : _experimento.FitnessFinal.ToString("0.00", CultureInfo.CurrentCulture);

    public string DataHoraTexto => _experimento is null ? "não executado" : _experimento.DataHora.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture);

    /// <summary>Tempo total de treinamento — "—" quando não medido, em vez de 0s (que seria lido como "instantâneo").</summary>
    public string TempoTexto => _experimento?.TempoSegundos is not { } segundos
        ? "—"
        : segundos < 60
            ? $"{segundos.ToString("0.0", CultureInfo.CurrentCulture)} s"
            : $"{TimeSpan.FromSeconds(segundos):mm\\:ss} (mm:ss)";

    public string FitnessPorSegundoTexto =>
        FitnessPorSegundo is { } valor ? valor.ToString("0.000", CultureInfo.CurrentCulture) : "—";

    /// <summary>Rótulo curto lido direto na tabela (requisito de identificar a melhor configuração sem precisar comparar números coluna a coluna).</summary>
    public string Destaque =>
        !Executado ? string.Empty
        : MelhorFitness && MelhorCustoBeneficio ? "★ melhor fitness e custo-benefício"
        : MelhorFitness ? "★ melhor fitness"
        : MelhorCustoBeneficio ? "⚡ melhor custo-benefício"
        : string.Empty;
}
