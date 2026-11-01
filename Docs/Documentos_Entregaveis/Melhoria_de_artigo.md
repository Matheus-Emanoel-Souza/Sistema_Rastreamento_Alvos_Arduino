Funcionalidade: checklist de revisão acadêmica do artigo do TCC (`Artigo.tex`, fonte canônica) e registro de decisões pendentes levantadas nas rodadas de revisão.

# Melhorias do Artigo — RadarTorres

Checklist de revisão acadêmica do `Artigo.tex`. Status real de aplicação no documento, não intenção.

## Restrição de tamanho

- **Limite: 20 páginas.** O artigo (`Artigo.tex`) não deve exceder 20 páginas no PDF compilado.
  Status em 01/11/2026: ainda acima do limite (a revisão de 30/10 já reduziu de 30 para 27; segue
  pendente um novo corte estrutural para chegar a 20).

## Objetivo geral e objetivos específicos

Documentados aqui (não como seção formal `\section{Objetivo}` no `Artigo.tex`, por decisão do
autor) para orientar a revisão e a redação do artigo. Refletem a lógica de **engajamento
cooperativo multi-torre** (01/11/2026): mais de uma torre pode engajar o mesmo alvo
simultaneamente — a decisão não é "qual é a torre ideal", e sim "qual combinação de torres está
apta a responder, com que grau de certeza, e quando autorizar o disparo".

**Objetivo geral:**

Desenvolver um protótipo (RadarTorres) capaz de detectar um alvo em movimento, estimar sua
posição por sensoriamento ultrassônico e visão computacional, e autorizar automaticamente o
engajamento cooperativo das torres aptas a responder a esse alvo — elevando a certeza de
identificação no menor tempo possível até o grau de confiança mínimo necessário —, de forma
auditável e sem depender de sensores ou atuadores de uso industrial.

**Objetivos específicos:**

1. Combinar sensores ultrassônicos com visão computacional (quatro câmeras) para detectar e
   classificar o alvo por cor, forma e tamanho, estimando sua posição em coordenadas cartesianas e
   exibindo-a em tempo real num radar circular.
2. Determinar, para cada alvo, o conjunto de torres com câmera e linha de tiro válidas — torre do
   próprio quadrante sempre elegível, torres de quadrante adjacente dentro do alcance de câmera, e
   reforço adicional para alvos rápidos —, permitindo que mais de uma torre engaje o mesmo alvo
   simultaneamente.
3. Implementar um controlador híbrido (heurística determinística + rede neural evoluída por
   algoritmo genético próprio) que refine, dentro do conjunto de torres elegíveis, a priorização
   de engajamento e o instante de acionamento — nunca autorizando disparo fora do que a camada
   heurística de segurança já validou.
4. Condicionar todo acionamento a um limiar mínimo de certeza de identificação do alvo, válido
   para qualquer torre do conjunto de engajamento — o disparo só ocorre depois de atingida a
   confiança necessária, nunca por decisão isolada de uma única torre "ideal".
5. Treinar esse controlador inteiramente em simulação, via neuroevolução, antes da operação real,
   incorporando os parâmetros aprendidos de forma fixa à aplicação (sem ajuste em tempo real).
6. Registrar em auditoria toda ação executada e documentar as limitações da validação atual
   (realizada inteiramente por simulação de software; estrutura física ainda não montada nem
   testada).

## Resolvido — titulação do orientador (rodapé da capa)

Trecho grifado em amarelo no rodapé do orientador que eu tinha deixado de processar na revisão de
30/10 (estava numa NOTA DE RODAPÉ do docx, não no corpo do texto -- meu script de extração só
percorria o corpo). Pesquisa web (agregador de Lattes, fonte secundária) sugeriu bacharelado em
Ciência da Computação pela UFES (1999) e especialização em Gestão de Projetos pela UCL, sem
mestrado. **Autor confirmou diretamente** (30/10/2026) que a informação está correta. Rodapé em
`Artigo.tex` atualizado para "Professor especialista, bacharel em Ciência da Computação pela
Universidade Federal do Espírito Santo (UFES)" -- sem mestrado, removido o placeholder "Mestre em
xxx pela xxx" e os destaques amarelos (confirmado, não mais pendente).

## Desenvolvimento físico do projeto

