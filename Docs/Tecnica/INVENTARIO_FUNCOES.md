# Inventário de funções e classes por fluxo

Mapa de navegação do código — **não substitui** `ARQUITETURA.md`, `ALGORITMO_SELECAO_TORRE.md`,
`CONTROLADOR_NEUROEVOLUTIVO.md`, `DETECCAO_VISAO_YOLO.md`, `COMUNICACAO_ARDUINO.md` nem
`MODELO_DADOS.md`, que continuam sendo a documentação de referência de cada assunto. Aqui cada
item ganha uma linha: o que é, onde está, do que depende e a observação técnica que vale lembrar
antes de mexer.

Caminhos relativos a `src/RadarTorres.App/`, salvo indicação em contrário.

---

## 1. Simulação 2D (radar e alvos fictícios)

| Item | Arquivo | Responsabilidade | Dependências | Observação |
|---|---|---|---|---|
| `SimulationService` | `Services/SimulationService.cs` | Gera alvos fictícios que se movem e emite `SensorReading` iguais aos da serial | `ILoggingService`, `IConfiguracoesAplicativoRepository`, `AppConfig` | `Timer` do pool + `Random.Shared`; `IsRunning` é a chave que impede o `Tick` de emitir depois do `Stop` |
| `SensorReading` / `DataSource` | `Models/SensorReading.cs` | DTO único de leitura (ângulo, distância, origem) | — | Mesma forma vinda do Arduino e do simulador — o resto do sistema não distingue |
| `RadarControl` | `Views/RadarControl.xaml(.cs)` | Desenho do radar 2D, varredura, alvos, torres e zonas mortas | `CoordinateConverter`, `IDesempenhoMonitorService` | ~42 KB de code-behind: é o ponto mais pesado de desenho do aplicativo |
| `CoordinateConverter` | `Helpers/CoordinateConverter.cs` | Polar↔cartesiano, mundo↔tela, normalização de ângulo | — | Centro do canvas = base; `NormalizeAngle` garante 0–360 |
| `QuadrantHelper` | `Helpers/QuadrantHelper.cs` | Quadrante a partir de X/Y, rótulo e adjacência | — | `AreAdjacent` é usado pelas camadas 2/3 da seleção de torre |
| `DistanceCalculator` / `BearingCalculator` | `Helpers/` | Distância euclidiana alvo↔torre/base e ângulo de mira | `Target`, `Tower` | Puros, sem estado |
| `AmbienteTreinamento` | `NeuralEvolution/AmbienteTreinamento.cs` | Simulação 2D **headless** usada no treinamento (spawn, movimento, disparo, fitness) | `ParametrosTreinamento`, `ControladorHibrido`, `CatalogoCenarios` | Simulação independente da tela; é a que roda milhares de episódios |
| `EstadoEpisodio` | `NeuralEvolution/EstadoEpisodio.cs` | Estado mutável de um episódio (torres, alvos, fitness, contadores) | `Random` | `AlvosNascidos` é derivado do contador de ids |

## 2. Sensores e detecção de alvos

