# Controlador Neuroevolutivo (Heurística + Rede Neural)

Documentação técnica do controlador híbrido que combina a heurística em camadas de seleção de
torre (ver [[ALGORITMO_SELECAO_TORRE]]) com uma rede neural evoluída por algoritmo genético.
Código-fonte em `src/RadarTorres.App/NeuralEvolution/`.

## 1. Visão geral

O objetivo deste módulo é refinar a escolha de torre e a decisão do instante de disparo além
do que a heurística pura (menor distância) consegue — sem substituí-la. A heurística continua
decidindo a **camada** de candidatas (torre do próprio quadrante → adjacente → sobressalente,
ver [[ALGORITMO_SELECAO_TORRE]] seção 4) e garante que a decisão nunca seja absurda (nunca
escolhe torre ocupada, nunca ignora a cooperação entre quadrantes). Dentro da camada escolhida,
uma rede neural evoluída por algoritmo genético ajusta a pontuação das candidatas e decide o
instante exato do disparo.

O treinamento ocorre num **ambiente simulado** próprio (`AmbienteTreinamento`), separado do
sistema em produção, e o resultado (um genoma, isto é, um vetor de pesos) é exportado e
embutido no executável para ser usado em tempo real por `TowerSelectionService`.

Implementação própria, sem biblioteca de IA/ML: a rede neural e o algoritmo genético são
código escrito do zero no projeto, "para manter o algoritmo auditável/explicável no TCC"
(comentário em `AlgoritmoGenetico.cs`).

## 2. Classes principais

| Classe/arquivo | Responsabilidade |
|---|---|
| `RedeNeural.cs` | Rede feedforward totalmente conectada, ativação `tanh` em todas as camadas. Pesos e bias não vêm de backpropagation — são lidos de um genoma plano (`double[]`) passado no construtor. |
| `AlgoritmoGenetico.cs` | Evolui uma população de genomas: seleção por torneio (3 indivíduos), crossover uniforme (50% de chance por gene), mutação gaussiana (Box-Muller) e elitismo. |
| `ControladorHibrido.cs` | Combina heurística + rede: monta o vetor de entradas, pontua candidatas (`Pontuar`) e decide o instante de disparo (`DecidirDisparo`). É a MESMA classe usada tanto no ambiente de treinamento quanto (via os métodos estáticos `ConstruirEntradas`/`Pontuar`) em produção por `TowerSelectionService`. |
| `AmbienteTreinamento.cs` | Mundo 2D de simulação: torres fixas (mesmas posições do `appsettings.json`), alvos nascendo na borda e avançando até a base. Função de fitness do algoritmo genético (`ExecutarEpisodioRapido`/`AvaliarFitness`). |
| `EstadoEpisodio.cs` | Estado mutável de um episódio em andamento (torres, alvos, relógio, estatísticas). |
| `TorreTreinamento.cs` / `AlvoTreinamento.cs` | Modelos de torre e alvo exclusivos do ambiente de treinamento (não são `Tower`/`Target` do domínio real). |
| `ParametrosTreinamento.cs` | Parâmetros ajustáveis de um episódio (raio do mundo, velocidades, recompensas/penalidades, tamanho de população, taxa de mutação etc.), expostos na tela de treinamento. |
| `EpisodioResultado.cs` | Resumo de um episódio: fitness total, alvos abatidos/escapados, tiros errados. |
| `ExtratorDeRegras.cs` | Aproxima, por análise de sensibilidade (variação de cada entrada em torno de um ponto de referência), frases em português descrevendo tendências aprendidas pela rede — não é extração exata, é uma leitura qualitativa para acompanhar o treinamento. |

## 3. Arquitetura da rede

Definida em `ControladorHibrido.ArquiteturaPadrao = [6, 8, 2]`:

- **6 entradas** (`ConstruirEntradas`): distância normalizada até o alvo, indicador de mesmo
  quadrante, indicador de quadrante adjacente, ângulo de mira normalizado, velocidade do alvo
  normalizada, fator da camada heurística (1.0 = própria, 0.5 = adjacente, 0.0 = sobressalente).
- **1 camada oculta de 8 neurônios.**
- **2 saídas**: ajuste de pontuação da torre candidata, e confiança de disparo.

