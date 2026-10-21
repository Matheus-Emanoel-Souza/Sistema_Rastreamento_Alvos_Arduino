# Diagramas e Requisitos — RadarTorres (TCC)

Levantamento de engenharia de software do **Sistema de Rastreamento e Monitoramento de Alvos
(RadarTorres)**, produzido por análise do código-fonte do repositório
(`src/RadarTorres.App/`, `Arduino/ArduinoSimulation.ino`, `tests/`) e da documentação já
existente em `Docs/`. Nenhuma informação foi inventada; pontos não confirmáveis por código ou
documentação estão sinalizados como **inferência** nos respectivos arquivos.

## Documentos desta pasta

| Arquivo | Conteúdo |
|---|---|
| Diagrama de Classes — *removido* | O artefato de diagrama de classes (antigo `Diagrama_de_Classes/`) foi removido do repositório e ainda não foi regenerado; ver observação em `UML_RadarTorres.md`, Seção 5 |
| [`Diagrama_de_Pacotes.md`](Diagrama_de_Pacotes.md) | Organização em pacotes/namespaces reais do repositório + diagrama Mermaid de dependências |
| [`../Banco_de_Dados/Modelo_Banco_de_Dados.md`](../Banco_de_Dados/Modelo_Banco_de_Dados.md) | Modelo de dados atual (SQLite) com DER |
| [`Requisitos_de_Sistema/Requisitos_RadarTorres.pdf`](../Requisitos_de_Sistema/Requisitos_RadarTorres.pdf) | RF01–RF32 e RNF01–RNF30 consolidados em um único documento, com prioridade/categoria e status |
| [`Decisoes_Arquiteturais.md`](Decisoes_Arquiteturais.md) | Escolhas internas de implementação (persistência, MVVM manual, protocolo serial, UX de cards) — não são requisitos do produto |
| [`Limitacoes_Conhecidas.md`](Limitacoes_Conhecidas.md) | Funcionalidades ainda não implementadas e divergências entre a especificação revisada e o código atual |
| [`../Requisitos_de_Sistema/Matriz_de_Rastreabilidade.md`](../Requisitos_de_Sistema/Matriz_de_Rastreabilidade.md) | Requisito/decisão/limitação → arquivo/classe/função → status |
| [`Diagrama_Casos_de_Uso.puml`](Diagrama_Casos_de_Uso.puml) | Diagrama de Casos de Uso (PlantUML) — atores, 28 casos de uso agrupados, `<<include>>`/`<<extend>>` |
| [`Casos_de_Uso.md`](Casos_de_Uso.md) | Especificação textual de cada caso de uso (objetivo, atores, fluxos, status) e matriz Caso de Uso × Requisito |

Diagramas Mermaid também foram renderizados como imagem (`.png`) nesta mesma pasta, quando a
geração foi bem-sucedida — ver seção "Diagramas renderizados" abaixo. O código Mermaid
permanece nos `.md` para permitir edição futura.

## Como este levantamento foi produzido

1. Leitura da estrutura completa de diretórios do repositório.
2. Leitura de `Docs/Tecnica/ARQUITETURA.md`, `Docs/Tecnica/MODELO_DADOS.md`, `Docs/Tecnica/COMUNICACAO_ARDUINO.md`,
   `Docs/Projeto/CONTEXTO_PROJETO.md` e `Docs/Tecnica/DOCUMENTACAO_TECNICA.md` (documentação técnica já mantida
   pelo autor do projeto).
3. Leitura direta do código-fonte: todos os arquivos em `Models/`, as interfaces de `Services/`
   e `Repositories/`, `appsettings.json`, `Arduino/ArduinoSimulation.ino` e ViewModels
   relevantes.
4. Cruzamento entre código e documentação para montar diagramas, requisitos e matriz de
   rastreabilidade, sinalizando qualquer ponto sem evidência direta como inferência.

## Principais inferências assumidas (resumo)

* Responsabilidade de `ILocalizationService`, `IThemeService`, `INavigationService` e
  `IZonaMortaService` — assinaturas completas não foram lidas nesta varredura; a responsabilidade
  foi inferida do nome da interface e do uso descrito em `Docs/Tecnica/ARQUITETURA.md`.
* RF16, RF17 (Ações Realizadas, Histórico de Modos) — as telas dedicadas
  (`AcoesRealizadasView`/`ViewModel`, `HistoricoModosView`/`ViewModel`) existem e estão roteadas
  em `Services/NavigationService.cs` (`Status: Implementado`).
* RF18 (Usuários) — a tela `UsuariosView`/`UsuariosViewModel` existe e está roteada em
  `Services/NavigationService.cs` (`Status: Implementado`). O módulo de Chamados de Ajuda
  (antigo RF19/RF20) foi removido do código-fonte — não há mais `IChamadoAjudaRepository` nem
  qualquer tela associada.
* O modelo de banco de `Modelo_Banco_de_Dados.md` descreve o estado **atual** de persistência:
  SQLite (`RadarTorres.db`), via `SqliteConnectionFactory` e os repositórios `Sqlite*Repository`
  — não é mais uma proposta/extrapolação, a migração já foi concluída.

## Revisão de 2026-09

Os documentos desta pasta foram revisados para refletir a migração de persistência para SQLite e
a remoção do módulo de Chamados de Ajuda, além de corrigir referências cruzadas defasadas (ver
histórico de revisão anterior de 2026-08-30 em `Docs/Projeto/LOG_SOLICITACOES.md`).

**Situação atual:** as telas de Ações Realizadas, Histórico de Modos e Usuários (RF15–RF17)
estão **implementadas e roteadas** em `Services/NavigationService.cs` — não existe mais nenhuma
classe `PlaceholderView` no código-fonte. O módulo de Chamados de Ajuda (RF19/RF20) foi removido
do sistema. Ver `Limitacoes_Conhecidas.md`, item L02, e o campo `Status` de cada requisito em
`Requisitos_de_Sistema/Requisitos_RadarTorres.pdf`.
