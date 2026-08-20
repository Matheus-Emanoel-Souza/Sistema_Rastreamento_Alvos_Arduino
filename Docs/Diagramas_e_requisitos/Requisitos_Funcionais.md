# Requisitos Funcionais

Levantados a partir do código-fonte (`src/RadarTorres.App/`), do firmware
(`Arduino/ArduinoSimulation.ino`) e da documentação existente (`Docs/*.md`). Requisitos
marcados como **(inferido)** não têm uma linha de código isolada como evidência direta, mas são
razoavelmente deduzidos do conjunto do sistema (nome de interface, uso documentado, ou
funcionalidade "em construção" citada no roadmap) — cada um deles indica isso explicitamente.

---

**RF01 – Comunicação serial com o Arduino**
**Descrição:** o sistema conecta a uma porta serial USB, lista portas disponíveis, envia e
recebe mensagens no protocolo textual `TIPO;CHAVE=VALOR;...`, e detecta perda de conexão por
watchdog.
**Ator(es):** Usuário (operador), Arduino.
**Origem/Evidência:** `Services/SerialCommunicationService.cs`, `Services/ISerialCommunicationService.cs`,
`Services/SerialProtocolParser.cs`, `Docs/COMUNICACAO_ARDUINO.md`.

**RF02 – Recepção e validação de leituras de alvo**
**Descrição:** o sistema interpreta mensagens `TARGET;ID=;ANGLE=;DIST=`, valida os campos
(numéricos, ângulo em `[0,360)`, distância ≥ 0) e converte leituras inválidas em mensagem de
erro registrada no console, sem interromper a aplicação.
**Ator(es):** Arduino, Sistema.
**Origem/Evidência:** `Services/SerialProtocolParser.cs` (`TryParse`), `Docs/COMUNICACAO_ARDUINO.md`, seção 2.

**RF03 – Rastreamento de alvos em tempo real**
**Descrição:** cada leitura válida cria um novo alvo (`Target`) ou atualiza um alvo existente
com o mesmo `Id`; alvos sem leitura recente são expirados por timeout configurável.
**Ator(es):** Sistema.
**Origem/Evidência:** `Services/TargetTrackingService.cs`, `Services/ITargetTrackingService.cs`
(`ProcessReading`, `PurgeStaleTargets`).

**RF04 – Exibição do radar circular em tempo real**
**Descrição:** os alvos ativos são exibidos em um radar circular dividido em 4 quadrantes,
reposicionados continuamente conforme novas leituras chegam.
**Ator(es):** Usuário.
**Origem/Evidência:** `Views/RadarControl.xaml.cs`, `Helpers/CoordinateConverter.cs`,
`Docs/ARQUITETURA.md`, seção 3.

**RF05 – Seleção automática de torre**
**Descrição:** o sistema seleciona, entre as torres configuradas, a mais adequada para cada
alvo detectado, considerando quadrante preferencial e distância; respeita zonas mortas.
**Ator(es):** Sistema.
**Origem/Evidência:** `Services/TowerSelectionService.cs`, `Services/ITowerSelectionService.cs`,
`Docs/ALGORITMO_SELECAO_TORRE.md`.

**RF06 – Acionamento demonstrativo (manual e automático)**
**Descrição:** o sistema autoriza e executa/simula um acionamento demonstrativo (nunca
armamento real) sobre um alvo, respeitando a distância mínima de segurança e as zonas mortas
ativas; cada tentativa (autorizada, bloqueada ou com erro) é auditada.
**Ator(es):** Usuário, Sistema.
**Origem/Evidência:** `Services/FireControlService.cs`, `Services/IFireControlService.cs`,
`Models/AcaoRealizada.cs`.

**RF07 – Modo de simulação sem hardware**
**Descrição:** o sistema gera alvos fictícios em memória (posição, movimento), produzindo o
mesmo formato de dado (`SensorReading`) que a leitura real, permitindo uso completo sem Arduino
conectado.
**Ator(es):** Usuário, Sistema.
**Origem/Evidência:** `Services/SimulationService.cs`, `Services/ISimulationService.cs`.

