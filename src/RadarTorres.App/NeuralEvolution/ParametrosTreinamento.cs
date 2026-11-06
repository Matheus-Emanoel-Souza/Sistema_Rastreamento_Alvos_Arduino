using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>Parâmetros ajustáveis de um episódio de treinamento — expostos na tela para o usuário experimentar.</summary>
public sealed record ParametrosTreinamento
{
    public double RaioMundoMetros { get; init; } = 10.0;
    public double DistanciaEngajamentoMetros { get; init; } = 3.0;
    public double VelocidadeMinimaAlvoMetrosPorSegundo { get; init; } = 0.5;
    public double VelocidadeMaximaAlvoMetrosPorSegundo { get; init; } = 2.5;
    public double DuracaoEpisodioSegundos { get; init; } = 45.0;
    public double IntervaloSpawnMinSegundos { get; init; } = 1.5;
    public double IntervaloSpawnMaxSegundos { get; init; } = 4.0;

    /// <summary>Intervalo mínimo de nascimento no FIM do episódio — <see cref="IntervaloSpawnMinSegundos"/> é o do início; a interpolação linear entre os dois ao longo de <see cref="DuracaoEpisodioSegundos"/> é a escalada de dificuldade (mais alvos quanto mais o episódio avança), ver <see cref="AmbienteTreinamento"/>.</summary>
    public double IntervaloSpawnMinFinalSegundos { get; init; } = 0.6;

    /// <summary>Intervalo máximo de nascimento no FIM do episódio — ver <see cref="IntervaloSpawnMinFinalSegundos"/>.</summary>
    public double IntervaloSpawnMaxFinalSegundos { get; init; } = 1.8;

    /// <summary>Teto de alvos ATIVOS simultaneamente no MESMO quadrante — não é mais um teto global: quadrantes diferentes podem estar cada um no próprio limite ao mesmo tempo, forçando o controlador a lidar com concentração local em vez de só a contagem total.</summary>
    public int MaxAlvosPorQuadrante { get; init; } = 5;

    /// <summary>
    /// Liga a terceira dimensão: com <c>true</c>, cada alvo nasce com uma altura entre 0 e
    /// <see cref="AlturaMaximaAlvoMetros"/> e essa altura passa a influenciar quais torres
    /// conseguem enxergá-lo (ver <see cref="AlturaVisivelPorTodasAsTorresMetros"/>). Com
    /// <c>false</c> — o padrão — todo alvo fica em <c>Z = 0</c> e a simulação se comporta
    /// exatamente como antes, em duas dimensões.
    /// </summary>
    /// <remarks>
    /// O padrão é desligado de propósito: é o modo bidimensional que já foi usado em todos os
    /// treinamentos e experimentos registrados até aqui, e trocar o padrão mudaria em silêncio o
    /// significado de qualquer comparação com os resultados anteriores. Quem quiser a terceira
    /// dimensão liga explicitamente — na tela de treinamento há um controle para isso.
    /// </remarks>
    public bool EixoZHabilitado { get; init; } = false;

    /// <summary>Altura máxima (m) que um alvo pode ter quando <see cref="EixoZHabilitado"/> está ligado; o sorteio é uniforme entre 0 e este valor.</summary>
    public double AlturaMaximaAlvoMetros { get; init; } = 2.0;

    /// <summary>
    /// Altura (m) a partir da qual o alvo deixa de ser coberto apenas pelas torres do próprio
    /// quadrante (e das adjacentes) e passa a ser visível para TODAS as torres: acima desta altura
    /// ele sobe o suficiente para aparecer no campo de visão de todas elas.
    /// </summary>
    /// <remarks>
    /// Não é um atalho que ignora as demais condições — ser visível apenas amplia o conjunto de
    /// torres CANDIDATAS. Zona morta, certeza mínima, tempo mínimo de mira e disponibilidade da
    /// torre continuam valendo exatamente como antes (ver <see cref="ControladorHibrido"/> e
    /// <see cref="AmbienteTreinamento.DecidirDisparos"/>).
    /// </remarks>
    public double AlturaVisivelPorTodasAsTorresMetros { get; init; } = 0.5;

    public double RecompensaAbate { get; init; } = 10.0;
    public double PenalidadeEscape { get; init; } = 8.0;

