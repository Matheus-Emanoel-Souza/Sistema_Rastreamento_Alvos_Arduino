# Contexto do Projeto — RadarTorres (TCC)

> Documento-resumo pensado para ser colado/anexado em **outra IA** (ChatGPT, Gemini, uma nova
> sessão do Claude, etc.) como contexto rápido do projeto, sem precisar enviar o código-fonte
> completo. Cobre: a ideia, como foi construído, o estado atual e o que falta. Para detalhes
> técnicos linha a linha, os links para `Docs/*.md` no final apontam para as fontes completas.
>
> Última atualização: 2026-10-26.

---

## 1. O que é o projeto

**RadarTorres** é um aplicativo desktop **C#/WPF (.NET 9)** desenvolvido como base de
**Trabalho de Conclusão de Curso (TCC) em Engenharia da Computação**, por Matheus Emanoel
Souza.

O software se comunica via porta serial (USB) com um **Arduino** responsável por sensores de
detecção de alvos ao redor de uma base. Ele:

1. Recebe leituras de sensores (ângulo + distância) via protocolo texto simples pela serial.
2. Converte cada leitura em posição cartesiana e exibe em tempo real num radar circular
   dividido em 4 quadrantes.
3. Seleciona automaticamente, entre um conjunto configurável de torres demonstrativas
   posicionadas ao redor da base, qual está mais próxima/adequada para cada alvo.
4. Permite um modo de acionamento **demonstrativo** (laser de baixa potência / LED —
   **nunca armamento real**), respeitando uma distância mínima de segurança.

Repositório: `Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino` (GitHub).

---

## 2. Como foi construído

### 2.1 Stack e decisões técnicas

| Camada | Escolha | Por quê |
|---|---|---|
| Linguagem/Runtime | C# 13 / .NET 9 | — |
| Interface | WPF | Data-binding real, gráficos vetoriais 2D (radar), MVVM natural, bom desempenho de redesenho em tempo real |
| MVVM | Implementado à mão (`ViewModelBase`, `RelayCommand`, ~60 linhas), sem framework externo (Prism/CommunityToolkit.Mvvm) | Projeto didático (TCC) — mantém o mecanismo de binding 100% explicável na defesa |
| Comunicação serial | `System.IO.Ports` | — |
| Configuração | `Microsoft.Extensions.Configuration` + `appsettings.json` | Torres, portas, distâncias configuráveis sem recompilar |
| Injeção de dependência | `Microsoft.Extensions.DependencyInjection`, composition root em `App.xaml.cs` | — |
| Persistência | **SQLite** (`%AppData%\RadarTorres\Data\radartorres.db`) | Migrada de CSV para SQLite — DI em `App.xaml.cs:141-146` registra `Sqlite*Repository` para as 5 tabelas; falhas de escrita são enfileiradas em `pending-writes.sql` e reaplicadas no próximo start. Único uso de CSV remanescente é o botão manual "Exportar CSV" da tela de Objetos Detectados |
| Empacotamento | Inno Setup 6 (`installer/RadarTorres.iss`) | Instalador `Setup.exe`, self-contained (embute o .NET 9 Desktop Runtime — usuário final não precisa instalar nada) |

### 2.2 Arquitetura (camadas)

```
Views (WPF/XAML)  <-->  ViewModels (MainViewModel, ...)  <-->  Services (regras de negócio)
                                                                       |
                                                                Models (entidades)
```

- **Views**: só XAML + pequenos encaminhamentos de eventos de UI.
- **ViewModels**: orquestram serviços, sem regra de negócio própria.
- **Services**: 100% da lógica (protocolo serial, rastreamento, seleção de torre,
  acionamento, auth, permissões, i18n, tema, layout do painel). Nenhum referencia
  WPF diretamente — interfaces `I*Service` permitem trocar implementação (ex.: testes).
- **Models**: entidades simples com `INotifyPropertyChanged`.

### 2.3 Concorrência

Leitura serial roda em `Task.Run` dedicado; timers (`System.Threading.Timer` /
`DispatcherTimer`) cuidam de watchdog de conexão e expiração de alvos. Regra geral:
qualquer classe com coleção/evento vinculado à UI é responsável por despachar para o
`Dispatcher` internamente (captura o Dispatcher no construtor).

### 2.4 Como o app evoluiu (linha do tempo funcional)

1. **Base inicial**: app único (`MainWindow`), lógica de radar/serial/torres.
2. **Instalador Windows**: `Setup.exe` (Inno Setup, self-contained), launcher avulso, ícone,
   tratamento global de erros de inicialização.