**RF08 – Troca de modo de operação do sistema**
**Descrição:** o usuário troca entre os modos `Off`, `LocationOnly`, `LocationAutoTower`,
`LocationAutoFire`, `Maintenance` e `Emergency`, com confirmação antes de aplicar e registro em
auditoria (sucesso ou erro).
**Ator(es):** Usuário.
**Origem/Evidência:** `Models/SystemState.cs` (`SystemMode`), `Models/AlteracaoModo.cs`,
`Docs/MODELO_DADOS.md`, seção 3.

**RF09 – Autenticação multiusuário**
**Descrição:** o sistema exige login (usuário/senha) independente da conta do Windows, com
senha protegida por hash PBKDF2-HMACSHA256 + salt por usuário; usuário padrão `admin`/`admin123`
é semeado no primeiro uso.
**Ator(es):** Usuário.
**Origem/Evidência:** `Services/AuthService.cs`, `Services/IAuthService.cs`,
`Services/PasswordHasher.cs`, `Data/DataSeeder.cs`.

**RF10 – Controle de acesso por perfil**
**Descrição:** o sistema restringe visibilidade de menu e execução de ações conforme o perfil
do usuário logado (`Administrador`, `Operador`, `Visualizador`) — Visualizador é somente
consulta.
**Ator(es):** Sistema, Usuário.
**Origem/Evidência:** `Services/PermissionService.cs`, `Services/IPermissionService.cs`,
`Models/Auditoria.cs` (`PerfilUsuario`).

**RF11 – Troca de senha**
**Descrição:** o usuário logado altera sua própria senha, com validação da senha atual antes da
troca.
**Ator(es):** Usuário.
**Origem/Evidência:** `Services/IAuthService.cs` (`AlterarSenhaAsync`), `ViewModels/ProfileViewModel.cs`.

**RF12 – Registro histórico de objetos detectados**
**Descrição:** a primeira detecção de cada alvo é gravada como um registro histórico
(`ObjetoDetectado`) com posição, quadrante, horário e dispositivo de origem.
**Ator(es):** Sistema.
**Origem/Evidência:** `Models/ObjetoDetectado.cs`, `Repositories/CsvObjetoDetectadoRepository.cs`,
`Docs/MODELO_DADOS.md`, seção 3 (`MainViewModel.OnTargetCreated`).

**RF13 – Visualização de objetos detectados em tabela**
**Descrição:** o sistema exibe o histórico de detecções em uma tela de tabela dedicada.
**Ator(es):** Usuário.
**Origem/Evidência:** `Views/ObjetosDetectadosView.xaml.cs`, `ViewModels/ObjetosDetectadosViewModel.cs`.

**RF14 – Exportação de objetos detectados (CSV, XML, PDF)**
**Descrição:** o usuário exporta a lista de objetos detectados nos formatos CSV, XML ou PDF,
disponível para qualquer perfil autenticado.
**Ator(es):** Usuário.
**Origem/Evidência:** `Services/ObjetoDetectadoExportService.cs`, `Services/IObjetoDetectadoExportService.cs`
(`ExportCsv`, `ExportXml`, `ExportPdf`).

**RF15 – Importação de objetos detectados (CSV, XML)**
**Descrição:** o usuário importa registros de um arquivo CSV ou XML no mesmo formato de
exportação; cada linha importada é inserida como um novo registro (Id reatribuído); restrito a
perfis que podem executar ações (Visualizador não pode importar).
**Ator(es):** Usuário.
**Origem/Evidência:** `Services/IObjetoDetectadoExportService.cs` (`ImportCsv`, `ImportXml`),
`ViewModels/ObjetosDetectadosViewModel.cs` (`PodeImportar`).

**RF16 – Auditoria de ações realizadas** *(tela ainda pendente — inferido)*
**Descrição:** cada acionamento é gravado para consulta posterior, exclusivamente somente-inserção.
**Ator(es):** Sistema, Usuário (consulta).
**Origem/Evidência:** `Repositories/IAcaoRealizadaRepository.cs`, `Models/AcaoRealizada.cs`;
tela de listagem citada como "pendente" em `Docs/CONTEXTO_PROJETO.md`, seção 3 — o registro já
existe, mas a tela de consulta dedicada ainda não foi confirmada como implementada.