    /// <summary>Normaliza a entrada "tempo detectado sem abate" da rede (<see cref="ControladorHibrido.ConstruirEntradas(double,double,bool,bool,double,double,double,double,double,double)"/>) — satura em 1,0 a partir deste tempo de observação contínua.</summary>
    public double TempoDeteccaoRelevanteSegundos { get; init; } = 3.0;

    /// <summary>Distância (m) de uma torre abaixo da qual um alvo ainda ativo é considerado ter rompido a defesa — distinto de <see cref="DistanciaPerdaPontoMetros"/> (que é em torno da base, só penaliza) ou de <see cref="PenalidadeEscape"/> (fuga pela base): romper uma torre encerra o episódio imediatamente.</summary>
    public double DistanciaInvasaoTorreMetros { get; init; } = 1.0;

    /// <summary>Penalidade de fitness aplicada quando um alvo rompe uma torre (ver <see cref="DistanciaInvasaoTorreMetros"/>) — maior que <see cref="PenalidadeEscape"/> porque, além da penalidade, o episódio é interrompido e perde toda pontuação futura possível.</summary>
    public double PenalidadeInvasaoTorre { get; init; } = 16.0;

    /// <summary>Fator que converte o desvio padrão (segundos) do tempo comprometido entre as torres, ao fim do episódio, em penalidade de fitness — incentiva distribuir alvos entre torres em vez de sobrecarregar sempre a mesma (gestão de recursos), ver <see cref="TorreTreinamento.TempoComprometidoSegundos"/>.</summary>
    public double PenalidadeDesequilibrioTorresPorSegundo { get; init; } = 0.5;

    /// <summary>
    /// Penalidade de fitness aplicada toda vez que a rede RECUSA uma oportunidade de disparo
    /// legítima — os vetos inegociáveis (zona morta, certeza mínima, tempo mínimo de mira) já
    /// liberaram, e ainda assim <see cref="ControladorHibrido.DecidirDisparo"/> devolve falso (ver
    /// <see cref="EstadoEpisodio.DisparosRecusadosPelaRede"/>). Adicionada depois de medir que as
    /// redes treinadas sem este termo recusavam entre 10% e 56% dessas oportunidades e, por isso,
    /// perdiam em fitness médio para a heurística pura (que nunca recusa) — o fitness antes só
    /// recompensava abate e penalizava fuga/invasão, sem custo explícito para a HESITAÇÃO em si.
    /// </summary>
    public double PenalidadeRecusaDisparo { get; init; } = 3.0;

    /// <summary>
    /// Quando <c>true</c>, o episódio IGNORA <see cref="DuracaoEpisodioSegundos"/> como fim normal
    /// — só termina por invasão de torre (<see cref="AmbienteTreinamento.TratarInvasaoDeTorre"/>)
    /// ou ao atingir <see cref="DuracaoMaximaSegurancaSegundos"/> (salvaguarda contra um genoma tão
    /// bom que nunca seria invadido). <see cref="DuracaoEpisodioSegundos"/> continua sendo usado
    /// como referência da escalada de dificuldade (ver <see cref="AmbienteTreinamento.ProximoIntervaloSpawn"/>) —
    /// só o corte por tempo decorrido é desligado. Existe para testar a hipótese de que o teto de
    /// fitness observado vem do episódio ser curto demais (poucos alvos nascem), não da qualidade
    /// do controlador.
    /// </summary>
    public bool SemLimiteDeDuracao { get; init; } = false;

    /// <summary>Salvaguarda contra loop efetivamente infinito quando <see cref="SemLimiteDeDuracao"/> está ativo — nenhum episódio passa disto, mesmo que nenhum alvo jamais invada uma torre.</summary>
    public double DuracaoMaximaSegurancaSegundos { get; init; } = 1800.0;

    /// <summary>Piso absoluto do limiar de certeza — mesma regra do sistema real (ver <see cref="Configuration.ConfiguracoesAplicativo.ConfiancaMinimaDisparoPercentual"/>).</summary>
    public const double LimiarConfiancaMinimaFracao = 0.40;

