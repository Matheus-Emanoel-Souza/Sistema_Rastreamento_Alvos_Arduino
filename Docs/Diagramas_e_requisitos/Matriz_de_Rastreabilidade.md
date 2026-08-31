# Matriz de Rastreabilidade

Relaciona cada requisito ao(s) componente(s) real(is) que o implementam, comprovando que o
levantamento não é especulativo. Cobre `Requisitos_Funcionais.md`, `Requisitos_Nao_Funcionais.md`,
`Decisoes_Arquiteturais.md` e `Limitacoes_Conhecidas.md`.

## Requisitos Funcionais → implementação

| Requisito | Status | Arquivo/Módulo | Classe/Função | Responsabilidade |
|---|---|---|---|---|
| RF01 | Implementado | `Services/SerialCommunicationService.cs` | `SerialCommunicationService` (`ISerialCommunicationService`) | Conectar/desconectar, listar portas, ler/enviar, watchdog |
| RF01 | Implementado | `Services/SerialProtocolParser.cs` | `SerialProtocolParser` | Codificar comandos PC→Arduino |
| RF02 | Implementado | `Services/SerialProtocolParser.cs` | `SerialProtocolParser.TryParse` | Parse e validação de `TARGET`/`STATUS`/`ACK`/`ERROR` |
| RF03 | Implementado | `Services/TargetTrackingService.cs` | `TargetTrackingService` (`ITargetTrackingService`) | Criar/atualizar/expirar `Target` |
| RF03 | Implementado | `Models/Target.cs` | `Target` | Entidade alvo com estado vivo |
| RF04 | Implementado | `Views/RadarControl.xaml.cs` | `RadarControl` | Desenho do radar circular |
| RF04 | Implementado | `Helpers/CoordinateConverter.cs` | `CoordinateConverter` | Conversão polar↔cartesiano↔tela |
| RF05 | Implementado | `Services/TowerSelectionService.cs` | `TowerSelectionService` (`ITowerSelectionService`) | Algoritmo de seleção de torre |
| RF05 | Implementado | `Helpers/DistanceCalculator.cs` | `DistanceCalculator` | Cálculo de distância euclidiana |
| RF06 | Implementado (ver D1) | `Services/FireControlService.cs` | `FireControlService.TryFireAsync`/`Authorize` | Autorização e execução do acionamento |
| RF06 | Implementado (ver D1) | `Models/AcaoRealizada.cs`, `Repositories/CsvAcaoRealizadaRepository.cs` | — | Registro de auditoria de acionamento |
| RF07 | **Removido** — não é mais requisito funcional | `Services/SimulationService.cs` | `SimulationService` (`ISimulationService`) | Geração de alvos fictícios — documentado em `Docs/DOCUMENTACAO_TECNICA.md`, não em RF |
| RF08 | Parcial (ver D1) | `Models/SystemState.cs` | `SystemMode` (enum — ainda 6 valores, não Verde/Amarelo/Vermelho) | Modos de operação do sistema |
| RF08 | Parcial (ver D1) | `Models/AlteracaoModo.cs`, `Repositories/CsvAlteracaoModoRepository.cs` | — | Registro de auditoria de troca de modo |
| RF09 | Implementado | `Services/AuthService.cs` | `AuthService` (`IAuthService`) | Login/logout/sessão |
| RF09 | Implementado | `Services/PasswordHasher.cs` | `PasswordHasher` (`IPasswordHasher`) | Hash PBKDF2-HMACSHA256 |
| RF09 | Implementado | `Data/DataSeeder.cs` | `DataSeeder` | Semeia usuário `admin` padrão |
| RF10 | Implementado | `Services/PermissionService.cs` | `PermissionService` (`IPermissionService`) | Regras de acesso por `PerfilUsuario` |
| RF11 | Implementado | `Services/IAuthService.cs` | `IAuthService.AlterarSenhaAsync` | Troca de senha com validação da atual |
| RF11 | Implementado | `ViewModels/ProfileViewModel.cs` | `ProfileViewModel` | Orquestra a troca de senha na tela de Perfil |
| RF12 | Implementado | `Models/ObjetoDetectado.cs` | `ObjetoDetectado` | Entidade de registro histórico |
| RF12 | Implementado | `Repositories/CsvObjetoDetectadoRepository.cs` | `CsvObjetoDetectadoRepository` | Persistência em `objetos_detectados.csv` |
| RF13 | Implementado | `ViewModels/ObjetosDetectadosViewModel.cs` | `ObjetosDetectadosViewModel` | Lista o histórico (`Itens`) |
| RF13 | Implementado | `Views/ObjetosDetectadosView.xaml.cs` | `ObjetosDetectadosView` | Tela de tabela |
| RF14 | Implementado | `Services/ObjetoDetectadoExportService.cs` | `ObjetoDetectadoExportService.ExportCsv/Xml/Pdf` | Exportação nos 3 formatos |
| RF15 | Implementado | `Services/ObjetoDetectadoExportService.cs` | `ObjetoDetectadoExportService.ImportCsv/Xml` | Leitura de arquivo CSV/XML |
| RF15 | Implementado | `ViewModels/ObjetosDetectadosViewModel.cs` | `ObjetosDetectadosViewModel.PodeImportar` | Restrição de perfil na importação |
| RF16 | Parcial — registro ok, tela pendente | `Repositories/IAcaoRealizadaRepository.cs` | `IAcaoRealizadaRepository` | Contrato de consulta (sem Update/Delete); gravação ocorre em `FireControlService` |
| RF17 | Parcial — registro ok, tela pendente | `Repositories/IAlteracaoModoRepository.cs` | `IAlteracaoModoRepository` | Contrato de consulta (sem Update/Delete); gravação ocorre em `MainViewModel` |
| RF18 | Planejado — sem UI | `Repositories/IUsuarioRepository.cs` | `IUsuarioRepository` | Contrato CRUD de usuários (nenhum ViewModel/View o consome hoje) |
| RF18 | Planejado — sem UI | `Services/IPermissionService.cs` | `IPermissionService.PodeGerenciarUsuarios` | Restrição a Administrador (pronta, mas sem tela para aplicar) |
| RF19 | Implementado | `Models/PreferenciasUsuario.cs` | `PreferenciasUsuario` | Entidade de preferências |
| RF19 | Implementado | `Repositories/CsvPreferenciasUsuarioRepository.cs` | `CsvPreferenciasUsuarioRepository` | Persistência 1:1 por usuário |
| RF20 | Implementado | `Services/LocalizationService.cs` | `LocalizationService` (`ILocalizationService`) | Troca de cultura pt-BR/en-US |
| RF20 | Implementado | `Localization/LocExtension.cs` | `LocExtension` | Extensão XAML de texto localizado |
| RF21 | Implementado | `Services/ThemeService.cs` | `ThemeService` (`IThemeService`) | Aplicação de tema |
| RF22 | Implementado | `ViewModels/HelpDeskFormViewModel.cs` | `HelpDeskFormViewModel` | Orquestra envio do chamado |
| RF22 | Implementado | `Models/ChamadoAjuda.cs` | `ChamadoAjuda` | Entidade do chamado |
| RF23 | Planejado — sem UI | `Repositories/IChamadoAjudaRepository.cs` | `IChamadoAjudaRepository` | Único repositório com `Update` (situação/resposta), sem tela que o consuma |
| RF24 | Implementado | `Views/Shared/DashboardCanvas.cs` | `DashboardCanvas` | Anticolisão, limites, reescala |
| RF24 | Implementado | `Views/Shared/DashboardCard.xaml.cs` | `DashboardCard` | Card arrastável/redimensionável |
| RF24 | Implementado | `Services/DashboardLayoutRepository.cs` | `DashboardLayoutRepository` (`IDashboardLayoutRepository`) | Persistência do layout por usuário |
| RF25 | **Removido** — consolidado em RNF11 | `Models/DashboardCardLayout.cs` | `DashboardCardLayout` | Posição/tamanho como fração 0..1 |
| RF26 | Implementado | `Views/MonitoramentoView.xaml.cs` | `MonitoramentoView.SetLogPinned` | Realoca o console para a lateral fixa |
| RF26 | Implementado | `Models/DashboardCardLayout.cs` | `IsPinnedRight` | Campo persistido do estado fixado |
| RF27 | Implementado | `Models/DeadZone.cs` | `DeadZone` | Entidade de zona morta |
| RF27 | Implementado | `Services/IDeadZoneService.cs` | `IDeadZoneService` | Avaliação de bloqueio |
| RF27 | Implementado | `Services/IDeadZoneRepository.cs` | `IDeadZoneRepository` | Persistência de zonas |
| RF28 | Implementado | `Services/ArduinoCliLocatorService.cs` | `ArduinoCliLocatorService.Locate` | Localização do `arduino-cli.exe` |
| RF29 | Implementado | `Services/ArduinoCompilerService.cs` | `ArduinoCompilerService.CompileAsync` | Compilação assíncrona/cancelável |
| RF30 | Implementado | `ViewModels/ArduinoSettingsViewModel.cs` | `ArduinoSettingsViewModel` | Orquestra monitor serial + reuso da conexão |
| RF31 | Implementado | `Services/ArduinoSettingsRepository.cs` | `ArduinoSettingsRepository` (`IArduinoSettingsRepository`) | Persistência JSON das preferências da aba |
| RF32 | **Removido** — consolidado em RNF17/RNF18 | `installer/RadarTorres.iss` | Script Inno Setup | Geração do `Setup.exe` self-contained |