**RF17 – Auditoria de alterações de modo** *(tela ainda pendente — inferido)*
**Descrição:** cada troca de `SystemMode` é gravada para consulta posterior.
**Ator(es):** Sistema, Usuário (consulta).
**Origem/Evidência:** `Repositories/IAlteracaoModoRepository.cs`, `Models/AlteracaoModo.cs`;
mesma ressalva de RF16 (`Docs/CONTEXTO_PROJETO.md`, seção 3).

**RF18 – Gerenciamento de usuários** *(tela ainda pendente — inferido)*
**Descrição:** um Administrador cria, edita e inativa contas de usuário.
**Ator(es):** Usuário (Administrador).
**Origem/Evidência:** `Repositories/IUsuarioRepository.cs`, `Services/IPermissionService.cs`
(`PodeGerenciarUsuarios`); citado como "pendente (CRUD completo, restrito a Administrador)" em
`Docs/CONTEXTO_PROJETO.md`, seção 3.

**RF19 – Preferências de usuário (idioma, tema, sidebar)**
**Descrição:** o sistema salva e restaura, por usuário, idioma preferido, tema (claro/escuro/
sistema) e estado recolhido/expandido da barra lateral.
**Ator(es):** Usuário.
**Origem/Evidência:** `Models/PreferenciasUsuario.cs`, `Repositories/CsvPreferenciasUsuarioRepository.cs`,
`Services/ILocalizationService.cs`, `Services/IThemeService.cs`.

**RF20 – Internacionalização (pt-BR / en-US)**
**Descrição:** a interface pode ser exibida em português (Brasil) ou inglês (EUA), trocável em
tempo de execução.
**Ator(es):** Usuário.
**Origem/Evidência:** `Services/LocalizationService.cs`, `Localization/LocExtension.cs`,
`Docs/CONTEXTO_PROJETO.md`, seção 3.

**RF21 – Tema claro/escuro/sistema**
**Descrição:** a interface pode ser exibida em tema claro, escuro, ou seguindo a configuração do
Windows, trocável em tempo de execução.
**Ator(es):** Usuário.
**Origem/Evidência:** `Services/ThemeService.cs`, `Services/IThemeService.cs`, `Models/PreferenciasUsuario.cs` (`TemaPreferido`).

**RF22 – Abertura de chamado de ajuda/suporte**
**Descrição:** o usuário abre um chamado de suporte (título, descrição, categoria, módulo
relacionado, mensagem de erro), com usuário e data preenchidos automaticamente.
**Ator(es):** Usuário.
**Origem/Evidência:** `Models/ChamadoAjuda.cs`, `ViewModels/HelpDeskFormViewModel.cs`,
`Views/Shell/HelpDeskFormWindow.xaml.cs`.

**RF23 – Tratamento administrativo de chamados de ajuda** *(inferido)*
**Descrição:** um Administrador altera status e resposta de um chamado (`Status`, `RespostaAdmin`).
**Ator(es):** Usuário (Administrador).
**Origem/Evidência:** `Repositories/IChamadoAjudaRepository.cs` (único repositório que expõe
`Update`), `Models/ChamadoAjuda.cs` (`RespostaAdmin`, `DataResolucao`) — a tela de tratamento em
si não foi lida linha a linha nesta varredura, apenas o repositório que a sustentaria.

**RF24 – Painel principal com cards personalizáveis**
**Descrição:** o usuário arrasta e redimensiona cards de indicadores no painel principal; o
layout (posição, tamanho, visibilidade, ordem) é salvo por usuário e restaurado no próximo
acesso; comando de restaurar layout padrão disponível.
**Ator(es):** Usuário.
**Origem/Evidência:** `Views/Shared/DashboardCanvas.cs`, `Views/Shared/DashboardCard.xaml.cs`,
`Services/DashboardLayoutRepository.cs`, `Models/DashboardCardLayout.cs`, `Docs/ARQUITETURA.md`, seção 5.2.

**RF25 – Layout responsivo do painel**
**Descrição:** posição/tamanho dos cards são guardados como fração (0..1) do canvas, mantendo a
mesma proporção ao redimensionar a janela ou trocar de resolução.
**Ator(es):** Sistema.
**Origem/Evidência:** `Models/DashboardCardLayout.cs`, `Docs/ARQUITETURA.md`, seção 5.2.

