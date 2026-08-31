# Diagramas e Requisitos — RadarTorres (TCC)

Levantamento de engenharia de software do **Sistema de Rastreamento e Monitoramento de Alvos
(RadarTorres)**, produzido por análise do código-fonte do repositório
(`src/RadarTorres.App/`, `Arduino/ArduinoSimulation.ino`, `tests/`) e da documentação já
existente em `Docs/`. Nenhuma informação foi inventada; pontos não confirmáveis por código ou
documentação estão sinalizados como **inferência** nos respectivos arquivos.

## Documentos desta pasta

| Arquivo | Conteúdo |
|---|---|
| [`Diagrama_de_Classes.md`](Diagrama_de_Classes.md) | Classes/módulos principais (Models, Services, Repositories, ViewModels, Helpers, e o firmware Arduino representado como módulo/struct) + diagrama Mermaid |
| [`Diagrama_de_Pacotes.md`](Diagrama_de_Pacotes.md) | Organização em pacotes/namespaces reais do repositório + diagrama Mermaid de dependências |
| [`Modelo_Banco_de_Dados.md`](Modelo_Banco_de_Dados.md) | Modelo de dados atual (CSV) com DER, e modelo proposto/inferido para migração futura a SQL |
| [`Requisitos_Funcionais.md`](Requisitos_Funcionais.md) | RF01–RF32, com descrição, atores, prioridade, status e evidência em código/documentação |
| [`Requisitos_Nao_Funcionais.md`](Requisitos_Nao_Funcionais.md) | RNF01–RNF30, classificados por categoria (desempenho, segurança, usabilidade etc.) e status |
| [`Decisoes_Arquiteturais.md`](Decisoes_Arquiteturais.md) | Escolhas internas de implementação (persistência, MVVM manual, protocolo serial, UX de cards) — não são requisitos do produto |
| [`Limitacoes_Conhecidas.md`](Limitacoes_Conhecidas.md) | Funcionalidades ainda não implementadas e divergências entre a especificação revisada e o código atual |
| [`Matriz_de_Rastreabilidade.md`](Matriz_de_Rastreabilidade.md) | Requisito/decisão/limitação → arquivo/classe/função → status |
| [`Diagrama_Casos_de_Uso.puml`](Diagrama_Casos_de_Uso.puml) | Diagrama de Casos de Uso (PlantUML) — atores, 28 casos de uso agrupados, `<<include>>`/`<<extend>>` |
| [`Casos_de_Uso.md`](Casos_de_Uso.md) | Especificação textual de cada caso de uso (objetivo, atores, fluxos, status) e matriz Caso de Uso × Requisito |

Diagramas Mermaid também foram renderizados como imagem (`.png`) nesta mesma pasta, quando a
geração foi bem-sucedida — ver seção "Diagramas renderizados" abaixo. O código Mermaid
permanece nos `.md` para permitir edição futura.

## Como este levantamento foi produzido

1. Leitura da estrutura completa de diretórios do repositório.
2. Leitura de `Docs/ARQUITETURA.md`, `Docs/MODELO_DADOS.md`, `Docs/COMUNICACAO_ARDUINO.md`,
   `Docs/CONTEXTO_PROJETO.md` e `Docs/DOCUMENTACAO_TECNICA.md` (documentação técnica já mantida
   pelo autor do projeto).
3. Leitura direta do código-fonte: todos os arquivos em `Models/`, as interfaces de `Services/`
   e `Repositories/`, `appsettings.json`, `Arduino/ArduinoSimulation.ino` e ViewModels
   relevantes.
4. Cruzamento entre código e documentação para montar diagramas, requisitos e matriz de
   rastreabilidade, sinalizando qualquer ponto sem evidência direta como inferência.

## Principais inferências assumidas (resumo)

* Responsabilidade de `ILocalizationService`, `IThemeService`, `INavigationService` e
  `IDeadZoneService` — assinaturas completas não foram lidas nesta varredura; a responsabilidade
  foi inferida do nome da interface e do uso descrito em `Docs/ARQUITETURA.md`.
* RF16, RF17 (Ações Realizadas, Histórico de Modos) — **confirmado na revisão de 2026-08-30**:
  o registro de dados já funciona, a tela de consulta dedicada não existe (`Status: Parcial`).
* RF18, RF23 (Usuários, tratamento administrativo de chamados) — **confirmado na revisão de
  2026-08-30**: nenhuma tela consome `IUsuarioRepository`/`IChamadoAjudaRepository.Update`
  hoje (`Status: Planejado`).
* Modelo de banco "proposto" em `Modelo_Banco_de_Dados.md` — é a extrapolação do plano de
  migração já documentado pelo próprio projeto (`TODO(SQL)`), não uma implementação existente.

## Revisão de 2026-08-30

Os documentos desta pasta foram revisados para separar corretamente RF, RNF, decisões
arquiteturais e limitações conhecidas — ver `Docs/LOG_SOLICITACOES.md`, entrada
"2026-08-30 — Revisão dos documentos de Requisitos". O ponto abaixo, deixado em aberto na
versão anterior deste README, foi confirmado nessa revisão.

**Confirmado:** as telas de Ações Realizadas, Histórico de Modos, Usuários e Gestão/listagem de
Chamados de Ajuda (RF16–RF18, RF23) **continuam em `PlaceholderView`** — lido diretamente em
`Services/NavigationService.cs`. RF16/RF17 têm o registro de dados já funcionando (falta só a
tela de consulta); RF18/RF23 não têm nenhuma interface, nem de consulta nem de gestão. Ver
`Limitacoes_Conhecidas.md`, item L02, e o campo `Status` de cada requisito em
`Requisitos_Funcionais.md`.