## Requisitos Não Funcionais reformulados → implementação

Só as linhas alteradas nesta revisão; as demais RNF mantêm a evidência já publicada em
`Requisitos_Nao_Funcionais.md`.

| Requisito | Arquivo/Módulo | Classe/Função | Observação |
|---|---|---|---|
| RNF12 | `Services/LocalizationService.cs` | `LocalizationService` | Reformulado para descrever só a troca em runtime, sem duplicar RF20 |
| RNF13 | `Services/ThemeService.cs`, `Themes/Light.xaml`, `Themes/Dark.xaml` | `ThemeService` | Reformulado para descrever só a consistência visual, sem duplicar RF21 |
| RNF16 | `Services/ArduinoCompilerService.cs` | `CompileAsync` (`CancellationToken`) | Reformulado como propriedade de não bloqueio/cancelamento, sem duplicar RF29 |
| RNF24 | `tests/RadarTorres.Tests/*.cs` | — | Reformulado como cobertura de componentes críticos; status rebaixado para Parcial (só a aba Arduino CLI tem teste hoje) |

## Decisões Arquiteturais → implementação (ver `Decisoes_Arquiteturais.md`)

| ID | Arquivo/Módulo | Observação |
|---|---|---|
| DA01 *(ex-RNF20)* | `Repositories/I*Repository.cs` | Persistência substituível — decisão interna, não requisito |
| DA02 *(ex-RNF21)* | `Services/SerialProtocolParser.cs` | Protocolo serial centralizado — decisão interna |
| DA03 *(ex-RNF22)* | `ViewModelBase.cs`, `RelayCommand.cs` | MVVM manual — decisão tecnológica |
| DA04 *(ex-RNF23)* | interfaces `I*Service` | Independência de WPF em Services/Models — decisão de camadas |
| DA05 *(ex-RNF30)* | `Views/Shared/DashboardCanvas.cs` | Anticolisão por rejeição — decisão de UX |

