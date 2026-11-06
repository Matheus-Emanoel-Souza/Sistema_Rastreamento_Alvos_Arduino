Funcionalidade: documenta a remodelagem experimental do controlador híbrido/NEAT do RadarTorres
(branch `feature/radartorres-neat-controlador-v2`) — o que foi implementado, o que foi
simplificado (e por quê) e os resultados REAIS da bateria comparativa V1×V2, sem números
inventados.

# Redesenho do controlador neuroevolutivo — V2 (experimental)

## 1. Ponto de partida (análise do que já existia)

Antes de qualquer mudança, o estado atual foi mapeado em `src/RadarTorres.App/NeuralEvolution/`:

- **`ControladorHibrido`** (V1, produção): 7 entradas (distância, mesmo quadrante?, quadrante
  adjacente?, ângulo de mira, velocidade, tempo detectado sem abate, fator da camada heurística) →
  2 saídas (ajuste de pontuação, confiança de disparo). Três camadas heurísticas filtram as
  candidatas (próprio quadrante → adjacente → sobressalente); a rede só ajusta dentro do que a
  heurística já aceitou. `ModoDecisao` permite isolar `Hibrido` / `RedePura` / `HeuristicaPura`
  para o estudo de ablação já existente.
- **NEAT (`TreinadorNeat`/`RedeNeat`)**: SharpNEAT 4.1.0, rede **acíclica** por padrão
  (`Aciclica = true`), sem estado entre ativações — `RedeNeat.Ativar` chamava
  `IBlackBox.Reset()` ao fim de toda ativação.
- **Fitness (`AmbienteTreinamento`/`ParametrosTreinamento`)**: recompensa de abate, penalidade de
  escape, de invasão de torre, de desequilíbrio de uso entre torres e de recusa de disparo. Nada
  media tempo de resposta, cooperação entre torres ou troca de alvo.
- **Mundo de treino**: 4 torres fixas, 1 por quadrante (layout do `appsettings.json`). Isso importa
  para interpretar os resultados da seção 4.

## 2. O que foi implementado

Tudo abaixo é **aditivo e opt-in** — nenhum parâmetro padrão de `ParametrosTreinamento` mudou de
valor, `ControladorHibrido` (V1) não foi alterado em nenhuma linha de lógica, e a suíte de testes
pré-existente passa sem modificação (ver seção 5). V1 continua sendo a referência de produção.

### 2.1 Novo contrato de entradas/saídas — `ControladorHibridoV2`

Classe nova, lado a lado com a V1, com **15 entradas** e **3 saídas**:

| # | Entrada nova | De onde vem |
|---|---|---|
| 7 | Torres também acompanhando o alvo (normalizado) | quantas outras torres já teriam este alvo em seu campo de alcance (próprio quadrante, adjacente, ou todas se o eixo Z tornar o alvo visível por todas) |
| 8 | Disponibilidade da torre candidata | `torre.Livre` — relevante sobretudo fora do modo híbrido, onde torres ocupadas também poderiam ser avaliadas |
| 9 | Direção do movimento do alvo | ângulo do vetor velocidade do alvo, normalizado — distinto do ângulo de mira (torre→alvo) |
| 10 | Tempo estimado até saída de alcance | distância até a base / velocidade, normalizado contra o pior caso plausível |
| 11 | Altura do alvo (eixo Z) | `0` sempre que o eixo Z estiver desligado — mesma convenção da V1 |
| 12 | Visível por todas as torres (eixo Z) | idem |
| 13 | Acompanhamento prévio | `true` quando a torre avaliada já é a comprometida com aquele alvo |
| 14 | Trocas recentes de alvo desta torre | quantos novos compromissos a torre assumiu dentro de uma janela de tempo (`JanelaTrocaDeAlvoSegundos`, padrão 6s) — ver `TorreTreinamento.ContarTrocasRecentes` |

Saída nova: **prioridade de cooperação** (saída 2) — somada à regra de velocidade já existente
para decidir se uma torre de ajuda deve ser chamada.