3. **Fundação multiusuário** (Etapa 1, parte A): login, 3 perfis (Administrador/Operador/
   Visualizador), hash de senha (PBKDF2-HMACSHA256), permissões, i18n (pt-BR/en-US), tema
   claro/escuro/sistema, Shell (barra superior + barra lateral + navegação), painel principal
   com indicadores, auditoria (`objetos_detectados`, `acoes_realizadas`, `modo_atual_torre`),
   persistência CSV. `MainWindow` virou `MonitoramentoView`, um item de menu dentro da Shell.
4. **Aba "Configurações do Arduino"**: detecção do `arduino-cli`, compilação de sketch `.ino`
   assíncrona/cancelável (via `Process`/`ArgumentList`, nunca shell), monitor serial reaproveitando
   a mesma conexão da tela de Monitoramento (sem duas portas concorrentes). Primeiro projeto de
   testes automatizados do repositório (`tests/RadarTorres.Tests`, xUnit, 21 testes).
5. **Painel principal com layout personalizável**: cards arrastáveis/redimensionáveis
   (`DashboardCanvas`/`DashboardCard`, WPF puro, sem lib de terceiros), posição/tamanho
   guardados como fração (0..1) do canvas (responsivo), anticolisão por rejeição, persistidos
   por usuário em JSON.
6. **Consolidação de branches (2026-08-12)**: as branches `Sistema`, `TESTE` e `homologacao`
   foram todas mescladas na `main` (uma por vez, com merge commit, histórico preservado). Um
   conflito real em `README.md` (a `main` havia excluído o arquivo numa limpeza antiga; a
   `Sistema` o reescreveu por completo, unificando com `README_LOCAL.md`) foi resolvido
   mantendo a versão reescrita da `Sistema`, por decisão do usuário. Build (0 erros/avisos) e
   os 21 testes automatizados validados após o merge. `Sistema` e `homologacao` foram
   excluídas (local + remoto); `main` e `TESTE` permanecem.
7. **Zonas mortas** (via branch `TESTE`, mesclada em `main`): modelo de domínio `ZonaMorta`,
   persistência em JSON único da instalação, serviço de negócio que avalia zonas ativas e
   bloqueia seleção de torre/disparo dentro delas, gestão restrita ao perfil Administrador,
   criação por clique/arraste direto no radar, card dedicado "ZONAS MORTAS" com sombreamento
   visual. Ajustes de UX no mesmo ciclo: torres desenhadas como quadrado no radar, console de
   eventos fixado na faixa lateral direita.
8. **Tela de Objetos Detectados** (via branch `Tela_de_logs`, mesclada em `main`): a tela sai
   do placeholder e vira uma tabela real (ViewModel + View), com serviço de
   exportação/importação em CSV, XML e PDF.
9. **Formulário de Chamado de Ajuda**: `ChamadoAjuda` (model), `CsvChamadoAjudaRepository`,
   `HelpDeskFormViewModel`/`HelpDeskFormWindow`, acionado pela TopBar — permite abrir um
   ticket. Era só o lado de *criação*; nunca ganhou tela de listagem/gestão — o módulo inteiro
   foi **removido** do projeto em etapa posterior (ver seção 3, "Módulo removido").
10. **Levantamento de engenharia de software (2026-08-20)**: `Docs/Documentos_Entregaveis/Diagramas_e_requisitos/`
    adicionado por análise do código-fonte e da documentação existente — diagrama de classes,
    diagrama de pacotes, DER atual (CSV) e proposto (SQL), Requisitos Funcionais (RF01–RF32),
    Requisitos Não Funcionais (RNF01–RNF30) e matriz de rastreabilidade requisito → arquivo/
    classe/função. Pontos não confirmáveis por código foram sinalizados como inferência no
    próprio documento (ver `Docs/Documentos_Entregaveis/Diagramas_e_requisitos/README.md`).
11. **Visão computacional e controlador neuroevolutivo** (múltiplas sessões, entre a versão
    anterior deste documento e agora): módulo "Câmeras" com detecção YOLO via ONNX Runtime
    local (`OnnxYoloDetectionService`), telas "Perfis de Alvo" e "Desempenho", projeto de
    testes automatizados recriado e expandido (`tests/RadarTorres.Tests`, 18 arquivos/145
    testes), e o controlador neuroevolutivo (`NeuralEvolution/`) — rede neural treinada por
    algoritmo genético e combinada a regras heurísticas, com telas "Treinamento do
    Controlador" e "Estatísticas de Treinamento". Nova identidade visual aplicada ao app
    (ver `Docs/Projeto/IDENTIDADE_VISUAL.md`). Ver seção 3.1 para detalhes de cada
    funcionalidade.
