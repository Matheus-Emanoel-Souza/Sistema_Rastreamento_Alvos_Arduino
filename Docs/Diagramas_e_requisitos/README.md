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
| [`Requisitos_Funcionais.md`](Requisitos_Funcionais.md) | RF01–RF32, com descrição, atores e evidência em código/documentação |
| [`Requisitos_Nao_Funcionais.md`](Requisitos_Nao_Funcionais.md) | RNF01–RNF30, classificados por categoria (desempenho, segurança, usabilidade etc.) |
| [`Matriz_de_Rastreabilidade.md`](Matriz_de_Rastreabilidade.md) | Requisito → arquivo/classe/função → responsabilidade |

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
* RF16, RF17, RF18 (telas de Ações Realizadas, Histórico de Modos, Usuários) — os repositórios e
  modelos já existem, mas `Docs/CONTEXTO_PROJETO.md` lista essas telas como "pendentes"; o
  requisito de dado já está implementado, a tela de consulta dedicada pode não estar.
* RF23 (tratamento administrativo de chamados) — inferido da existência de `Update` em
  `IChamadoAjudaRepository` e dos campos `RespostaAdmin`/`DataResolucao`, sem leitura direta da
  tela correspondente.
* Modelo de banco "proposto" em `Modelo_Banco_de_Dados.md` — é a extrapolação do plano de
  migração já documentado pelo próprio projeto (`TODO(SQL)`), não uma implementação existente.

## Ponto não confirmado

Não foi possível confirmar, apenas por leitura estática do código nesta sessão, se as telas de
Ações Realizadas, Histórico de Modos e Usuários (RF16–RF18) já saíram do estado de placeholder
mencionado em `Docs/CONTEXTO_PROJETO.md` (datado de 2026-08-12) — o histórico de commits mais
recente do repositório mostra trabalho ativo na tela de Objetos Detectados, mas não confirma o
estado das demais.
