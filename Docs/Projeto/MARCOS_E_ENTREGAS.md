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
| 1 | 19/08/2026 | `Docs/Documentos_Entregaveis/Controle_de_Sessoes/1ª entrega.pdf` (assinada 27–28/08/2026) | [`1bcbfd2`](https://github.com/Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino/commit/1bcbfd2e9ac5352486272918d6751bb26e82f2c4) — 2026-09-24 — "docs: adiciona controle de sessoes e diagrama de classes .drawio" | `69fa4fd` |
| 2 | 26/09/2026 | `Docs/Documentos_Entregaveis/Controle_de_Sessoes/2ª entrega.pdf` | [`306aec2`](https://github.com/Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino/commit/306aec2ec652ccd53ff940116f908786ba3761a0) — 2026-09-28 — "docs: atualiza artigo, banco de dados, diagrama eletrico e controle de sessoes" | `6515670` |
| 3 | 17/10/2026 | `Docs/Documentos_Entregaveis/Controle_de_Sessoes/Entrega_03_Matheus_Gabriel_assinado.pdf` (assinada por todos em 17/10/2026) | [`2bf1886`](https://github.com/Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino/commit/2bf1886ff2c96ddab0a489cbdb19bc5623c44bd2) — 2026-10-21 — merge do PR #1 na `main` | `43989e6` |

**Nota sobre as datas:** a coluna "Reunião de orientação" é a data real do encontro com o
orientador (ver `Docs/Documentos_Entregaveis/Controle_de_Sessoes/ANEXO_B_Acompanhamento_Orientacao_TCC.tex`).
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
