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

---

## Configuration

### `AppSettings` / `AppConfig` (`Configuration/`)
**Função:** modelo tipado para `appsettings.json` (`AppSettings`) e ponto de acesso estático
único (`AppConfig.Current`). **Por que existe:** evita espalhar `ConfigurationBuilder` por
todo o código; qualquer classe que precise de um valor configurável (baud rate, distância
mínima, torres, timeouts) lê de `AppConfig.Current`. **Quem usa:** praticamente todos os
serviços (`SerialCommunicationService`, `TargetTrackingService`, `TowerSelectionService`,
`SimulationService`, `MainViewModel`).

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

---

## ViewModels

### `MainViewModel`
**Função:** orquestra todos os serviços acima e expõe propriedades/comandos para a
`MainWindow`. **Por que existe:** é a fronteira MVVM — nenhuma regra de negócio deveria
"vazar" para dentro dela; ela só decide *quando* chamar cada serviço. **Fluxo de dados:**
descrito em detalhe no diagrama de sequência de `ARQUITETURA.md`. **Quem usa:**
`MainWindow.xaml` (via `DataContext`).

---

## Views

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

---

## Converters (`Converters/*.cs`)

Pequenos `IValueConverter` usados apenas para tradução de exibição no XAML — nenhum contém
lógica de negócio: `EnumToBooleanConverter` (RadioButtons de modo), `ConnectionStateToBrushConverter`,
`QuadrantToLabelConverter`, `SystemModeToLabelConverter`, `LogLevelToBrushConverter`,
`NullToVisibilityConverter`.

---

## Limitações e próximos passos

**Limitações conhecidas desta entrega:**

* O algoritmo de seleção de torre não considera ângulo de cobertura/orientação física da
  torre, apenas distância e quadrante preferencial.
* Não há reconexão automática após queda de conexão (o campo `ReconnectAttempts` já existe na
  configuração, mas a lógica de retry ainda não foi implementada — ver abaixo).
* Testes automatizados (unitários) ainda não foram incluídos nesta entrega inicial, embora a
  arquitetura (serviços com interface, sem dependência de UI) tenha sido desenhada
  especificamente para viabilizá-los facilmente.
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