| Item | Arquivo | Responsabilidade | Dependências | Observação |
|---|---|---|---|---|
| `SerialCommunicationService` | `Services/SerialCommunicationService.cs` | Conexão, laço de leitura e envio de comandos ao Arduino | `SerialPort`, `SerialProtocolParser` | Task de leitura dedicada + watchdog por `Timer`; eventos saem em thread de fundo |
| `SerialProtocolParser` | `Services/SerialProtocolParser.cs` | Texto da serial ↔ `SerialMessage` tipada; monta os comandos | `SerialMessages.cs` | `TryParse` sempre devolve mensagem (`Unknown`/`Error`), nunca `null` |
| `TargetTrackingService` | `Services/TargetTrackingService.cs` | Decide se uma leitura é alvo novo ou atualização; expira alvos sem leitura | `ConcurrentDictionary`, `Dispatcher`, `ClassificadorPerigoAlvo` | Velocidade por diferença finita calculada **antes** de sobrescrever X/Y; `dt<=0` descarta a amostra |
| `ClassificadorPerigoAlvo` | `Helpers/ClassificadorPerigoAlvo.cs` | Velocidade → grau de perigo | — | Limiares constantes (0,5 / 1,2 / 2,0 m/s) |
| `CameraCaptureService` (+`Factory`, `CameraDeviceEnumerator`) | `Services/` | Captura DirectShow (AForge) de um dispositivo por painel | AForge.Video.DirectShow | O `Bitmap` nunca cruza a fronteira da interface: saem bytes BGR24 |
| `AnalisadorQuadros` | `Helpers/AnalisadorQuadros.cs` | Liga câmera→detecção: limita frequência, garante uma rodada por vez, remonta o `Bitmap` | `IAlvoVisualService` | Caminho quente; o vetor copiado é **fixado** (`GCHandle`) em vez de recopiado |
| `OnnxYoloDetectionService` | `Services/OnnxYoloDetectionService.cs` | Inferência YOLO (ONNX Runtime) propondo caixas | modelo embarcado | `IDisposable` — libera a `InferenceSession` |
| `AlvoVisualService` | `Services/AlvoVisualService.cs` | Casa as caixas do YOLO com o perfil de alvo ativo e estima a distância | `IObjectDetectionService`, `IPerfilAlvoService` | Trabalha num quadro reduzido (`FrameTrabalho`) e reprojeta as caixas |
| `RastreamentoVisualService` | `Services/RastreamentoVisualService.cs` | Identidade do objeto entre quadros (IoU + centro dentro da caixa) | `AppConfig.VisionSettings` | Fechar identidade **nunca** é abate; tudo sob um único `lock` |
| `OcorrenciaDeteccaoService` | `Services/OcorrenciaDeteccaoService.cs` | Transforma detecções confirmadas em ocorrências persistidas | `IRastreamentoVisualService`, `IObjetoDetectadoRepository`, `ICameraQuadranteService` | Descarta detecção dentro do raio defendido; só abre ocorrência após N confirmações |
| `CameraQuadranteService` | `Services/CameraQuadranteService.cs` | Regra "uma câmera, um quadrante" | `ICameraQuadranteRepository` | Mantém cache em memória e avisa por evento |

## 3. Rede neural

| Item | Arquivo | Responsabilidade | Dependências | Observação |
|---|---|---|---|---|
| `RedeNeural` | `NeuralEvolution/RedeNeural.cs` | MLP de camadas densas montado a partir de um genoma plano | — | `TamanhoGenoma(camadas)` é o contrato entre rede e AG; `AtivarComCamadas` alimenta a visualização |
| `ControladorHibrido` | `NeuralEvolution/ControladorHibrido.cs` | Combina heurística e rede para escolher torre e decidir disparo | `RedeNeural` | `ModoDecisao` (Heurística / Rede / Híbrido); `ConstruirEntradas` estático é o mesmo vetor usado em produção |
| `ExtratorDeRegras` | `NeuralEvolution/ExtratorDeRegras.cs` | Traduz a rede em frases legíveis ("quanto mais perto, maior a nota") | `RedeNeural` | Explicabilidade para a tela de treinamento — varredura por sensibilidade |
| `ControladorTreinadoProvider` | `Services/ControladorTreinadoProvider.cs` | Carrega o genoma embutido (`EmbeddedResource`) e monta a rede de produção | `RedeNeural` | Falha de leitura cai para heurística pura, nunca derruba o app |
| `RedeEmbutidaSincronizadorService` | `Services/RedeEmbutidaSincronizadorService.cs` | Envia o genoma ao Arduino e valida as entradas pela serial | `ISerialCommunicationService`, `IControladorTreinadoProvider` | Protocolo `GENOMA;…` / `REDE;…` |

## 4. Algoritmo genético e treinamento

