# Marcos e Entregas — RadarTorres

Esta branch (`marcos-e-entregas`) preserva **snapshots completos e imutáveis** do projeto a
cada entrega formal do TCC, para servir de histórico de referência independente do
desenvolvimento contínuo em `main`. Cada marco é **um único commit** cujo conteúdo é idêntico,
byte a byte, ao estado da `main` no commit indicado — nenhum marco já criado é alterado ou
reescrito quando um novo é adicionado (ver seção "Como funciona" abaixo).

Esta branch não recebe desenvolvimento normal. Trabalho de código continua inteiramente em
`main` (e nas branches de feature que a alimentam); esta branch só acumula marcos.

## Marcos registrados

| Nº | Reunião de orientação | Entrega arquivada | Commit na `main` (conteúdo) | Commit do marco nesta branch |
|---|---|---|---|---|
| 1 | 19/08/2026 | `Docs/Documentos_Entregaveis/Controle_de_Sessoes/entrega_01.pdf` (assinada 27–28/08/2026; criptografada via git-crypt desde 2026-11-05) | [`58e65ac`](https://github.com/Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino/commit/58e65ac30c021268aa0796feb802e89022925c50) — 2026-09-24 — "docs: adiciona controle de sessoes e diagrama de classes .drawio" | `9b439a3` |
| 2 | 26/09/2026 | `Docs/Documentos_Entregaveis/Controle_de_Sessoes/entrega_02.pdf` | [`84dae07`](https://github.com/Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino/commit/84dae07ae98d6e3cdf42070c7b13818ec0eb169c) — 2026-09-28 — "docs: atualiza artigo, banco de dados, diagrama eletrico e controle de sessoes" | `8be983a` |
| 3 | 17/10/2026 | `Docs/Documentos_Entregaveis/Controle_de_Sessoes/entrega_03_assinada.pdf` (assinada por todos em 17/10/2026) | [`9922599`](https://github.com/Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino/commit/99225994bd7d2638ea137e9be723d50c2229174a) — 2026-10-21 — merge do PR #1 na `main` | `54a005e` |

> Os hashes acima já refletem a reescrita de histórico de 2026-11-05 (ver seção **"Exceção:
> reescrita de histórico por segurança"** ao final deste documento). Os hashes originais,
> anteriores a essa data, estão documentados naquela seção — não removidos daqui por acidente,
> apenas superados deliberadamente.

**Nota sobre as datas:** a coluna "Reunião de orientação" é a data real do encontro com o
orientador (ver `Docs/Documentos_Entregaveis/Controle_de_Sessoes/anexo_acompanhamento_orientacao.tex`).
A coluna "Commit na `main`" é a data em que o comprovante assinado daquela entrega foi
efetivamente adicionado ao repositório — por decisão explícita do autor, cada marco usa o
commit onde o próprio PDF assinado já está presente (em vez do commit mais próximo da data da
reunião), para que o marco contenha sua prova de entrega, não só o código daquele período.

## Decisões de negócio e justificativas por período

Resumo das decisões mais relevantes registradas em `Docs/Projeto/LOG_SOLICITACOES.md` e
`Docs/Projeto/CONTEXTO_PROJETO.md` até cada marco — consulte esses dois documentos (na `main`,
no commit correspondente) para o detalhamento completo; não duplicado aqui de propósito.

### Até o Marco 1 (19/08/2026)
Fundação do projeto: persistência inicial em CSV (decisão explícita de adiar banco relacional),
coordenada Z de objetos detectados como campo nullable (sensor 2D), extensão do `SystemMode`
técnico existente em vez de um conceito paralelo, autenticação/permissões por perfil
(Administrador/Operador/Visualizador), instalador Windows self-contained (dispensa checagem de
.NET na máquina do usuário), zonas mortas e tela de Objetos Detectados implementadas,
levantamento de RF/RNF e diagramas a partir de engenharia reversa do código.

### Entre Marco 1 e Marco 2 (19/08 → 26/09/2026)
Revisão completa dos Requisitos Funcionais/Não Funcionais (RF06/RF07/RF08 reformulados —
acionamento manual removido da especificação, modo de simulação explicitamente marcado como
ferramenta de desenvolvimento, não requisito do produto), Decisões Arquiteturais e Limitações
Conhecidas separadas em documentos próprios, Diagrama de Casos de Uso (28 casos, confirmados
contra `PermissionService`), documentação acadêmica UML e Requisitos do Sistema para o TCC, site
oficial do projeto via GitHub Pages (React/TypeScript/Tailwind, com simulação do radar
reproduzindo a lógica real de seleção de torre).

### Entre Marco 2 e Marco 3 (26/09 → 17/10/2026)
Planejamento do módulo de visão computacional (ONNX Runtime in-process, decisão explícita contra
um serviço Python separado) e implementação da tela "Perfis de Alvo" (cor/formato/tamanho do
alvo a travar, só cores primárias RGB, um perfil ativo por vez); preview ao vivo da câmera na
foto de referência; detecção por câmera (YOLO) integrada ao Monitoramento com alarme sonoro;
**cooperação entre torres por adjacência de quadrante** (`TowerSelectionService` em camadas:
casa → adjacente → cobertura → sobrecarregada, com exclusividade e mira física via comando
`AIM`) — decisão explícita por heurística determinística em vez de rede neural, justificada por
espaço de decisão pequeno/conhecido, exigência de auditoria/explicabilidade e hardware do
Arduino Mega sem capacidade confortável de inferência; correções visuais (ícone do app, layout
de login 70/30, ícones dos itens "Desempenho" e "Objetos detectados" na Sidebar).

## Como funciona (para quem for revisar o histórico)

Cada marco é criado com `git merge --squash` (ou, quando necessário por conflitos de
"add/add" entre marcos com históricos não relacionados, forçando a árvore via
`git read-tree --reset -u <commit>`) a partir do commit da `main` indicado na tabela, seguido de
um `git commit` comum. Isso produz:

- **Um commit por entrega**, nunca vários — o diff de cada marco contra o anterior é o
  equivalente a "tudo que mudou na `main` entre uma entrega e a seguinte", mas a árvore de
  arquivos daquele commit é sempre o **estado completo** do projeto naquele momento (não um
  diff incremental que precise ser somado aos anteriores para fazer sentido).
- **Nenhuma reescrita de marco anterior** — `merge --squash` e `read-tree` só criam um commit
  novo; os hashes dos marcos já registrados nunca mudam depois de criados.
  **Excepcionalmente, essa regra foi quebrada uma única vez em 2026-11-05**, para remover dados
  pessoais expostos publicamente — ver a seção "Exceção: reescrita de histórico por segurança"
  ao final deste documento, que documenta o motivo e o mapeamento completo de hashes
  antigos → novos.

## Procedimento para registrar uma nova entrega

1. **Confirme a data e o commit da `main`** que representa a entrega (normalmente o commit onde
   o comprovante assinado daquela entrega foi adicionado ao repositório — mesma regra usada nos
   marcos 1–3).
2. **Verifique que a entrega ainda não foi registrada**: `git log marcos-e-entregas --oneline`
   e confira a tabela acima — evita duplicar um marco já existente para a mesma data/commit.
3. Em uma worktree separada (recomendado, evita qualquer risco à sua pasta de trabalho atual):
   ```
   git worktree add ../radartorres-marcos-wt marcos-e-entregas
   cd ../radartorres-marcos-wt
   git read-tree --reset -u <commit-da-main-da-nova-entrega>
   git diff --quiet <commit-da-main-da-nova-entrega> -- . && echo "OK: árvore idêntica ao commit-fonte"
   git commit -m "marco(entrega-N): estado completo do projeto na Na entrega (DD/MM/AAAA)"
   ```
4. **Atualize esta tabela** (`Docs/Projeto/MARCOS_E_ENTREGAS.md`, nesta mesma branch) com a nova
   linha e, se houver, as decisões de negócio do período desde o marco anterior.
5. `git push origin marcos-e-entregas`.
6. Remova a worktree temporária: `git worktree remove ../radartorres-marcos-wt`.

Nunca use `rebase` nem `reset --hard` em commits de marco já enviados ao remoto — isso
reescreveria hashes já publicados, violando o propósito de preservação desta branch.

## Exceção: reescrita de histórico por segurança (2026-11-05)

Em 2026-11-05, o autor identificou que `Docs/Documentos_Entregaveis/Controle_de_Sessoes/`
continha documentos com nomes completos de aluno(s) e orientador, versionados em texto puro
num repositório **público** no GitHub, alcançável em `main`, nesta branch e em `refs/pull/1/head`
desde 2026-09-28. A correção adotada foi: renomear os 7 arquivos da pasta para nomes genéricos
e cifrá-los com `git-crypt` (AES-256, chave simétrica mantida fora do repositório), reescrevendo
com `git filter-repo` todos os commits — em `main` e nesta branch — que já continham esses
arquivos em texto puro.

Isso contraria a regra "os hashes dos marcos já registrados nunca mudam", declarada acima. Foi
uma decisão consciente do autor, feita uma única vez, por não haver outra forma de remover dado
pessoal já commitado sem reescrever o histórico que o contém. Nenhum outro conteúdo desta branch
foi alterado — a reescrita foi restrita a `--refs main marcos-e-entregas`, preservando intactas
as tags `v1.0.0`, `v1.1.0`, `estrutura`, `organizacao-docs` e `organizacao-docs-entregaveis-v2`
(hashes idênticos antes/depois, confirmado byte a byte).

**Mapeamento de hashes (antigo → novo):**

| Commit (resumo) | Hash antigo | Hash novo |
|---|---|---|
| docs: adiciona controle de sessoes e diagrama de classes .drawio (`main`) | `1bcbfd2e9ac5352486272918d6751bb26e82f2c4` | `58e65ac30c021268aa0796feb802e89022925c50` |
| docs: atualiza artigo, banco de dados, diagrama eletrico e controle de sessoes (`main`) | `306aec2ec652ccd53ff940116f908786ba3761a0` | `84dae07ae98d6e3cdf42070c7b13818ec0eb169c` |
| chore(estrutura): reorganiza arquitetura de pastas (`main`) | `e6b8118e92401c4682ceadf7cd081efc90c7db0f` | `4abdd9c02c15db4d99fe343ac62e6f64f1d7bce4` |
| docs(projeto): adiciona entrega 03 assinada (`main`) | `b83883a391f645a75c8b95c4cef7836c5b5855b6` | `60a821e11bbf981fb1625541673a224f155f6816` |
| documentacao: unifica imagens do projeto em assets/ (`main`) | `82ed1522869241b1e4581964a25add55930a2000` | `23bee6007a652ac4d7bb9115682a1d36b1466430` |
| merge do PR #1 na `main` | `2bf1886ff2c96ddab0a489cbdb19bc5623c44bd2` | `99225994bd7d2638ea137e9be723d50c2229174a` |
| HEAD de `main` no momento da reescrita | `2036a6122f40401b7d76365b3a9721c66cd02487` | `f2d9798d32cbdf2f06f846414921f01fc7938f52` |
| marco(entrega-1) | `69fa4fdd65efe312f1964fe8d93d76e7cb27b048` | `9b439a3f229772dd8054ba48a1eceddb9bd860c5` |
| marco(entrega-2) | `6515670ffd52896134f8e000935005b27af4d277` | `8be983a17f67ec8ef2905c1300f76950cd3ebb19` |
| marco(entrega-3) | `43989e6448e919cd9e468b868381b24b877cfc28` | `54a005e8ab145f3315d5ef51a9dc019d85882c9a` |
| HEAD de `marcos-e-entregas` no momento da reescrita | `9206f4d6e3e0b9c35e63930d21e58c9af2ff0d87` | `b7ff4e65ab8d05013beaf6783223e0ebfcdc44d7` |

**Links/hashes antigos citados em issues, PRs ou conversas anteriores a 2026-11-05 ficam
quebrados/apontando para commits que deixaram de existir no histórico publicado** — isso é
esperado e é exatamente o efeito pretendido (os blobs em texto puro não são mais alcançáveis).
Um backup completo e integral do repositório **anterior** a esta reescrita (todas as branches,
tags e histórico original, incluindo os nomes e o conteúdo em texto puro) foi preservado fora
do GitHub, fora deste repositório, para referência interna/arquivística — não publicado.
