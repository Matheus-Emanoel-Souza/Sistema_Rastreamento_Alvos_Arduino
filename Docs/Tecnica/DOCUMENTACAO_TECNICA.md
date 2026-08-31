# Documentação Técnica — Referência de Classes e Serviços

Para o racional arquitetural geral, veja [`ARQUITETURA.md`](ARQUITETURA.md). Este documento
descreve, classe a classe, o que cada uma faz, por que existe, como funciona internamente,
seus métodos principais e quem a consome.

---

## Models

### `Target` (`Models/Target.cs`)
**Função:** representa um alvo detectado, com posição, quadrante, torre associada e estado de
atividade. **Por que existe:** é a entidade central do sistema — tudo (radar, painel de
status, seleção de torre) gira em torno de instâncias de `Target`. **Como funciona:**
implementa `INotifyPropertyChanged` manualmente (via `SetField`) para que qualquer mudança de
`Angle`, `Distance`, `X`, `Y`, `Quadrant`, `SelectedTower` etc. seja refletida
automaticamente na UI sem código adicional. **Quem usa:** `TargetTrackingService` (cria/atualiza),
`TowerSelectionService` e `FireControlService` (leem/escrevem `SelectedTower`),
`RadarControl` e `MainWindow.xaml` (bindings de leitura).

### `Tower` (`Models/Tower.cs`)
**Função:** representa uma torre demonstrativa. **Por que existe:** separar a "torre" (posição
fixa, estado operacional) do "alvo" (dinâmico) permite que o algoritmo de seleção trate ambos
como entidades independentes, ligadas apenas no momento da decisão. **Como funciona:** também
implementa `INotifyPropertyChanged`; `PreferredQuadrant` é calculado uma vez, na carga da
configuração, a partir da própria posição da torre. **Quem usa:** `TowerSelectionService`
(dono da coleção, calcula `State`/`DistanceToTarget`), `RadarControl` (desenha), `FireControlService`
(muda `State` para `Firing`).

### `SensorReading` (`Models/SensorReading.cs`)
**Função:** DTO imutável que representa uma leitura já validada, mas ainda não associada a um
`Target`. **Por que existe:** para que `TargetTrackingService` não precise saber se a leitura
veio da serial real ou do simulador — ambos produzem `SensorReading`. **Quem usa:**
`MainViewModel` cria a partir de `TargetMessage` (serial) ou recebe pronto do
`SimulationService`; `TargetTrackingService.ProcessReading` consome.

### `SystemState.cs`
**Função:** agrupa os enums de estado do sistema: `SystemMode`, `ConnectionState`, `TowerState`,
`Quadrant`, `DataSource`. **Por que existe:** centraliza vocabulário compartilhado por várias
camadas, evitando "strings mágicas" espalhadas pelo código.

### `LogEntry` (`Models/LogEntry.cs`)
**Função:** uma linha imutável do console de eventos, com timestamp e nível de severidade.
**Quem usa:** `LoggingService` (cria), `MainWindow.xaml` (exibe via `ListBox`).

### `ArduinoBoardOption` / `ArduinoBoardCatalog` (`Models/ArduinoBoardOption.cs`)
**Função:** `ArduinoBoardOption` é um `record` (FQBN + nome amigável) selecionável no combo de
placas da aba Configurações do Arduino; `ArduinoBoardCatalog.DefaultBoards` é uma lista
curada de placas comuns (Uno, Nano, Mega, Leonardo, ESP32, NodeMCU), sempre disponível mesmo
sem o Arduino CLI instalado. **Quem usa:** `ArduinoSettingsViewModel`.

### `ArduinoCliInfo` / `ArduinoCliSource` (`Models/ArduinoCliInfo.cs`)
**Função:** resultado da detecção do executável `arduino-cli.exe` (encontrado ou não, caminho,
versão, origem — configuração salva, pasta do app, PATH, ou local comum de instalação).
**Quem usa:** `IArduinoCliLocatorService` (produz), `ArduinoSettingsViewModel` (consome).