| Item | Arquivo | Responsabilidade | Dependências | Observação |
|---|---|---|---|---|
| `AlgoritmoGenetico` | `NeuralEvolution/AlgoritmoGenetico.cs` | Uma geração: avaliação, elitismo, torneio, cruzamento, mutação gaussiana | `Func<double[],double>` de fitness | `ResultadoGeracao` carrega melhor/médio e o melhor genoma |
| `ParametrosTreinamento` | `NeuralEvolution/ParametrosTreinamento.cs` | Todos os números do ambiente (raio, spawn, velocidades, zonas mortas) | — | `record` imutável — trocar parâmetro é criar outro |
| `AmbienteTreinamento.AvaliarFitness` | `NeuralEvolution/AmbienteTreinamento.cs` | Fitness = média de vários episódios (seeds fixos ou catálogo de cenários) | `ExecutarEpisodioRapido` | Média sobre seeds evita que o torneio vire sorteio |
| `EpisodioResultado` | `NeuralEvolution/EpisodioResultado.cs` | Fitness total + abatidos/escapados/tiros errados/nascidos | — | Fonte dos indicadores das telas de estatística |
| `CatalogoCenarios` / `SpawnScriptado` | `NeuralEvolution/` | Casos táticos nomeados (saturação, disputa de torre, zona morta…) e spawns roteirizados | `Quadrant` | Permite avaliação reprodutível e o "cenário personalizado" desenhado pelo usuário |
| `TorreTreinamento` / `AlvoTreinamento` | `NeuralEvolution/` | Entidades do ambiente headless (recarga, compromisso, certeza do alvo) | — | `CertezaEfetiva` cai com a distância — espelha a câmera real |
| `ExecutorExperimentosHiperparametros` | `Services/ExecutorExperimentosHiperparametros.cs` | Varredura planejada de população × neurônios × taxa de mutação | `IExperimentoHiperparametrosRepository`, `AmbienteTreinamento` | Baseline 60/8/0,08, 300 gerações; grava curva por marco |
| `TreinamentoControladorViewModel` | `ViewModels/TreinamentoControladorViewModel.cs` | Orquestra o laço de treinamento e alimenta a visualização ao vivo | tudo acima | ~50 KB — maior ViewModel do projeto |

## 5. Heurísticas e seleção de torre

| Item | Arquivo | Responsabilidade | Dependências | Observação |
|---|---|---|---|---|
| `TowerSelectionService.SelectTowerFor` | `Services/TowerSelectionService.cs` | Escolhe a torre principal do alvo em três camadas (casa → adjacente → cobertura) | `IZonaMortaService`, `IControladorTreinadoProvider`, `IConfiguracoesAplicativoRepository` | Exclusividade de torre + "roubo" por urgência (regra 2) |
| `EscolherMelhorCandidata` | idem | Pontua as candidatas (rede ou `-distância`) e aplica balanceamento e anti-oscilação | `ControladorHibrido.Pontuar` | Margem de empate muda de escala conforme haja rede (0,1) ou não (0,3 m) |
| `ComputarTorresEngajando` | idem | Todas as torres autorizadas a atirar, não só a de mira | `QuadrantHelper`, `DistanceCalculator` | Alvo rápido puxa torre adjacente a até 2 m (regra 6) |
| `LinhaDeTiroCruzaZonaMorta` / `OutraTorreNaFrenteDoTiro` | idem | Corredor de tiro: zona morta no caminho (regra 3) e canhão na frente | `IZonaMortaService` | Amostragem de 12 pontos no segmento; corredor de 0,3 m |
| `CalcularAnguloDeMira` | idem | Predição de trajetória 0,3 s à frente (regra 5) | `Target.Velocity*` | Desligável por configuração |
| `ZonaMortaService` | `Services/ZonaMortaService.cs` | CRUD das zonas e `FindBlockingZone` | `IZonaMortaRepository` | Grava o arquivo inteiro a cada mudança (volume mínimo) |
| `FireControlService` | `Services/FireControlService.cs` | Autoriza e executa o acionamento demonstrativo | `IZonaMortaService`, `IOcorrenciaDeteccaoService`, `IAcaoRealizadaRepository` | Única porta automática para `AbatePendente`; acerto é probabilístico |
| `CalculadoraErroDisparo` | `Helpers/CalculadoraErroDisparo.cs` | Chance de errar cresce com a velocidade do alvo | — | Fator 0,35 por m/s |
| `PermissionService` | `Services/PermissionService.cs` | Heurística de permissão por perfil (menu e ações) | `PerfilUsuario` | Ver seção 9 |

