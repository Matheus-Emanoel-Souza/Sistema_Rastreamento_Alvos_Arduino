Funcionalidade: registro das métricas escolhidas para os objetivos específicos do RadarTorres —
hardware/mecânica (projeto físico) e treinamento do controlador neuroevolutivo (software) —,
definição operacional, forma de medição, e onde cada uma se apoia no hardware real ou na
literatura de neuroevolução.

## Remodelagem das regras da simulação (2026-11-12)

> **A incluir no artigo** (Seção de procedimentos metodológicos / descrição do ambiente de
> treinamento): o ambiente de simulação mudou nestes três pontos, com impacto direto na forma de
> medir as métricas de software (seção abaixo) e de treinar o controlador.

1. **Removido o teto de alvos ativos por quadrante** (`MaxAlvosPorQuadrante`, era 5) — o cenário
   `EnxameDeSaturacao` (6 alvos quase simultâneos) deixou de ter um teto artificial descartando
   nascimentos; agora testa genuinamente a distribuição de torres sob carga sem limite.
2. **Removido o fim de episódio por duração** (`DuracaoEpisodioSegundos`, era 45s, e o toggle
   `SemLimiteDeDuracao`) — episódio agora só termina por invasão de torre (alvo a <1,0 m de uma
   torre) ou pelo teto de segurança contra loop infinito (`DuracaoMaximaSegurancaSegundos`, 1800s,
   inalterado). `DuracaoEpisodioSegundos` continua existindo só como referência da escalada de
   dificuldade (ritmo de nascimento), não mais como critério de fim.
3. **Aumentada a aleatoriedade da trajetória dos alvos rumo à base** — desvio inicial na direção de
   nascimento de ±30° para ±50°, e o passeio aleatório contínuo (`VariacaoMaximaDirecaoGrausPorSegundo`)
   de 15°/s para 35°/s. O mecanismo em si (nasce na borda, mira a base, sofre desvio contínuo) já
   existia; o que mudou foi só a intensidade da curva.

**Impacto nas métricas de software**: sem corte por tempo, um episódio só acaba quando o
controlador falha (invasão) ou no teto de segurança — o fitness por episódio deixa de ser
limitado pela duração curta e passa a refletir diretamente a qualidade de decisão do controlador.
Isso reforça a importância do gap (fitness máximo − médio, item 3 abaixo) como critério de
estabilização: sem fim por tempo, a única forma de saber que a população "convergiu" é observar o
próprio comportamento agregado de fitness entre gerações, não mais um teto artificial do ambiente.

## Métricas do projeto físico — RadarTorres

Como o firmware não aciona nenhum atuador de disparo real (`FIRE` é só sinalização lógica,
sem projétil/laser — ver `Projeto_Fisico/Eletronica/Diagrama_Eletrico.md`, linhas 150 e 178), as
métricas abaixo avaliam a parte física que de fato existe: sensoriamento (ultrassom), apontamento
(motores de passo) e a estimativa de posição resultante da fusão desses dois sinais.

## 1. Latência

**Definição**: tempo entre o comando de apontamento (`AIM;TOWER=<id>;ANGLE=...;TILT=...`, enviado
pelo PC) e o eixo da torre estabilizar no ângulo alvo, dentro de uma tolerância definida (ex.: ±1°).

**Como medir**: registrar o timestamp de envio do `AIM` (lado PC) e o timestamp em que o firmware
reporta a posição final do eixo (via `ACK`/`STATUS`, ou por leitura de um encoder/câmera externa
apontada para o eixo). A diferença é a latência de apontamento.

**Relevância**: mede o tempo de resposta mecânico do sistema — relevante porque o controlador
(heurística + NEAT) decide "quando" disparar com base na certeza de identificação, mas essa decisão
só é útil se a torre física conseguir se posicionar a tempo.

**Variáveis a reportar**: latência média e desvio-padrão, por torre (Q1-Q4) e por eixo (pan/tilt),
já que pan usa NEMA17+A4988/DRV8825 (câmeras) ou 28BYJ-48+ULN2003 (torres) com velocidades e
microstepping distintos (`TORRE_MICROPASSOS`/`CAM_MICROPASSOS = 16`, ver `Diagrama_Eletrico.md`
seção 5).