### `ArduinoCliOutputLine` / `ArduinoCliOutputStream` (`Models/ArduinoCliOutputLine.cs`)
**Função:** uma linha do console de compilação (timestamp + origem: mensagem interna do
RadarTorres, stdout ou stderr do `arduino-cli`). **Quem usa:** `ArduinoCompilerService` (gera
via `IProgress<T>`), `ArduinoSettingsViewModel` (acumula em `CompileOutputLines`, com limite de
linhas).

### `ArduinoCompileResult` / `ArduinoCompileStatus` (`Models/ArduinoCompileResult.cs`)
**Função:** resultado de uma compilação (`Success`/`Failed`/`Cancelled`, código de saída,
duração). **Por que existe:** separa a interpretação de sucesso/falha (só código de saída +
cancelamento explícito) do texto bruto do console. **Quem usa:** `ArduinoCompilerService`
(produz), `ArduinoSettingsViewModel` (exibe o status final).

### `DashboardCardLayout` (`Models/DashboardCardLayout.cs`)
**Função:** posição (`RelX`/`RelY`) e tamanho (`RelWidth`/`RelHeight`) de um card do painel
principal, sempre como fração (0..1) do tamanho do `DashboardCanvas` — nunca em pixels
absolutos. **Por que existe:** é o único jeito de o layout continuar proporcional em qualquer
resolução de tela (ver `Docs/Tecnica/ARQUITETURA.md`, seção 5.2). **Quem usa:** `DashboardCanvas`
(produz/consome via `GetLayoutSnapshot`/`ApplyLayoutSnapshot`), `IDashboardLayoutRepository`
(persiste).

---

## Configuration

### `AppSettings` / `AppConfig` (`Configuration/`)
**Função:** modelo tipado para `appsettings.json` (`AppSettings`) e ponto de acesso estático
único (`AppConfig.Current`). **Por que existe:** evita espalhar `ConfigurationBuilder` por
todo o código; qualquer classe que precise de um valor configurável (baud rate, distância
mínima, torres, timeouts) lê de `AppConfig.Current`. **Quem usa:** praticamente todos os
serviços (`SerialCommunicationService`, `TargetTrackingService`, `TowerSelectionService`,
`SimulationService`, `MainViewModel`).

### `ArduinoCliSettings` (`Configuration/ArduinoCliSettings.cs`)
**Função:** modelo das preferências da aba Configurações do Arduino (caminho do CLI, último
sketch, FQBN, porta/baud, preferências do console). **Por que existe separado de
`AppSettings`:** `AppSettings` é lido uma única vez de `appsettings.json`, na pasta de
instalação (somente leitura em runtime); `ArduinoCliSettings` é gravável pelo usuário comum e
persistido em `%LocalAppData%\RadarTorres\arduino-settings.json` via
`IArduinoSettingsRepository`/`ArduinoSettingsRepository`. **Quem usa:**
`ArduinoSettingsViewModel`.

---

## Helpers

### `CoordinateConverter`
**Função:** converte entre coordenadas polares (sensor), cartesianas de mundo (metros) e
cartesianas de tela (pixels). **Por que existe:** é a única classe que "sabe" as fórmulas
trigonométricas — mantê-las centralizadas evita divergência entre o que o radar desenha e o
que a lógica de negócio (quadrantes, distâncias) calcula. **Métodos:**
`PolarToCartesian(angle, distance)`, `WorldToScreen(x, y, canvasSize, maxDistance)`,
`DegreesToRadians`, `RadiansToDegrees`, `NormalizeAngle`. **Quem usa:**
`TargetTrackingService` (mundo), `RadarControl` (tela).

### `DistanceCalculator`
**Função:** distância Euclidiana entre pontos/entidades. **Métodos:** `Euclidean(x1,y1,x2,y2)`,
`Between(Target, Tower)`, `DistanceFromBase(Target)`. **Quem usa:** `TowerSelectionService`.

### `QuadrantHelper`
**Função:** determina o quadrante (Q1–Q4) de um ponto e fornece o rótulo de exibição.
**Método principal:** `Determine(x, y)`. **Quem usa:** `TargetTrackingService` (ao criar/atualizar
alvos), `TowerSelectionService` (para calcular `PreferredQuadrant` de cada torre).