## 6. Persistência e histórico

| Item | Arquivo | Responsabilidade | Dependências | Observação |
|---|---|---|---|---|
| `SqliteConnectionFactory` | `Data/SqliteConnectionFactory.cs` | Connection string única + `WAL` e `busy_timeout` por conexão | Microsoft.Data.Sqlite | `UsarBancoDeTeste` é `internal` (só testes); falha após `Open` descarta a conexão |
| `SqliteSchema` | `Data/SqliteSchema.cs` | `EnsureColumn`/`EnsureIndex` e `GarantirUmaVez` | — | `GarantirUmaVez` aplica criação/migração **uma vez por banco por processo** |
| `PendingWriteQueue` | `Data/PendingWriteQueue.cs` | Fila offline em `pending-writes.sql` quando o banco está inacessível | `SqliteConnectionFactory` | Reexecuta tudo numa transação no início do app; só apaga o arquivo se aplicar inteiro |
| `SqlLiteral` | `Data/SqlLiteral.cs` | Literais SQL escapados para a fila offline | — | Só usado onde não há parâmetros (texto SQL puro) |
| `SqliteObjetoDetectadoRepository` | `Repositories/` | Ocorrências + histórico de estados | `SqliteSchema`, `PendingWriteQueue` | `AdicionarSeNaoExistir` resolve corrida entre câmeras por índice único parcial |
| `SqliteUsuarioRepository` / `SqlitePreferenciasUsuarioRepository` | `Repositories/` | Usuários e preferências por usuário | `SqliteSchema` | Login é chave natural |
| `SqliteAcaoRealizadaRepository`, `SqliteModoAtualTorreRepository`, `SqliteClassificacaoPerigoRepository` | `Repositories/` | Trilhas de auditoria (append-only) | `SqliteSchema` | Não expõem update nem delete |
| `SqliteHistoricoTreinamentoRepository`, `SqliteMarcoFitnessTreinamentoRepository`, `SqliteInstantaneoComportamentoRepository`, `SqliteExperimentoHiperparametrosRepository` | `Repositories/` | Histórico de gerações, marcos, instantâneos e experimentos | `SqliteSchema` | Gravados a cada geração/marco — caminho quente do treinamento |
| `SqliteCameraQuadranteRepository` | `Repositories/` | Vínculo câmera↔quadrante | `SqliteSchema` | Unicidade do quadrante no banco |
| `CsvTableStore` / `CsvColumn` / `CsvConvert` / `CsvObjetoDetectadoColumns` | `Data/`, `Repositories/` | CSV genérico | — | Hoje só serve à **exportação** manual; nada do sistema persiste em CSV |
| `ConfiguracoesAplicativoRepository`, `PerfilAlvoRepository`, `ZonaMortaRepository`, `ArduinoSettingsRepository`, `DashboardLayoutRepository` | `Services/` | Preferências em JSON no perfil do usuário | `System.Text.Json` | Todos tratam arquivo corrompido voltando ao padrão |
| `AppDataPaths` | `Data/AppDataPaths.cs` | Pastas de dados, diagnóstico e treinamento | — | Base de todos os caminhos gravados |
| `DataSeeder` | `Data/DataSeeder.cs` | Cria o administrador padrão na primeira execução | `IUsuarioRepository`, `IPasswordHasher` | `admin/admin123` — troca obrigatória documentada |
| `ObjetoDetectadoExportService` | `Services/ObjetoDetectadoExportService.cs` | Exporta ocorrências em CSV, XML e PDF | `CsvTableStore`, PDFsharp, `ResolvedorFontesPdf` | PDF depende do resolvedor de fontes registrado no start |
| `PerformanceMonitorService` | `Services/PerformanceMonitorService.cs` | Relatório de desempenho em arquivo (CPU, memória, tempos) | `IConfiguracoesAplicativoRepository` | `IDisposable` — fecha o `StreamWriter` no encerramento |

