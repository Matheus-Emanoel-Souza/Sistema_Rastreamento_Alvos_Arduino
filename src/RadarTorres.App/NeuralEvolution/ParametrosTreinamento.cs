using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>Parâmetros ajustáveis de um episódio de treinamento — expostos na tela para o usuário experimentar.</summary>
public sealed record ParametrosTreinamento
{
    public double RaioMundoMetros { get; init; } = 10.0;
    public double DistanciaEngajamentoMetros { get; init; } = 3.0;
    public double VelocidadeMinimaAlvoMetrosPorSegundo { get; init; } = 0.5;

    /// <summary>
    /// Velocidade MÁXIMA (m/s) que um alvo pode ter — derivada da velocidade angular da torre
    /// (<see cref="VelocidadeAngularTorreGrausPorSegundo"/>), não escolhida à parte: um alvo mais
    /// rápido que isto pode, em tese, exigir giro mais rápido do que o motor de passo consegue
    /// entregar (ver a dedução completa em <see cref="VelocidadeMaximaAlvoDerivadaMetrosPorSegundo"/>).
    /// Mantido como propriedade própria (em vez de só usar a derivada direto) porque é um parâmetro
    /// de MUNDO (afeta o sorteio de nascimento) e precisa continuar ajustável/registrável como os
    /// demais — o valor de fábrica é a derivada calculada com os parâmetros de fábrica.
    /// </summary>
    public double VelocidadeMaximaAlvoMetrosPorSegundo { get; init; } = VelocidadeMaximaAlvoDerivadaMetrosPorSegundo;

    public double DuracaoEpisodioSegundos { get; init; } = 45.0;
    public double IntervaloSpawnMinSegundos { get; init; } = 1.5;
    public double IntervaloSpawnMaxSegundos { get; init; } = 4.0;

    /// <summary>
    /// Variação máxima de direção (°/s) aplicada à trajetória de um alvo nascido pelo sorteio
    /// padrão (não pelos cenários scriptados — ver <see cref="AlvoTreinamento.AnguloMovimentoRadianos"/>)
    /// — a cada passo a direção do movimento é perturbada por um valor sorteado dentro de
    /// ±<see cref="VariacaoMaximaDirecaoGrausPorSegundo"/>·dt, um passeio aleatório (random walk)
    /// contínuo em vez do desvio fixo sorteado só uma vez no nascimento (comportamento anterior).
    /// Reproduzível: usa o <see cref="EstadoEpisodio.Rng"/> do episódio, igual a todo o resto do
    /// sorteio — a MESMA seed sempre produz a MESMA sequência de curvas.
    /// </summary>
    public double VariacaoMaximaDirecaoGrausPorSegundo { get; init; } = 15.0;

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

    // ---------------------------------------------------------------------------------------
    // Velocidade angular da torre (motor de passo) — base física de VelocidadeMaximaAlvoMetrosPorSegundo
    // acima e de ToleranciaMiraGraus abaixo. Premissas documentadas aqui porque são elas que
    // justificam os números, não os números em si.
    //
    // HARDWARE: cada torre de decisão tem DOIS motores de passo 28BYJ-48 (um por eixo — azimute e
    // elevação), cada um com seu driver ULN2003 (ver Projeto_Fisico/Eletronica/Diagrama_Eletrico.md).
    // Datasheet do 28BYJ-48: engrenagem de redução ~1:64, 2048 passos por volta do eixo de saída
    // em modo meio-passo (mesma convenção já usada pelo motor de varredura central deste projeto —
    // ver Arduino/RadarTorres_Firmware/RadarTorres_Firmware.ino, SWEEP_STEPS_POR_VOLTA).
    //
    // VELOCIDADE ESCOLHIDA: 15 RPM — o EXTREMO INFERIOR da faixa 15-60 RPM, de propósito. O
    // 28BYJ-48 com driver ULN2003 entrega torque suficiente para girar com carga (o canhão/câmera
    // da torre) de forma confiável só na faixa baixa dessa janela; velocidades mais altas (até 60
    // RPM) só são alcançáveis sem carga ou com menos torque de retenção — sem ensaio de torque real
    // da torre montada, 15 RPM é a escolha defensável (não otimista) e documentada aqui como
    // premissa explícita, não medição.
    // ---------------------------------------------------------------------------------------

    /// <summary>Velocidade de operação assumida para os dois motores de passo 28BYJ-48 da torre — ver a nota acima. Dentro da faixa 15-60 RPM informada; escolhido o piso (mais conservador/confiável sob carga).</summary>
    public const double VelocidadeAngularTorreRpm = 15.0;

