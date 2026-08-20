# Matriz de Rastreabilidade

Relaciona cada requisito funcional (ver `Requisitos_Funcionais.md`) ao(s) componente(s) real(is)
que o implementam, comprovando que o levantamento não é especulativo.

| Requisito | Arquivo/Módulo | Classe/Função | Responsabilidade |
|---|---|---|---|
| RF01 | `Services/SerialCommunicationService.cs` | `SerialCommunicationService` (`ISerialCommunicationService`) | Conectar/desconectar, listar portas, ler/enviar, watchdog |
| RF01 | `Services/SerialProtocolParser.cs` | `SerialProtocolParser` | Codificar comandos PC→Arduino |
| RF02 | `Services/SerialProtocolParser.cs` | `SerialProtocolParser.TryParse` | Parse e validação de `TARGET`/`STATUS`/`ACK`/`ERROR` |
| RF03 | `Services/TargetTrackingService.cs` | `TargetTrackingService` (`ITargetTrackingService`) | Criar/atualizar/expirar `Target` |
| RF03 | `Models/Target.cs` | `Target` | Entidade alvo com estado vivo |
| RF04 | `Views/RadarControl.xaml.cs` | `RadarControl` | Desenho do radar circular |
| RF04 | `Helpers/CoordinateConverter.cs` | `CoordinateConverter` | Conversão polar↔cartesiano↔tela |
| RF05 | `Services/TowerSelectionService.cs` | `TowerSelectionService` (`ITowerSelectionService`) | Algoritmo de seleção de torre |
| RF05 | `Helpers/DistanceCalculator.cs` | `DistanceCalculator` | Cálculo de distância euclidiana |
| RF06 | `Services/FireControlService.cs` | `FireControlService` (`IFireControlService`) | Autorização e execução do acionamento |
| RF06 | `Models/AcaoRealizada.cs` | `AcaoRealizada` | Registro de auditoria de acionamento |
| RF06 | `Repositories/CsvAcaoRealizadaRepository.cs` | `CsvAcaoRealizadaRepository` | Persistência (só-inserção) |
| RF07 | `Services/SimulationService.cs` | `SimulationService` (`ISimulationService`) | Geração de alvos fictícios |
| RF08 | `Models/SystemState.cs` | `SystemMode` (enum) | Modos de operação do sistema |
| RF08 | `Models/AlteracaoModo.cs` | `AlteracaoModo` | Registro de auditoria de troca de modo |
| RF08 | `Repositories/CsvAlteracaoModoRepository.cs` | `CsvAlteracaoModoRepository` | Persistência (só-inserção) |
| RF09 | `Services/AuthService.cs` | `AuthService` (`IAuthService`) | Login/logout/sessão |
| RF09 | `Services/PasswordHasher.cs` | `PasswordHasher` (`IPasswordHasher`) | Hash PBKDF2-HMACSHA256 |
| RF09 | `Data/DataSeeder.cs` | `DataSeeder` | Semeia usuário `admin` padrão |
| RF10 | `Services/PermissionService.cs` | `PermissionService` (`IPermissionService`) | Regras de acesso por `PerfilUsuario` |
| RF11 | `Services/IAuthService.cs` | `IAuthService.AlterarSenhaAsync` | Troca de senha com validação da atual |
| RF11 | `ViewModels/ProfileViewModel.cs` | `ProfileViewModel` | Orquestra a troca de senha na tela de Perfil |
| RF12 | `Models/ObjetoDetectado.cs` | `ObjetoDetectado` | Entidade de registro histórico |
| RF12 | `Repositories/CsvObjetoDetectadoRepository.cs` | `CsvObjetoDetectadoRepository` | Persistência em `objetos_detectados.csv` |
| RF13 | `ViewModels/ObjetosDetectadosViewModel.cs` | `ObjetosDetectadosViewModel` | Lista o histórico (`Itens`) |
| RF13 | `Views/ObjetosDetectadosView.xaml.cs` | `ObjetosDetectadosView` | Tela de tabela |
| RF14 | `Services/ObjetoDetectadoExportService.cs` | `ObjetoDetectadoExportService.ExportCsv/Xml/Pdf` | Exportação nos 3 formatos |
| RF15 | `Services/ObjetoDetectadoExportService.cs` | `ObjetoDetectadoExportService.ImportCsv/Xml` | Leitura de arquivo CSV/XML |
| RF15 | `ViewModels/ObjetosDetectadosViewModel.cs` | `ObjetosDetectadosViewModel.PodeImportar` | Restrição de perfil na importação |
| RF16 | `Repositories/IAcaoRealizadaRepository.cs` | `IAcaoRealizadaRepository` | Contrato de consulta (sem Update/Delete) |
| RF17 | `Repositories/IAlteracaoModoRepository.cs` | `IAlteracaoModoRepository` | Contrato de consulta (sem Update/Delete) |
| RF18 | `Repositories/IUsuarioRepository.cs` | `IUsuarioRepository` | Contrato CRUD de usuários |
| RF18 | `Services/IPermissionService.cs` | `IPermissionService.PodeGerenciarUsuarios` | Restrição a Administrador |
| RF19 | `Models/PreferenciasUsuario.cs` | `PreferenciasUsuario` | Entidade de preferências |
| RF19 | `Repositories/CsvPreferenciasUsuarioRepository.cs` | `CsvPreferenciasUsuarioRepository` | Persistência 1:1 por usuário |
| RF20 | `Services/LocalizationService.cs` | `LocalizationService` (`ILocalizationService`) | Troca de cultura pt-BR/en-US |
| RF20 | `Localization/LocExtension.cs` | `LocExtension` | Extensão XAML de texto localizado |
| RF21 | `Services/ThemeService.cs` | `ThemeService` (`IThemeService`) | Aplicação de tema |
| RF22 | `ViewModels/HelpDeskFormViewModel.cs` | `HelpDeskFormViewModel` | Orquestra envio do chamado |
| RF22 | `Models/ChamadoAjuda.cs` | `ChamadoAjuda` | Entidade do chamado |
| RF23 | `Repositories/IChamadoAjudaRepository.cs` | `IChamadoAjudaRepository` | Único repositório com `Update` (status/resposta) |
| RF24 | `Views/Shared/DashboardCanvas.cs` | `DashboardCanvas` | Anticolisão, limites, reescala |
| RF24 | `Views/Shared/DashboardCard.xaml.cs` | `DashboardCard` | Card arrastável/redimensionável |
| RF24 | `Services/DashboardLayoutRepository.cs` | `DashboardLayoutRepository` (`IDashboardLayoutRepository`) | Persistência do layout por usuário |
| RF25 | `Models/DashboardCardLayout.cs` | `DashboardCardLayout` | Posição/tamanho como fração 0..1 |
| RF26 | `Views/MonitoramentoView.xaml.cs` | `MonitoramentoView.SetLogPinned` | Realoca o console para a lateral fixa |
| RF26 | `Models/DashboardCardLayout.cs` | `IsPinnedRight` | Campo persistido do estado fixado |
| RF27 | `Models/DeadZone.cs` | `DeadZone` | Entidade de zona morta |
| RF27 | `Services/IDeadZoneService.cs` | `IDeadZoneService` | Avaliação de bloqueio |
| RF27 | `Services/IDeadZoneRepository.cs` | `IDeadZoneRepository` | Persistência de zonas |
| RF28 | `Services/ArduinoCliLocatorService.cs` | `ArduinoCliLocatorService.Locate` | Localização do `arduino-cli.exe` |
| RF29 | `Services/ArduinoCompilerService.cs` | `ArduinoCompilerService.CompileAsync` | Compilação assíncrona/cancelável |
| RF30 | `ViewModels/ArduinoSettingsViewModel.cs` | `ArduinoSettingsViewModel` | Orquestra monitor serial + reuso da conexão |
| RF31 | `Services/ArduinoSettingsRepository.cs` | `ArduinoSettingsRepository` (`IArduinoSettingsRepository`) | Persistência JSON das preferências da aba |
| RF32 | `installer/RadarTorres.iss` | Script Inno Setup | Geração do `Setup.exe` self-contained |

## Rastreabilidade requisito → documentação existente

| Requisito(s) | Documento de apoio no repositório |
|---|---|
| RF01, RF02, RF28–RF31 | `Docs/COMUNICACAO_ARDUINO.md` |
| RF03, RF04, RF24–RF26 | `Docs/ARQUITETURA.md` |
| RF05 | `Docs/ALGORITMO_SELECAO_TORRE.md` |
| RF08, RF12, RF16–RF19, RF22, RF23 | `Docs/MODELO_DADOS.md` |
| RF32 | `Docs/INSTALADOR.md` |
| Todos | `Docs/DOCUMENTACAO_TECNICA.md`, `Docs/CONTEXTO_PROJETO.md` |