## 7. Interface gráfica

| Item | Arquivo | Responsabilidade | Dependências | Observação |
|---|---|---|---|---|
| `ShellView` / `ShellViewModel` / `MainWindow` | `Views/Shell/`, `ViewModels/` | Casca da aplicação: sidebar, barra superior, host de conteúdo | `INavigationService`, `IAuthService` | Menu filtrado por `IPermissionService` |
| `NavigationService` | `Services/NavigationService.cs` | Resolve a View do item de menu pelo contêiner de DI | `IServiceProvider` | Mantém `CurrentItem` e dispara `Navigated` |
| `MainViewModel` | `ViewModels/MainViewModel.cs` | Orquestra radar, serial, simulação, seleção de torre e disparo | quase todos os serviços | ~36 KB — núcleo do monitoramento |
| `MonitoramentoView` | `Views/MonitoramentoView.xaml` | Painel operacional (radar, status, zonas mortas, console) | `DashboardCanvas`, `DashboardCard` | Cards arrastáveis com layout persistido |
| `PainelPrincipalView` / `DashboardToolbar` / `DashboardCard` | `Views/`, `Views/Shared/` | Dashboard configurável e ações de layout | `IDashboardLayoutRepository` | Layout padrão vem de `EmbeddedResource` |
| `CamerasView` / `CameraPanelView` / `CameraSlotViewModel` | `Views/`, `ViewModels/` | Grade 2×2 de câmeras independentes | `ICameraCaptureServiceFactory`, `AnalisadorQuadros` | Um serviço de captura por painel |
| `DeteccaoCameraViewModel` | `ViewModels/DeteccaoCameraViewModel.cs` | Card de detecção por câmera (prévia, candidatos, diagnóstico) | `IAlvoVisualService` | Despacha para a UI o que chega da thread de análise |
| `PerfilAlvoView` / `PerfilAlvoViewModel` | `Views/`, `ViewModels/` | Cadastro de alvos em assistente de 4 etapas (modal) | `IPerfilAlvoService`, `ValidacaoFotoAlvo` | Já segue o padrão "tabela + modal" |
| `TreinamentoControladorView` / `EstatisticasTreinamentoView` | `Views/` | Treinamento ao vivo e análise experimental | ViewModels de treinamento | Ver seção 10 (UX) |
| `ObjetosDetectadosView`, `AcoesRealizadasView`, `HistoricoModosView`, `DesempenhoView` | `Views/` | Consultas e auditoria em `DataGrid` | repositórios | Telas de leitura, já em largura total |
| `ConfiguracoesView` / `ConfiguracoesViewModel` | `Views/`, `ViewModels/` | Todas as preferências do aplicativo | `IConfiguracoesAplicativoRepository`, `IThemeService`, `ILocalizationService` | 8 seções numa rolagem só |
| `ArduinoSettingsView` / `ArduinoSettingsViewModel` | `Views/`, `ViewModels/` | Ambiente Arduino CLI, compilação e envio | `IArduinoCliLocatorService`, `IArduinoCompilerService` | Console de saída embutido |
| `RelayCommand` | `Helpers/RelayCommand.cs` | `ICommand` padrão do projeto | `CommandManager` | `RaiseCanExecuteChangedForAll` força reavaliação global |
| `Converters/*` | `Converters/` | 14 conversores de binding (cores, rótulos, visibilidade) | — | Todos sem estado |
| `LocalizationService` / `ThemeService` / `FocusModeService` | `Services/` | Idioma (pt-BR/en-US), tema e modo foco | `AppConfig` | `LocalizationService.Current` é o acesso estático usado pelo XAML |
| `LoggingService` | `Services/LoggingService.cs` | Console de eventos observável | `Dispatcher` | Teto de 500 linhas; `BeginInvoke` fora da thread de UI |
| `DesempenhoMonitorService` | `Services/DesempenhoMonitorService.cs` | Janelas móveis de taxa de atualização e tempos | — | Três `lock` independentes, um por métrica |
| `AlertaSonoroService` | `Services/AlertaSonoroService.cs` | Beep + anúncio por voz (SAPI) | `System.Speech` | Sempre em `Task.Run`; falha de TTS só vira aviso |

