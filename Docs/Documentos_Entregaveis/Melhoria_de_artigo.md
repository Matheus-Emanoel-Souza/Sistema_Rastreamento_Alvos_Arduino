# Melhorias do Artigo — RadarTorres

Checklist de revisão acadêmica do `Artigo.tex`. Status real de aplicação no documento, não intenção.

## Restrição de tamanho

- **Limite: 20 páginas.** O artigo (`Artigo.tex`) não deve exceder 20 páginas no PDF compilado.
  Hoje está acima disso (ver nota de compilação mais recente); reduzir é parte do trabalho de
  revisão, não só adicionar conteúdo.

## Objetivo geral e objetivos específicos

Documentados aqui (não como seção formal `\section{Objetivo}` no `Artigo.tex`, por decisão do
autor) para orientar a revisão e a redação do artigo. Refletem a lógica de **engajamento
cooperativo multi-torre** (2026-11-01): mais de uma torre pode acompanhar/engajar o mesmo alvo
simultaneamente — a decisão não é mais "qual é a torre ideal", e sim "qual combinação de torres
está apta a responder, com que grau de certeza, e quando autorizar o disparo".

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

## Desenvolvimento físico do projeto



  Texto redigido, pendente de inserção no `Artigo.tex` (seção "Projeto elétrico e estrutural"):

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

---

- pendente