Com essa arquitetura, `RedeNeural.TamanhoGenoma([6, 8, 2])` resulta em um genoma de 74 números
(pesos + bias de todas as camadas) — esse é o tamanho citado em
`IControladorTreinadoProvider.Genoma` ("vetor plano de 74 números").

Todas as ativações usam `tanh`, então cada saída fica no intervalo `[-1, 1]`.

## 4. Como o controlador híbrido combina heurística + rede

### 4.1 Seleção de torre (`ControladorHibrido.SelecionarTorre` / `Pontuar`)

A pontuação final de uma torre candidata é:

```
pontuação = pontuação_heurística + ajuste_rede_neural
pontuação_heurística = (1 - distância_normalizada) + fator_camada
ajuste_rede_neural = rede.Ativar(entradas)[0]
```

Ou seja, a rede neural não decide do zero: ela **ajusta** uma pontuação heurística de base
(quanto mais perto e quanto "melhor" a camada, maior). Essa é a mesma combinação usada tanto
no ambiente de treinamento (`AmbienteTreinamento`/`ControladorHibrido.SelecionarTorre`) quanto
em produção (`TowerSelectionService.EscolherMelhorCandidata`, via os métodos estáticos
`ConstruirEntradas`/`Pontuar`).

### 4.2 Decisão de disparo (`ControladorHibrido.DecidirDisparo`)

A heurística impõe um limite inegociável: tempo mínimo de mira de 0,3 s
(`TempoMinimoDeMiraSegundos`) desde que a torre foi comprometida com o alvo. Passado esse
tempo, a rede decide o instante exato — a segunda saída da rede (`confiançaDisparo`) autoriza
o disparo quando é maior que zero. A distância até o alvo **não** é critério dentro desse
método; fora-de-zona-morta e certeza mínima são checados por quem chama (no treinamento,
`AmbienteTreinamento.DecidirDisparos`; em produção, `FireControlService`/regra de autorização).

### 4.3 Em produção (`TowerSelectionService`)

`TowerSelectionService` usa a rede treinada quando ela está disponível
(`IControladorTreinadoProvider.Rede` não nulo); caso contrário, cai para a heurística pura de
menor distância — comentário no código afirma que esse fallback é "idêntico ao que a rede
produz quando o genoma embutido é o zerado de fábrica". A margem de empate para a regra de
anti-oscilação (seção 7.1 de [[ALGORITMO_SELECAO_TORRE]]) muda conforme há ou não rede: 0,3 m
de distância sem rede, ou um valor de pontuação equivalente com rede.

## 5. Treinamento

### 5.1 Episódio de treinamento (`AmbienteTreinamento`)

Cada episódio simula um mundo circular de raio configurável (`RaioMundoMetros`, padrão 10 m)
com as mesmas posições de torre do `appsettings.json` (ou um layout padrão de 4 torres, uma
por quadrante, se não houver configuração — usado nos testes automatizados). Alvos nascem em
pontos aleatórios da borda e avançam em direção à base com um pequeno desvio angular (até
±30°), com velocidade sorteada entre `VelocidadeMinimaAlvoMetrosPorSegundo` e
`VelocidadeMaximaAlvoMetrosPorSegundo`.

Cada alvo também tem uma "certeza" de detecção que começa baixa (`CertezaBase`, sorteada entre
0,20 e 0,50) e sobe com o tempo de observação (`TaxaCrescimentoCertezaPorSegundo`, sorteada
entre 0,08 e 0,25 por segundo) — simula a câmera "confirmando" o alvo ao longo de vários
quadros. Só pode haver disparo quando essa certeza supera `LimiarConfiancaDisparoFracao`
(padrão 0,70).

A cada passo de simulação (`Avancar`, `dt` = 0,2 s no modo rápido): possível nascimento de
alvo, movimento, penalidade de proximidade (ver abaixo), tratamento de alvos que chegaram à
base (fuga), seleção de torre para alvos ainda sem torre comprometida, e decisão de disparo.

**Fitness** (`EstadoEpisodio.FitnessAcumulado`) é incrementado/decrementado por:
- `+RecompensaAbate` (padrão 10,0) por alvo abatido, com bônus proporcional à distância da
  base no momento do abate (incentiva abater longe, não esperar o alvo se aproximar).
