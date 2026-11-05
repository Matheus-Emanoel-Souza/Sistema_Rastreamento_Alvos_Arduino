Funcionalidade: índice geral da documentação do projeto — o que já foi implementado/decidido
(registro) e o que ainda está em planejamento/pendente (apoio).

# Registro Geral — Índice da Documentação

> Consolida `Registro_index.md` (histórico — o que já foi implementado) e `Apoio_index.md`
> (planejamento — tarefas pendentes, ideias, análises comparativas) em um único índice. Os links
> abaixo já refletem a reorganização de pastas em `Rede_Neural/`, `Projeto_Fisico/`, `Artigo/` e
> `Software/`.

## Parte 1 — Registro: o que já foi implementado

### Controlador e visão computacional

- [[CONTROLADOR_NEUROEVOLUTIVO]] (`Rede_Neural/Arquitetura/`) — rede neural (NEAT/SharpNEAT) +
  treinamento evolutivo, arquitetura e persistência; inclui, como anexo, a explicação acessível
  antes publicada em `Como_a_IA_funciona_no_RadarTorres.md` (consolidada neste mesmo arquivo).
- [[ALGORITMO_SELECAO_TORRE]] (`Rede_Neural/Algoritmos/`) — fundamentação matemática da heurística
  de seleção de torre em produção.
- [[DETECCAO_VISAO_YOLO]] (`Rede_Neural/Algoritmos/`) — detecção por câmera com YOLO26n, como foi
  implementada hoje.

### Arquitetura de software

- [[ARQUITETURA_SISTEMA]] (`Software/Arquitetura/`) — decisões arquiteturais e diagramas (camadas,
  concorrência, fluxos de dados), incluindo o diagrama de arquitetura geral (antes
  `Diagrama_Arquitetura_Geral.md`, consolidado neste mesmo arquivo).
- [[DOCUMENTACAO_TECNICA]] (`Software/Arquitetura/`) — referência classe a classe/serviço a
  serviço, limitações e bugs conhecidos.
- [[INVENTARIO_FUNCOES]] (`Software/Qualidade/`) — mapa de navegação do código por fluxo.
- [[BANCO_DE_DADOS]] (`Software/Banco_de_Dados/`) — schema técnico completo das tabelas SQLite e
  seus relacionamentos (consolida o antigo `MODELO_DADOS.md` e `Modelo_Banco_de_Dados.md`).
- [[DESIGN_INTERFACE]] (`Software/Design/`) — identidade visual (cores, tipografia, ícones) e
  padrão de interface/UX já adotado nas telas administrativas (consolida `IDENTIDADE_VISUAL.md` e
  `DIRETRIZ_UX.md`).

### Estrutura física e comunicação

- [[Diagrama_Eletrico]] (`Projeto_Fisico/Eletronica/`) — componentes e pinagem real do Arduino
  MEGA 2560.
- [[COMUNICACAO_ARDUINO]] (`Software/Integracoes/`) — protocolo serial PC ↔ Arduino completo.

### Histórico e gestão do projeto

- [[HISTORICO_ALTERACOES]] (`Software/Qualidade/`) — histórico cronológico de todas as
  solicitações feitas ao assistente (antigo `LOG_SOLICITACOES.md`) + pendências/próximas
  modificações (antigo `Apoio_Modificações.md`).

### Entregáveis acadêmicos do TCC

- **Artigo**: `Artigo/Texto/Artigo.tex` — fonte canônica em LaTeX (não é markdown; `.pdf`/`.docx`
  são gerados a partir dele).
- [[UML_RadarTorres]], [[Casos_de_Uso]], [[Decisoes_Arquiteturais]], [[Diagrama_de_Implantacao]],
  [[Diagrama_de_Pacotes]] (`Software/UML/` e `Software/Arquitetura/`) — UML do sistema já
  implementado.
- [[Matriz_de_Rastreabilidade]] (`Software/Qualidade/`) — requisito → arquivo/classe/função já
  implementada.
- [[Fluxo_Operacional]] (`Software/Arquitetura/`) — fluxograma do ciclo completo de operação já
  implementado.
- [[BANCO_DE_DADOS]] (`Software/Banco_de_Dados/`) — inclui o modelo de dados do entregável
  acadêmico (DER, schema SQL), antes em `Modelo_Banco_de_Dados.md`.
- `Artigo/Revisoes/Parecer_Banca_TCC.*` — parecer da banca e sugestões de referências
  (`referencias_sugeridas.bib`).

### Como este levantamento foi produzido

Os documentos de UML/requisitos foram produzidos por análise do código-fonte do repositório
(`src/RadarTorres.App/`, `Arduino/`, `tests/`) e da documentação já existente. Nenhuma informação
foi inventada; pontos não confirmáveis por código ou documentação estão sinalizados como
**inferência** nos respectivos arquivos.