## 8. Autenticação e autorização

| Item | Arquivo | Responsabilidade | Dependências | Observação |
|---|---|---|---|---|
| `AuthService` | `Services/AuthService.cs` | Login, logout, troca e redefinição de senha; sessão corrente | `IUsuarioRepository`, `IPasswordHasher` | `SessionChanged` é o gatilho de refiltragem do menu |
| `PasswordHasher` | `Services/PasswordHasher.cs` | Hash com salt por usuário e verificação | `System.Security.Cryptography` | Senha nunca é gravada em claro |
| `PermissionService` | `Services/PermissionService.cs` | O que cada perfil pode ver e fazer | `PerfilUsuario`, `MenuItem` | Visualizador não executa ações; só Administrador gerencia usuários e zonas mortas |
| `Usuario` / `PerfilUsuario` | `Models/Usuario.cs` | Identidade e perfil | — | Três perfis: Administrador, Operador, Visualizador |
| `LoginView` / `LoginViewModel`, `UsuariosView` / `UsuariosViewModel`, `UsuarioEdicaoWindow`, `RedefinirSenhaWindow`, `ProfileWindow` | `Views/`, `ViewModels/` | Telas de acesso e gestão de usuários | `IAuthService`, `IPermissionService` | `UsuariosView` já reorganizada (tabela em largura total + modais) |
| `TermosDeUsoService` / `TermosDeUsoWindow` | `Services/`, `Views/` | Texto oficial dos termos, aceite e exportação em PDF | `ResolvedorFontesPdf` | Aceite é pré-requisito do primeiro uso |

---

## 9. Observações transversais levantadas na auditoria

- **Criação de esquema SQLite** era reexecutada a cada operação de repositório; passou a rodar uma
  vez por banco via `SqliteSchema.GarantirUmaVez`. Importa sobretudo no caminho quente da detecção
  por câmera e no laço de treinamento.
- **Escrita de JSON de preferências não é atômica** (`File.WriteAllText` direto sobre o arquivo
  final, em `ConfiguracoesAplicativoRepository`, `PerfilAlvoRepository`, `ZonaMortaRepository`,
  `ArduinoSettingsRepository`, `DashboardLayoutRepository`). Queda de energia no meio da gravação
  trunca o arquivo; hoje a leitura trata isso voltando ao padrão — ou seja, a preferência se perde
  silenciosamente. Correção sugerida: gravar em `.tmp` e `File.Move(..., overwrite: true)`.
- **`AppConfig.Current` é estado estático global**, lido de dentro de serviços (`SimulationService`,
  `TargetTrackingService`, `RastreamentoVisualService`, `TowerSelectionService`). Funciona, mas é o
  motivo de os testes precisarem rodar em série.
- **`TowerSelectionService._usoPorTorre`** é um `Dictionary` comum mutado durante a seleção: só é
  seguro porque a seleção roda sempre na thread de interface.
- **`CsvTableStore`** converte quebras de linha para a sequência literal `\n` e desconverte na
  leitura — um campo que contenha o texto `\n` de verdade vira quebra de linha. Irrelevante no uso
  atual (exportação), mas não serve para persistência.