## Limitações e Divergências → implementação (ver `Limitacoes_Conhecidas.md`)

| ID | Arquivo/Módulo | Observação |
|---|---|---|
| L01 *(ex-RNF29)* | `appsettings.json` (`SerialSettings.ReconnectAttempts`) | Configuração existe, retry automático não implementado |
| L02 | `Services/NavigationService.cs` | Telas ainda em `PlaceholderView`: Ações realizadas, Histórico de modos, Usuários, Chamados/Ajuda, Configurações |
| L03 | `Docs/DOCUMENTACAO_TECNICA.md` | Bugs conhecidos não corrigidos (referência, sem duplicar o texto) |
| D1 | `Models/SystemState.cs` (`SystemMode`), `ViewModels/MainViewModel.cs` (`ManualFireCommand`) | Enum ainda com 6 valores antigos; acionamento manual ainda existe e não é restrito por modo |

## Rastreabilidade requisito → caso de uso

A matriz Caso de Uso × Requisito completa (28 casos de uso, atores e status) está em
`Casos_de_Uso.md`, seção 5, para não duplicar aqui — ela nasce diretamente das linhas de RF
acima, então qualquer requisito removido/reformulado nesta tabela deve ser conferido também lá.

## Rastreabilidade requisito → documentação existente

| Requisito(s) | Documento de apoio no repositório |
|---|---|
| RF01, RF02, RF28–RF31 | `Docs/COMUNICACAO_ARDUINO.md` |
| RF03, RF04, RF24, RF26 | `Docs/ARQUITETURA.md` |
| RF05 | `Docs/ALGORITMO_SELECAO_TORRE.md` |
| RF08, RF12, RF16–RF19, RF22, RF23 | `Docs/MODELO_DADOS.md` |
| RF07 (removido) | `Docs/DOCUMENTACAO_TECNICA.md` (seção `SimulationService`), `README.md` |
| RNF17, RNF18 (ex-RF32) | `Docs/INSTALADOR.md` |
| Todos | `Docs/DOCUMENTACAO_TECNICA.md`, `Docs/CONTEXTO_PROJETO.md` |