A camada heurística (próprio quadrante → adjacente → sobressalente) foi **copiada, não
reinventada** — é bit-a-bit a mesma regra de elegibilidade de torre da V1, só o vetor de entrada
da rede mudou.

### 2.2 Fitness V2 (`ParametrosTreinamento.UsarFitnessV2`, padrão `false`)

Quando ligado (só nas avaliações V2), soma-se ao fitness já existente:

- **Tempo de resposta**: bônus (até `RecompensaTempoRespostaMaximo`) proporcional a quão rápido a
  torre disparou depois de comprometer o alvo. Medido para V1 *e* V2 (ver
  `EstadoEpisodio.TempoDeRespostaMedioSegundos`), mas só vira fitness com a flag ligada.
- **Cooperação bem-sucedida**: bônus quando uma torre de ajuda concedida efetivamente contribuiu
  para o abate.
- **Concentração indevida**: penalidade — e a cooperação é **negada** — quando a rede pede ajuda
  enquanto existe outro alvo ativo sem nenhuma torre comprometida. É a resposta direta ao pedido
  de "não incentivar concentrar todas as torres em um alvo quando outro precisa de atendimento".
- **Trocas excessivas de alvo**: penalidade quando uma torre assume um novo alvo além do limite
  configurado (`LimiteTrocasAntesDePenalizar`, padrão 2) dentro da janela de tempo.

### 2.3 Rede recorrente

Dois níveis, com escopo diferente — documentado assim porque o SharpNEAT 4.1 não dá os dois de
graça no mesmo lugar:

1. **Dentro do laço evolutivo** (treinamento de verdade, `TreinadorNeat(..., redeRecorrente: true)`):
   o SharpNEAT evolui uma rede **cíclica** de verdade (`IsAcyclic = false`,
   `NetFileModel`/`ToCyclicNeuralNet`). `RedeNeat` ganhou um modo
   `manterEstadoEntreAtivacoes` que **não chama `Reset()`** a cada ativação — o estado interno da
   caixa-preta persiste de uma decisão para a próxima **dentro do mesmo episódio**.
   `AmbienteTreinamento.ExecutarEpisodioRapidoV2` chama `RedeNeat.Reiniciar()` no **início de cada
   episódio novo** — isola entre simulações (seeds/episódios) e, como o SharpNEAT decodifica uma
   caixa-preta nova por genoma avaliado, também isola entre indivíduos.
   **Limitação aceita**: essa única caixa-preta é compartilhada entre as decisões de TORRES
   diferentes dentro do mesmo passo — não há isolamento por torre aqui. O motivo é de framework,
   não de preguiça: `IPhenomeEvaluator<IBlackBox<double>>.Evaluate` recebe só a caixa-preta já
   decodificada, nunca o genoma — não há como decodificar N cópias independentes (uma por torre)
   sem acesso ao genoma dentro do avaliador.
2. **Fora do laço evolutivo** (replay/avaliação isolada de um genoma já escolhido —
   `RedeNeatRecorrente`, construída por `TreinadorNeat.CriarRedeRecorrente(genoma)`): aqui SIM há
   genoma + decodificador em mãos, então cada torre ganha sua PRÓPRIA caixa-preta (decodificada na
   primeira vez que aparece), sem se misturar com a de outra torre.
   `ReiniciarTudo()` é chamado no início de cada episódio. Testado em
   `RedeNeatRecorrenteIsolamentoTests` com um genoma REAL (treinado por `TreinadorNeat`, não
   montado à mão) — confirma que dois contextos diferentes nunca compartilham estado e que
   `ReiniciarTudo` zera os dois de uma vez.

**O que não foi feito, e por quê**: isolamento por torre *dentro* do laço evolutivo (treinamento)
exigiria um avaliador que opere sobre o `NeatGenome`, não sobre o `IBlackBox` já decodificado —
`IBlackBoxEvaluationScheme<T>` do SharpNEAT 4.1 não expõe essa extensão. Faria-la exigiria
substituir a integração por uma mais baixo nível com o motor evolutivo do framework (fora do
escopo desta mudança) ou decodificar o genoma por fora e reimplementar manualmente o laço de
seleção/reprodução do NEAT — essencialmente abandonar o SharpNEAT. Por isso o treinamento usa a
versão com estado compartilhado por episódio, e a versão isolada por torre fica reservada para
replay/avaliação pós-treinamento.

