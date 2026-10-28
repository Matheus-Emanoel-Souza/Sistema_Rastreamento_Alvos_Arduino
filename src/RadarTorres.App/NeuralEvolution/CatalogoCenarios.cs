using System;
using System.Collections.Generic;
using System.Linq;
using RadarTorres.App.Models;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// Casos táticos que o sistema físico vai enfrentar, mapeados para cenários determinísticos de
/// treinamento — cada um isola UMA dificuldade específica (em vez do sorteio uniforme de
/// <see cref="AmbienteTreinamento.TalvezNascerAlvo"/>, que mistura tudo e raramente insiste no
/// mesmo caso difícil duas vezes). Usado em dois lugares:
/// <list type="bullet">
/// <item>avaliação do algoritmo genético (ver <see cref="AmbienteTreinamento.AvaliarFitness"/>) — a IA é forçada a enfrentar todos os casos a cada geração, não só cenários aleatórios;</item>
/// <item>tela de treinamento (ver <see cref="ViewModels.TreinamentoControladorViewModel.SimularCenarioCommand"/>) — o usuário escolhe um caso e observa isoladamente como o melhor genoma atual se comporta nele.</item>
/// </list>
/// </summary>
public enum TipoCenarioTreinamento
{
    /// <summary>Sorteio padrão (ângulo/velocidade/certeza aleatórios) — comportamento histórico, sem script.</summary>
    Aleatorio,

    /// <summary>Carga/saturação: 6 alvos nascem quase juntos, testando o limite de <see cref="ParametrosTreinamento.MaxAlvosPorQuadrante"/>.</summary>
    EnxameDeSaturacao,

    /// <summary>Disputa de torre: dois alvos nascem ao mesmo tempo, próximos um do outro, forçando a escolha de qual uma única torre deve comprometer primeiro.</summary>
    DisputaPorMesmaTorre,

    /// <summary>Alvo rápido (acima de <see cref="ParametrosTreinamento.VelocidadeRapidaMetrosPorSegundo"/>) — tende a precisar da ajuda de torre adjacente (regra 6) para não escapar de um disparo único.</summary>
    AlvoRapidoDeAltaVelocidade,

    /// <summary>Detecção duvidosa: certeza nasce baixa e sobe lentamente — pode nunca cruzar o limiar de disparo dentro da duração do episódio.</summary>
    CertezaBaixaPersistente,

    /// <summary>Alvo nasce muito perto da base (não da borda do mundo) — pouquíssimo tempo de reação antes de poder escapar.</summary>
    AlvoNasceProximoDaBase,

    /// <summary>Alvo nasce dentro de um quadrante tratado como zona morta neste episódio — nenhuma torre pode autorizar disparo nele, mesmo com certeza alta e dentro do alcance.</summary>
    AlvoEmZonaMorta,
}

public static class CatalogoCenarios
{
    /// <summary>Todos os tipos de cenário, na ordem em que devem aparecer no seletor da tela.</summary>
    public static readonly IReadOnlyList<TipoCenarioTreinamento> Todos = Enum.GetValues<TipoCenarioTreinamento>();

    /// <summary>Todos os cenários com script (exclui <see cref="TipoCenarioTreinamento.Aleatorio"/>) — usados para forçar a avaliação do algoritmo genético contra todos os casos mapeados em toda geração.</summary>
    public static readonly IReadOnlyList<TipoCenarioTreinamento> TodosComScript =
        Todos.Where(t => t != TipoCenarioTreinamento.Aleatorio).ToList();

    public static string NomeExibicao(TipoCenarioTreinamento tipo) => tipo switch
    {
        TipoCenarioTreinamento.Aleatorio => "Aleatório (padrão)",
        TipoCenarioTreinamento.EnxameDeSaturacao => "Enxame / saturação",
        TipoCenarioTreinamento.DisputaPorMesmaTorre => "Disputa pela mesma torre",
        TipoCenarioTreinamento.AlvoRapidoDeAltaVelocidade => "Alvo rápido (ajuda adjacente)",
        TipoCenarioTreinamento.CertezaBaixaPersistente => "Certeza baixa persistente",
        TipoCenarioTreinamento.AlvoNasceProximoDaBase => "Alvo nasce perto da base",
        TipoCenarioTreinamento.AlvoEmZonaMorta => "Alvo em zona morta",
        _ => tipo.ToString()
    };