### `RelayCommand`
**Função:** implementação de `ICommand` para os botões da UI, sem depender de framework MVVM
externo. **Quem usa:** `MainViewModel` (todos os comandos expostos à `MainWindow`).

---

## Services

### `SerialProtocolParser`
**Função:** interpreta linhas recebidas do Arduino em objetos `SerialMessage` tipados e
constrói as strings de comando PC→Arduino. **Por que existe:** é o único lugar que conhece o
formato textual do protocolo (ver `COMUNICACAO_ARDUINO.md`) — trocar o protocolo no futuro
significa alterar apenas este arquivo. **Métodos principais:** `TryParse(rawLine, out message)`,
`BuildSystemOn/Off`, `BuildModeDetection/Auto`, `BuildSetMinDistance/MaxDistance`, `BuildFire`.
**Quem usa:** `SerialCommunicationService` (parse de entrada), `MainViewModel` (construção de
comandos de saída).

### `SerialCommunicationService` (`ISerialCommunicationService`)
**Função:** camada de transporte serial: listar portas, conectar/desconectar, ler
continuamente em segundo plano, enviar comandos, detectar perda de conexão (watchdog).
**Por que existe:** isola toda a complexidade de `System.IO.Ports` e de concorrência (ver
`ARQUITETURA.md`, seção 4) do resto do sistema, que só enxerga eventos e um contrato simples.
**Métodos principais:** `GetAvailablePorts()`, `ConnectAsync(port, baud, ct)`, `Disconnect()`,
`SendCommandAsync(command)`. **Eventos:** `MessageReceived`, `ConnectionStateChanged`,
`CommunicationError`. **Quem usa:** `MainViewModel` (único consumidor direto).

### `TargetTrackingService` (`ITargetTrackingService`)
**Função:** mantém o conjunto de alvos ativos; decide se uma leitura corresponde a um alvo já
existente (atualiza) ou a um novo (cria); remove alvos que expiraram (timeout). **Por que
existe:** centraliza a regra "não recriar um alvo a cada pacote com o mesmo ID", pedida
explicitamente no escopo do projeto, e o correspondente controle de concorrência (múltiplas
fontes de leitura escrevendo ao mesmo tempo). **Métodos principais:**
`ProcessReading(SensorReading)`, `PurgeStaleTargets()`, `ClearAll()`. **Eventos:**
`TargetCreated`, `TargetUpdated`, `TargetRemoved`. **Quem usa:** `MainViewModel` (chama
`ProcessReading` e assina os eventos), `RadarControl` (bind direto de `Targets`).

### `TowerSelectionService` (`ITowerSelectionService`)
**Função:** implementa o algoritmo de seleção de torre (ver `ALGORITMO_SELECAO_TORRE.md`).
**Por que existe:** isola a regra de negócio mais importante do projeto em uma classe sem
nenhuma dependência de UI, facilitando tanto a explicação na defesa do TCC quanto testes
automatizados futuros. **Métodos principais:** `SelectTowerFor(Target)`,
`RecomputeTowerStates(IEnumerable<Target>)`. **Quem usa:** `MainViewModel`.

### `FireControlService` (`IFireControlService`)
**Função:** aplica a regra de segurança (distância mínima) e executa/simula o acionamento
demonstrativo. **Por que existe:** concentra em um único lugar a decisão "pode ou não pode
acionar", que é a parte mais sensível do sistema — nenhum outro componente decide isso.
**Métodos principais:** `Authorize(Target, minDistance)`, `TryFireAsync(Target, serial?,
simulationMode, minDistance)`. **Quem usa:** `MainViewModel` (acionamento manual e automático).

### `SimulationService` (`ISimulationService`)
**Função:** gera e movimenta alvos fictícios, produzindo o mesmo DTO (`SensorReading`) que a
leitura real. **Por que existe:** permitir desenvolvimento e demonstração completos sem
hardware. **Métodos principais:** `Start(count?)`, `Stop()`, `AddRandomTarget()`,
`RemoveTarget(id)`. **Evento:** `ReadingGenerated`. **Quem usa:** `MainViewModel`.