- [x] **superada** — Redação da descrição do desenvolvimento físico (estrutura em formato de torre, impressão 3D). A seção "Projeto elétrico e estrutural" em que esse texto seria inserido foi removida do `Artigo.tex` na revisão de 30/10 (corte de páginas); o texto abaixo não foi reinserido para não reabrir essa seção e inflar a contagem de páginas de volta. Mantido aqui só como registro histórico.

  > A estrutura das torres foi projetada em formato de torre e fabricada por impressão 3D. As torres
  > armadas possuem suportes impressos para os motores e para as câmeras, que funcionam como os
  > "canhões" de acionamento demonstrativo; a torre central possui um suporte próprio para os
  > sensores ultrassônicos. Ambos os tipos de torre -- armada e central -- possuem suporte para um
  > rolamento axial na base, o que permite a rotação da estrutura sobre o próprio eixo.

## Figuras e diagramas

- [x] **concluída** — Gerar e inserir diagrama de pacotes atualizado (inclui pacote `NeuralEvolution` e as dependências novas de `Services`/`ViewModels`), a partir de `Docs/Documentos_Entregaveis/Diagrama_de_Pacotes.md`.
- [x] **concluída** — Gerar e inserir novo DER (SQLite, 11 tabelas, chaves estrangeiras reais), a partir de `Docs/Documentos_Entregaveis/Banco_de_Dados/figuras/der_sqlite.mmd` — figura `DER_SQLite_Completo.png`.
- [x] **concluída** — Gerar e inserir fluxograma do fluxo operacional (início → detecção → decisão → ação), a partir de `Docs/Documentos_Entregaveis/Fluxo_Operacional/fluxo_operacional.mmd` — figura `Fluxo_Operacional.png`.
- [ ] **pendente** — Reescrever o texto corrido da seção "Modelo de dados" (ainda descreve CSV/6 tabelas sem FK real; já existe a figura nova, falta o texto acompanhar).
- [ ] **pendente** — Decidir o destino de `DER_Atual.png`/`DER_Proposto.png` (modelo CSV antigo) — hoje seguem no artigo como nota histórica; avaliar se vale manter ou remover.
- [ ] **pendente** — Confirmar se os 3 ciclos de dependência do diagrama de pacotes antigo (`NavigationService`, `DataSeeder`) ainda existem no código atual; a regeração simplificada não reproduz o destaque em vermelho que a versão anterior tinha.
- [ ] **pendente** — Atualizar `Docs/Documentos_Entregaveis/Banco_de_Dados/conteudo_modelo_banco_dados.tex` com as 4 tabelas de treinamento neuroevolutivo (também está defasado, embora menos que o artigo).

## Referências bibliográficas

### Candidatas encontradas (gratuitas, arXiv, PDF direto sem paywall)

