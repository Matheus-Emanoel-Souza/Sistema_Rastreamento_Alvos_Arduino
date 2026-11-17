Funcionalidade: relatório da auditoria de penalidades/eventos de fitness do ambiente de treinamento (2026-11-12) — o que foi corrigido, o que NÃO foi implementado (e por quê), e os resultados dos experimentos reais de comparação antes/depois.

# Auditoria do ambiente de treinamento — RadarTorres (2026-11-12)

Branch: `refatoracao/treinamento-fitness-v3`, a partir de `fc81f76` (branch
`feature/radartorres-neat-controlador-v2`).

## 1. Escopo real entregue vs. escopo pedido

O pedido original cobria 7 frentes (auditoria de penalidades, novas entradas de rede, curriculum
de dificuldade progressiva, bateria de experimentos com 3+ seeds por configuração principal,
validação estatística completa, documentação e commit). Dado o tempo disponível nesta sessão —
cada rodada real de treinamento genético leva de segundos a minutos de execução de verdade, não é
instantânea — as frentes abaixo foram **efetivamente implementadas e testadas**; as demais ficaram
**fora do escopo desta sessão**, documentadas como trabalho futuro na Seção 5.

| Frente do pedido | Status |
|---|---|
| 1. Preservar estado, branch, referência | **Feito** (Seção 2) |
| 2. Auditar penalidades/eventos | **Feito** (Seção 3) |
| 3. Novas entradas de rede | **Não implementado** (Seção 5.1 — risco de quebra do firmware) |
| 4. Curriculum de dificuldade progressiva | **Não implementado** (Seção 5.2) |
| 5. Experimentos com origem da melhora isolada | **Parcial** — 3 seeds por configuração, mas escala pequena (pop=50/ger=150), não o "orçamento completo" de produção (Seção 4) |
| 6. Pontuação alta = melhora real? | **Feito, resultado honesto: inconclusivo na escala testada** (Seção 4) |
| 7. Documentar, validar, commitar | **Feito** (este relatório + testes + commit) |

## 2. Referência preservada

- Commit de partida: `fc81f76` (branch `feature/radartorres-neat-controlador-v2`), árvore de
  trabalho limpa antes de começar.
- Branch nova: `refatoracao/treinamento-fitness-v3`.
- Comparação antes/depois feita via `git worktree` apontando para `fc81f76` (removido ao final,
  não deixa rastro na árvore principal) — não foram descartados modelos/históricos existentes; o
  banco de dados de treinamento da aplicação (SQLite) não foi tocado por este trabalho.
- Versão da fórmula de fitness identificada explicitamente em
  `ParametrosTreinamento.VersaoFormulaFitness`: `"v1.1-recusa-por-janela-sobrecarga-por-demanda"`
  (era implicitamente "v1" antes desta revisão — nunca tinha um identificador próprio).

## 3. Auditoria de penalidades — achados e correções

### 3.1 Recusa de disparo recontada a cada passo (CONFIRMADO E CORRIGIDO)

**Achado**: em `AmbienteTreinamento.DecidirDisparos`/`DecidirDisparosV2`, toda vez que os vetos já
liberavam o disparo (zona morta, mira alinhada, certeza suficiente, tempo mínimo de mira) e a rede
recusava, o código incrementava `OportunidadesDeDisparo` e `DisparosRecusadosPelaRede`, e descontava
`PenalidadeRecusaDisparo` **a cada passo de simulação (0,2s)**, enquanto a MESMA janela de
hesitação continuasse aberta. Uma recusa de 2 segundos contínuos virava 10 oportunidades e -30 de
fitness, não 1 oportunidade e -3.

**Correção**: `AlvoTreinamento.OportunidadeDeDisparoContabilizada` marca se a janela contínua atual
já foi contada; é reiniciada (`false`) sempre que qualquer veto volta a bloquear o disparo (zona
morta, mira perdida, certeza insuficiente) ou quando o disparo finalmente acontece — a PRÓXIMA vez
que a janela abrir é uma oportunidade nova. Testado em
`AuditoriaFitnessTests.RecusaDeDisparo_JanelaContinua_ContaUmaUnicaOportunidadeNaoUmaPorPasso`.

### 3.2 Penalidade de desequilíbrio penalizava concentração justificada pela geometria (CONFIRMADO E CORRIGIDO)

**Achado**: `AplicarPenalidadeDesequilibrioTorres` calculava o desvio padrão do tempo comprometido
BRUTO entre as 4 torres. Como cada torre só pode assumir UM alvo por vez (`TorreTreinamento.Livre`),
e a arquitetura não permite reatribuir uma torre ocupada (só a seleção escolhe entre torres
LIVRES), não existe caminho pelo qual o controlador "escolha mal" atribuindo trabalho a uma torre
ocupada quando outra livre serviria — a única fonte de desigualdade bruta é a posição dos alvos
(ex.: todos nascendo no mesmo quadrante). A fórmula antiga penalizava isso como se fosse uma falha
de decisão.