## 2. Repetibilidade

**Definição**: variação do resultado (erro angular ou de posição) ao repetir N vezes o mesmo
comando de apontamento para o mesmo alvo fixo — mede consistência mecânica, não só acurácia pontual.

**Como medir**: comandar o mesmo `AIM;ANGLE=<valor fixo>` N vezes (ex.: N=10 ou N=30), medindo a
cada repetição o ângulo real do eixo (goniômetro/transferidor ou câmera externa). Calcular
desvio-padrão (ou faixa) das N leituras.

**Relevância**: separa dois tipos de erro — um erro sistemático (viés, calibrável via `CALIB` ou
ajuste de `camPassosPorPixelPan/Tilt`) de um erro aleatório (backlash/histerese dos motores de
passo, folga mecânica), que não se corrige por calibração simples.

**Observação**: os motores 28BYJ-48 (torres) têm engrenagem redutora (~64:1, `SWEEP_STEPS_POR_VOLTA
= 2048`) com folga de engrenagem conhecida — repetibilidade tende a ser pior ao inverter sentido de
rotação (histerese). Se possível, medir repetibilidade separadamente por sentido de aproximação
(CW vs. CCW).

## 3. Erro da estimativa de posição cartesiana

**Definição**: diferença entre a posição (x, y) do alvo exibida no radar circular (calculada por
fusão do ângulo do motor de varredura central + distância lida pelo HC-SR04) e a posição real do
alvo, medida fisicamente (ex.: régua/trena a partir da base central).

**Como medir**: posicionar o alvo em coordenadas conhecidas (várias distâncias e ângulos de
referência), ler a posição estimada pelo sistema (mensagem `USONIC;SENSOR=<1|2>;ANGLE=<0-360>;
DIST=<cm>` convertida para cartesiano) e calcular o erro euclidiano: erro = sqrt((x_estimado -
x_real)² + (y_estimado - y_real)²).

**Relevância**: é a métrica mais direta de "onde o sistema acha que o alvo está" vs. "onde o alvo
realmente está" — acumula tanto o erro angular do motor de varredura (28BYJ-48, varredura 0°-180°
em zigue-zague) quanto o erro de distância do sensor ultrassônico (HC-SR04).

**Observação**: os 2 sensores HC-SR04 estão opostos a 180° no mesmo eixo (SENSOR=2 reporta ângulo =
ângulo do motor + 180°, mod 360) — reportar o erro separadamente por sensor permite verificar se há
viés diferente entre os dois.

**Variáveis a reportar**: erro médio, desvio-padrão e erro máximo, idealmente em função da
distância do alvo à base (o erro angular tende a crescer linearmente com a distância: erro_linear ≈
distância × tan(erro_angular)).

## Ver também (projeto físico)

- `Projeto_Fisico/Eletronica/Diagrama_Eletrico.md` — pinagem e protocolo serial (`AIM`, `USONIC`,
  `CALIB`) usados para medir as três métricas.
- `Projeto_Fisico/Pecas/ESPECIFICACOES_MECANICAS.md` — componentes mecânicos (motores, folgas de
  montagem) relevantes para repetibilidade.
- `Artigo/Texto/Artigo.tex` — objetivo específico do projeto físico, onde estas métricas devem ser
  referenciadas.

---

# Métricas do treinamento do controlador (software) — RadarTorres