## 3. Testes

- `ControladorHibridoV2Tests`: contrato de entradas/saídas, camada heurística idêntica à V1,
  leitura da saída de cooperação, veto de tempo mínimo de mira, e a entrada "acompanhamento
  prévio" isolada.
- `RedeNeatRecorrenteIsolamentoTests`: isolamento por contexto com genoma real treinado (não
  construído à mão) — ver seção 2.3.
- Suíte completa: **242/242 aprovados** (excluindo `PersistenciaSqliteTests`, que falha de forma
  **idêntica e pré-existente na `main`**, sem relação com esta mudança — confirmado rodando a
  mesma suíte com as alterações desta branch empilhadas via `git stash` e sem elas).

## 4. Resultados reais da comparação V1 × V2

Executados com `dotnet run --project tools/RadarTorres.Experimentos -- v2 ...` (comando novo,
`V2()` em `Program.cs`), que treina cada variante do zero e valida em episódios NUNCA vistos no
treino (seeds 95000+). Nada abaixo foi estimado — são as médias reais impressas pela ferramenta,
também gravadas em `tools/RadarTorres.Experimentos/Resultados/comparacao_v2.json`.

**Aviso de escala**: por orçamento de tempo desta sessão, as duas baterias abaixo usam população e
gerações bem menores que o regime histórico do projeto (população 60 / 300 gerações, ver
`ExecutorExperimentosHiperparametros`). Leia os números como teste de sanidade/viabilidade, não
como conclusão definitiva sobre qual controlador é melhor em convergência plena.

**Aviso de comparabilidade**: o fitness bruto de V1 e V2 **não é a mesma escala** — V2 soma/subtrai
os termos novos da seção 2.2. Comparar os números de "fitness" entre V1 e V2 diretamente não é
válido; a comparação justa é pelas métricas operacionais comuns (abates, escapes, erros, tempo de
resposta).

### Bateria 1 — população 24, 40 gerações, 3 repetições, 30 episódios de validação

| Variante | VAL fitness | abatidos | escapados | tiros errados | tempo resposta (s) | trocas penalizadas | cooperações conc./neg. |
|---|---|---|---|---|---|---|---|
| V1 (referência) | 65,17 | 8,97 | 0,90 | 5,98 | 2,12 | — | — |
| V2 | 60,49 | 8,97 | 0,90 | 5,98 | 2,12 | 4,84 | 0,00 / 0,00 |
| V2-recorrente | 60,40 | 8,97 | 0,90 | 5,98 | 2,12 | 4,89 | 0,00 / 0,00 |

### Bateria 2 — população 40, 120 gerações, 2 repetições, 40 episódios de validação, teto de complexidade 80

| Variante | VAL fitness | abatidos | escapados | tiros errados | tempo resposta (s) | trocas penalizadas | cooperações conc./neg. |
|---|---|---|---|---|---|---|---|
| V1 (referência) | 63,37 | 8,81 | 0,91 | 6,50 | 2,08 | — | — |
| V2 | 60,63 | 9,03 | 0,90 | 6,65 | 2,08 | 4,99 | 0,00 / 0,00 |
| V2-recorrente | 58,63 | 8,81 | 0,91 | 6,50 | 2,08 | 4,88 | 0,00 / 0,00 |

### Leitura honesta dos números

- **Abates/escapes/erros praticamente iguais entre V1 e V2** nas duas baterias. Com orçamento tão
  pequeno, as três variantes convergem rápido para "disparar assim que a heurística libera" — é o
  sinal de fitness mais fácil de aprender (a penalidade de recusa de disparo já empurra fortemente
  nessa direção mesmo na V1) — e isso domina o comportamento antes que as entradas/saída novas da
  V2 consigam diferenciar a tática. Não há, nestes dados, evidência de que a V2 já supera a V1 em
  resultado tático neste orçamento — só de que a INSTRUMENTAÇÃO nova funciona (ver próximo ponto).