### `LoggingService` (`ILoggingService`)
**Função:** console de eventos com timestamp, thread-safe, consumido por toda a aplicação.
**Por que existe:** dar visibilidade textual e cronológica de tudo que acontece no sistema,
sem acoplar quem gera o log a nenhum componente visual específico. **Métodos:** `Info`,
`Success`, `Warning`, `Error`, `Clear`. **Quem usa:** todos os serviços e o `MainViewModel`.

### `ArduinoCliLocatorService` (`IArduinoCliLocatorService`)
**Função:** localiza `arduino-cli.exe` no computador (caminho salvo → pasta do aplicativo →
`PATH` → locais comuns de instalação), obtém a versão (`arduino-cli version`) e lista placas
instaladas (`arduino-cli board listall`). **Por que existe:** o Arduino CLI é uma ferramenta
externa, não faz parte do runtime do RadarTorres — esta classe isola toda a lógica de
detecção (só leitura de disco/PATH e execução do CLI já instalado, nunca download) do resto da
aba. **Métodos principais:** `Locate(savedPath)`, `GetVersionAsync(cliPath, ct)`,
`ListInstalledBoardsAsync(cliPath, ct)`. **Quem usa:** `ArduinoSettingsViewModel`.

### `ArduinoCompilerService` (`IArduinoCompilerService`)
**Função:** compila um sketch via `arduino-cli compile --fqbn <fqbn> <pasta-do-sketch>` como
processo filho assíncrono e cancelável, repassando `stdout`/`stderr` em tempo real. **Por que
existe:** centraliza a montagem segura de argumentos (`ProcessStartInfo.ArgumentList`, nunca
concatenação de string) e a interpretação de sucesso/falha (código de saída, nunca o texto de
`stderr` isolado) em um único lugar testável sem depender do Arduino CLI instalado (ver
`BuildCompileProcessStartInfo`/`ExecuteAsync`/`DetermineStatus`, `internal` para uso em
`RadarTorres.Tests`). **Método principal:** `CompileAsync(ArduinoCompileRequest,
IProgress<ArduinoCliOutputLine>, CancellationToken)`. **Quem usa:** `ArduinoSettingsViewModel`.

### `ArduinoSettingsRepository` (`IArduinoSettingsRepository`)
**Função:** carrega/grava `ArduinoCliSettings` em JSON, em
`%LocalAppData%\RadarTorres\arduino-settings.json` por padrão (o construtor aceita um caminho
alternativo, usado nos testes). **Quem usa:** `ArduinoSettingsViewModel`.

### `DashboardLayoutRepository` (`IDashboardLayoutRepository`)
**Função:** carrega/grava o layout dos cards do painel principal
(`Dictionary<string, DashboardCardLayout>`, chave = `DashboardCard.CardId`) em JSON, em
`%LocalAppData%\RadarTorres\dashboard-layout.json` por padrão (mesmo padrão de
`ArduinoSettingsRepository`, inclusive o caminho alternativo para testes). **Métodos:**
`Load()` (retorna `null` se o arquivo nunca existiu), `Save(layout)`, `Clear()` (usado por
"Restaurar layout padrão"). **Quem usa:** `PainelPrincipalView.xaml.cs` (nunca a ViewModel —
ver seção Views).

---

## ViewModels

### `MainViewModel`
**Função:** orquestra todos os serviços acima e expõe propriedades/comandos para a
`MainWindow`. **Por que existe:** é a fronteira MVVM — nenhuma regra de negócio deveria
"vazar" para dentro dela; ela só decide *quando* chamar cada serviço. **Fluxo de dados:**
descrito em detalhe no diagrama de sequência de `ARQUITETURA.md`. **Quem usa:**
`MainWindow.xaml` (via `DataContext`).