Três métricas por geração do algoritmo genético/NEAT, já coerentes com o que o próprio artigo
reporta (ex.: `Artigo.tex`, linhas 1006-1007: "fitness médio subiu de 69,28 para 86,09 ... e o
máximo (434,59) empatou") e com o que a literatura de neuroevolução usa como prática padrão.

## 1. Fitness médio (da população, por geração)

**Definição**: média do *fitness* de todos os indivíduos da população em uma geração.

**Por que é padrão na literatura**: é a métrica clássica de progresso evolutivo em algoritmos
genéticos — se a média sobe ao longo das gerações, seleção+crossover+mutação estão de fato
encontrando genomas melhores (já usado no raciocínio do próprio artigo, linhas 855-856). No survey
de neuroevolução em DNNs (Galván & Mooney, 2020 — `Neuroevolucao_Survey_Tendencias2020.pdf`,
p. 8), o fitness médio da população é tratado como sinal primário de tendência evolutiva, inclusive
comparado a uma segunda métrica em paralelo (no caso do survey, número médio de camadas ocultas) para
detectar se duas grandezas estão em conflito ao longo do treino — mesma lógica de comparar fitness
médio com outra métrica (aqui, o máximo) que o item 3 abaixo propõe.

## 2. Fitness máximo (melhor indivíduo da geração)

**Definição**: maior *fitness* entre todos os indivíduos de uma geração (o "campeão").

**Por que é padrão na literatura**: é a métrica usada para reportar desempenho de pico do método —
Such et al. (Uber AI, 2017, "Deep Neuroevolution" — `DeepNeuroevolucao_Such2017.pdf`) comparam GA
contra DQN/A3C/ES justamente pelo desempenho do melhor agente evoluído, não pela média da
população; é também o valor citado no `Artigo.tex` como o que efetivamente entra em produção
("Genoma embutido no executável atual: geração 159, fitness 188,47", linha 361-362; genoma de
produção a 4% do fitness máximo da heurística pura, linha 175).

## 3. Distância entre fitness máximo e fitness médio (gap/dispersão da geração)

> **A incluir no artigo**: esta é a métrica escolhida para decidir se o treinamento
> **estabilizou/convergiu** — não o fitness médio nem o máximo isoladamente. Critério: gerações
> consecutivas em que o gap se mantém dentro de uma faixa estreita (definir limiar, ex.: variação
> menor que X% por N gerações seguidas) indicam que a população parou de progredir em conjunto —
> critério de parada do treino, a reportar na Seção de procedimentos metodológicos.

**Definição**: `gap = fitness_máximo - fitness_médio` da mesma geração (em valor absoluto ou como
percentual do máximo).

**Por que é relevante**: não é uma métrica isolada na literatura, mas é a forma usual de expressar
algo que a literatura trata separadamente como **diversidade da população / pressão seletiva**:

- **Gap pequeno** → população pouco diversa, maioria dos indivíduos já perto do melhor —
  indício de convergência (ou estagnação prematura, se ocorrer cedo demais).
- **Gap grande** → população ainda heterogênea, com indivíduos fracos convivendo com o campeão —
  indício de que a busca ainda está explorando.

O próprio `Artigo.tex` já documenta um caso concreto desse padrão sem nomeá-lo como "gap": antes da
correção do bug de alvos presos abaixo do limiar, "o fitness médio da população [ficou] estagnado
em torno de 9 ... ao longo de 25.500 gerações" enquanto o melhor indivíduo não estagnava do mesmo
jeito — ou seja, um gap grande e persistente mascarando estagnação (linhas 1153-1154). Depois da
correção, "o fitness médio ... passou a acompanhar de perto o fitness do melhor indivíduo" — gap
caindo como evidência de que a evolução voltou a funcionar (linha 1154-1155). Reportar o gap
explicitamente, geração a geração, formaliza esse diagnóstico que o artigo já faz informalmente.

**Como calcular/registrar**: o próprio banco de histórico de gerações do treinamento já grava
"Fitness (melhor e médio) ... a cada geração" (linha 1120-1121 do `Artigo.tex`) — o gap é só a
subtração desses dois valores já armazenados, sem necessidade de nova coleta de dados.

## Ver também (software/treinamento)

- `Rede_Neural/Arquitetura/CONTROLADOR_NEUROEVOLUTIVO.md` — arquitetura da rede e do algoritmo
  evolutivo (NEAT/SharpNEAT) que produz essas três métricas por geração.
- `Artigo/Texto/Artigo.tex`, Seção~\ref{sec:procedimentos-metodologicos} — tabelas de fitness já
  publicadas (ex.: linhas 1032-1034) onde o gap pode ser calculado retroativamente.
- `Artigo/Fundamentacao/Neuroevolucao_Survey_Tendencias2020.pdf` (Galván & Mooney, 2020) — uso de
  fitness médio da população como métrica de tendência evolutiva (p. 8).
- `Artigo/Fundamentacao/DeepNeuroevolucao_Such2017.pdf` (Such et al., Uber AI) — uso do melhor
  indivíduo (fitness máximo) para comparar desempenho de pico contra outros métodos de RL.
