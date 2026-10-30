Funcionalidade: índice de navegação dos documentos entregáveis do TCC.

# Documentos_Entregaveis/

Entregáveis oficiais do TCC (o que vai para a banca/orientador). Todas as notas desta área ficam
neste nível (sem subpastas de notas) — as subpastas que você vê abaixo guardam só os artefatos de
apoio (`.png`, `.pdf`, `.puml`, `.tex`, `.drawio`) que cada nota referencia, mantendo o grafo do
Obsidian com no máximo 3 níveis da raiz do vault até qualquer arquivo.

- **Artigo**: `Artigo/Artigo.tex` — artigo do TCC em LaTeX (não é markdown, editar direto no
  `.tex`). Notas de apoio ao artigo (não são o entregável em si): [[Como_a_IA_funciona_no_RadarTorres]]
  (explicação em linguagem acessível do controlador heurística+rede neural) e
  [[Melhoria_de_artigo]] (checklist de revisão acadêmica do `Artigo.tex`).
- **Banco de Dados**: [[Modelo_Banco_de_Dados]] — modelo de dados atual (SQLite), DER. Figuras em
  `Banco_de_Dados/figuras/`.
- **Diagrama Elétrico**: [[Diagrama_Eletrico]] — componentes e pinagem real (Arduino MEGA 2560).
  Fonte PlantUML/PNG em `Diagrama_Eletrico/`.
- **Diagrama de Arquitetura Geral**: [[Diagrama_Arquitetura_Geral]] — visão geral PC + Arduino +
  câmeras + sensores + torres (`.drawio`).
- **Fluxo Operacional**: [[Fluxo_Operacional]] — fluxograma do ciclo completo de operação. Fonte
  Mermaid/PNG em `Fluxo_Operacional/`.
- **Requisitos de Sistema**: [[Matriz_de_Rastreabilidade]] — requisito → arquivo/classe/função.
  PDF/LaTeX dos requisitos em `Requisitos_de_Sistema/`.
- **UML**: [[UML_RadarTorres]], [[Casos_de_Uso]], [[Decisoes_Arquiteturais]],
  [[Diagrama_de_Implantacao]], [[Diagrama_de_Pacotes]], [[Limitacoes_Conhecidas]] — diagramas
  fonte (`.puml`/`.png`/`.pdf`) em `UML/`.

Outras subpastas desta área sem nota markdown própria (só PDF/LaTeX/drawio de apoio):
`Controle_de_Sessoes/` (acompanhamento de orientação), `Diagrama_de_Classes/` (artefato
`.drawio`, atualizado — ver observação em [[UML_RadarTorres]], Seção 5).

## Como este levantamento foi produzido

Os documentos de UML/requisitos foram produzidos por análise do código-fonte do repositório
(`src/RadarTorres.App/`, `Arduino/`, `tests/`) e da documentação já existente em `Docs/`. Nenhuma
informação foi inventada; pontos não confirmáveis por código ou documentação estão sinalizados
como **inferência** nos respectivos arquivos.

1. Leitura da estrutura completa de diretórios do repositório.
2. Leitura de `Docs/Tecnica/ARQUITETURA.md`, `Docs/Tecnica/MODELO_DADOS.md`,
   `Docs/Tecnica/COMUNICACAO_ARDUINO.md`, `Docs/Projeto/CONTEXTO_PROJETO.md` e
   `Docs/Tecnica/DOCUMENTACAO_TECNICA.md` (documentação técnica já mantida pelo autor do
   projeto).
3. Leitura direta do código-fonte: arquivos em `Models/`, interfaces de `Services/` e
   `Repositories/`, `appsettings.json`, firmware do Arduino e ViewModels relevantes.
4. Cruzamento entre código e documentação para montar diagramas, requisitos e matriz de
   rastreabilidade, sinalizando qualquer ponto sem evidência direta como inferência.

## Principais inferências assumidas (resumo)

* Responsabilidade de `ILocalizationService`, `IThemeService`, `INavigationService` e
  `IZonaMortaService` — assinaturas completas não foram lidas nesta varredura; a responsabilidade
  foi inferida do nome da interface e do uso descrito em `Docs/Tecnica/ARQUITETURA.md`.
* RF16, RF17 (Ações Realizadas, Histórico de Modos) e RF18 (Usuários) — telas dedicadas existem e
  estão roteadas em `Services/NavigationService.cs` (`Status: Implementado`). O módulo de
  Chamados de Ajuda (antigo RF19/RF20) foi removido do código-fonte.
* O [[Modelo_Banco_de_Dados]] descreve o estado **atual** de persistência: SQLite
  (`RadarTorres.db`), via `SqliteConnectionFactory` e os repositórios `Sqlite*Repository` — não é
  mais uma proposta/extrapolação, a migração já foi concluída.

## Revisões

* **2026-09**: migração de persistência para SQLite e remoção do módulo de Chamados de Ajuda
  refletidas em todos os documentos; telas de Ações Realizadas, Histórico de Modos e Usuários
  (RF15–RF17) confirmadas como implementadas e roteadas.
* **2026-10-26/27**: auditoria completa contra o código real — corrigidas 11 divergências
  factuais (numeração de requisitos, contagem de tabelas SQLite, topologia do hardware real
  `RadarTorres_Firmware.ino`, status do projeto de testes), reorganização do vault (notas
  achatadas para no máximo 3 níveis, grupos no grafo do Obsidian) — ver
  [[Docs/Projeto/LOG_SOLICITACOES|Log de Solicitações]].

Ver também: [[Índice]] (raiz do vault), [[Guia_da_Documentacao|Docs]].