### `ArduinoSettingsViewModel`
**Função:** orquestra as três seções da aba Configurações do Arduino (ambiente/detecção do
CLI, compilação, monitor serial). **Por que existe:** mesma fronteira MVVM de `MainViewModel`
— nenhuma chamada a `Process`/arquivo/porta serial acontece na View. **Reuso importante:**
recebe a **mesma instância Singleton** de `ISerialCommunicationService` usada por
`MainViewModel`, então não existe uma segunda conexão serial concorrente (ver
`Docs/Tecnica/ARQUITETURA.md`, seção 5.1, e `Docs/Tecnica/COMUNICACAO_ARDUINO.md`, seção 8.4).
**Métodos/comandos principais:** `AutoDetectCommand`, `RefreshBoardsAndPortsCommand`,
`CompileCommand`/`CancelCompileCommand`, `ConnectCommand`/`DisconnectCommand`. **Quem usa:**
`ArduinoSettingsView.xaml` (via `DataContext`).

### `PainelPrincipalViewModel` (trecho de layout)
**Função (nesta funcionalidade):** expõe `RestoreDefaultLayoutCommand` e o evento
`RestoreLayoutRequested`, disparado quando o comando executa. **Por que não faz mais do que
isso:** posição/tamanho de elementos visuais (`DashboardCanvas`) não é estado de domínio —
a ViewModel não referencia nenhum tipo de `System.Windows.Controls`. Quem efetivamente aplica
a restauração é o code-behind da View (ver `DashboardCanvas`/`DashboardCard` em Views abaixo).

---

## Views

### `DashboardCanvas` (`Views/Shared/DashboardCanvas.cs`)
**Função:** `Canvas` customizado que hospeda os `DashboardCard` do painel principal e decide
se um arraste/redimensionamento proposto é válido. **Por que existe:** é o único lugar que
conhece as regras de posicionamento do painel — nenhum `DashboardCard` sabe sobre os outros.
**Como funciona:** guarda posição/tamanho como pixels em `Canvas.Left`/`Top`/`Width`/`Height`
(a fonte de verdade em tempo de execução), mas produz/consome `DashboardCardLayout` (frações)
para persistência; em `SizeChanged`, reescala todos os cards pela razão entre o tamanho novo e
o anterior, preservando a fração ocupada por cada um. **Métodos principais:**
`RequestMove(card, deltaX, deltaY)`, `RequestResize(card, deltaWidth, deltaHeight)` (ambos
recusam a mudança se ela colidir com outro card via `Rect.IntersectsWith` ou sair dos limites
do canvas), `GetLayoutSnapshot()`/`ApplyLayoutSnapshot(layout)`, `ResetToDefaultLayout()`
(grade fixa de 3 colunas). **Evento:** `LayoutChanged` (disparado por `DashboardCard` ao soltar
o mouse — `DragCompleted`). **Quem usa:** `PainelPrincipalView.xaml` (hospeda os 6 cards do
painel), `PainelPrincipalView.xaml.cs` (carrega/salva o layout).

### `DashboardCard` (`Views/Shared/DashboardCard.xaml`)
**Função:** card individual do painel principal, com cabeçalho arrastável (`Thumb` com
`Cursor=SizeAll`) e alça de redimensionamento no canto inferior direito (`Thumb` com
`Cursor=SizeNWSE`). **Por que existe:** encapsula a chrome visual (borda, cabeçalho, alça) uma
única vez, para os 6 cards do painel reutilizarem. **Como funciona:** não decide nada sozinho —
cada `Thumb.DragDelta` só repassa o gesto para `(Parent as DashboardCanvas).RequestMove/
RequestResize`; o conteúdo interno de cada card é definido por quem usa o controle e mapeado
para a propriedade `CardContent` via `[ContentProperty]`. **Propriedades:** `CardId` (chave
estável usada na persistência), `Title`, `CardContent`. **Quem usa:**
`PainelPrincipalView.xaml` (6 instâncias, uma por indicador do painel).

### `RadarControl`
**Função:** desenha o radar circular (ver README, seção 9, para o funcionamento visual
completo). **Por que existe:** encapsula toda a lógica de desenho/posicionamento fora da
`MainWindow`, como um controle reutilizável e testável isoladamente. **Quem usa:**
`MainWindow.xaml`.

