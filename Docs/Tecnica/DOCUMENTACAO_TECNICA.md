Funcionalidade: referência técnica das classes e serviços do código-fonte.

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
`Target`. **Quem usa:** `MainViewModel` cria a partir de `TargetMessage` (serial);
`TargetTrackingService.ProcessReading` consome.

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
`MainViewModel`).

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
**Função:** aplica a regra de segurança (distância mínima) e executa o acionamento
demonstrativo. **Por que existe:** concentra em um único lugar a decisão "pode ou não pode
acionar", que é a parte mais sensível do sistema — nenhum outro componente decide isso.
**Métodos principais:** `Authorize(Target, minDistance)`, `TryFireAsync(Target, serial?,
simulationMode, minDistance)`. **Quem usa:** `MainViewModel` (acionamento manual e automático).

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

### Visão computacional — detecção de objetos (`Services/IObjectDetectionService.cs`, `OnnxYoloDetectionService.cs`, pasta `ComputerVision/`)
**Função:** `IObjectDetectionService.Detect(Bitmap frame)` devolve apenas caixas
(posição/tamanho/confiança) encontradas num quadro de câmera já decodificado; não decide o que é
ou não o alvo procurado — isso é responsabilidade de `IAlvoVisualService`. **Por que existe
separado:** permite trocar o modelo/motor de inferência sem tocar na lógica de negócio (mesmo
racional de `ICameraCaptureService` para a câmera em si). **Implementação:**
`OnnxYoloDetectionService` roda o modelo `tiny-yolov3-11.onnx` (ONNX Model Zoo) localmente via
`Microsoft.ML.OnnxRuntime`, sem nenhum serviço/processo externo — o `.onnx` fica em
`ComputerVision/Models/YoloModel/tiny-yolov3-11.onnx` (copiado para a pasta de saída pelo
`.csproj`). Entrada `input_1` [1,3,416,416] float32 RGB 0–1 (quadro chega em BGR24, canais
trocados no pré-processamento) + `image_shape` [1,2]; saídas já filtradas por NMS pelo próprio
modelo (a classe COCO escolhida é ignorada pelo restante do pipeline — o modelo só propõe
regiões candidatas). **Quem usa:** `IAlvoVisualService`/`RastreamentoVisualService` (compara cada
caixa com o `PerfilAlvo` ativo), consumido pelos cards de detecção de `CamerasView`/
`MonitoramentoView`.

### `IAlvoVisualService` / `AlvoVisualService`
**Função:** decide, para um quadro de câmera, se alguma detecção de `IObjectDetectionService`
bate com o `PerfilAlvo` ativo (cor/formato/tamanho) e, se bater, estima a distância do alvo.
**Por que existe:** fica entre a detecção pura de caixas e a ViewModel do card "Detecção por
Câmera" — pura lógica de negócio, sem referência a WPF (mesmo princípio de
`TowerSelectionService`). **Método principal:** `AvaliarFrame(Bitmap frame)` → `AlvoVisualAvaliacao`
(`Resultado` é `null` se não há perfil ativo ou nenhuma detecção bate o suficiente). **Quem
usa:** `CameraSlotViewModel`, `PerfilAlvoViewModel` (modal de cadastro).

### `ICameraQuadranteService` / `CameraQuadranteService`
**Função:** mantém a associação "qual câmera monitora qual quadrante" (`CameraQuadrante`, Q1–Q4),
validando a regra "uma câmera, um quadrante". **Métodos principais:** `Associacoes`,
`ObterQuadrante(dispositivoId)`, `ObterCameraDoQuadrante(quadrante)`, `Atribuir(camera,
quadrante)` (evento `AssociacoesAlteradas` para as telas abertas se realinharem). **Quem usa:**
`CamerasViewModel` (atribuição na tela), detecção por visão (rotula a ocorrência com o quadrante
de quem a enxergou).