---

## 10. Telas que precisam de reorganização

Padrão alvo: **conteúdo principal em largura total; formulários e ações secundárias em modal,
drawer ou menu "Opções"** — como já está em `UsuariosView` (tabela cheia + modais) e em
`PerfilAlvoView` (tabela cheia + assistente modal de 4 etapas). As duas ficam fora da lista abaixo.

Ordenado por ganho de usabilidade.

| # | Arquivo | O que está "na lateral"/fixo | Campos e ações | Ação sugerida |
|---|---|---|---|---|
| 1 | `Views/TreinamentoControladorView.xaml` | `ScrollViewer` com `DockPanel.Dock="Right" Width="280"` fixo, com os cards CONTROLE, GERAÇÃO ATUAL, MELHOR FITNESS, EVOLUÇÃO DO FITNESS, CENÁRIO PERSONALIZADO e REGRAS | 4 campos de hiperparâmetro + 1 combo de cenário + 6 botões, sempre visíveis | **Modal "Parâmetros do treinamento"** para os 4 campos + combo (valem só a partir do próximo Iniciar/Resetar, então não precisam ficar à vista) e **menu "Opções"** para Exportar genoma / Resetar / Simular caso. Deixar na lateral apenas os indicadores ao vivo — ou movê-los para uma faixa superior e devolver os 280 px ao canvas 2D, que é o conteúdo principal da tela |
| 2 | `Views/CameraPanelView.xaml` (4 instâncias em `CamerasView`) | Bloco de configuração **acima do vídeo** em cada um dos 4 painéis da grade 2×2 | 2 combos (câmera, quadrante) + 3 botões (Iniciar, Parar, Salvar) + 2 textos de ajuda, replicados 4× | **Modal/flyout de engrenagem por painel** ("Configurar câmera"): escolher dispositivo e quadrante é tarefa de setup, feita uma vez. O painel fica com vídeo + status + Iniciar/Parar, que é o que o operador usa |
| 3 | `Views/MonitoramentoView.xaml` (card "Zonas Mortas") | Formulário "Nova zona morta" permanente, na parte de baixo do card, abaixo da lista | 1 checkbox + 2 radios + 1 campo de nome + instruções de desenho | **Botão "+ Nova zona" abrindo modal** (ou drawer) com o modo de desenho; o card volta a ser só a lista de zonas, que hoje fica espremida |
| 4 | `Views/ConfiguracoesView.xaml` | Oito seções (`Janela principal`, `Aparência e idioma`, `Câmera e detecção`, `Radar e mira`, `Disparo`, `Diagnóstico`, `Treinamento da IA`, `Termos de Uso`) numa única rolagem | 19 campos + 8 botões | **`TabControl` lateral ou accordion por seção** — achar uma preferência hoje exige rolar a tela inteira |
| 5 | `Views/ArduinoSettingsView.xaml` | Rolagem única com ambiente, placa, compilação e console de saída (altura fixa de 180 px) | 9 campos + 10 botões | **Abas "Ambiente / Placa / Compilação"** e console de saída em painel recolhível — o console ocupa espaço fixo mesmo quando não há compilação em curso |
| 6 | `Views/EstatisticasTreinamentoView.xaml` | Card "Análise experimental de hiperparâmetros" com os controles de execução fixos no topo, acima de três tabelas | 3 botões de execução + 2 textos de status | **Menu "Opções"** (Executar pendentes / Refazer tudo / Parar) na barra do título da tela, liberando altura para as tabelas comparativas |
| 7 | `Views/ObjetosDetectadosView.xaml` | Faixa de exportação acima do `DataGrid` | 6 botões (CSV/XML/PDF + ações de estado) | **Menu "Exportar"** único; o `DataGrid` já é de largura total e ganharia altura |

> Nenhuma dessas mudanças foi implementada aqui — esta seção é inventário.