TEWA — substituem os placeholders `naseem_tewa`/`roux_tewa`:
- [arXiv:0907.0067](https://arxiv.org/abs/0907.0067) — "A Novel Two-Staged Decision Support based Threat Evaluation and Weapon Assignment Algorithm, Asset-based Dynamic Weapon Scheduling using Artificial Intelligence Techniques".
- [arXiv:0906.5038](https://arxiv.org/abs/0906.5038) — "A Novel Two-Stage Dynamic Decision Support based Optimal Threat Evaluation and Defensive Resource Scheduling Algorithm for Multi Air-borne threats" (cobre o caso multiameaça).

Action masking (fundamenta a heurística filtrando candidatas antes da rede decidir):
- [arXiv:2006.14171](https://arxiv.org/abs/2006.14171) — Huang & Ontañón, "A Closer Look at Invalid Action Masking in Policy Gradient Algorithms".
- [arXiv:2504.02662](https://arxiv.org/pdf/2504.02662) — "Integrating Human Knowledge Through Action Masking in Reinforcement Learning for Operations Research".

Safe RL / shielding (fundamenta o pós-filtro de disparo: zona morta, certeza mínima, tempo de mira):
- [arXiv:1708.08611](https://arxiv.org/abs/1708.08611) — Alshiekh et al., "Safe Reinforcement Learning via Shielding".
- [arXiv:2201.12819](https://arxiv.org/pdf/2201.12819) — "A Safety-Critical Decision Making and Control Framework Combining Machine Learning and Rule-based Algorithms" (espelho quase direto da arquitetura heurística+rede do projeto).

Neuroevolução (survey gratuito, alternativa ao Stanley et al. 2019 que é pago na Nature Machine Intelligence):
- [arXiv:2006.05415](https://arxiv.org/pdf/2006.05415) — "Neuroevolution in Deep Neural Networks: Current Trends and Future Challenges".

- [ ] **pendente** — Preencher `naseem_tewa` e `roux_tewa` em `referencias.bib` (hoje `PREENCHER` literal; renderiza errado no PDF já compilado) ou remover a citação e reformular o parágrafo de TEWA (`Artigo.tex:149-155`) sem apoio bibliográfico até ter referência real.
- [ ] **pendente** — Revisar a citação a `such2017deepneuroevolution` em `Artigo.tex:165-171`: o paper trata de redes de até 4 milhões de parâmetros (Atari/locomoção), escala muito diferente da rede de 74 parâmetros do projeto; a referência sustenta "GA é competitivo com gradiente", não sustenta a conclusão de "não vale a pena aumentar camadas/neurônios aqui". Reformular a frase ou trocar a referência.
- [ ] **pendente** — Conferir na fonte original (Majoni et al., 2025) se a divisão de papéis "Arduino Mega trata ultrassom + Raspberry Pi roda YOLOv4-tiny, comunicação serial entre os dois" (`Artigo.tex:139-142`) está de fato descrita assim no paper, antes de manter a analogia com o RadarTorres.
- [ ] **pendente** — Adicionar referências sobre: action masking / restrição de ações válidas em RL; safe RL via shielding (camada de segurança que veta ações de uma política aprendida); neuroevolução aplicada especificamente a weapon-target assignment/TEWA; survey de neuroevolução (ex.: Stanley et al.) — fundamentam a arquitetura híbrida heurística+rede já implementada e hoje descrita sem apoio bibliográfico específico.

## Estrutura do artigo

- [ ] **pendente** — Criar seção de Conclusão/Considerações finais (hoje o texto vai direto de "Desenvolvimento" para a bibliografia; faltam síntese dos resultados, limitações consolidadas e trabalhos futuros unificados).
- [ ] **pendente** — Reescrever título, resumo, abstract e palavras-chave para refletir o escopo real do artigo (hoje nomeiam só "Modelo de dados", mas o corpo cobre requisitos, projeto elétrico, visão computacional, diagrama de pacotes e controlador neuroevolutivo) — ou dividir o conteúdo em artigos separados por entregável.

## Controlador neuroevolutivo — evidência e rigor

- [ ] **pendente** — Incluir gráfico/tabela de fitness por geração do algoritmo genético, mostrando a estagnação antes da correção do bug de certeza estática (commit `a02d46a`) e a recuperação depois — hoje o impacto é só descrito em texto, sem número.
- [ ] **pendente** — Reportar parâmetros reais do treinamento executado (nº de gerações, tamanho de população, fitness final do genoma embutido, taxa de abate).
- [ ] **pendente** — Suavizar a afirmação de garantia de segurança do controlador híbrido (`Artigo.tex:188-192`, "limita o pior caso de uma generalização imperfeita") para forma condicional — a arquitetura foi concebida para isso, mas nunca validada em hardware real.
- [ ] **pendente** — Adicionar diagrama de fluxo de dados mostrando a mesma classe `ControladorHibrido` sendo usada em treino e produção, reforçando visualmente o ponto já descrito em texto.

## Caso de estudo — bug da certeza estática

- [ ] **pendente** — Promover o episódio do bug de certeza estática (`Artigo.tex:238-244`) de parágrafo metodológico para um mini-caso de estudo com dado antes/depois (ver item de gráfico de fitness acima) — é a contribuição mais original do artigo e está subexplorada.

> O registro do que já foi aplicado na revisão de 30/10/2026 (remoção de seção obsoleta, zona
> morta, tempo mínimo de mira, correção de número de genoma, etc.) está em [[LOG_SOLICITACOES]],
> entrada "2026-10-30 — Revisão do artigo a partir do docx grifado". Esta nota (Apoio) guarda só o
> que ainda está pendente ou é proposta de trabalho futuro.

## Resolvido (01/11) — lógica cooperativa multi-torre refletida em todo o texto

Na primeira passada (em `artigo_revisado.tex`), eu tinha avaliado a lógica de seleção de torre só
por `ControladorHibrido.SelecionarTorre`/`DecidirDisparo` e concluído que o sistema escolhe
EXATAMENTE uma torre por alvo, sem nenhuma cooperação entre torres. **Isso estava incompleto**:
`Artigo.tex` já documenta, na seção de regras heurísticas de `TowerSelectionService`, a regra
"Ajuda de torre adjacente contra alvo rápido (sempre ativa)", e o código (`ComputarTorresEngajando`)
já permite, independentemente da velocidade, que a torre do próprio quadrante E qualquer torre de
quadrante adjacente dentro do alcance de câmera engajem o mesmo alvo ao mesmo tempo -- a regra de
velocidade só acrescenta uma torre extra, fora do alcance normal, por cima dessa base. Ou seja,
cooperação entre torres **já existe** hoje, de forma mais ampla do que a primeira passada havia
percebido.

Na revisão de 01/11/2026, o resumo, o abstract, a introdução, o parágrafo de TEWA e toda a seção do
controlador neuroevolutivo (cascata de camadas, regras, saídas da rede, hierarquia heurística↔rede)
foram reescritos para descrever essa cooperação explicitamente, em vez de "a torre mais adequada"
no singular -- sem alterar nenhum comportamento de código, só corrigindo a descrição para bater com
o que `TowerSelectionService`/`FireControlService` já fazem.

**Proposta do autor (registrada, ainda NÃO implementada)**: generalizar esse comportamento -- ao
identificar QUALQUER alvo (não só os rápidos), todas as câmeras e todas as torres voltam a atenção
para ele simultaneamente, objetivo de reconhecer mais rápido (mais câmeras observando ao mesmo
tempo melhora a confiança de identificação) e acelerar o abate.

- [ ] **pendente** — Decidir se a generalização estende a regra 5 existente (remover a condição de
  velocidade) ou se é uma lógica nova e mais ampla (toda câmera olha pra todo alvo identificado,
  não só torres adjacentes a até 2m) -- escopo diferente de esforço de implementação.
- [ ] **pendente** — Projetar o impacto em `Torre.Livre`/`Target.EngagingTowers` e no cálculo de
  `outrosAlvosAtivos`/`quadrantesComOutrosAlvos` usado na filtragem por quadrante, hoje desenhado
  para 1 torre "principal" por alvo mesmo com reforço.
- [ ] **pendente** — Re-treinar a rede neural com a nova lógica, se ela mudar o que conta como
  "candidata" nas 7 entradas atuais.
- [ ] **pendente** — Atualizar o artigo (texto completo da seção, não só o destaque já inserido) e
  `Docs/Tecnica/ALGORITMO_SELECAO_TORRE.md` quando a implementação acontecer.

**Decisões pendentes após revisão no dia 30/10/2026 às 13:51.**

- **Comentário #3 do docx de referência ("Revisar trecho")** — ancorado logo após "... até quatro
  milhões de parâmetros, contra os 82 parâmetros da rede do RadarTorres)." (parágrafo sobre
  `such2017deepneuroevolution`, Seção 2.2/Revisão da literatura). Já apliquei uma correção
  razoável (a citação sustenta "GA é competitivo", não sustenta "não vale aumentar camadas aqui";
  reformulei para apontar à Seção de hiperparâmetros como a justificativa real) -- mas o comentário
  original não dizia exatamente o quê revisar, então vale o autor confirmar se a correção aplicada
  é a que ele tinha em mente.
- **Comentário #5 do docx de referência (fusão ultrassom × câmera)** — **Resolvido (01/11):** opção
  (b) escolhida. `Artigo.tex` (seção "Módulo de visão") registra a ideia -- distância combinada,
  com peso por sensor variando conforme a distância do alvo (ultrassom prioritário de perto, câmera
  prioritária de longe) -- como trabalho futuro explícito, não como funcionalidade existente.
  Verificado no código: essa fusão não existe hoje (o campo `DIST` do ultrassom só alimenta a
  posição no radar; não há cruzamento com o `Z` da câmera em
  `SerialProtocolParser`/`SimulationService`/`AlvoVisualService`).

---

- pendente
