Funcionalidade: índice temático dos entregáveis acadêmicos do TCC (artigo, UML, requisitos, diagramas, banco de dados, referências bibliográficas).

# Artigo — Índice

Entregáveis oficiais do TCC (o que vai para a banca/orientador) e o material de apoio à redação
e à defesa.

- **Artigo**: `Docs/Documentos_Entregaveis/Artigo/artigo_revisado.tex` — artigo do TCC em LaTeX
  (não é markdown, editar direto no `.tex`; `.pdf`/`.docx` são gerados a partir dele).
  - [[Melhoria_de_artigo]] — checklist de revisão acadêmica e decisões pendentes sobre o artigo.
  - [[ApoioDeApresentacao]] — detalhamento técnico (tipos de mutação, biblioteca de
    neuroevolução) retirado do corpo do artigo para a defesa.
  - [[ROTEIRO_APRESENTACAO]] — roteiro sugerido para apresentar o projeto à banca.
- **UML**: [[UML_RadarTorres]], [[Casos_de_Uso]], [[Decisoes_Arquiteturais]],
  [[Diagrama_de_Implantacao]], [[Diagrama_de_Pacotes]], [[Limitacoes_Conhecidas]].
- **Requisitos de Sistema**: [[Matriz_de_Rastreabilidade]] — requisito → arquivo/classe/função.
- **Diagrama de Arquitetura Geral**: [[Diagrama_Arquitetura_Geral]] — visão geral PC + Arduino +
  câmeras + sensores + torres.
- **Diagrama Elétrico**: [[Diagrama_Eletrico]] — componentes e pinagem real (Arduino MEGA 2560).
- **Fluxo Operacional**: [[Fluxo_Operacional]] — fluxograma do ciclo completo de operação.
- **Banco de Dados**: [[Modelo_Banco_de_Dados]] — modelo de dados para o entregável acadêmico
  (DER, schema SQL).
- **Embasamento teórico**: [[Conceito_UML]] (conceitos de UML usados como base), e
  [[Referencias_Bibliograficas]] (artigos/PDFs usados na revisão de literatura).

Outras subpastas de `Documentos_Entregaveis/` sem nota markdown própria (só PDF/LaTeX/drawio de
apoio): `Controle_de_Sessoes/` (acompanhamento de orientação), `Diagrama_de_Classes/` (artefato
`.drawio` — ver observação em [[UML_RadarTorres]], Seção 5).

## Como este levantamento foi produzido

Os documentos de UML/requisitos foram produzidos por análise do código-fonte do repositório
(`src/RadarTorres.App/`, `Arduino/`, `tests/`) e da documentação já existente em `Docs/`. Nenhuma
informação foi inventada; pontos não confirmáveis por código ou documentação estão sinalizados
como **inferência** nos respectivos arquivos.

1. Leitura da estrutura completa de diretórios do repositório.
2. Leitura de [[ARQUITETURA]], [[MODELO_DADOS]], [[COMUNICACAO_ARDUINO]], [[CONTEXTO_PROJETO]] e
   [[DOCUMENTACAO_TECNICA]] (documentação técnica já mantida pelo autor do projeto).
3. Leitura direta do código-fonte: arquivos em `Models/`, interfaces de `Services/` e
   `Repositories/`, `appsettings.json`, firmware do Arduino e ViewModels relevantes.
4. Cruzamento entre código e documentação para montar diagramas, requisitos e matriz de
   rastreabilidade, sinalizando qualquer ponto sem evidência direta como inferência.

### Principais inferências assumidas (resumo)

- Responsabilidade de `ILocalizationService`, `IThemeService`, `INavigationService` e
  `IZonaMortaService` — assinaturas completas não foram lidas nesta varredura; a responsabilidade
  foi inferida do nome da interface e do uso descrito em [[ARQUITETURA]].
- RF16, RF17 (Ações Realizadas, Histórico de Modos) e RF18 (Usuários) — telas dedicadas existem e
  estão roteadas em `Services/NavigationService.cs` (`Status: Implementado`). O módulo de
  Chamados de Ajuda (antigo RF19/RF20) foi removido do código-fonte.
- O [[Modelo_Banco_de_Dados]] descreve o estado **atual** de persistência: SQLite
  (`RadarTorres.db`), via `SqliteConnectionFactory` e os repositórios `Sqlite*Repository` — não é
  mais uma proposta/extrapolação, a migração já foi concluída.

### Revisões

- **2026-09**: migração de persistência para SQLite e remoção do módulo de Chamados de Ajuda
  refletidas em todos os documentos; telas de Ações Realizadas, Histórico de Modos e Usuários
  (RF15–RF17) confirmadas como implementadas e roteadas.
- **2026-10-26/27**: auditoria completa contra o código real — corrigidas 11 divergências
  factuais (numeração de requisitos, contagem de tabelas SQLite, topologia do hardware real
  `RadarTorres_Firmware.ino`, status do projeto de testes), reorganização do vault (notas
  achatadas para no máximo 3 níveis, grupos no grafo do Obsidian) — ver [[LOG_SOLICITACOES]].
- **2026-10-30**: revisão do autor sobre `artigo_revisado.docx` (remoção da seção "Projeto
  elétrico e estrutural", da zona morta e do tempo mínimo de mira do corpo do artigo; proposta de
  cooperação entre torres/câmeras registrada como trabalho futuro) e reorganização do vault em
  índices temáticos (`index.md` + `RedeNeural_index`/`Artigo_index`/`Estrutura_index`/
  `Projeto_index`) — ver [[Melhoria_de_artigo]] e [[LOG_SOLICITACOES]].

Ver também: [[RedeNeural_index]] (lógica do controlador descrita no artigo),
[[Estrutura_index]] (versão técnica detalhada do modelo de dados e da arquitetura).