**Correção**: `TorreTreinamento.TempoDemandaSegundos` acumula, independente de quem engajou, o
tempo em que havia pelo menos um alvo ativo no PRÓPRIO quadrante de cada torre. A penalidade passa
a usar o desvio padrão do RESÍDUO (`TempoComprometidoSegundos - TempoDemandaSegundos`) entre
torres — uma torre comprometida tanto quanto sua própria demanda local não é mais penalizada, só o
excesso ou déficit em relação a essa demanda. Testado em
`AuditoriaFitnessTests.SobrecargaEvitavel_ConcentracaoDeAlvosNoMesmoQuadrante_NaoGeraPenalidadeAlta`
(residual < 1,0 numa concentração de 100% dos alvos em 1 torre, contra o que a fórmula antiga teria
penalizado pesadamente pela desigualdade bruta entre 4 torres).

**Limitação conhecida**: esta é uma correção por PROXY (demanda = presença de alvo no quadrante),
não uma simulação completa de "outra torre conseguiria atender dentro do prazo" (que exigiria as
estimativas de margem de atendimento do item 3, não implementado — ver Seção 5.1).

### 3.3 Invasão de torre mascarando outros eventos — AUDITADO, comportamento mantido por ser intencional e documentado

**Achado**: a invasão de QUALQUER torre encerra o episódio imediatamente
(`TratarInvasaoDeTorre` → `FinalizarEpisodio`), o que de fato impede que outros alvos ainda em voo
no mesmo episódio cheguem a gerar seus próprios eventos de proximidade/fuga/abate — esse
comportamento já era documentado explicitamente no código-fonte ("descartando qualquer pontuação
futura possível") como decisão de projeto deliberada (uma invasão é uma falha catastrófica de
defesa), não um bug. Não foi alterado.

**O que foi feito**: instrumentação para tornar esse efeito mensurável em vez de invisível —
`EstadoEpisodio.MotivoFim` (`MotivoFimEpisodio.InvasaoDeTorre` vs. `TetoDeSeguranca`) e a divisão do
contador combinado `AlvosEscapados` (mantido por compatibilidade com BD/telas existentes) em
`AlvosEscapadosPelaBase` e `AlvosQueInvadiramTorre` — antes os dois eventos incrementavam o MESMO
contador, sem distinção possível depois do fato. Testado em
`AuditoriaFitnessTests.MotivoFim_InvasaoDeTorre_EDistintoDeFugaPelaBase` e
`MotivoFim_SemNenhumAlvo_TerminaNoTetoDeSeguranca`.

### 3.4 Critérios de segurança/certeza — preservados

Nenhuma mudança em: zona morta, limiar de certeza mínima (`LimiarConfiancaDisparoFracao`), veto
físico de mira (`MiraAlinhada`), tempo mínimo de mira (`TempoMinimoDeMiraSegundos`). As correções
desta seção são só de CONTAGEM/PENALIDADE, não de quais disparos são autorizados.

## 4. Experimentos: a pontuação mais alta representa melhora real?

### 4.1 Metodologia

- **Mesmo ambiente físico/geometria** nos dois lados: layout real de torres (`appsettings.json`,
  4 torres a 1,75m da base), parâmetros de episódio idênticos.
- **3 seeds de treinamento** (1, 2, 3) por lado (baseline = `fc81f76`, novo = este branch),
  população 50, 150 gerações, cenários táticos fixos ligados — escala REDUZIDA (não o orçamento de
  produção do projeto, que usa população maior e mais gerações) para caber no tempo da sessão.
- **Mesma regra de pontuação na comparação final**: os 6 genomas campeões (3 baseline + 3 novo)
  foram avaliados sob a MESMA fórmula (a nova, deste branch), via `sobrevivencia --episodios=20`,
  sem corte de tempo (só invasão ou teto de segurança) — isola efeito de COMPORTAMENTO do efeito
  de FÓRMULA, como pedido.
- Seeds de validação (90000-90049) nunca vistas pelo algoritmo genético, para o número de
  "fitness" reportado durante o treino não ser otimista por viés de seleção.

### 4.2 Resultados — treino (fórmula de cada lado, não comparável diretamente entre si)

| Seed | Baseline (fórmula antiga) VAL fitness médio | Novo (fórmula corrigida) VAL fitness médio | Tempo treino |
|---|---|---|---|
| 1 | 605,43 | 567,88 | 43s / 83s |
| 2 | 567,82 | 586,39 | 46s / 59s |
| 3 | 567,82 | 567,88 | 76s / 58s |

Fitness bruto de geração 0 (população inicial, não treinada): a fórmula antiga produz médias
NEGATIVAS (-135 a -266) nas 3 seeds; a fórmula corrigida produz médias levemente positivas (28 a
56) nas MESMAS seeds — efeito direto e esperado da correção 3.1/3.2 (menos penalização por
recontagem/desigualdade bruta já na população inicial, antes de qualquer aprendizado).

### 4.3 Resultados — os 6 genomas campeões, avaliados sob a MESMA regra nova (`sobrevivencia`, 20 episódios cada, sem corte de tempo)

