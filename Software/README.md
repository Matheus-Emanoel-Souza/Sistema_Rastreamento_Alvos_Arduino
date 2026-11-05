# Software — Índice

Documentação do software do RadarTorres (app WPF): arquitetura, design de interface, banco de
dados, UML, segurança, integrações e qualidade.

## Arquitetura/

- [`DOCUMENTACAO_TECNICA.md`](Arquitetura/DOCUMENTACAO_TECNICA.md) — referência classe a
  classe/serviço a serviço, limitações e bugs conhecidos. Ponto de entrada técnico do projeto.
- [`ARQUITETURA_SISTEMA.md`](Arquitetura/ARQUITETURA_SISTEMA.md) — camadas, concorrência, fluxos
  de dados e o diagrama de arquitetura geral (implantação PC + Arduino + sensores + câmeras +
  torres).
- [`Decisoes_Arquiteturais.md`](Arquitetura/Decisoes_Arquiteturais.md) — decisões/restrições
  arquiteturais (DA01–DA05).
- [`Fluxo_Operacional.md`](Arquitetura/Fluxo_Operacional.md) — fluxograma do ciclo completo de
  operação.
- `Requisitos_RadarTorres.tex`/`.pdf` — requisitos funcionais e não funcionais do sistema
  (entregável acadêmico).

## Design/

- [`DESIGN_INTERFACE.md`](Design/DESIGN_INTERFACE.md) — identidade visual (cores, tipografia,
  ícones) e diretriz de UX (ações no topo, formulários em modal).
- [`Guia_do_Site.md`](Design/Guia_do_Site.md) — guia de desenvolvimento/build/deploy do site
  estático do projeto (`site/`).

## Banco_de_Dados/

- [`BANCO_DE_DADOS.md`](Banco_de_Dados/BANCO_DE_DADOS.md) — schema completo das tabelas SQLite,
  DER, índices e histórico da migração CSV → SQLite.
- `schema.sql`, `Modelo_Banco_de_Dados.dbml`, `der_sqlite.mmd` — fontes do modelo de dados.
- `RadarTorres.db` — banco SQLite de exemplo/desenvolvimento.

## UML/

- [`UML_RadarTorres.md`](UML/UML_RadarTorres.md) — introdução acadêmica à UML aplicada ao projeto.
- [`Casos_de_Uso.md`](UML/Casos_de_Uso.md), [`Diagrama_de_Pacotes.md`](UML/Diagrama_de_Pacotes.md),
  [`Diagrama_de_Implantacao.md`](UML/Diagrama_de_Implantacao.md) — diagramas e especificações.
- Fontes `.puml`/`.drawio`/`.tex` dos diagramas de casos de uso, implantação e classes.

## Seguranca/

Reservada para documentação futura de autenticação/autorização (hoje coberta dentro de
`DOCUMENTACAO_TECNICA.md` e `ARQUITETURA_SISTEMA.md`; nenhum documento dedicado existia para
mover sem duplicar conteúdo).

## Integracoes/

- [`COMUNICACAO_ARDUINO.md`](Integracoes/COMUNICACAO_ARDUINO.md) — protocolo serial completo PC ↔
  Arduino.

## Qualidade/

- [`REGISTRO_GERAL.md`](Qualidade/REGISTRO_GERAL.md) — **índice geral de toda a documentação do
  projeto** (registro do que já existe + pendências).
- [`HISTORICO_ALTERACOES.md`](Qualidade/HISTORICO_ALTERACOES.md) — log cronológico de todas as
  solicitações ao assistente + pendências/próximas modificações.
- [`INVENTARIO_FUNCOES.md`](Qualidade/INVENTARIO_FUNCOES.md) — mapa de navegação do código por
  fluxo.
- [`Limitacoes_Conhecidas.md`](Qualidade/Limitacoes_Conhecidas.md) — divergências entre
  especificação e implementação.
- [`Matriz_de_Rastreabilidade.md`](Qualidade/Matriz_de_Rastreabilidade.md) — requisito →
  arquivo/classe/função.

## Ver também

- [`../Rede_Neural/README.md`](../Rede_Neural/README.md), [`../Projeto_Fisico/README.md`](../Projeto_Fisico/README.md),
  [`../Artigo/README.md`](../Artigo/README.md) — demais índices da documentação do projeto.