12. **5 regras heurísticas do controlador, desligáveis em "Configurações"** (2026-10-23):
    anti-oscilação, priorização por urgência, corredor de tiro, balanceamento de carga e
    predição de trajetória — cada uma com toggle em `ConfiguracoesAplicativo`/
    `ConfiguracoesView`. Nesta mesma janela de trabalho, o item de menu genérico
    "Configurações" (`MenuItem.Configuracoes`) foi implementado, cobrindo preferências da
    máquina além dos toggles das heurísticas.
13. **Diagrama interativo da rede neural na tela de Treinamento do Controlador** (2026-10-23):
    visualização em tempo real dos pesos/ativações durante o treinamento, com tooltip por
    neurônio; correção de um bug de redesenho no cone de visão das torres na mesma tela
    (mutação de `PointCollection` no lugar não disparava `PropertyChanged`).
14. **Firmware do Arduino embute a rede neural treinada** (commit `dc6325b`): somente para
    diagnóstico/leitura, não substitui a lógica de seleção de torre do app.

---

## 3. Estado atual (o que já funciona)

- Radar em tempo real, seleção automática de torre, modo de acionamento demonstrativo —
  funcionalidade original, validada.
- Login multiusuário, 3 perfis, troca de senha, auditoria de ações/modos/detecções gravada em
  SQLite.
- Internacionalização (pt-BR/en-US) e tema (claro/escuro/sistema) trocáveis em runtime.
- Painel principal com cards de indicadores, arrastáveis/redimensionáveis, layout persistido
  por usuário e responsivo a mudanças de tamanho de janela.
- Aba de Configurações do Arduino: detectar `arduino-cli`, compilar sketch, monitor serial.
- Instalador Windows completo (`Setup.exe`), self-contained, com upgrade preservando
  configurações do usuário.
- `dotnet build`: 0 erros/avisos. Projeto de testes automatizados `tests/RadarTorres.Tests`
  (xUnit) existe e cresceu bastante além da aba do Arduino CLI: 18 arquivos de teste cobrindo
  algoritmo genético, rede neural, seleção de torre, persistência SQLite, visão computacional,
  perfis de alvo, termos de uso e outros (`AlgoritmoGeneticoTests.cs`,
  `TowerSelectionServiceTests.cs`, `PersistenciaSqliteTests.cs`, `RedeNeuralTests.cs`, etc. —
  145 testes aprovados na última sessão registrada em `LOG_SOLICITACOES.md`).
- **Todas as branches de trabalho consolidadas na `main`** — é o branch canônico agora.
- Zonas mortas: criação por clique/arraste no radar, bloqueio de torre/disparo, restrito a
  Administrador (ver item 7 da linha do tempo).
- Objetos detectados: tela de tabela real, com exportar/importar (CSV/XML/PDF).
- Ações realizadas: tela de tabela real (auditoria, somente leitura) — `AcoesRealizadasView`/
  `AcoesRealizadasViewModel` implementados e roteados.
- Histórico de modos: tela de tabela real (auditoria, somente leitura) — `HistoricoModosView`/
  `HistoricoModosViewModel` implementados e roteados.
- Usuários: CRUD completo (criar/editar/inativar/resetar senha), restrito a Administrador —
  `UsuariosView`/`UsuariosViewModel` implementados e roteados.

### 3.1 Funcionalidades centrais adicionadas após a versão anterior deste documento

Estas passaram a ser parte central do app e não constavam ainda neste resumo:

- **Visão computacional (módulo "Câmeras")**: `OnnxYoloDetectionService`
  (`Services/OnnxYoloDetectionService.cs`) implementa `IObjectDetectionService` rodando o
  modelo `tiny-yolov3-11.onnx` (ONNX Model Zoo) localmente via `Microsoft.ML.OnnxRuntime` —
  sem processo/serviço externo. Entrada 416x416 RGB; o modelo devolve caixas candidatas já
  filtradas por NMS interno; a classe COCO prevista é ignorada pelo resto do pipeline (o
  modelo serve só para propor regiões candidatas). Tela `CamerasView`/`CameraPanelView`
  mostra as webcams conectadas em tempo real (`MenuItem.Cameras`).
