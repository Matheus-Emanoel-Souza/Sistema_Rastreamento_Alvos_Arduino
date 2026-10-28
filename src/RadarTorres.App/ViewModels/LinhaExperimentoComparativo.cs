using System;
using System.Globalization;
using System.Linq;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

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
        ConfiguracaoExperimento configuracao,
        ExperimentoHiperparametros? experimento,
        bool ehBaseline)
    {
        TamanhoPopulacao = configuracao.TamanhoPopulacao;
        NeuroniosOcultos = configuracao.NeuroniosOcultos;
        TaxaMutacao = configuracao.TaxaMutacao;
        _experimento = experimento;
        EhBaseline = ehBaseline;
    }

    /// <summary>Linha de uma configuração efetivamente treinada.</summary>
    public static LinhaExperimentoComparativo Executada(
        ConfiguracaoExperimento configuracao, ExperimentoHiperparametros experimento, bool ehBaseline) =>
        new(configuracao, experimento, ehBaseline);

    /// <summary>Linha de uma configuração prevista no plano mas ainda sem execução registrada.</summary>
    public static LinhaExperimentoComparativo Pendente(ConfiguracaoExperimento configuracao, bool ehBaseline) =>
        new(configuracao, experimento: null, ehBaseline);

    public int TamanhoPopulacao { get; }
    public int NeuroniosOcultos { get; }
    public double TaxaMutacao { get; }

    /// <summary>A configuração de referência, compartilhada pelas três séries (ver <see cref="ConfiguracaoExperimento"/>).</summary>
    public bool EhBaseline { get; }

    public bool Executado => _experimento is not null;

    public double FitnessFinal => _experimento?.FitnessFinal ?? 0;

    /// <summary>Primeira geração em que o fitness final foi atingido — quanto menor, mais rápido evoluiu.</summary>
    public int? GeracaoDoPico => _experimento?.GeracaoDoPico;

    /// <summary>Marcado pela série na linha de maior <see cref="FitnessFinal"/>.</summary>
    public bool MelhorFitness { get; set; }

    /// <summary>Marcado pela série na linha de maior <see cref="FitnessPorSegundo"/> (só entre as que têm tempo medido).</summary>
    public bool MelhorCustoBeneficio { get; set; }

    /// <summary>Marcado pela série na linha de menor <see cref="GeracaoDoPico"/> — a que convergiu mais rápido.</summary>
    public bool MaisRapido { get; set; }

    /// <summary>
    /// Fitness final por segundo de treinamento — a métrica de custo-benefício: uma configuração
    /// que chega quase ao mesmo fitness em metade do tempo é preferível. <c>null</c> quando o
    /// registro é anterior à medição de tempo (ou a configuração não foi executada).
    /// </summary>
    public double? FitnessPorSegundo =>
        _experimento?.TempoSegundos is > 0 ? _experimento.FitnessFinal / _experimento.TempoSegundos.Value : null;

    public string GeracoesTexto => Texto(_experimento?.Geracoes);

    public string FitnessFinalTexto => _experimento is null ? Ausente : _experimento.FitnessFinal.ToString("0.00", Cultura);

    /// <summary>
    /// Taxa de mutação da execução. Registros anteriores a este campo não a gravaram; como
    /// rodaram com o padrão do sistema, são exibidos com o valor presumido e marcados com "*"
    /// para a tabela não afirmar como medido algo que foi deduzido.
    /// </summary>
    public string TaxaMutacaoTexto =>
        _experimento is null ? TaxaMutacao.ToString("0.###", Cultura)
        : _experimento.TaxaMutacao is { } taxa ? taxa.ToString("0.###", Cultura)
        : $"{ConfiguracaoExperimento.TaxaMutacaoBaseline.ToString("0.###", Cultura)} *";

    /// <summary>True quando a taxa exibida foi presumida, não lida do registro — ver <see cref="TaxaMutacaoTexto"/>.</summary>
    public bool TaxaMutacaoPresumida => _experimento is not null && _experimento.TaxaMutacao is null;

    public string GeracaoDoPicoTexto => Texto(_experimento?.GeracaoDoPico);

    /// <summary>Curva de convergência em uma célula: melhor fitness nas gerações 25 / 50 / 100 / 200.</summary>
    public string CurvaTexto
    {
        get
        {
            if (_experimento is null) return Ausente;

            double?[] pontos =
            [
                _experimento.FitnessGeracao25,
                _experimento.FitnessGeracao50,
                _experimento.FitnessGeracao100,
                _experimento.FitnessGeracao200
            ];

            if (pontos.All(p => p is null)) return Ausente;
            return string.Join(" › ", pontos.Select(p => p is { } v ? v.ToString("0.0", Cultura) : "—"));
        }
    }

    public string DataHoraTexto => _experimento is null ? "não executado" : _experimento.DataHora.ToString("dd/MM/yyyy HH:mm", Cultura);

    /// <summary>Tempo total de treinamento — "—" quando não medido, em vez de 0s (que seria lido como "instantâneo").</summary>
    public string TempoTexto => _experimento?.TempoSegundos is not { } segundos
        ? Ausente
        : segundos < 60
            ? $"{segundos.ToString("0.0", Cultura)} s"
            : $"{TimeSpan.FromSeconds(segundos):mm\\:ss}";

    public string FitnessPorSegundoTexto =>
        FitnessPorSegundo is { } valor ? valor.ToString("0.000", Cultura) : Ausente;

    /// <summary>Rótulo curto lido direto na tabela (requisito de identificar a melhor configuração sem precisar comparar números coluna a coluna).</summary>
    public string Destaque
    {
        get
        {
            if (!Executado) return string.Empty;

            string[] marcas =
            [
                MelhorFitness ? "★ melhor fitness" : string.Empty,
                MaisRapido ? "⏱ convergiu antes" : string.Empty,
                MelhorCustoBeneficio ? "⚡ melhor custo-benefício" : string.Empty
            ];

            return string.Join(" · ", marcas.Where(m => m.Length > 0));
        }
    }

    private const string Ausente = "—";

    private static CultureInfo Cultura => CultureInfo.CurrentCulture;

    private string Texto(int? valor) => valor is { } v ? v.ToString(Cultura) : Ausente;
}