    /// <summary>Certeza mínima (0 a 1) para autorizar disparo — equivalente a <see cref="Configuration.ConfiguracoesAplicativo.LimiarConfiancaDisparoPercentual"/>, mas em fração.</summary>
    public double LimiarConfiancaDisparoFracao { get; init; } = 0.70;

    /// <summary>Distância (m) da torre abaixo da qual a câmera não consegue mais confirmar o alvo com boa certeza (alvo perto demais da lente) — ver <see cref="AlvoTreinamento.CertezaEfetiva"/>.</summary>
    public double DistanciaMinimaBoaCertezaMetros { get; init; } = 2.0;

    /// <summary>Quadrantes tratados como zona morta neste episódio — vazio por padrão (nenhuma restrição). Espelha <see cref="Services.IZonaMortaService"/> do sistema real, só que fixo por episódio em vez de configurável em tela.</summary>
    public IReadOnlySet<Quadrant> ZonasMortasQuadrantes { get; init; } = new HashSet<Quadrant>();

    /// <summary>Distância (m) da base abaixo da qual o alvo, mesmo sem escapar de fato, já custa pontos à geração — aviso de proximidade perigosa.</summary>
    public double DistanciaPerdaPontoMetros { get; init; } = 0.5;

    /// <summary>Penalidade de fitness aplicada uma única vez por alvo ao cruzar <see cref="DistanciaPerdaPontoMetros"/> — menor que <see cref="PenalidadeEscape"/> porque o alvo pode ainda vir a ser abatido depois.</summary>
    public double PenalidadePerdaPonto { get; init; } = 3.0;

    /// <summary>Velocidade (m/s) a partir da qual um alvo aciona ajuda de torre adjacente — mesma regra e mesmo valor de fábrica de <see cref="Configuration.ConfiguracoesAplicativo.VelocidadeRapidaMetrosPorSegundo"/> (ver <see cref="AmbienteTreinamento.DecidirDisparos"/>).</summary>
    public double VelocidadeRapidaMetrosPorSegundo { get; init; } = 1.2;

    public int TamanhoPopulacao { get; init; } = 60;

    /// <summary>
    /// Probabilidade de um descendente ganhar um neurônio novo (mutação estrutural do NEAT).
    /// Substitui a antiga <c>TaxaMutacao</c> (probabilidade por gene), que não tem equivalente
    /// aqui: o SharpNEAT trata mutação de PESO e mutação de ESTRUTURA como eventos distintos, e
    /// é a segunda que define o quanto a rede pode mudar de forma ao longo da evolução.
    /// </summary>
    public double ProbabilidadeAdicionarNo { get; init; } = 0.01;

    /// <summary>
    /// Número de conexões a partir do qual a evolução passa a SIMPLIFICAR em vez de crescer
    /// (ver <see cref="TreinadorNeat"/>). Sem teto, a topologia cresce indefinidamente e cada
    /// avaliação — já são <see cref="EpisodiosPorAvaliacao"/> episódios por indivíduo — fica
    /// progressivamente mais cara sem ganho de fitness correspondente.
    /// </summary>
    public int TetoComplexidade { get; init; } = 60;

    /// <summary>Em quantas espécies a população é dividida. Mecanismo central do NEAT: protege uma topologia recém-criada de competir de igual para igual com redes já otimizadas antes de ter tempo de ser ajustada.</summary>
    public int QuantidadeEspecies { get; init; } = 6;

    /// <summary>
    /// Quantos episódios independentes (seeds diferentes) são rodados e tirada a MÉDIA para
    /// chegar ao fitness de um genoma — ver <see cref="AmbienteTreinamento.AvaliarFitness"/>.
    /// Maior que 1 é necessário porque o ambiente é todo sorteado (nascimento, velocidade,
    /// certeza): com um único episódio, o mesmo genoma pode tirar um cenário fácil ou difícil só
    /// por sorte, e esse ruído de ambiente acaba maior que a diferença real de qualidade entre
    /// genomas — a seleção por torneio e até o elitismo passam a escolher baseado em sorte, não
    /// em mérito, o que parece (e na prática é) estagnação da evolução.
    /// </summary>
    /// <remarks>
    /// Elevado de 3 para 12 depois de medir, com <c>sobrevivencia --semlimite=nao</c>, que o
    /// desvio padrão de fitness de um único genoma (treinado ou heurística pura, tanto faz) é da
    /// mesma ordem de grandeza da própria média (~65 de desvio numa média ~70) sob o ambiente
    /// endurecido (invasão de torre acaba o episódio de uma vez) — com 3 episódios por avaliação
    /// essa amostra é ruído demais para a seleção distinguir genoma melhor de genoma sortudo.
    /// </remarks>
    public int EpisodiosPorAvaliacao { get; init; } = 12;