### `IOcorrenciaDeteccaoService` / `OcorrenciaDeteccaoService`
**Função:** converte detecções por câmera em ocorrências no histórico ("Objetos Detectados"),
aplicando a regra central: enquanto o mesmo objeto continuar sendo acompanhado no mesmo
quadrante e não for abatido, não se cria ocorrência nova — o que avança é só o "visto por
último" (um mesmo alvo cadastrado pode gerar até quatro ocorrências, uma por quadrante, sem
duplicar dentro do mesmo quadrante). **Métodos principais:** `RegistrarDeteccao(ContextoDeteccao)`,
`RegistrarRodadaSemDeteccao`, `EncerrarCamera`, `MarcarAbatePendente` (chamado por
`IFireControlService` ao confirmar abate). **Evento:** `ConfiancaAtualizada` (pontuação 0–1 por
quadrante, usada por `Target.Confidence`/regra de disparo por certeza mínima). **Quem usa:**
`CameraSlotViewModel`, `MonitoramentoView`/`MainViewModel` (card de detecção), testado em
`OcorrenciaDeteccaoServiceTests`, `DeteccaoConcorrenteTests`.

### `IPerfilAlvoRepository` / `PerfilAlvoRepository`, `IPerfilAlvoService` / `PerfilAlvoService`
**Função:** CRUD de `PerfilAlvo` (nome, cor, formato, dimensões, fotografias, mínimo de fotos) —
persistência via SQLite — e exposição do perfil atualmente ativo (`PerfilAtivo`), que é o que a
detecção por visão computacional deve travar. **Métodos principais do serviço:** `Add`, `Update`,
`Remove`, `DefinirAtivo`, `PerfilAtivo`. **Quem usa:** `PerfilAlvoViewModel` (tela "Perfis de
Alvo"), `IAlvoVisualService` (consulta o perfil ativo), testado em `PerfilAlvoServiceTests`,
`CadastroAlvoModalTests`, `ValidacaoFotoAlvoTests`.

### `IDesempenhoMonitorService` / `DesempenhoMonitorService`
**Função:** coleta, em tempo real, a taxa de atualização efetiva do radar e o tempo do ciclo
detecção → seleção de torre, reportados por `RadarControl`/`MainViewModel` sem que esses
componentes tenham referência à tela "Desempenho". **Quem usa:** `DesempenhoViewModel`.

### `ITermosDeUsoService` / `TermosDeUsoService`
**Função:** expõe o documento vigente de Termos de Uso (`DocumentoTermos`, já com blocos
classificados para exibição) e o nome de arquivo sugerido para exportação. **Quem usa:**
`TermosDeUsoViewModel`, `ConfiguracoesViewModel` (link para reabrir os termos), testado em
`TermosDeUsoTests`.

### `ResolvedorFontesPdf`
**Função:** helper de resolução de fontes usado na geração de PDF (Termos de Uso / exportação de
"Objetos Detectados") via PdfSharp. **Quem usa:** `ObjetoDetectadoExportService.ExportPdf`,
exportação de PDF da janela de Termos de Uso.

### `ICameraCaptureServiceFactory`/`CameraCaptureServiceFactory`, `ICameraCaptureService`/`CameraCaptureService`, `ICameraDeviceEnumerator`/`CameraDeviceEnumerator`
**Função:** abstraem a captura de vídeo de uma webcam Windows (enumerar dispositivos, abrir/
fechar sessão de captura, entregar quadros decodificados) sem acoplar a lógica de negócio ao
driver/biblioteca de captura. A fábrica cria uma instância de captura por painel/uso (tela
"Câmeras" tem 4 painéis independentes; o modal de "Perfis de Alvo" abre sua própria sessão). **Quem
usa:** `CamerasViewModel`/`CameraSlotViewModel`, `PerfilAlvoViewModel`.

### `IControladorTreinadoProvider` / `ControladorTreinadoProvider`
**Função:** lê o melhor genoma treinado (ver `NeuralEvolution/` em `ARQUITETURA.md`, seção 5.6),
persistido em `Resources/ControladorTreinado/genoma-controlador.json`, e reconstrói o
`RedeNeural`/`ControladorHibrido` correspondente. **Quem usa:** `TreinamentoControladorViewModel`
(para continuar evoluindo a partir do estado salvo), `EstatisticasTreinamentoViewModel`, testado
em `ControladorTreinadoProviderTests`.

### `IRedeEmbutidaSincronizadorService` / `RedeEmbutidaSincronizadorService`
**Função:** sincroniza o genoma treinado para dentro do sketch do Arduino, embutido apenas para
diagnóstico/leitura — o firmware não usa esse valor para decidir o acionamento real, que
continua em `TowerSelectionService`/`FireControlService` puros. **Quem usa:**
`TreinamentoControladorViewModel`/`ConfiguracoesViewModel` (ação de sincronizar).

### `ConfiguracoesAplicativoRepository` (`IConfiguracoesAplicativoRepository`)
**Função:** carrega/grava `ConfiguracoesAplicativo` (preferências desta máquina: tamanho/posição
de janela, idioma, tema, comportamentos de disparo) em disco. **Por que existe separado de
`ArduinoCliSettings`/`AppSettings`:** é a persistência própria da tela "Configurações" genérica
(distinta de "Configurações do Arduino"). **Quem usa:** `ConfiguracoesViewModel`.

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

### `CamerasViewModel` / `CameraSlotViewModel`
**Função:** ViewModel do módulo "Câmeras" — orquestra até 4 painéis independentes
(`CameraSlotViewModel`), cada um com sua própria webcam selecionável, visualização em tempo real
(grade 2x2 na View) e detecção por visão rodando na sua própria câmera, registrando as
ocorrências no quadrante atribuído a ela. **Por que existe separado por painel:** cada
`CameraSlotViewModel` não sabe nada sobre os outros; `CamerasViewModel` só consulta a lista de
câmeras do Windows (`ICameraDeviceEnumerator`) e garante a regra de exclusividade — uma mesma
câmera física nunca reservada em mais de um painel ao mesmo tempo. **Sem gravação de vídeo e sem
integração direta com o Arduino/torres** nesta entrega. **Quem usa:** `CamerasView.xaml`.

### `PerfilAlvoViewModel`
**Função:** ViewModel da tela "Perfis de Alvo" — cadastra, edita e remove os perfis (cor +
formato + dimensões + fotografias) que a detecção por visão computacional deve travar, e
escolhe qual está ativo. **Como funciona:** cadastro/edição acontecem num modal com etapas
(`EtapaCadastroAlvo`: Informacoes → Fotografias → Dimensoes → Revisao), sempre sobre um
rascunho — nada é gravado até concluir, o que permite desistir no meio sem deixar o perfil num
estado intermediário; editar nunca cria registro novo (atualiza pelo `Id`, preservando o vínculo
com o histórico de detecções). A câmera só é ligada enquanto o modal está aberto (mesma
infraestrutura do módulo "Câmeras", sessão própria). O botão de cadastrar fica desabilitado
enquanto não houver `PerfilAlvo.MinimoFotos` fotos válidas (a checagem de verdade é no serviço).
**Quem usa:** `PerfilAlvoView.xaml`, testado em `CadastroAlvoModalTests`, `ValidacaoFotoAlvoTests`.

### `DesempenhoViewModel`
**Função:** ViewModel da tela "Desempenho" — mostra em tempo real a taxa de atualização do
radar e o tempo do ciclo detecção → seleção de torre, lidos de `IDesempenhoMonitorService`.
**Como funciona:** amostra a cada 300 ms via `DispatcherTimer`, só enquanto a tela está visível
(`StartMonitoring`/`StopMonitoring` chamados pela View no Loaded/Unloaded); `ZerarEstatisticasCommand`
zera o monitor e atualiza os valores exibidos. **Quem usa:** `DesempenhoView.xaml`.

### `TermosDeUsoViewModel`
**Função:** ViewModel da janela "Termos de Uso" — entrega os blocos já classificados do
documento vigente (`ITermosDeUsoService.Obter()`) e coordena a exportação em PDF. **Como
funciona:** nenhum diálogo de arquivo aqui — só dispara o evento `SalvarPdfSolicitado` com o
nome sugerido; quem mostra o `SaveFileDialog` e devolve o caminho é a View (mesmo padrão de
`ObjetosDetectadosViewModel`); `EstaGerandoPdf` desabilita o botão e evita cliques duplicados
durante a geração. **Quem usa:** `TermosDeUsoWindow.xaml`.

### `TreinamentoControladorViewModel`
**Função:** ViewModel da tela "Treinamento do Controlador" — comanda o `AlgoritmoGenetico`
(`NeuralEvolution/`) em segundo plano, o mais rápido possível sem travar a UI, enquanto anima
num `Canvas` 2D, em tempo real, o melhor genoma encontrado até agora. **Como funciona:** dois
laços independentes sobre o mesmo `AmbienteTreinamento` — um rápido (fitness, em `Task`
dedicada/cancelável) e um visual de prévia (`DispatcherTimer`); registra marcos de fitness médio
a cada 500 gerações (`IMarcoFitnessTreinamentoRepository`) e um "instantâneo" completo do
comportamento do melhor genoma a cada 1000 gerações (`IInstantaneoComportamentoRepository`);
expõe as coleções visuais do diagrama da rede (`NeuroniosVisuais`/`ConexoesVisuais`) e a lista
"Regras aprendidas" (via `ExtratorDeRegras`). **Quem usa:** `TreinamentoControladorView.xaml`,
testado indiretamente por `AlgoritmoGeneticoTests`, `AmbienteTreinamentoTests`,
`EvolucaoDoTreinamentoTests`.

### `EstatisticasTreinamentoViewModel`
**Função:** ViewModel da tela "Estatísticas de Treinamento" — exclusiva do Administrador (ver
`IPermissionService`): mostra a evolução de longo prazo do fitness médio a cada 500 gerações
(`IMarcoFitnessTreinamentoRepository`), o histórico recente geração a geração (mesma fonte da
tela de treinamento) e os instantâneos de comportamento a cada 1000 gerações. **Por que existe
separada de `TreinamentoControladorViewModel`:** ali o foco é acompanhar o treinamento rodando
agora; aqui é analisar a tendência de longuíssimo prazo sem o ruído geração a geração. **Quem
usa:** `EstatisticasTreinamentoView.xaml`.

### `ConfiguracoesViewModel`
**Função:** ViewModel da tela "Configurações" (genérica — distinta de "Configurações do
Arduino"): edita as `ConfiguracoesAplicativo` desta máquina (tamanho/posição de janela, idioma,
tema, comportamento de disparo) e grava em disco para a próxima execução já abrir nas condições
escolhidas. **Como funciona:** trabalha sobre uma cópia (`ConfiguracoesAplicativo.Clonar()`) e só
grava no "Salvar" — "Cancelar" tem o que desfazer e mexer em um controle não altera o
comportamento do app em uso antes de confirmar; expõe também acesso aos Termos de Uso e ao
histórico/marcos de treinamento do controlador (para sincronizar o genoma embutido no firmware).
**Quem usa:** `ConfiguracoesView.xaml` (item de menu `IPermissionService.MenuItem.Configuracoes`).

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

### `CamerasView.xaml`
**Função:** grade 2x2 com os 4 painéis de câmera (`CameraSlotViewModel`) do módulo "Câmeras" —
seleção de webcam por painel, visualização em tempo real e indicação de detecção/quadrante.
**DataContext:** `CamerasViewModel`.

### `PerfilAlvoView.xaml`
**Função:** lista os perfis de alvo cadastrados e abre o modal de cadastro/edição em etapas
(Informações → Fotografias → Dimensões → Revisão), incluindo a prévia da câmera durante a etapa
de fotografias. **DataContext:** `PerfilAlvoViewModel`.

### `DesempenhoView.xaml`
**Função:** exibe a taxa de atualização do radar (configurada vs. medida) e o tempo do ciclo
detecção → seleção de torre, com botão para zerar as estatísticas acumuladas. **DataContext:**
`DesempenhoViewModel`.

### `TermosDeUsoWindow.xaml`
**Função:** janela (não aba da shell) que exibe o documento de Termos de Uso vigente, já
formatado em blocos, com botão para exportar em PDF. **DataContext:** `TermosDeUsoViewModel`.

### `TreinamentoControladorView.xaml`
**Função:** tela de treinamento do controlador neuroevolutivo — Canvas 2D animando o episódio
do melhor genoma em tempo real (torres, alvos, miras, campos de visão), diagrama da rede neural
(nós/conexões), gráfico de evolução do fitness por geração, lista "Regras aprendidas" e
controles para iniciar/pausar/resetar o treinamento e ajustar `ParametrosTreinamento`.
**DataContext:** `TreinamentoControladorViewModel`.

### `EstatisticasTreinamentoView.xaml`
**Função:** tela exclusiva do Administrador com os marcos de fitness médio a cada 500 gerações,
o histórico recente geração a geração e os instantâneos de comportamento do melhor genoma a
cada 1000 gerações. **DataContext:** `EstatisticasTreinamentoViewModel`.

### `ConfiguracoesView.xaml`
**Função:** tela de preferências desta máquina (tamanho/posição de janela com opções
predefinidas, idioma, tema, comportamentos de disparo), distinta da aba "Configurações do
Arduino" (`ArduinoSettingsView`, que é específica de ambiente/compilação/monitor serial).
**DataContext:** `ConfiguracoesViewModel`.

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
> `Views/Shell/ShellView.xaml` — que por sua vez é exibida como conteúdo de uma aba de
> `Views/Shell/MainWindow.xaml` (ver seção 4.1 de `ARQUITETURA.md`); a composição de serviços
> descrita passou para `App.xaml.cs`
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
* Testes automatizados: o projeto `tests/RadarTorres.Tests` (xUnit, 18 arquivos) cobre bem mais
  do que a aba Configurações do Arduino. Áreas principais cobertas hoje:
  * **Seleção de torre:** `TowerSelectionServiceTests` (algoritmo de seleção por camadas).
  * **Controlador neuroevolutivo (`NeuralEvolution/`):** `RedeNeuralTests`,
    `AlgoritmoGeneticoTests`, `AmbienteTreinamentoTests`, `AlvoTreinamentoTests`,
    `ExtratorDeRegrasTests`, `ControladorTreinadoProviderTests`, `EvolucaoDoTreinamentoTests`.
  * **Persistência SQLite:** `PersistenciaSqliteTests`,
    `SqliteHistoricoTreinamentoRepositoryTests`.
  * **Detecção/visão computacional e concorrência:** `DeteccaoConcorrenteTests`,
    `OcorrenciaDeteccaoServiceTests`, `RastreamentoVisualServiceTests`.
  * **Perfis de alvo:** `PerfilAlvoServiceTests`, `CadastroAlvoModalTests`,
    `ValidacaoFotoAlvoTests`.
  * **Termos de Uso:** `TermosDeUsoTests`.
  * **Aba Configurações do Arduino:** localização do CLI, montagem segura de argumentos,
    interpretação de código de saída, cancelamento, persistência, limite de linhas dos
    consoles e disputa pela porta serial (ver `Docs/Projeto/LOG_SOLICITACOES.md`).

  A arquitetura (serviços com interface, sem dependência de UI) foi desenhada especificamente
  para viabilizar esse tipo de teste; áreas ainda sem cobertura direta incluem o parser serial
  (`SerialProtocolParser`) e os ViewModels/Views em si (testados apenas indiretamente, via os
  serviços que consomem).
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

## Ver também

- [[ARQUITETURA]] — racional arquitetural geral (já referenciado no topo deste documento).
- [[MODELO_DADOS]] — schema das tabelas usadas pelos repositórios aqui descritos.
- [[Matriz_de_Rastreabilidade]] — requisito → classe/função, cruzando com este documento.
