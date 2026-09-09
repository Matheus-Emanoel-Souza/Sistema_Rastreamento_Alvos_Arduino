# Matriz de Rastreabilidade

Relaciona cada requisito ao(s) componente(s) real(is) que o implementam, comprovando que o
levantamento não é especulativo. Cobre os requisitos funcionais e não funcionais consolidados em
`Requisitos_RadarTorres.pdf` (nesta mesma pasta), além de
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
| RF07 | Parcial (ver D1) | `Models/SystemState.cs` | `SystemMode` (enum — ainda 6 valores, não Verde/Amarelo/Vermelho) | Modos de operação do sistema |
| RF07 | Parcial (ver D1) | `Models/AlteracaoModo.cs`, `Repositories/CsvAlteracaoModoRepository.cs` | — | Registro de auditoria de troca de modo |
| RF08 | Implementado | `Services/AuthService.cs` | `AuthService` (`IAuthService`) | Login/logout/sessão |
| RF08 | Implementado | `Services/PasswordHasher.cs` | `PasswordHasher` (`IPasswordHasher`) | Hash PBKDF2-HMACSHA256 |
| RF08 | Implementado | `Data/DataSeeder.cs` | `DataSeeder` | Semeia usuário `admin` padrão |
| RF09 | Implementado | `Services/PermissionService.cs` | `PermissionService` (`IPermissionService`) | Regras de acesso por `PerfilUsuario` |
| RF10 | Implementado | `Services/IAuthService.cs` | `IAuthService.AlterarSenhaAsync` | Troca de senha com validação da atual |
| RF10 | Implementado | `ViewModels/ProfileViewModel.cs` | `ProfileViewModel` | Orquestra a troca de senha na tela de Perfil |
| RF11 | Implementado | `Models/ObjetoDetectado.cs` | `ObjetoDetectado` | Entidade de registro histórico |
| RF11 | Implementado | `Repositories/CsvObjetoDetectadoRepository.cs` | `CsvObjetoDetectadoRepository` | Persistência em `objetos_detectados.csv` |
| RF12 | Implementado | `ViewModels/ObjetosDetectadosViewModel.cs` | `ObjetosDetectadosViewModel` | Lista o histórico (`Itens`) |
| RF12 | Implementado | `Views/ObjetosDetectadosView.xaml.cs` | `ObjetosDetectadosView` | Tela de tabela |
| RF13 | Implementado | `Services/ObjetoDetectadoExportService.cs` | `ObjetoDetectadoExportService.ExportCsv/Xml/Pdf` | Exportação nos 3 formatos |
| RF14 | Implementado | `Services/ObjetoDetectadoExportService.cs` | `ObjetoDetectadoExportService.ImportCsv/Xml` | Leitura de arquivo CSV/XML |
| RF14 | Implementado | `ViewModels/ObjetosDetectadosViewModel.cs` | `ObjetosDetectadosViewModel.PodeImportar` | Restrição de perfil na importação |
| RF15 | Parcial — registro ok, tela pendente | `Repositories/IAcaoRealizadaRepository.cs` | `IAcaoRealizadaRepository` | Contrato de consulta (sem Update/Delete); gravação ocorre em `FireControlService` |
| RF16 | Parcial — registro ok, tela pendente | `Repositories/IAlteracaoModoRepository.cs` | `IAlteracaoModoRepository` | Contrato de consulta (sem Update/Delete); gravação ocorre em `MainViewModel` |
| RF17 | Planejado — sem UI | `Repositories/IUsuarioRepository.cs` | `IUsuarioRepository` | Contrato CRUD de usuários (nenhum ViewModel/View o consome hoje) |
| RF17 | Planejado — sem UI | `Services/IPermissionService.cs` | `IPermissionService.PodeGerenciarUsuarios` | Restrição a Administrador (pronta, mas sem tela para aplicar) |
| RF18 | Implementado | `Models/PreferenciasUsuario.cs` | `PreferenciasUsuario` | Entidade de preferências |
| RF18 | Implementado | `Repositories/CsvPreferenciasUsuarioRepository.cs` | `CsvPreferenciasUsuarioRepository` | Persistência 1:1 por usuário |
| RF18 | Implementado | `Services/LocalizationService.cs` | `LocalizationService` (`ILocalizationService`) | Troca de cultura pt-BR/en-US |
| RF18 | Implementado | `Localization/LocExtension.cs` | `LocExtension` | Extensão XAML de texto localizado |
| RF18 | Implementado | `Services/ThemeService.cs` | `ThemeService` (`IThemeService`) | Aplicação de tema |
| RF19 | Implementado | `ViewModels/HelpDeskFormViewModel.cs` | `HelpDeskFormViewModel` | Orquestra envio do chamado |
| RF19 | Implementado | `Models/ChamadoAjuda.cs` | `ChamadoAjuda` | Entidade do chamado |
| RF20 | Planejado — sem UI | `Repositories/IChamadoAjudaRepository.cs` | `IChamadoAjudaRepository` | Único repositório com `Update` (situação/resposta), sem tela que o consuma |
| RF21 | Implementado | `Views/Shared/DashboardCanvas.cs` | `DashboardCanvas` | Anticolisão, limites, reescala |
| RF21 | Implementado | `Views/Shared/DashboardCard.xaml.cs` | `DashboardCard` | Card arrastável/redimensionável |
| RF21 | Implementado | `Services/DashboardLayoutRepository.cs` | `DashboardLayoutRepository` (`IDashboardLayoutRepository`) | Persistência do layout por usuário |
| RF22 | Implementado | `Views/MonitoramentoView.xaml.cs` | `MonitoramentoView.SetLogPinned` | Realoca o console para a lateral fixa |
| RF22 | Implementado | `Models/DashboardCardLayout.cs` | `IsPinnedRight` | Campo persistido do estado fixado |
| RF23 | Implementado | `Models/DeadZone.cs` | `DeadZone` | Entidade de zona morta |
| RF23 | Implementado | `Services/IDeadZoneService.cs` | `IDeadZoneService` | Avaliação de bloqueio |
| RF23 | Implementado | `Services/IDeadZoneRepository.cs` | `IDeadZoneRepository` | Persistência de zonas |
| RF24 | Implementado | `Services/ArduinoCliLocatorService.cs` | `ArduinoCliLocatorService.Locate` | Localização do `arduino-cli.exe` |
| RF24 | Implementado | `Services/ArduinoCompilerService.cs` | `ArduinoCompilerService.CompileAsync` | Compilação assíncrona/cancelável |
| RF25 | Implementado | `ViewModels/ArduinoSettingsViewModel.cs` | `ArduinoSettingsViewModel` | Orquestra monitor serial + reuso da conexão |
| RF25 | Implementado | `Services/ArduinoSettingsRepository.cs` | `ArduinoSettingsRepository` (`IArduinoSettingsRepository`) | Persistência JSON das preferências da aba (inclui baud rate) |

