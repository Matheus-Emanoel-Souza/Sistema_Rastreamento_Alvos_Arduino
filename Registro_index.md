Funcionalidade: índice dos documentos de histórico — o que já foi implementado, decidido e executado no projeto.

# Registro — Índice

Documentos que registram o estado atual e o que já aconteceu, não planejamento futuro (ver
[[Apoio_index]] para isso).

## Controlador e visão computacional

- [[CONTROLADOR_NEUROEVOLUTIVO]] — rede neural + algoritmo genético implementados, arquitetura,
  treinamento e persistência.
- [[ALGORITMO_SELECAO_TORRE]] — fundamentação matemática da heurística de seleção de torre em
  produção.
- [[DETECCAO_VISAO_YOLO]] — detecção por câmera com YOLO26n, como foi implementada hoje.
- [[Como_a_IA_funciona_no_RadarTorres]] — a mesma arquitetura explicada em linguagem acessível.

## Arquitetura de software

- [[ARQUITETURA]] — decisões arquiteturais e diagramas (camadas, concorrência, fluxos de dados).
- [[DOCUMENTACAO_TECNICA]] — referência classe a classe/serviço a serviço, limitações e bugs
  conhecidos.
- [[INVENTARIO_FUNCOES]] — mapa de navegação do código por fluxo.
- [[MODELO_DADOS]] — schema técnico completo das tabelas SQLite e seus relacionamentos.
- [[DIRETRIZ_UX]] — padrão de interface já adotado nas telas administrativas.

## Estrutura física e comunicação

- [[Diagrama_Eletrico]] — componentes e pinagem real do Arduino MEGA 2560.
- [[COMUNICACAO_ARDUINO]] — protocolo serial PC ↔ Arduino completo.

## Histórico e gestão do projeto

- [[CONTEXTO_PROJETO]] — resumo do estado atual do projeto (stack, arquitetura).
- [[ETAPA1_FUNDACAO]] — fundação multiusuário (Etapa 1, parte A), já implementada.
- [[IDENTIDADE_VISUAL]] — identidade visual adotada (cores, tipografia, ícones).
- [[INSTALADOR]] — processo de criação do instalador Windows (Setup.exe), já implementado.
- [[LOG_SOLICITACOES]] — histórico cronológico de todas as solicitações feitas ao assistente.

## Entregáveis acadêmicos do TCC

- **Artigo**: `Docs/Documentos_Entregaveis/Artigo/Artigo.tex` — fonte canônica em LaTeX (não é
  markdown; `.pdf`/`.docx` são gerados a partir dele).
- [[UML_RadarTorres]], [[Casos_de_Uso]], [[Decisoes_Arquiteturais]], [[Diagrama_de_Implantacao]],
  [[Diagrama_de_Pacotes]] — UML do sistema já implementado.
- [[Matriz_de_Rastreabilidade]] — requisito → arquivo/classe/função já implementada.
- [[Diagrama_Arquitetura_Geral]] — visão geral PC + Arduino + câmeras + sensores + torres.
- [[Fluxo_Operacional]] — fluxograma do ciclo completo de operação já implementado.
- [[Modelo_Banco_de_Dados]] — modelo de dados para o entregável acadêmico (DER, schema SQL).

### Como este levantamento foi produzido

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
  factuais, reorganização do vault (notas achatadas, grupos no grafo do Obsidian) — ver
  [[LOG_SOLICITACOES]].
- **2026-10-30**: revisão do artigo refeita em `Artigo.tex` (ver [[LOG_SOLICITACOES]], entrada
  "Revisão do artigo a partir do docx grifado"); vault reorganizado por finalidade
  (`index.md` + `Apoio_index`/`Registro_index`), substituindo a organização temática anterior.

Ver também: [[Apoio_index]] (pendências e propostas sobre os mesmos temas).