- **"Trocas penalizadas" só existe para V2** (~5 por episódio em média) — confirma que o rastreio
  de troca de alvo por torre está de fato sendo contado e penalizado; não há como comparar com V1
  porque a V1 não tem esse conceito.
- **Cooperação ficou em 0/0 nas duas baterias.** Investigado a fundo numa revisão posterior (ver
  `AmbienteTreinamentoTests.DecidirDisparosV2_SorteioAleatorioComTrajetoriaCurva_ConcedeCooperacaoEmAlgumaSeed`):
  **não é raridade por pouco treino, é geometria do layout.** Testei primeiro
  `CatalogoCenarios.AlvoRapidoDeAltaVelocidade` isoladamente (que nasce na diagonal Q1→Q3) e depois
  um spawn scriptado à mão cruzando perto da fronteira Q1/Q4 — as duas tentativas deram ZERO
  cooperação em 15 episódios combinados. Motivo: no layout real (quadrado 3,5 m de lado, torres a
  ~2,475 m do centro), QUALQUER trajetória RADIAL (linha reta) perto o bastante de uma torre
  ADJACENTE para entrar no raio de ajuda (`RaioAjudaTorreAdjacenteMetros` = 2,0 m) passa, pela
  simetria do quadrado, IGUALMENTE perto da torre do PRÓPRIO quadrante — e é essa torre principal
  que perde o alinhamento primeiro (o "flyby" de perto exige mais que os 90°/s do motor — ver seção
  2.3), nunca chega a disparar, e sem tiro perdido não há cooperação para pedir. Só com a
  trajetória ALEATÓRIA (curva, ver `ParametrosTreinamento.VariacaoMaximaDirecaoGrausPorSegundo`,
  não existia na época desta bateria) o caminho deixa de ser reto e a cooperação passa a disparar —
  confirmado em pelo menos 1 de 40 seeds do sorteio aleatório no teste citado acima. Conclusão
  prática: **a regra de ajuda de 2,0 m, herdada da V1, é quase morta neste layout de 4 torres com
  trajetória reta** — ela só ganhou vida com a trajetória curva adicionada depois. Vale reavaliar
  se 2,0 m é o raio certo para este layout (o ponto mais próximo possível de uma torre adjacente, a
  partir do centro do mundo, é 2,475 m — ou seja, mesmo no melhor caso geométrico sem flyby na
  torre principal, o raio de 2,0 m já está abaixo da distância mínima teórica ao QUADRANTE
  adjacente, dependendo só da curva levar o alvo pra mais perto que isso).
- **Rede recorrente (V2-recorrente) não mostrou diferença consistente** em relação à V2 acíclica
  nestas duas baterias — esperado neste orçamento: o sinal que a recorrência poderia explorar
  (memória de decisões anteriores da mesma torre) é sutil e precisa de mais gerações para a
  evolução "descobrir" como usá-lo, além de a entrada 14 (trocas recentes) já cobrir parte dessa
  informação de forma explícita, sem precisar de memória implícita.

### Bateria 3 — escala grande (população 60, 300 gerações, 5 repetições, 50 episódios de validação)

Rodada numa sessão posterior, já sob a física nova (trajetória aleatória, velocidade de alvo
derivada do motor, certeza a 1,2 m — ver `HISTORICO_ALTERACOES.md`/commits desta branch). Levou
195 s no total para as 15 execuções (3 variantes × 5 repetições). Comando exato na seção 5.

| Variante | VAL fitness | abatidos | escapados | tiros errados | tempo resposta (s) | trocas penalizadas | cooperações conc./neg. | tempo treino (s) |
|---|---|---|---|---|---|---|---|---|
| V1 (referência) | 117,96 | 14,16 | 0,56 | 8,99 | 2,22 | — | — | 13,6 |
| V2 | 102,40 | 13,50 | 0,60 | 8,44 | 2,18 | 8,41 | 0,01 / 0,00 | 12,5 |
| V2-recorrente | 98,58 | 13,32 | 0,60 | 8,41 | 2,22 | 8,33 | 0,01 / 0,00 | 12,5 |