**RF26 – Console de eventos fixável na lateral**
**Descrição:** o usuário fixa o card de console de eventos na borda direita da tela de
Monitoramento, saindo do canvas arrastável; o estado fixado/não fixado é persistido.
**Ator(es):** Usuário.
**Origem/Evidência:** `Models/DashboardCardLayout.cs` (`IsPinnedRight`),
`Views/MonitoramentoView.xaml.cs` (`SetLogPinned`), `Docs/ARQUITETURA.md`, seção 5.4.

**RF27 – Gestão de zonas mortas (áreas de exclusão)**
**Descrição:** um Administrador cria, ativa/desativa e remove zonas (por quadrante ou por faixa
de distância) onde alvos não recebem torre nem podem ser acionados, embora continuem
visíveis/rastreados; demais perfis visualizam a lista somente leitura.
**Ator(es):** Usuário (Administrador), Sistema.
**Origem/Evidência:** `Models/DeadZone.cs`, `Services/IDeadZoneService.cs`,
`Services/IDeadZoneRepository.cs`, `Services/IPermissionService.cs` (`PodeGerenciarZonasMortas`),
`Docs/ARQUITETURA.md`, seção 5.3.

**RF28 – Detecção do Arduino CLI**
**Descrição:** o sistema localiza `arduino-cli.exe` no computador (caminho salvo, pasta do
aplicativo, `PATH`, locais comuns de instalação), sem baixar nada automaticamente, e exibe a
versão detectada.
**Ator(es):** Usuário, Sistema.
**Origem/Evidência:** `Services/ArduinoCliLocatorService.cs`, `Services/IArduinoCliLocatorService.cs`,
`Docs/COMUNICACAO_ARDUINO.md`, seção 8.1.

**RF29 – Compilação de sketch Arduino pela interface**
**Descrição:** o usuário seleciona um sketch `.ino` e uma placa (FQBN), compila via
`arduino-cli compile` como processo filho assíncrono e cancelável, acompanhando a saída em
tempo real; sucesso/falha decidido só pelo código de saída do processo.
**Ator(es):** Usuário, Sistema.
**Origem/Evidência:** `Services/ArduinoCompilerService.cs`, `Services/IArduinoCompilerService.cs`,
`Docs/COMUNICACAO_ARDUINO.md`, seção 8.3.

**RF30 – Monitor serial pela aba Configurações do Arduino**
**Descrição:** o usuário acompanha mensagens da porta serial diretamente nesta aba, reutilizando
a mesma conexão da tela de Monitoramento (nunca duas portas abertas concorrentemente); em caso
de conflito de parâmetros, o sistema pede confirmação antes de reconectar.
**Ator(es):** Usuário, Sistema, Arduino.
**Origem/Evidência:** `ViewModels/ArduinoSettingsViewModel.cs`, `Docs/COMUNICACAO_ARDUINO.md`, seção 8.4.

**RF31 – Persistência de preferências da aba Arduino**
**Descrição:** caminho do CLI, último sketch, FQBN, porta/baud e preferências do console são
salvos e restaurados entre sessões.
**Ator(es):** Sistema.
**Origem/Evidência:** `Services/ArduinoSettingsRepository.cs`, `Services/IArduinoSettingsRepository.cs`.

**RF32 – Instalação via instalador Windows**
**Descrição:** o sistema é distribuído como um instalador self-contained (embute o .NET 9
Desktop Runtime), preservando configurações do usuário em upgrades.
**Ator(es):** Usuário.
**Origem/Evidência:** `installer/RadarTorres.iss`, `dist/Setup.exe`, `Docs/INSTALADOR.md`.

---

## Tabela consolidada

