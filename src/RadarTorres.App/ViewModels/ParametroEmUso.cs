namespace RadarTorres.App.ViewModels;

/// <summary>
/// Uma linha do card "parâmetros em uso" da tela de treinamento.
/// </summary>
/// <remarks>
/// Existe para separar duas coisas que a tela confundia: os campos editáveis mostram o que vai
/// valer no PRÓXIMO "Iniciar"/"Resetar", enquanto esta lista mostra o que a execução em andamento
/// está realmente usando — incluindo o que não é editável ali (episódios por avaliação, elitismo,
/// paralelismo, função de ativação) e o que vem da tela de Configurações (alcance de mira,
/// certeza mínima de disparo).
/// </remarks>
/// <param name="Nome">Rótulo curto exibido à esquerda.</param>
/// <param name="Valor">Valor já formatado para leitura.</param>
/// <param name="Explicacao">Texto do tooltip — por que este parâmetro importa.</param>
public sealed record ParametroEmUso(string Nome, string Valor, string Explicacao);