1. Leitura da estrutura completa de diretórios do repositório.
2. Leitura de [[ARQUITETURA_SISTEMA]], [[BANCO_DE_DADOS]], [[COMUNICACAO_ARDUINO]] e
   [[DOCUMENTACAO_TECNICA]] (documentação técnica já mantida pelo autor do projeto).
3. Leitura direta do código-fonte: arquivos em `Models/`, interfaces de `Services/` e
   `Repositories/`, `appsettings.json`, firmware do Arduino e ViewModels relevantes.
4. Cruzamento entre código e documentação para montar diagramas, requisitos e matriz de
   rastreabilidade, sinalizando qualquer ponto sem evidência direta como inferência.

### Principais inferências assumidas (resumo)

- Responsabilidade de `ILocalizationService`, `IThemeService`, `INavigationService` e
  `IZonaMortaService` — assinaturas completas não foram lidas nesta varredura; a responsabilidade
  foi inferida do nome da interface e do uso descrito em [[ARQUITETURA_SISTEMA]].
- RF16, RF17 (Ações Realizadas, Histórico de Modos) e RF18 (Usuários) — telas dedicadas existem e
  estão roteadas em `Services/NavigationService.cs` (`Status: Implementado`). O módulo de
  Chamados de Ajuda (antigo RF19/RF20) foi removido do código-fonte.
- O [[BANCO_DE_DADOS]] descreve o estado **atual** de persistência: SQLite (`RadarTorres.db`), via
  `SqliteConnectionFactory` e os repositórios `Sqlite*Repository` — não é mais uma
  proposta/extrapolação, a migração já foi concluída.

### Revisões

- **2026-09**: migração de persistência para SQLite e remoção do módulo de Chamados de Ajuda
  refletidas em todos os documentos; telas de Ações Realizadas, Histórico de Modos e Usuários
  (RF15–RF17) confirmadas como implementadas e roteadas.
- **2026-10-26/27**: auditoria completa contra o código real — corrigidas 11 divergências
  factuais, reorganização do vault (notas achatadas, grupos no grafo do Obsidian) — ver
  [[HISTORICO_ALTERACOES]].
- **2026-10-30**: revisão do artigo refeita em `Artigo.tex` (ver [[HISTORICO_ALTERACOES]], entrada
  "Revisão do artigo a partir do docx grifado"); vault reorganizado por finalidade (`index.md` +
  `Apoio_index`/`Registro_index`), substituindo a organização temática anterior.
- **Data desta reorganização**: migração da estrutura de pastas para `Rede_Neural/`,
  `Projeto_Fisico/`, `Artigo/` e `Software/` na raiz do projeto, substituindo `Docs/` +
  `Apoio/` — consolidações descritas em cada arquivo mesclado (ver notas no topo de
  [[BANCO_DE_DADOS]], [[ARQUITETURA_SISTEMA]], [[DESIGN_INTERFACE]], [[CONTROLADOR_NEUROEVOLUTIVO]]
  e [[HISTORICO_ALTERACOES]]).

## Parte 2 — Apoio: planejamento e pendências

Documentos que ajudam nas próximas decisões.

### Artigo e apresentação

- [[Melhoria_de_artigo]] (`Artigo/Revisoes/`) — checklist de revisão acadêmica ainda pendente do
  `Artigo.tex` e propostas registradas (ex.: cooperação entre torres) não implementadas.
- [[regrasDoArtigo]] (`Artigo/Texto/`) — regras obrigatórias de formatação (máximo de páginas,
  padronização de imagens) e status real de conformidade do `Artigo.tex` contra elas.
- [[ApoioDeApresentacao]] (`Artigo/Apresentacao/`) — detalhamento técnico de apoio à defesa (tipos
  de mutação, biblioteca de neuroevolução em avaliação) retirado do corpo do artigo para manter o
  texto objetivo.
- [[Conceito_UML]] (`Artigo/Fundamentacao/`) — embasamento teórico sobre UML, usado como base para
  os diagramas do artigo.
- [[Referencias_Bibliograficas]] (`Artigo/Fundamentacao/`) — catálogo de artigos/PDFs candidatos a
  referência bibliográfica.

### Análises e comparações técnicas

- [[Limitacoes_Conhecidas]] (`Software/Qualidade/`) — divergências entre especificação e
  implementação, usadas para decidir o que corrigir a seguir.

### Pendências gerais do projeto

- [[HISTORICO_ALTERACOES]] (`Software/Qualidade/`) — Parte 1 deste documento lista tarefas
  pendentes, itens em avaliação e decisões de ferramental ainda em aberto (antigo
  `Apoio_Modificações.md`).

## Ver também

- [[HISTORICO_ALTERACOES]] — log cronológico completo das solicitações e decisões.