| ID | Requisito Funcional | Descrição | Ator | Evidência |
|---|---|---|---|---|
| RF01 | Comunicação serial com o Arduino | Conectar, listar portas, enviar/receber mensagens, watchdog | Usuário, Arduino | `SerialCommunicationService.cs`, `SerialProtocolParser.cs` |
| RF02 | Recepção e validação de leituras de alvo | Parse e validação de `TARGET;...` | Arduino, Sistema | `SerialProtocolParser.cs` |
| RF03 | Rastreamento de alvos em tempo real | Criar/atualizar/expirar `Target` | Sistema | `TargetTrackingService.cs` |
| RF04 | Exibição do radar circular | Desenho em tempo real por quadrante | Usuário | `RadarControl.xaml.cs` |
| RF05 | Seleção automática de torre | Algoritmo de seleção por distância/quadrante | Sistema | `TowerSelectionService.cs` |
| RF06 | Acionamento demonstrativo | Autoriza/executa/audita acionamento | Usuário, Sistema | `FireControlService.cs` |
| RF07 | Modo de simulação sem hardware | Gera alvos fictícios | Usuário, Sistema | `SimulationService.cs` |
| RF08 | Troca de modo de operação | 6 modos, confirmação, auditoria | Usuário | `SystemState.cs`, `AlteracaoModo.cs` |
| RF09 | Autenticação multiusuário | Login com hash PBKDF2 | Usuário | `AuthService.cs`, `PasswordHasher.cs` |
| RF10 | Controle de acesso por perfil | Menu/ações por `PerfilUsuario` | Sistema, Usuário | `PermissionService.cs` |
| RF11 | Troca de senha | Validação da senha atual | Usuário | `IAuthService.cs` |
| RF12 | Registro histórico de detecções | 1ª detecção vira `ObjetoDetectado` | Sistema | `CsvObjetoDetectadoRepository.cs` |
| RF13 | Visualização de objetos detectados | Tabela dedicada | Usuário | `ObjetosDetectadosViewModel.cs` |
| RF14 | Exportação de objetos detectados | CSV/XML/PDF | Usuário | `ObjetoDetectadoExportService.cs` |
| RF15 | Importação de objetos detectados | CSV/XML, restrito por perfil | Usuário | `IObjetoDetectadoExportService.cs` |
| RF16 | Auditoria de ações realizadas *(inferido)* | Registro só-inserção de acionamentos | Sistema, Usuário | `IAcaoRealizadaRepository.cs` |
| RF17 | Auditoria de alterações de modo *(inferido)* | Registro só-inserção de trocas de modo | Sistema, Usuário | `IAlteracaoModoRepository.cs` |
| RF18 | Gerenciamento de usuários *(inferido)* | CRUD de contas por Administrador | Usuário (Admin) | `IUsuarioRepository.cs`, `IPermissionService.cs` |
| RF19 | Preferências de usuário | Idioma, tema, sidebar | Usuário | `PreferenciasUsuario.cs` |
| RF20 | Internacionalização pt-BR/en-US | Troca de idioma em runtime | Usuário | `LocalizationService.cs` |
| RF21 | Tema claro/escuro/sistema | Troca de tema em runtime | Usuário | `ThemeService.cs` |
| RF22 | Abertura de chamado de ajuda | Formulário de suporte | Usuário | `HelpDeskFormViewModel.cs` |
| RF23 | Tratamento administrativo de chamados *(inferido)* | Status/resposta do admin | Usuário (Admin) | `IChamadoAjudaRepository.cs` |
| RF24 | Painel com cards personalizáveis | Arrastar/redimensionar, persistido | Usuário | `DashboardCanvas.cs` |
| RF25 | Layout responsivo do painel | Frações 0..1, reescala proporcional | Sistema | `DashboardCardLayout.cs` |
| RF26 | Console de eventos fixável | Fixar/desafixar na lateral | Usuário | `MonitoramentoView.xaml.cs` |
| RF27 | Gestão de zonas mortas | Área sem torre/acionamento | Usuário (Admin), Sistema | `DeadZone.cs`, `IDeadZoneService.cs` |
| RF28 | Detecção do Arduino CLI | Localiza `arduino-cli.exe` | Usuário, Sistema | `ArduinoCliLocatorService.cs` |
| RF29 | Compilação de sketch pela interface | `arduino-cli compile` assíncrono/cancelável | Usuário, Sistema | `ArduinoCompilerService.cs` |
| RF30 | Monitor serial na aba Arduino | Reaproveita conexão existente | Usuário, Sistema, Arduino | `ArduinoSettingsViewModel.cs` |
| RF31 | Persistência de preferências da aba Arduino | JSON em `%LocalAppData%` | Sistema | `ArduinoSettingsRepository.cs` |
| RF32 | Instalação via instalador Windows | Setup self-contained | Usuário | `installer/RadarTorres.iss` |
