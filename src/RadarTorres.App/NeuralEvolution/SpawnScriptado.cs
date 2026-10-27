namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// Nascimento de alvo determinístico (sem sorteio) usado pelos cenários nomeados em
/// <see cref="CatalogoCenarios"/> — ao contrário do nascimento aleatório de
/// <see cref="AmbienteTreinamento.TalvezNascerAlvo"/>, cada campo aqui é fixo, para o mesmo
/// cenário produzir sempre a mesma situação tática (mesmo instante, ângulo, velocidade e certeza).
/// </summary>
/// <param name="InstanteSegundos">Momento (s, desde o início do episódio) em que o alvo nasce.</param>
/// <param name="AnguloNascimentoGraus">Ângulo (graus, mesma convenção de <see cref="System.Math.Cos"/>/<see cref="System.Math.Sin"/>) do ponto de nascimento em torno da base — o alvo se move em linha reta na direção oposta.</param>
/// <param name="VelocidadeMetrosPorSegundo">Velocidade constante do alvo.</param>
/// <param name="CertezaBase">Certeza (0 a 1) no instante do nascimento — ver <see cref="AlvoTreinamento.CertezaBase"/>.</param>
/// <param name="TaxaCrescimentoCertezaPorSegundo">Taxa de crescimento da certeza com o tempo — ver <see cref="AlvoTreinamento.TaxaCrescimentoCertezaPorSegundo"/>.</param>
/// <param name="FracaoRaioNascimento">Fração (0 a 1) do raio do mundo onde o alvo nasce — 1.0 é a borda (padrão do sorteio); menor aproxima o nascimento da base.</param>
public sealed record SpawnScriptado(
    double InstanteSegundos,
    double AnguloNascimentoGraus,
    double VelocidadeMetrosPorSegundo,
    double CertezaBase,
    double TaxaCrescimentoCertezaPorSegundo,
    double FracaoRaioNascimento = 1.0);