    public static string Descricao(TipoCenarioTreinamento tipo) => tipo switch
    {
        TipoCenarioTreinamento.Aleatorio =>
            "Sorteio padrão de nascimento — ângulo, velocidade e certeza aleatórios, sem script.",
        TipoCenarioTreinamento.EnxameDeSaturacao =>
            "6 alvos nascem quase juntos — testa o limite de alvos simultâneos e se as torres se distribuem bem entre eles.",
        TipoCenarioTreinamento.DisputaPorMesmaTorre =>
            "2 alvos nascem ao mesmo tempo, próximos um do outro — testa qual a rede escolhe comprometer primeiro.",
        TipoCenarioTreinamento.AlvoRapidoDeAltaVelocidade =>
            "Alvo acima da velocidade 'rápida' configurada — sozinho tem mais chance de escapar de um único disparo; tende a precisar de ajuda de torre adjacente.",
        TipoCenarioTreinamento.CertezaBaixaPersistente =>
            "Certeza nasce baixa e sobe muito lentamente — pode nunca cruzar o limiar mínimo de disparo antes do alvo chegar à base.",
        TipoCenarioTreinamento.AlvoNasceProximoDaBase =>
            "Alvo nasce bem mais perto da base que o normal — pouco tempo de reação antes de contar como escape.",
        TipoCenarioTreinamento.AlvoEmZonaMorta =>
            "Alvo nasce num quadrante marcado como zona morta neste episódio — disparo nunca deveria ser autorizado, mesmo com certeza alta.",
        _ => string.Empty
    };

    /// <summary>
    /// Script de nascimentos do cenário, ou <c>null</c> para <see cref="TipoCenarioTreinamento.Aleatorio"/>
    /// (sinaliza a <see cref="AmbienteTreinamento"/> para manter o sorteio padrão).
    /// </summary>
    public static IReadOnlyList<SpawnScriptado>? ObterSpawns(TipoCenarioTreinamento tipo) => tipo switch
    {
        TipoCenarioTreinamento.Aleatorio => null,

        TipoCenarioTreinamento.EnxameDeSaturacao => Enumerable.Range(0, 6)
            .Select(i => new SpawnScriptado(
                InstanteSegundos: i * 0.4,
                AnguloNascimentoGraus: i * 60.0,
                VelocidadeMetrosPorSegundo: 1.0,
                CertezaBase: 0.5,
                TaxaCrescimentoCertezaPorSegundo: 0.15))
            .ToList(),

        TipoCenarioTreinamento.DisputaPorMesmaTorre =>
        [
            new SpawnScriptado(InstanteSegundos: 0, AnguloNascimentoGraus: 40, VelocidadeMetrosPorSegundo: 1.0, CertezaBase: 0.5, TaxaCrescimentoCertezaPorSegundo: 0.15),
            new SpawnScriptado(InstanteSegundos: 0.2, AnguloNascimentoGraus: 50, VelocidadeMetrosPorSegundo: 1.0, CertezaBase: 0.5, TaxaCrescimentoCertezaPorSegundo: 0.15),
        ],

        TipoCenarioTreinamento.AlvoRapidoDeAltaVelocidade =>
        [
            new SpawnScriptado(InstanteSegundos: 0, AnguloNascimentoGraus: 45, VelocidadeMetrosPorSegundo: 2.3, CertezaBase: 0.6, TaxaCrescimentoCertezaPorSegundo: 0.2),
        ],

        TipoCenarioTreinamento.CertezaBaixaPersistente =>
        [
            // Taxa de crescimento quase nula: com CertezaBase 0.15 e 0.02/s, só cruza o limiar
            // de 0.40 depois de ~12,5s de observação — muito perto do alvo já ter chegado à base.
            new SpawnScriptado(InstanteSegundos: 0, AnguloNascimentoGraus: 200, VelocidadeMetrosPorSegundo: 1.0, CertezaBase: 0.15, TaxaCrescimentoCertezaPorSegundo: 0.02),
        ],

        TipoCenarioTreinamento.AlvoNasceProximoDaBase =>
        [
            new SpawnScriptado(InstanteSegundos: 0, AnguloNascimentoGraus: 300, VelocidadeMetrosPorSegundo: 1.0, CertezaBase: 0.6, TaxaCrescimentoCertezaPorSegundo: 0.2, FracaoRaioNascimento: 0.15),
        ],

        TipoCenarioTreinamento.AlvoEmZonaMorta =>
        [
            // Ângulo 45° cai sempre em Q1 (x>=0, y>=0) — mesmo quadrante forçado como zona morta
            // em QuadranteZonaMortaParaForcar, para o episódio inteiro testar a regra isolada.
            new SpawnScriptado(InstanteSegundos: 0, AnguloNascimentoGraus: 45, VelocidadeMetrosPorSegundo: 1.0, CertezaBase: 0.6, TaxaCrescimentoCertezaPorSegundo: 0.2),
        ],

        _ => null
    };

    /// <summary>Quadrante a forçar como zona morta só durante este episódio, ou <c>null</c> se o cenário não precisa de nenhum.</summary>
    public static Quadrant? QuadranteZonaMortaParaForcar(TipoCenarioTreinamento tipo) =>
        tipo == TipoCenarioTreinamento.AlvoEmZonaMorta ? Quadrant.Q1 : null;
}