### `MainWindow`
**Função:** janela principal; monta o layout 65%/35% pedido no escopo e serve como
"composition root" (instancia os serviços concretos e a `MainViewModel`). **Por que a
composição de serviços está aqui e não em um container de DI:** o projeto é de porte
pequeno/médio; um container completo (`Microsoft.Extensions.DependencyInjection`) é o próximo
passo natural caso o projeto cresça (ver seção de próximos passos no README).

> **Nota:** desde a introdução da Shell multiusuário (barra lateral + navegação), o conteúdo
> descrito acima em `MainWindow` vive em `Views/MonitoramentoView.xaml`, hospedada pela
> `Views/Shell/ShellWindow.xaml`; a composição de serviços descrita passou para `App.xaml.cs`
> (ver `Docs/Projeto/ETAPA1_FUNDACAO.md`). A `ArduinoSettingsView.xaml` segue exatamente o mesmo
> padrão — code-behind mínimo (diálogos de arquivo, rolagem automática do console), tudo o
> mais em `ArduinoSettingsViewModel`.

---

## Converters (`Converters/*.cs`)

Pequenos `IValueConverter` usados apenas para tradução de exibição no XAML — nenhum contém
lógica de negócio: `EnumToBooleanConverter` (RadioButtons de modo), `ConnectionStateToBrushConverter`,
`QuadrantToLabelConverter`, `SystemModeToLabelConverter`, `LogLevelToBrushConverter`,
`NullToVisibilityConverter`, `ArduinoCompileStatusToBrushConverter` (status final da
compilação), `ArduinoOutputStreamToBrushConverter` (linhas do console de compilação por
origem), `BoolToFoundBrushConverter` (indicador "CLI encontrado/não encontrado").

---

## Limitações e próximos passos

**Bugs conhecidos (não corrigidos ainda):**

1. **`App.OnDispatcherUnhandledException` pode entrar em loop infinito e derrubar o processo
   com `StackOverflowException`.**
   * **Onde:** `src/RadarTorres.App/App.xaml.cs`, método `OnDispatcherUnhandledException`.
   * **Causa raiz:** o tratador global de exceções da UI chama `MessageBox.Show(...)` para
     avisar o usuário. Se a *própria* exibição do `MessageBox` (que também depende de
     layout/renderização de texto do WPF) disparar uma nova exceção, o WPF a captura e chama
     `OnDispatcherUnhandledException` de novo — que tenta mostrar outro `MessageBox` — que
     falha de novo — e assim por diante, sem nenhuma trava contra reentrância. Isso estoura a
     pilha (`StackOverflowException`), que o CLR não deixa capturar: o processo morre
     imediatamente, sem a mensagem de erro amigável que o tratador deveria mostrar.
   * **Como foi observado:** ao rodar `dotnet run --project src/RadarTorres.App` em uma sessão
     sem um desktop interativo "de verdade" (ver bug 2 abaixo), uma falha de renderização de
     texto entrou nesse ciclo e derrubou o processo com `Stack overflow.` no console. O stack
     trace do crash mostra dezenas de repetições do mesmo ciclo
     `Dispatcher.CatchException → App.OnDispatcherUnhandledException → MessageBox.Show → nova
     exceção → Dispatcher.CatchException → ...`, sem nenhum frame do código da aplicação além
     do próprio tratador — só WPF interno.
   * **Impacto:** baixo em uso normal (a maioria das exceções não acontece durante a própria
     exibição do `MessageBox`), mas quando acontece o resultado é o pior possível: o app morre
     sem log e sem a mensagem que o usuário deveria ver.
   * **Sugestão de correção:** adicionar uma trava de reentrância (ex.: um `bool
     _isHandlingUnhandledException` verificado no início do método) e, se já estiver
     tratando uma exceção, pular o `MessageBox.Show` e ir direto para um log em arquivo (ou
     `Environment.FailFast` com a mensagem original) em vez de tentar mostrar UI de novo. O
     mesmo raciocínio vale para `OnDomainUnhandledException`, que também chama `MessageBox.Show`.