**Leitura honesta — agora com dado suficiente para uma conclusão, não mais "falta rodar maior":**

- **V1 continua abatendo mais que V2 nesta escala** (14,16 vs 13,50 vs 13,32 — V1 cerca de 5-6%
  acima). A diferença é pequena mas CONSISTENTE nas 5 repetições (ver médias) e na mesma direção
  da bateria pequena (seção anterior) — não é ruído que se inverteu com mais treino. **Não há, com
  estes dados, evidência de que a V2 (entradas/saída novas, fitness revisado) melhora o resultado
  tático em relação à V1** — ao contrário, ela fica ligeiramente atrás, possivelmente porque o
  espaço de busca maior (15 entradas vs 7, mais os termos de fitness V2) exige mais que 300
  gerações para a evolução explorar tão bem quanto já explora o espaço menor da V1.
- **Cooperação deixou de ser ZERO** (0,01 em média — cerca de 1 cooperação concedida a cada ~100
  episódios de validação) — confirma que, com a trajetória aleatória curva agora presente na
  física, o mecanismo FUNCIONA de ponta a ponta em produção, exatamente como a seção acima previu.
  Continua raro por ser geometricamente exigente (ver seção acima), não por estar quebrado.
- **V2-recorrente não supera V2 acíclica** mesmo nesta escala maior — mesma leitura de antes
  continua válida: a entrada explícita de trocas recentes (entrada 14) parece cobrir boa parte do
  sinal que a recorrência tentaria capturar implicitamente, e/ou 300 gerações ainda não é o
  suficiente para a evolução aprender a USAR memória de verdade.
- **Trocas penalizadas (~8,4 por episódio) são uma penalidade ativa e não-trivial no fitness da
  V2** — isso sozinho pode explicar parte da diferença de fitness bruto V1×V2 (lembrando que as
  escalas de fitness NÃO são diretamente comparáveis — ver aviso acima), mas o abatidos/escapados
  (comparável diretamente) já mostra a mesma tendência.

**Conclusão prática desta revisão:** no estado atual, com os hiperparâmetros de fábrica, a V2 não
demonstra vantagem tática sobre a V1 — demonstra, sim, que toda a instrumentação nova (cooperação,
trocas, tempo de resposta, rede recorrente) funciona corretamente de ponta a ponta. Antes de
considerar promover a V2 a candidata de produção, valeria: (a) tentar um teto de complexidade maior
(a V2 tem quase o dobro de entradas da V1, pode precisar de mais conexões para equivaler), (b) mais
gerações (600-1000) para ver se a diferença encolhe ou se estabiliza, e (c) isolar o efeito de CADA
termo novo (rodar com só `RecompensaTempoRespostaMaximo`, só a penalidade de trocas, etc., um de
cada vez) para saber qual está ajudando e qual está atrapalhando — agregados, o resultado líquido
hoje é neutro-a-levemente-negativo.

## 5. Como reproduzir

```powershell
dotnet test tests/RadarTorres.Tests/RadarTorres.Tests.csproj --filter "FullyQualifiedName!~PersistenciaSqliteTests"

dotnet run --project tools/RadarTorres.Experimentos -c Release -- v2 --pop=24 --ger=40 --repeticoes=3 --val=30 --teto=60 --recorrente=sim

# Bateria 3 (escala grande, regime histórico do projeto) — ~195 s
dotnet run --project tools/RadarTorres.Experimentos -c Release -- v2 --pop=60 --ger=300 --repeticoes=5 --val=50 --teto=60 --recorrente=sim

# Teste de integração que comprova cooperação funcionando de ponta a ponta (sorteio aleatório, trajetória curva)
dotnet test tests/RadarTorres.Tests/RadarTorres.Tests.csproj --filter "FullyQualifiedName~SorteioAleatorioComTrajetoriaCurva"
```

Resultados brutos (todas as execuções, inclusive as três baterias acima) em
`tools/RadarTorres.Experimentos/Resultados/comparacao_v2.json`.