## Requisitos Não Funcionais → implementação

| Requisito | Arquivo/Módulo | Classe/Função | Observação |
|---|---|---|---|
| RNF09 | `Services/AuthService.cs`, `Services/PermissionService.cs` | — | Controle de acesso por perfil — evidência principal em RF09 (funcional), aqui só a garantia de que a checagem é sempre centralizada |
| RNF11 | `Services/LocalizationService.cs` | `LocalizationService` | Descreve só a troca em runtime, sem duplicar RF18 |
| RNF12 | `Services/ThemeService.cs`, `Themes/Light.xaml`, `Themes/Dark.xaml` | `ThemeService` | Descreve só a consistência visual, sem duplicar RF18 |
| RNF14 | `Services/ArduinoCompilerService.cs` | `CompileAsync` (`CancellationToken`) | Não bloqueio/cancelamento, sem duplicar RF24 |
| RNF18 | `tests/RadarTorres.Tests/*.cs` | — | Cobertura de componentes críticos; status Parcial (só a aba Arduino CLI tem teste hoje) |

## Decisões Arquiteturais → implementação (ver `Decisoes_Arquiteturais.md`)

| ID | Arquivo/Módulo | Observação |
|---|---|---|
| DA01 | `Repositories/I*Repository.cs` | Persistência substituível — decisão interna, não requisito |
| DA02 | `Services/SerialProtocolParser.cs` | Protocolo serial centralizado, com codificação e terminador de linha fixos — decisão interna |
| DA03 | `ViewModelBase.cs`, `RelayCommand.cs` | MVVM manual — decisão tecnológica |
| DA04 | interfaces `I*Service` | Independência de WPF em Services/Models — decisão de camadas |
| DA05 | `Views/Shared/DashboardCanvas.cs` | Anticolisão por rejeição — decisão de UX |
| DA06 | ViewModels (coleções `ObservableCollection`) | Mutação de coleções da UI sempre na thread de UI — decisão de implementação |

## Limitações e Divergências → implementação (ver `Limitacoes_Conhecidas.md`)

| ID | Arquivo/Módulo | Observação |
|---|---|---|
| L01 | `appsettings.json` (`SerialSettings.ReconnectAttempts`) | Configuração existe, retry automático não implementado |
| L02 | `Services/NavigationService.cs` | Telas ainda em `PlaceholderView`: Ações realizadas, Histórico de modos, Usuários, Chamados/Ajuda, Configurações |
| L03 | `Docs/Tecnica/DOCUMENTACAO_TECNICA.md` | Bugs conhecidos não corrigidos (referência, sem duplicar o texto) |
| D1 | `Models/SystemState.cs` (`SystemMode`), `ViewModels/MainViewModel.cs` (`ManualFireCommand`) | Enum ainda com 6 valores antigos; acionamento manual ainda existe e não é restrito por modo |

## Rastreabilidade requisito → caso de uso

A matriz Caso de Uso × Requisito completa (28 casos de uso e atores) está em `Casos_de_Uso.md`,
seção 5, para não duplicar aqui — ela nasce diretamente das linhas de RF acima.

## Rastreabilidade requisito → documentação existente

| Requisito(s) | Documento de apoio no repositório |
|---|---|
| RF01, RF02, RF24, RF25 | `Docs/Tecnica/COMUNICACAO_ARDUINO.md` |
| RF03, RF04, RF21, RF22 | `Docs/Tecnica/ARQUITETURA.md` |
| RF05 | `Docs/Tecnica/ALGORITMO_SELECAO_TORRE.md` |
| RF07, RF11, RF15–RF18, RF19, RF20 | `Docs/Tecnica/MODELO_DADOS.md` |
| RNF15, RNF16 | `Docs/Projeto/INSTALADOR.md` |
| Todos | `Docs/Tecnica/DOCUMENTACAO_TECNICA.md`, `Docs/Projeto/CONTEXTO_PROJETO.md` |