| Genoma | Fitness médio | Abatidos (média) | Nascidos (média) | Taxa de recusa | Duração média até invasão/teto |
|---|---|---|---|---|---|
| baseline-s1 | 502,23 | 56,50 | 59,40 | 0,0% | 91,8s |
| baseline-s2 | 534,59 | 60,00 | 62,90 | 0,0% | 96,0s |
| baseline-s3 | 534,59 | 60,00 | 62,90 | 0,0% | 96,0s |
| novo-s1 | 534,59 | 60,00 | 62,90 | 0,0% | 96,0s |
| novo-s2 | **598,30** | **66,90** | 69,80 | 0,0% | 105,0s |
| novo-s3 | 534,59 | 60,00 | 62,90 | 0,0% | 96,0s |

### 4.4 Conclusão explícita (item 6 do pedido)

1. **Aumento da pontuação média**: SIM, mas só porque a fórmula mudou (ver 4.2, geração 0) — não
   deve ser lido como "o controlador aprendeu mais" sem o controle da Seção 4.3.
2. **Melhora de comportamento sob condições comparáveis** (Seção 4.3, mesma regra para todos): **NÃO
   CONCLUSIVO nesta escala**. Os 6 genomas (baseline e novo) convergem para comportamento quase
   idêntico — taxa de recusa 0,0% em todos, abate ~56-67 alvos por episódio, nenhuma diferença
   sistemática entre genomas treinados com a fórmula antiga vs. a nova. `novo-s2` teve o melhor
   resultado isolado, mas é 1 de 6 pontos e não forma um padrão (as outras 2 seeds do "novo" ficaram
   empatadas com o baseline).
3. **Generalização para cenários não usados no treino**: não testada separadamente nesta sessão —
   os cenários táticos fixos (`CatalogoCenarios`) já entravam no treino de ambos os lados; não foi
   isolado um conjunto de validação verdadeiramente "nunca visto" além das seeds aleatórias
   90000-90049 (que cobrem o sorteio padrão, não os cenários táticos).

**Limitação explícita**: pop=50/ger=150 é uma escala pequena. É plausível (não confirmado) que a
correção 3.1 (recusa recontada) tenha mais efeito em configurações onde o controlador hesita mais
— aqui, ambos os lados já convergiram para taxa de recusa 0,0% ao fim do treino, o que por
construção neutraliza boa parte do efeito da correção 3.1 na AVALIAÇÃO FINAL (o viés só aparece
enquanto a rede ainda hesita, isto é, no início do treino — ver 4.2). Um experimento na escala de
produção (população/gerações maiores, mais seeds) seria necessário para uma conclusão mais forte —
fica como trabalho futuro.

## 5. Não implementado nesta sessão — e por quê

### 5.1 Novas entradas de rede (velocidade de aproximação, tempo até invasão, margem de atendimento, etc.)

**Risco identificado que motivou adiar**: `ControladorHibrido.QuantidadeEntradas = 7` é CONTRATO
FÍSICO com o firmware real — `Arduino/RadarTorres_Firmware/RadarTorres_Firmware.ino` tem embutida
uma rede neural própria (7 entradas → 8 ocultos → 2 saídas) e o protocolo serial
`REDE;ENTRADAS=<d>;<mesmo_quad>;<quad_adj>;<angulo>;<vel>;<tempo_deteccao>;<fator_camada>` tem
exatamente 7 campos fixos. Mudar a quantidade de entradas do lado do software exige também mudar o
firmware físico (regravar o Arduino) e não pode ser feito/testado sem acesso à montagem física —
fora do escopo seguro de uma sessão de software. Fica registrado como trabalho futuro que precisa
de coordenação explícita entre as duas partes do projeto (PC + Arduino) antes de ser tentado.

### 5.2 Curriculum de dificuldade progressiva (etapas, critérios de avanço, proteção contra avanço prematuro)

Não implementado por tempo — é um mecanismo novo de controle do laço de treinamento
(`TreinadorNeat`/`ViewModels.TreinamentoControladorViewModel`), não um ajuste pontual, e merece
sua própria sessão de implementação + validação (os critérios de avanço "mensuráveis, com proteção
contra avanço prematuro" pedidos no item 4 do pedido original precisam de experimentação própria
para calibrar limiares, o que não cabia no tempo restante desta sessão).

## 6. Testes executados

- `dotnet build RadarTorres.sln` — 0 erros.
- `dotnet test tests/RadarTorres.Tests` — 281 aprovados, 5 falhas em `PersistenciaSqliteTests`
  **pré-existentes** (confirmado rodando a mesma suíte no commit `fc81f76`, antes de qualquer
  mudança desta sessão — falhas de um subsistema não relacionado, SQLite de ocorrências de
  detecção, não o ambiente de treinamento).
- 4 testes novos em `AuditoriaFitnessTests.cs`, cobrindo as correções 3.1-3.3, todos passando.

## 7. O que NÃO foi versionado

Os arquivos `.net` (genomas) e as linhas geradas em `tools/RadarTorres.Experimentos/Resultados/resultados.json`
pelos experimentos desta sessão foram descartados (não commitados) — são artefatos de pilot/scratch
desta auditoria, não o mecanismo de persistência oficial do projeto (banco SQLite da aplicação,
inalterado). Nenhuma credencial, banco local ou artefato volumoso foi versionado.