- `-PenalidadeEscape` (padrão 8,0) por alvo que chega à base sem ser abatido.
- `-PenalidadePerdaPonto` (padrão 3,0), aplicada uma única vez por alvo, quando ele cruza
  `DistanciaPerdaPontoMetros` (padrão 0,5 m) mesmo sem chegar a escapar de fato — penaliza
  deixar o alvo se aproximar demais antes de decidir.

Tiros errados (`TirosErrados`) contam por chance probabilística crescente com a velocidade do
alvo (`CalculadoraErroDisparo`), mesma regra usada no sistema real — não são penalizados
diretamente no fitness além de não gerarem a recompensa de abate.

Uma regra adicional de "ajuda de torre adjacente" dispara um segundo canhão (de um quadrante
adjacente, a até 2 m do alvo) quando o alvo está acima de `VelocidadeRapidaMetrosPorSegundo`
(padrão 1,2 m/s) — mesma regra 6 citada em commits recentes do projeto ("torre nunca solta
alvo por erro de tiro e ajuda de torre adjacente em alvo rápido").

### 5.2 Algoritmo genético (`AlgoritmoGenetico`)

- População padrão de 60 indivíduos (genomas de 74 genes cada, para a arquitetura `[6,8,2]`).
- Cada gene começa aleatório entre -1 e 1.
- A cada geração, todos os indivíduos são avaliados em paralelo (`AsParallel`) pela função de
  fitness fornecida.
- **Elitismo** (padrão 3): os melhores indivíduos passam direto para a próxima geração, sem
  alteração — garante que o melhor fitness nunca regrida entre gerações.
- O restante da população é gerado por **seleção por torneio** de 3 (sorteia 3, fica com o
  melhor), **crossover uniforme** (cada gene do filho vem do pai ou da mãe com 50% de chance)
  e **mutação gaussiana** (cada gene tem `TaxaMutacao`, padrão 8%, de chance de receber ruído
  com desvio padrão `ForcaMutacao`, padrão 0,5).

Testes automatizados (`tests/RadarTorres.Tests/AlgoritmoGeneticoTests.cs`) cobrem, com uma
função de fitness sintética (soma dos genes): que o melhor fitness não regride ao longo de
várias gerações, que o elitismo impede regressão mesmo geração a geração, que o contador de
geração incrementa corretamente, e — como regressão específica — que o `MelhorGenoma`
devolvido por `EvoluirGeracao` realmente reproduz o `MelhorFitness` relatado (inclusive com
`elitismo: 0`, caso em que um bug anterior devolvia um filho recém-cruzado em vez do melhor
genoma real).

`tests/RadarTorres.Tests/RedeNeuralTests.cs` cobre a rede neural isoladamente: cálculo correto
do tamanho do genoma a partir da arquitetura, determinismo da ativação para a mesma
entrada/genoma, saídas sempre dentro de `[-1, 1]` (saturação do `tanh`), erro ao construir com
genoma de tamanho errado, acesso a pesos individuais (`Peso`) e a ativação por camada
(`AtivarComCamadas`, usada só para visualização — nunca no laço de treinamento/avaliação).

### 5.3 Avaliação com múltiplas seeds (`AvaliarFitness`)

Por padrão o fitness de um genoma é a **média de 3 episódios** (`EpisodiosPorAvaliacao`) com
seeds diferentes, mas as MESMAS seeds para todos os indivíduos de uma geração — comentário no
código explica que um único episódio é "barulhento demais" (nascimento, velocidade e certeza
dos alvos são todos sorteados), e sem múltiplos episódios o ruído de ambiente passaria a
dominar sobre a diferença real de qualidade entre genomas, degradando a seleção por torneio e
até o elitismo a um sorteio.

### 5.4 Exportação e embutimento no app

O genoma do melhor indivíduo é exportado pela tela "Treinamento do Controlador"
(`TreinamentoControladorViewModel.ExportarMelhorGenoma`) para um JSON com arquitetura, genoma,
geração e melhor fitness. Esse JSON é embutido como `EmbeddedResource` em
`src/RadarTorres.App/Resources/ControladorTreinado/genoma-controlador.json` e carregado em
tempo de execução por `ControladorTreinadoProvider`
(`src/RadarTorres.App/Services/ControladorTreinadoProvider.cs`), que expõe a `RedeNeural`
pronta (`IControladorTreinadoProvider.Rede`) e o genoma plano (`Genoma`, 74 números) para quem
precisar. Se nenhum genoma válido estiver embutido (instalação nova, nunca treinada), `Rede` é
`null` e o sistema cai para a heurística pura (ver seção 4.3).

Esse genoma embutido também é enviado ao Arduino, mas **apenas para fins de
diagnóstico/auditoria** (`IRedeEmbutidaSincronizadorService` — comandos `GENOMA;*` e
`REDE;ENTRADAS=` do protocolo serial, ver [[COMUNICACAO_ARDUINO]]). O comentário na interface é
explícito: essa sincronização "nunca participa da seleção de torre nem da autorização de
disparo, que continuam decididas inteiramente pelo app".

## 6. Persistência (SQLite)

Três tabelas dedicadas registram o progresso de um treinamento em andamento (todas descritas
em [[MODELO_DADOS]]):

### `historico_treinamento_geracoes`
Uma linha por **geração** (do melhor indivíduo daquela geração): melhor fitness, fitness
médio, alvos abatidos/escapados. É um **buffer circular de 300 linhas** —
`SqliteHistoricoTreinamentoRepository.Registrar` descarta a linha de `ID` mais antigo ao
inserir a 301ª. Alimenta o gráfico/tabela de evolução recente na tela "Treinamento do
Controlador" (`TreinamentoControladorViewModel`).

### `marcos_fitness_treinamento`
Um marco a cada bloco de 500 gerações: fitness médio do bloco e o melhor fitness já visto
dentro dele. Sem limite de linhas (volume baixo, um marco a cada 500 gerações). Alimenta o
painel "Estatísticas de Treinamento" — exclusivo do Administrador
(`EstatisticasTreinamentoViewModel`), focado em tendência de longo prazo sem o ruído
geração a geração.

### `instantaneos_comportamento_melhor_gene`
Um retrato completo do melhor genoma a cada 1.000 gerações: fitness, abatidos/escapados/tiros
errados, taxas derivadas (acerto = abatidos/(abatidos+tiros errados); cobertura =
abatidos/(abatidos+escapados)), e o **genoma inteiro serializado em JSON** (`GenomaJson`) — para
poder recarregar e reproduzir exatamente aquele comportamento depois. Sem limite de linhas;
nunca sobrescrito. Propósito explícito (comentário no modelo
`InstantaneoComportamentoMelhorGene`): permitir estudar, marco a marco, se a evolução do
treinamento é real ou apenas oscilação de ruído.

Todas as três são somente-inserção (sem Update, exceto pela limpeza do buffer circular da
primeira).

## 7. Incertezas / não confirmado nesta leitura

- O valor exato de corte "margem de empate" usado por `TowerSelectionService` quando há rede
  treinada (`MargemEmpatePontuacaoRede`) não foi lido em detalhe — apenas confirmado que existe
  e é diferente do valor sem rede.
- A frequência/gatilho exato de quando `TreinamentoControladorViewModel` decide registrar um
  marco de 500/1.000 gerações (`RegistrarInstantaneoDeComportamentoSeCompletouBloco` e
  equivalente para `marcos_fitness_treinamento`) não foi inspecionado linha a linha — a
  descrição acima vem dos comentários em `MODELO_DADOS.md` e nos próprios modelos.

## Ver também

- [[ALGORITMO_SELECAO_TORRE]] — heurística em camadas que o controlador híbrido refina; seção 4
  descreve onde a rede neural entra no fluxo de seleção.
- [[MODELO_DADOS]] — schema completo das três tabelas de treinamento e das demais tabelas do
  sistema.
- [[ARQUITETURA]] — camadas do sistema e onde `TowerSelectionService`/`ControladorTreinadoProvider` se encaixam.
- [[COMUNICACAO_ARDUINO]] — protocolo `GENOMA;*`/`REDE;ENTRADAS=` usado para auditoria/diagnóstico do genoma embutido no firmware.