- **Controlador neuroevolutivo (`NeuralEvolution/`)**: conjunto de classes
  (`RedeNeural`, `AlgoritmoGenetico`, `ControladorHibrido`, `AmbienteTreinamento`,
  `AlvoTreinamento`, `TorreTreinamento`, `ExtratorDeRegras`, `EpisodioResultado`,
  `EstadoEpisodio`, `ParametrosTreinamento`) que treina por algoritmo genético uma rede
  neural pequena (arquitetura `[6,8,2]`: 6 entradas, 8 neurônios ocultos, 2 saídas) combinada
  a regras heurísticas (`ControladorHibrido`) para decidir o comportamento das torres. O
  genoma treinado é persistido em
  `src/RadarTorres.App/Resources/ControladorTreinado/genoma-controlador.json` e também
  embutido no firmware do Arduino (ver commit `dc6325b`, somente leitura/diagnóstico).
  Telas dedicadas: **"Treinamento do Controlador"** (`TreinamentoControladorView`,
  `MenuItem.TreinamentoControlador`) — roda o algoritmo genético, mostra episódios/alvos
  simulados em um Canvas e, desde 2026-10-23, um diagrama interativo da rede neural com
  tooltip por neurônio; e **"Estatísticas de Treinamento"** (`EstatisticasTreinamentoView`,
  `MenuItem.EstatisticasTreinamento`, exclusiva do Administrador) — evolução do fitness
  médio a cada 500 gerações, lido de `historico_treinamento_geracoes` (SQLite).
- **Perfis de Alvo**: tela `PerfilAlvoView`/`MenuItem.PerfilAlvo` — cadastro de cor/formato/
  tamanho que a detecção por visão computacional deve travar como alvo válido.
- **Desempenho**: tela `DesempenhoView`/`MenuItem.Desempenho` — métricas operacionais em
  tempo de execução.
- **Termos de Uso**: `TermosDeUsoWindow` — tela/janela de aceite, com testes dedicados
  (`TermosDeUsoTests.cs`).
- **As 5 regras heurísticas do controlador, cada uma desligável em "Configurações"**
  (sessão de 2026-10-23): anti-oscilação, priorização por urgência, corredor de tiro
  (considera a linha de tiro até o alvo, não só a posição dele), balanceamento de carga entre
  torres e predição de trajetória (mira na posição prevista 0,3 s à frente, usando a
  velocidade real do alvo calculada por diferença finita entre leituras). Documentado em
  `Docs/Tecnica/ALGORITMO_SELECAO_TORRE.md`, seção 7.
- **Identidade visual nova**: tokens de cor/tipografia/ícones documentados em
  `Docs/Projeto/IDENTIDADE_VISUAL.md`.

### Módulo removido

O módulo `ChamadoAjuda` (model, ViewModel, repositório, tela de formulário e entrada de menu na
TopBar) foi **removido inteiramente** do projeto — não existe mais nenhuma referência a ele em
`src/`.

### Telas ainda "em construção" (placeholder, navegação já funciona)

Nenhuma identificada no momento — o item de menu genérico "Configurações"
(`MenuItem.Configuracoes`, distinto de `MenuItem.ConfiguracoesArduino`) já está implementado e
roteado (`ConfiguracoesView`/`ConfiguracoesViewModel`), cobrindo preferências da máquina
(janela, idioma, tema, comportamento) e, mais recentemente, os toggles das 5 regras
heurísticas do controlador (ver seção 3.1).

### Bugs conhecidos (documentados em `Docs/Tecnica/DOCUMENTACAO_TECNICA.md`, seção "Bugs conhecidos")

1. `App.OnDispatcherUnhandledException` sem trava de reentrância — se o próprio `MessageBox`
   de erro lançar uma exceção, pode entrar em loop até estourar a pilha (`StackOverflowException`).
2. Falha nativa de renderização de texto (`DirectWrite`) observada uma vez em ambiente de
   automação (sem confirmação se ocorre em uso normal/desktop interativo real).

---

## 4. O que falta / próximos passos (roadmap)

Conforme plano em `Docs/Projeto/ETAPA1_FUNDACAO.md`, seção 6:

- ~~Fechar a Etapa 1: implementar as 4 telas de dados completas~~ — **concluído**: Ações
  realizadas, Histórico de modos e Usuários já estão implementadas (ver seção 3); o módulo de
  Chamados de Ajuda foi removido do projeto em vez de ganhar tela de listagem.
- **Etapa 2**: indicadores gráficos avançados, personalização completa de layout, filtros
  avançados, notificações.
- ~~Etapa 3 ("Qualidade"): recriar um projeto de testes automatizados~~ — **concluído**:
  `tests/RadarTorres.Tests` existe e tem 18 arquivos/145 testes (ver seção 3); seguem
  pendentes a revisão de segurança e ajustes de desempenho dessa etapa.