    /// <summary>Mesma velocidade acima, convertida para °/s (15 RPM × 360°/60s) — é a unidade usada para limitar o movimento simulado da torre nos dois eixos (ver <see cref="TorreTreinamento.AnguloAzimuteAtualGraus"/>/<see cref="TorreTreinamento.AnguloElevacaoAtualGraus"/> e <see cref="AmbienteTreinamento.AtualizarMiraDasTorres"/>).</summary>
    public const double VelocidadeAngularTorreGrausPorSegundoPadrao = VelocidadeAngularTorreRpm / 60.0 * 360.0;

    /// <summary>Velocidade angular (°/s) que os DOIS eixos da torre (azimute e elevação) conseguem girar — mesmo valor para os dois, já que são o mesmo modelo de motor.</summary>
    public double VelocidadeAngularTorreGrausPorSegundo { get; init; } = VelocidadeAngularTorreGrausPorSegundoPadrao;

    /// <summary>
    /// Diferença angular (°), em QUALQUER eixo, abaixo da qual a mira é considerada "alcançada" e o
    /// disparo passa a ser fisicamente possível — motor de passo real nunca para EXATAMENTE no
    /// ângulo-alvo (resolução finita de passo); esta tolerância absorve isso. Veto inegociável de
    /// disparo (ver <see cref="TorreTreinamento.MiraAlinhada"/>), igual em espírito a zona morta e
    /// certeza mínima.
    /// </summary>
    public double ToleranciaMiraGraus { get; init; } = 3.0;

    /// <summary>
    /// Dedução de <see cref="VelocidadeMaximaAlvoMetrosPorSegundo"/> a partir da velocidade angular
    /// da torre: para a torre conseguir acompanhar um alvo que se move tangencialmente (pior caso —
    /// movimento perpendicular à linha de visão, que exige a MAIOR velocidade angular para uma
    /// dada velocidade linear) a uma distância crítica <c>r</c>, a velocidade angular necessária é
    /// <c>ω = v / r</c>; isolando <c>v</c>: <c>v_max = ω_torre · r_critico</c>. Usa
    /// <see cref="DistanciaMinimaBoaCertezaMetrosPadrao"/> (2 m) como <c>r_critico</c> — a distância
    /// mais exigente em que o sistema ainda precisa confirmar/acompanhar com margem (mais perto que
    /// isso, ver a regra de certeza garantida em <see cref="AlvoTreinamento.CertezaEfetiva"/>, onde
    /// a torre já está em alcance de confirmação e girar um pouco mais devagar não compromete tanto
    /// a decisão). Um fator de margem de 70% é aplicado por cima do limite teórico — folga para o
    /// atraso de aceleração do motor (ele não atinge a velocidade máxima instantaneamente) e para o
    /// tempo de reação do resto do sistema (seleção de torre, processamento de imagem).
    /// </summary>
    /// <remarks>
    /// Resultado: 15 RPM = 90 °/s = π/2 rad/s; r_critico = 2 m ⇒ v_max teórico ≈ 3,14 m/s;
    /// com a margem de 70% ⇒ ≈ 2,2 m/s. Documentado como constante calculada (não um número
    /// "chutado") para a premissa inteira ficar auditável a partir daqui.
    /// </remarks>
    public const double VelocidadeMaximaAlvoDerivadaMetrosPorSegundo =
        0.7 * (VelocidadeAngularTorreGrausPorSegundoPadrao * System.Math.PI / 180.0) * DistanciaMinimaBoaCertezaMetrosPadrao;

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

    /// <summary>Valor de fábrica de <see cref="DistanciaMinimaBoaCertezaMetros"/> — <c>const</c> para poder entrar na dedução de <see cref="VelocidadeMaximaAlvoDerivadaMetrosPorSegundo"/> (que precisa de uma constante em tempo de compilação).</summary>
    public const double DistanciaMinimaBoaCertezaMetrosPadrao = 2.0;

    /// <summary>Distância (m) da torre abaixo da qual a câmera não consegue mais confirmar o alvo com boa certeza (alvo perto demais da lente) — ver <see cref="AlvoTreinamento.CertezaEfetiva"/>. Acima de <see cref="DistanciaCertezaMaximaMetros"/> esta regra nem chega a valer (ver lá).</summary>
    public double DistanciaMinimaBoaCertezaMetros { get; init; } = DistanciaMinimaBoaCertezaMetrosPadrao;

    /// <summary>Valor de fábrica de <see cref="DistanciaCertezaMaximaMetros"/>.</summary>
    public const double DistanciaCertezaMaximaMetrosPadrao = 1.2;

    /// <summary>
    /// Distância (m) da torre até a qual o alvo é confirmado com 100% de certeza, independente de
    /// <see cref="AlvoTreinamento.Certeza"/> — ver a dedução completa e a descontinuidade proposital
    /// com <see cref="DistanciaMinimaBoaCertezaMetros"/> em <see cref="AlvoTreinamento.CertezaEfetiva"/>.
    /// </summary>
    public double DistanciaCertezaMaximaMetros { get; init; } = DistanciaCertezaMaximaMetrosPadrao;

