# Índice — RadarTorres (TCC)

Mapa de navegação (MOC) do vault. Ponto de partida para encontrar qualquer documento do
projeto — não é documentação nova, só um índice de atalhos.

> Visão geral do próprio aplicativo (instalação, uso, build): [`README.md`](README.md) (raiz do
> repositório).

## Projeto

Acompanhamento do trabalho em si — contexto, etapas, notas, histórico de pedidos.

- [[CONTEXTO_PROJETO]] — resumo do projeto (stack, arquitetura, estado atual, roadmap).
- [[ETAPA1_FUNDACAO]] — fundação multiusuário (Etapa 1, parte A).
- [[INSTALADOR]] — processo de instalação Windows (Setup.exe).
- [[LOG_SOLICITACOES]] — histórico cronológico de pedidos ao assistente.
- [[Notas_de_Apoio]] — notas de apoio rápidas.
- [[PROXIMAS_MODIFICACOES]] — pendências e decisões futuras.
- Índice completo da pasta: [[Docs/Projeto/README|Projeto]]

## Documentação Técnica

Referência técnica interna do aplicativo.

- [[ARQUITETURA]] — decisões arquiteturais e diagramas (camadas, concorrência, fluxos).
- [[DOCUMENTACAO_TECNICA]] — referência classe a classe/serviço a serviço.
- [[COMUNICACAO_ARDUINO]] — protocolo serial PC ↔ Arduino.
- [[ALGORITMO_SELECAO_TORRE]] — matemática do radar e da seleção de torres.
- [[MODELO_DADOS]] — schema SQLite, relacionamentos.
- Índice completo da pasta: [[Docs/Tecnica/README|Técnica]]

## Embasamento Teórico

Material de apoio conceitual para o artigo (não é entregável).

- [[Conceito_UML]] — o que é UML, tipos de diagrama, notações.
- Índice completo da pasta: [[Docs/Embasamento_Teorico/README|Embasamento Teórico]]

## Documentos Entregáveis

Entregáveis oficiais do TCC.

- **Artigo**: `Docs/Documentos_Entregaveis/Artigo/Artigo.tex` (LaTeX — não é markdown).
- **Banco de Dados**: [[Modelo_Banco_de_Dados]].
- **Diagrama Elétrico**: [[Diagrama_Eletrico]].
- **Requisitos de Sistema**: [[Matriz_de_Rastreabilidade]].
- **UML**: [[UML_RadarTorres]], [[Casos_de_Uso]], [[Decisoes_Arquiteturais]],
  [[Diagrama_de_Implantacao]], [[Diagrama_de_Pacotes]], [[Limitacoes_Conhecidas]].
- Índice completo da pasta: [[Docs/Documentos_Entregaveis/README|Documentos Entregáveis]]

## Código-fonte

Não é markdown — pastas de interesse, sem wikilink:

- `src/RadarTorres.App/` — aplicativo C#/WPF (.NET 9).
- `src/RadarTorres.Launcher/` — launcher usado pelo instalador.
- `Arduino/` — firmware de simulação (`ArduinoSimulation.ino`).

## Documentação geral

- [[Docs/README|Docs]] — guia rápido de onde procurar cada tipo de documento.