- ~~Migração de persistência: CSV → banco relacional~~ — **concluído**: persistência é SQLite
  (ver seção 2.1, item "Persistência").
- **Upload/gravação de firmware** pelo Arduino CLI (hoje só compila) — próximo passo natural
  da aba de Configurações do Arduino, mas só deve ser implementado mediante nova consulta ao
  usuário.
- Corrigir os dois bugs conhecidos listados acima.
- Validação manual (interativa, em desktop real) do arraste/redimensionamento dos cards do
  painel principal — só foi validada por build/testes automatizados, não por clique manual.

---

## 5. Como trabalhar neste projeto (convenções e preferências do usuário)

Relevante para qualquer IA/assistente que for continuar o trabalho:

- **Analisar antes de codar**: em pedidos grandes/ambíguos, apresentar um plano ou fazer
  perguntas de esclarecimento *antes* de alterar código — especialmente em decisões que
  afetam arquitetura ou dados existentes.
- **Nunca resolver conflitos de merge sozinho**: parar e perguntar ao usuário qual versão
  manter.
- **Nunca commitar/dar push sem autorização explícita** do usuário na sessão.
- **Nunca usar comandos destrutivos** (`git push --force`, `reset --hard` etc.) sem consultar
  antes.
- **Documentar cada sessão**: toda sessão de trabalho relevante deve ganhar uma entrada em
  `Docs/Projeto/LOG_SOLICITACOES.md` (pedido + resumo do entregue) — não substituir entradas
  anteriores, só acrescentar.
- **Não quebrar funcionalidade existente**: mudanças devem ser aditivas quando possível;
  qualquer remoção/alteração de comportamento já existente deve ser sinalizada.
- **Validar de ponta a ponta**: `dotnet build` (0 erros/avisos) e `dotnet test` antes de
  considerar uma entrega concluída; documentar quando a validação visual/manual ficar
  pendente (ex.: ambiente sem display interativo).
- O repositório é aberto como **vault do Obsidian** (`.obsidian/` na raiz) — arquivos podem
  ser renomeados/tocados por fora do Git enquanto uma sessão está em andamento; se isso
  acontecer, identificar via `git status` e perguntar como proceder antes de seguir.

---

## 6. Onde encontrar mais detalhes

| Documento | Conteúdo |
|---|---|
| [`README.md`](../README.md) | Visão geral, instalação, uso, estrutura de pastas |
| [`Docs/Tecnica/ARQUITETURA.md`](../Tecnica/ARQUITETURA.md) | Decisões arquiteturais e diagramas (mermaid) |
| [`Docs/Tecnica/DOCUMENTACAO_TECNICA.md`](../Tecnica/DOCUMENTACAO_TECNICA.md) | Referência de cada classe/serviço, limitações e bugs conhecidos |
| [`Docs/Tecnica/COMUNICACAO_ARDUINO.md`](../Tecnica/COMUNICACAO_ARDUINO.md) | Protocolo serial completo |
| [`Docs/Tecnica/ALGORITMO_SELECAO_TORRE.md`](../Tecnica/ALGORITMO_SELECAO_TORRE.md) | Matemática do radar e da seleção de torres |
| [`Docs/Projeto/INSTALADOR.md`](INSTALADOR.md) | Processo de criação do instalador |
| [`Docs/Projeto/ETAPA1_FUNDACAO.md`](ETAPA1_FUNDACAO.md) | Fundação multiusuário: arquitetura, tabelas, validação, roadmap |
| [`Docs/Projeto/IDENTIDADE_VISUAL.md`](IDENTIDADE_VISUAL.md) | Tokens de cor, tipografia e ícones da identidade visual do app |
| [`Docs/Tecnica/MODELO_DADOS.md`](../Tecnica/MODELO_DADOS.md) | Schema das tabelas CSV, relacionamentos, plano de migração SQL |
| [`Docs/Documentos_Entregaveis/Diagramas_e_requisitos/`](../Documentos_Entregaveis/Diagramas_e_requisitos/README.md) | RF01–RF32, RNF01–RNF30, diagrama de classes, de pacotes, DER atual/proposto, matriz de rastreabilidade |
| [`Docs/Projeto/LOG_SOLICITACOES.md`](LOG_SOLICITACOES.md) | Histórico cronológico dos pedidos feitos à IA (pode não refletir ainda as sessões mais recentes, ex.: migração SQLite, remoção do `ChamadoAjuda`, remoção dos testes) |