    /// <summary>Quadrantes tratados como zona morta neste episódio — vazio por padrão (nenhuma restrição). Espelha <see cref="Services.IZonaMortaService"/> do sistema real, só que fixo por episódio em vez de configurável em tela.</summary>
    public IReadOnlySet<Quadrant> ZonasMortasQuadrantes { get; init; } = new HashSet<Quadrant>();

    /// <summary>Distância (m) da base abaixo da qual o alvo, mesmo sem escapar de fato, já custa pontos à geração — aviso de proximidade perigosa.</summary>
    public double DistanciaPerdaPontoMetros { get; init; } = 0.5;

    /// <summary>Penalidade de fitness aplicada uma única vez por alvo ao cruzar <see cref="DistanciaPerdaPontoMetros"/> — menor que <see cref="PenalidadeEscape"/> porque o alvo pode ainda vir a ser abatido depois.</summary>
    public double PenalidadePerdaPonto { get; init; } = 3.0;

    /// <summary>Velocidade (m/s) a partir da qual um alvo aciona ajuda de torre adjacente — mesma regra e mesmo valor de fábrica de <see cref="Configuration.ConfiguracoesAplicativo.VelocidadeRapidaMetrosPorSegundo"/> (ver <see cref="AmbienteTreinamento.DecidirDisparos"/>).</summary>
    public double VelocidadeRapidaMetrosPorSegundo { get; init; } = 1.2;

    public int TamanhoPopulacao { get; init; } = 100;

    /// <summary>
    /// Probabilidade de um descendente ganhar um neurônio novo (mutação estrutural do NEAT).
    /// Substitui a antiga <c>TaxaMutacao</c> (probabilidade por gene), que não tem equivalente
    /// aqui: o SharpNEAT trata mutação de PESO e mutação de ESTRUTURA como eventos distintos, e
    /// é a segunda que define o quanto a rede pode mudar de forma ao longo da evolução.
    /// </summary>
    /// <remarks>
    /// Elevado de 0,01 para 0,03 em 2026-11-07: baterias V2 em escala grande (ver
    /// <c>REDESENHO_CONTROLADOR_V2.md</c>, seção "Bateria 3") mostraram desempenho tático
    /// levemente abaixo de V1 mesmo com 300 gerações — hipótese de que o valor de fábrica,
    /// combinado com população pequena dividida em poucas espécies, satura a busca num ótimo
    /// local de topologia cedo demais. Não é conclusão fechada, é tentativa de reabrir o espaço
    /// de busca estrutural.
    /// </remarks>
    public double ProbabilidadeAdicionarNo { get; init; } = 0.03;

    /// <summary>
    /// Número de conexões a partir do qual a evolução passa a SIMPLIFICAR em vez de crescer
    /// (ver <see cref="TreinadorNeat"/>). Sem teto, a topologia cresce indefinidamente e cada
    /// avaliação — já são <see cref="EpisodiosPorAvaliacao"/> episódios por indivíduo — fica
    /// progressivamente mais cara sem ganho de fitness correspondente.
    /// </summary>
    public int TetoComplexidade { get; init; } = 60;

    /// <summary>Em quantas espécies a população é dividida. Mecanismo central do NEAT: protege uma topologia recém-criada de competir de igual para igual com redes já otimizadas antes de ter tempo de ser ajustada.</summary>
    /// <remarks>Elevado de 6 para 10 em 2026-11-07 junto com <see cref="TamanhoPopulacao"/> e <see cref="ProbabilidadeAdicionarNo"/> — mesma hipótese: mais espécies simultâneas sustentam diversidade de topologia por mais tempo antes de uma dominar a população.</remarks>
    public int QuantidadeEspecies { get; init; } = 10;

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
    // Controlador V2 (ver ControladorHibridoV2) e fitness V2 — tudo nesta seção é OPT-IN:
    // UsarFitnessV2/RedeRecorrente começam desligados, e os parâmetros exclusivos da V2
    // (TempoRespostaReferenciaSegundos em diante) só têm efeito quando usados pelos métodos "V2"
    // de AmbienteTreinamento (ExecutarEpisodioRapidoV2/AvaliarFitnessV2) — o controlador V1
    // (ExecutarEpisodioRapido/AvaliarFitness) nunca os lê. Isto NÃO significa que V1 com
    // parâmetros padrão seja idêntico a antes desta seção existir: outras mudanças (trajetória
    // aleatória, velocidade de alvo derivada da velocidade angular da torre, veto de mira física,
    // certeza garantida a 1,2 m) alteram o comportamento de V1 e V2 igualmente — são físicas do
    // mundo simulado, não específicas da V2. V1 continua sendo a REFERÊNCIA (camada heurística +
    // 7 entradas/2 saídas), só não é mais bit-a-bit igual ao código anterior a estas mudanças.
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