    // ---------------------------------------------------------------------------------------
    // Controlador V2 (ver ControladorHibridoV2) e fitness V2 — tudo nesta seção é OPT-IN e com
    // padrão que reproduz exatamente o comportamento V1/original. Nenhum parâmetro acima desta
    // linha foi alterado: um treinamento com ParametrosTreinamento padrão continua bit-a-bit
    // igual ao que era antes desta mudança — é o que garante o controlador V1 como referência
    // válida para comparação (ver AmbienteTreinamento.ExecutarEpisodioRapidoV2).
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Liga os termos de fitness NOVOS (tempo de resposta, cooperação, trocas excessivas de
    /// alvo) — ver <see cref="AmbienteTreinamento.DecidirDisparosV2"/>. Desligado por padrão para
    /// não mudar em silêncio o significado de uma comparação com resultados V1 já registrados;
    /// a bateria comparativa liga explicitamente só para o lado V2.
    /// </summary>
    public bool UsarFitnessV2 { get; init; } = false;

    /// <summary>Tempo de resposta (seleção → primeiro disparo) considerado "ótimo" — abaixo disto, o bônus de tempo de resposta satura no máximo (ver <see cref="RecompensaTempoRespostaMaximo"/>).</summary>
    public double TempoRespostaReferenciaSegundos { get; init; } = 1.0;

    /// <summary>Bônus de fitness por disparar no tempo de referência ou antes — cai linearmente a 0 em 2× <see cref="TempoRespostaReferenciaSegundos"/>.</summary>
    public double RecompensaTempoRespostaMaximo { get; init; } = 2.0;

    /// <summary>Bônus por uma cooperação concedida (torre de ajuda chamada pela saída "prioridade de cooperação" ou por velocidade alta) que efetivamente ajudou a abater o alvo.</summary>
    public double RecompensaCooperacaoBemSucedida { get; init; } = 4.0;

    /// <summary>Penalidade quando a rede pede cooperação (ou a regra de velocidade aciona ajuda) enquanto existe outro alvo ativo sem NENHUMA torre comprometida — concentrar torres num alvo só enquanto outro fica descoberto é o cenário que o enunciado pede para não incentivar.</summary>
    public double PenalidadeCooperacaoConcentrada { get; init; } = 2.0;

    /// <summary>Janela (s) usada para contar "trocas recentes de alvo" de uma torre — ver <see cref="TorreTreinamento.ContarTrocasRecentes"/>.</summary>
    public double JanelaTrocaDeAlvoSegundos { get; init; } = 6.0;

    /// <summary>A partir de quantas trocas DENTRO da janela a troca seguinte passa a ser penalizada (a primeira troca de um alvo livre nunca é "excessiva" por si só).</summary>
    public int LimiteTrocasAntesDePenalizar { get; init; } = 2;

    /// <summary>Penalidade de fitness por troca de alvo além do limite, dentro da janela — desencoraja uma torre pingando entre vários alvos em sequência rápida em vez de resolver um por vez.</summary>
    public double PenalidadeTrocaDeAlvoExcessiva { get; init; } = 1.5;

    /// <summary>
    /// Liga a rede RECORRENTE (NEAT cíclico — ver <see cref="RedeNeat"/> e <see cref="TreinadorNeat"/>):
    /// a mesma caixa-preta não é reiniciada entre ativações dentro do MESMO episódio, só ao
    /// início de cada episódio novo (ver <see cref="AmbienteTreinamento.ExecutarEpisodioRapidoV2"/>).
    /// Desligado por padrão — o padrão continua sendo a rede acíclica sem estado, mais rápida de
    /// avaliar e mais fácil de auditar (<see cref="AtivacoesRedeNeat"/> só sabe colorir o diagrama
    /// de redes acíclicas).
    /// </summary>
    public bool RedeRecorrente { get; init; } = false;
}