2. **Falha de renderização de texto (`DirectWrite`/`TextAnalyzer.GetGlyphs`) observada ao
   rodar o app fora de uma sessão de desktop interativa real.**
   * **Onde observado:** ao compilar e rodar o app através desta sessão de automação (sem um
     usuário logado interativamente em uma sessão de desktop do Windows com GPU/driver de
     vídeo completo), o processo `RadarTorres.App` chegou a abrir a janela de login
     normalmente, mas em algum momento de *layout* (`TextBlock.MeasureOverride` →
     `TextFormatterImp.FormatLine` → `TextAnalyzer.GetGlyphs`) uma exceção nativa foi lançada
     durante o *shaping* de texto — o que disparou o bug 1 acima.
   * **Hipótese (não confirmada):** este ambiente de automação provavelmente não tem uma
     sessão de desktop interativa "completa" (Window Station 0 com GPU/DirectWrite plenamente
     funcional), algo que WPF exige para desenhar texto. Não há evidência de que isso
     aconteça em uma sessão normal de desktop do Windows (uso interativo comum), mas **ainda
     não foi validado rodando o app diretamente em uma sessão de desktop real** — só nesta
     automação.
   * **Ação necessária:** validar se o app roda normalmente (sem esse erro) quando executado
     diretamente pelo usuário, fora de qualquer automação — se sim, o bug 2 é uma limitação do
     ambiente de teste e não do aplicativo; se o erro se repetir em uso normal, investigar a
     fundo (drivers de vídeo, fontes instaladas, hardware acceleration do WPF — ver
     `RenderOptions`/`ProcessRenderMode`).
   * **Nenhum frame do crash pertence ao código da aplicação** (nada de `DashboardCanvas`,
     `DashboardCard`, `PainelPrincipalView`, etc.) — todos os frames são internos do
     WPF/DirectWrite e do tratador do bug 1, o que descarta (mas não prova 100%) uma causa
     ligada à funcionalidade de layout arrastável/redimensionável do painel principal.

**Limitações conhecidas desta entrega:**

* O algoritmo de seleção de torre não considera ângulo de cobertura/orientação física da
  torre, apenas distância e quadrante preferencial.
* Não há reconexão automática após queda de conexão (o campo `ReconnectAttempts` já existe na
  configuração, mas a lógica de retry ainda não foi implementada — ver abaixo).
* Testes automatizados: o projeto `tests/RadarTorres.Tests` (xUnit) cobre a aba Configurações
  do Arduino (localização do CLI, montagem segura de argumentos, interpretação de código de
  saída, cancelamento, persistência, limite de linhas dos consoles e disputa pela porta
  serial) — ver `Docs/Projeto/LOG_SOLICITACOES.md`. O restante da aplicação (algoritmo de seleção de
  torre, parser serial, etc.) ainda não tem testes automatizados, embora a arquitetura
  (serviços com interface, sem dependência de UI) tenha sido desenhada especificamente para
  viabilizá-los facilmente.
* A aba Configurações do Arduino implementa apenas **compilação** do sketch — gravação/upload
  do firmware para a placa (`arduino-cli upload`) não foi implementada nesta entrega.
* O acionamento demonstrativo é, por definição do escopo, apenas indicativo (log + comando
  serial opcional) — não há nenhuma integração com atuadores de alta potência.

**Próximos passos sugeridos para a integração com sensores reais:**

1. Substituir a lógica de `ArduinoSimulation.ino` pela leitura real dos sensores de
   distância/ângulo, mantendo exatamente o mesmo formato de mensagem `TARGET;ID=;ANGLE=;DIST=`.
2. Calibrar o mapeamento ângulo↔posição física do sensor (motor de varredura, array de
   sensores, etc.) antes de enviar o valor de `ANGLE`.
3. Implementar reconexão automática com backoff em `SerialCommunicationService`, usando
   `SerialSettings.ReconnectAttempts`.
4. Adicionar testes unitários para `TowerSelectionService`, `QuadrantHelper`,
   `CoordinateConverter` e `SerialProtocolParser` (todos puros, sem I/O).
5. Avaliar métricas reais de latência ponta-a-ponta (sensor → radar) para validar se
   `RadarSettings.RefreshRateMs` e `SimulationSettings.GenerationIntervalMs` precisam de
   ajuste fino para o hardware definitivo.
