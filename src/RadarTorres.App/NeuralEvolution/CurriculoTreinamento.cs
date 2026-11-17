using System;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// Decide quando o curriculum de dificuldade progressiva avança de etapa (ver
/// <see cref="EtapaCurriculo"/>) — mecanismo independente do cálculo de fitness em si, só observa
/// o <c>FitnessMedio</c> já produzido por cada geração e diz "continua aqui" ou "sobe de nível".
/// </summary>
/// <remarks>
/// Avanço exige fitness médio acima do limiar por <see cref="_geracoesEstaveisNecessarias"/>
/// gerações CONSECUTIVAS — um pico isolado (sorte de seed fácil) não promove sozinho, porque a
/// contagem reinicia a cada geração abaixo do limiar. <see cref="_tetoGeracoesPorEtapa"/> é a
/// salvaguarda complementar: evita travar indefinidamente numa etapa se o limiar for alto demais
/// para a configuração em uso. Nunca retrocede — uma etapa mais difícil nunca volta a ser mais
/// fácil dentro da mesma execução.
/// </remarks>
public sealed class CurriculoTreinamento
{
    private readonly double _limiarFitness;
    private readonly int _geracoesEstaveisNecessarias;
    private readonly int _tetoGeracoesPorEtapa;

    private int _geracoesConsecutivasAcimaDoLimiar;
    private int _geracoesNaEtapaAtual;

    public EtapaCurriculo EtapaAtual { get; private set; } = EtapaCurriculo.Facil;

    /// <param name="limiarFitness">Fitness médio da geração a partir do qual conta como "etapa dominada".</param>
    /// <param name="geracoesEstaveisNecessarias">Quantas gerações CONSECUTIVAS acima do limiar são exigidas para avançar — protege contra promover por um pico isolado.</param>
    /// <param name="tetoGeracoesPorEtapa">Gerações máximas numa etapa antes de avançar de qualquer forma — evita travar o treino se o limiar nunca for atingido.</param>
    public CurriculoTreinamento(double limiarFitness, int geracoesEstaveisNecessarias, int tetoGeracoesPorEtapa)
    {
        if (geracoesEstaveisNecessarias < 1) throw new ArgumentOutOfRangeException(nameof(geracoesEstaveisNecessarias), "Precisa de ao menos 1 geração estável para avançar.");
        if (tetoGeracoesPorEtapa < geracoesEstaveisNecessarias) throw new ArgumentOutOfRangeException(nameof(tetoGeracoesPorEtapa), "O teto de segurança precisa ser maior ou igual às gerações estáveis exigidas.");

        _limiarFitness = limiarFitness;
        _geracoesEstaveisNecessarias = geracoesEstaveisNecessarias;
        _tetoGeracoesPorEtapa = tetoGeracoesPorEtapa;
    }

    /// <summary>
    /// Registra o fitness médio de mais uma geração concluída NA ETAPA ATUAL e devolve a etapa a
    /// usar na PRÓXIMA geração (pode ser a mesma). Chamar depois de cada geração, nunca durante.
    /// </summary>
    public EtapaCurriculo RegistrarGeracao(double fitnessMedioGeracao)
    {
        if (EtapaAtual == EtapaCurriculo.Dificil) return EtapaAtual;

        _geracoesNaEtapaAtual++;
        _geracoesConsecutivasAcimaDoLimiar = fitnessMedioGeracao >= _limiarFitness
            ? _geracoesConsecutivasAcimaDoLimiar + 1
            : 0;

        bool atingiuEstabilidade = _geracoesConsecutivasAcimaDoLimiar >= _geracoesEstaveisNecessarias;
        bool estourouTetoDeSeguranca = _geracoesNaEtapaAtual >= _tetoGeracoesPorEtapa;

        if (atingiuEstabilidade || estourouTetoDeSeguranca) Avancar();

        return EtapaAtual;
    }

    private void Avancar()
    {
        EtapaAtual = EtapaAtual switch
        {
            EtapaCurriculo.Facil => EtapaCurriculo.Medio,
            EtapaCurriculo.Medio => EtapaCurriculo.Dificil,
            _ => EtapaCurriculo.Dificil,
        };

        _geracoesConsecutivasAcimaDoLimiar = 0;
        _geracoesNaEtapaAtual = 0;
    }
}
