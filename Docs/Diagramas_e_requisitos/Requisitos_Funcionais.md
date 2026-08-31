# Requisitos Funcionais

Levantados a partir do código-fonte (`src/RadarTorres.App/`), do firmware
(`Arduino/ArduinoSimulation.ino`) e da documentação existente (`Docs/*.md`).

**Convenções desta revisão:**

* A descrição de cada requisito evita citar classe/método internos — isso fica exclusivamente
  na `Matriz_de_Rastreabilidade.md`, para não misturar "o que o sistema faz" com "como foi
  implementado".
* **Prioridade** segue o critério: funcionalidades do fluxo central de operação (comunicação
  serial, detecção/rastreamento, seleção de torre, modos de operação e mecanismos de
  segurança/autorização) recebem **Alta**; funcionalidades de suporte administrativo e
  ferramental (auditoria, gestão de usuários, aba do Arduino CLI) recebem **Média**;
  conveniências de UX sem impacto operacional (exportação, preferências, idioma, tema,
  chamados, layout do painel) recebem **Baixa**.
* **Status** reflete o estado real de implementação hoje, não a intenção: `Implementado` |
  `Parcial` | `Planejado` | `Removido da especificação funcional`.
* Requisitos que descreviam decisão de implementação, restrição arquitetural ou característica
  de qualidade em vez de comportamento observável do produto foram removidos desta lista e
  reclassificados — ver `Requisitos_Nao_Funcionais.md` e `Decisoes_Arquiteturais.md`. Nenhum ID
  foi renumerado, para preservar a rastreabilidade histórica; o ID removido permanece como
  registro, com a nova localização indicada.

---

**RF01 – Comunicação serial com o Arduino**
**Descrição:** o sistema deve conectar a uma porta serial USB, listar as portas disponíveis,
enviar e receber mensagens no protocolo textual `TIPO;CHAVE=VALOR;...`, e detectar perda de
conexão por watchdog.
**Atores:** Usuário (operador), Arduino.
**Prioridade:** Alta.
**Status:** Implementado.
**Evidência:** `Services/SerialCommunicationService.cs`, `Services/ISerialCommunicationService.cs`,
`Services/SerialProtocolParser.cs`, `Docs/COMUNICACAO_ARDUINO.md`.

**RF02 – Recepção e validação de leituras de alvo**
**Descrição:** o sistema deve interpretar mensagens de leitura de alvo, validar os campos
(numéricos, ângulo em `[0,360)`, distância ≥ 0) e registrar leituras inválidas como erro no
console, sem interromper a aplicação.
**Atores:** Arduino, Sistema.
**Prioridade:** Alta.
**Status:** Implementado.
**Evidência:** `Services/SerialProtocolParser.cs` (`TryParse`), `Docs/COMUNICACAO_ARDUINO.md`, seção 2.

**RF03 – Rastreamento de alvos em tempo real**
**Descrição:** o sistema deve criar um novo alvo a cada leitura válida com identificador inédito,
atualizar o alvo existente quando o identificador já for conhecido, e expirar automaticamente
alvos sem leitura recente após um tempo configurável.
**Atores:** Sistema.
**Prioridade:** Alta.
**Status:** Implementado.
**Evidência:** `Services/TargetTrackingService.cs`, `Services/ITargetTrackingService.cs`
(`ProcessReading`, `PurgeStaleTargets`).

**RF04 – Exibição do radar circular em tempo real**
**Descrição:** o sistema deve exibir os alvos ativos em um radar circular dividido em 4
quadrantes, reposicionando-os continuamente conforme novas leituras chegam.
**Atores:** Usuário.
**Prioridade:** Alta.
**Status:** Implementado.
**Evidência:** `Views/RadarControl.xaml.cs`, `Helpers/CoordinateConverter.cs`,
`Docs/ARQUITETURA.md`, seção 3.

**RF05 – Seleção automática de torre**
**Descrição:** o sistema deve selecionar, entre as torres configuradas, a mais adequada para
cada alvo detectado, considerando quadrante preferencial e distância, e respeitando zonas
mortas ativas.
**Atores:** Sistema.
**Prioridade:** Alta.
**Status:** Implementado.
**Evidência:** `Services/TowerSelectionService.cs`, `Services/ITowerSelectionService.cs`,
`Docs/ALGORITMO_SELECAO_TORRE.md`.

**RF06 – Acionamento demonstrativo automático**
**Descrição:** no modo **Vermelho**, o sistema deve realizar automaticamente o acionamento
demonstrativo (laser de baixa potência, LED ou simulação — nunca armamento real) sobre o alvo
acompanhado pela torre selecionada, respeitando as regras de segurança (distância mínima e
zonas mortas ativas). No modo **Amarelo**, as torres devem apenas acompanhar os alvos, sem
realizar acionamento. Toda tentativa de acionamento (autorizada, bloqueada ou com erro) deve
ser registrada em auditoria.
**Atores:** Sistema.
**Prioridade:** Alta.
**Status:** Implementado — ver divergência D1 em `Limitacoes_Conhecidas.md` (o código ainda expõe
um caminho de acionamento manual não previsto nesta especificação).
**Evidência:** `Services/FireControlService.cs`, `Services/IFireControlService.cs`,
`Models/AcaoRealizada.cs`.

**RF07 – Removido da especificação funcional**
**Motivo:** o modo de simulação sem hardware existe exclusivamente para desenvolvimento, testes
e demonstração do sistema sem um Arduino conectado — não é uma função operacional do produto
entregue ao usuário final, e sim um recurso de apoio a desenvolvimento/demonstração. Permanece
documentado tecnicamente em `Docs/DOCUMENTACAO_TECNICA.md` (seção `SimulationService`) e em
`README.md` ("Modo de simulação"); nenhum código foi alterado.

**RF08 – Modos de operação do sistema**
**Descrição:** o sistema deve operar em exatamente três estados:
* **Verde** — sistema ligado, porém sem operação funcional de rastreamento ou acionamento.
* **Amarelo** — sistema detecta e rastreia alvos, e as torres acompanham automaticamente o alvo
  selecionado, mas nenhum acionamento é realizado.
* **Vermelho** — sistema detecta e rastreia alvos, as torres acompanham automaticamente, e o
  acionamento demonstrativo ocorre automaticamente quando autorizado pelas regras de segurança
  (RF06).

A troca de estado deve exigir confirmação do usuário antes de aplicar e ser registrada em
auditoria (sucesso ou erro).
**Atores:** Usuário.
**Prioridade:** Alta.
**Status:** Parcial — o comportamento de "rastrear sem acionar" e "rastrear e acionar
automaticamente" já existe no sistema, mas rotulado com uma nomenclatura de modos diferente da
especificada aqui; não há um estado único claramente equivalente a "Verde". Ver divergência D1
em `Limitacoes_Conhecidas.md`.
**Evidência:** `Models/SystemState.cs` (`SystemMode`), `Models/AlteracaoModo.cs`,
`Docs/MODELO_DADOS.md`, seção 3.

**RF09 – Autenticação multiusuário**
**Descrição:** o sistema deve exigir login (usuário/senha) independente da conta do Windows,
com senha protegida por hash e salt por usuário; um usuário padrão deve ser criado
automaticamente no primeiro uso.
**Atores:** Usuário.
**Prioridade:** Alta.
**Status:** Implementado.
**Evidência:** `Services/AuthService.cs`, `Services/IAuthService.cs`,
`Services/PasswordHasher.cs`, `Data/DataSeeder.cs`.

**RF10 – Controle de acesso por perfil**
**Descrição:** o sistema deve restringir a visibilidade de menu e a execução de ações conforme
o perfil do usuário logado (Administrador, Operador, Visualizador) — o perfil Visualizador deve
ser somente consulta.
**Atores:** Sistema, Usuário.
**Prioridade:** Alta.
**Status:** Implementado.
**Evidência:** `Services/PermissionService.cs`, `Services/IPermissionService.cs`,
`Models/Auditoria.cs` (`PerfilUsuario`).

**RF11 – Troca de senha**
**Descrição:** o usuário logado deve poder alterar sua própria senha, mediante validação da
senha atual antes da troca.
**Atores:** Usuário.
**Prioridade:** Média.
**Status:** Implementado.
**Evidência:** `Services/IAuthService.cs` (`AlterarSenhaAsync`), `ViewModels/ProfileViewModel.cs`.

**RF12 – Registro histórico de objetos detectados**
**Descrição:** a primeira detecção de cada alvo deve ser gravada como um registro histórico,
com posição, quadrante, horário e dispositivo de origem.
**Atores:** Sistema.
**Prioridade:** Média.
**Status:** Implementado.
**Evidência:** `Models/ObjetoDetectado.cs`, `Repositories/CsvObjetoDetectadoRepository.cs`,
`Docs/MODELO_DADOS.md`, seção 3 (`MainViewModel.OnTargetCreated`).

**RF13 – Visualização de objetos detectados em tabela**
**Descrição:** o sistema deve exibir o histórico de detecções em uma tela de tabela dedicada.
**Atores:** Usuário.
**Prioridade:** Média.
**Status:** Implementado.
**Evidência:** `Views/ObjetosDetectadosView.xaml.cs`, `ViewModels/ObjetosDetectadosViewModel.cs`.

**RF14 – Exportação de objetos detectados (CSV, XML, PDF)**
**Descrição:** o usuário deve poder exportar a lista de objetos detectados nos formatos CSV,
XML ou PDF, disponível para qualquer perfil autenticado.
**Atores:** Usuário.
**Prioridade:** Baixa.
**Status:** Implementado.
**Evidência:** `Services/ObjetoDetectadoExportService.cs`, `Services/IObjetoDetectadoExportService.cs`
(`ExportCsv`, `ExportXml`, `ExportPdf`).

**RF15 – Importação de objetos detectados (CSV, XML)**
**Descrição:** o usuário deve poder importar registros de um arquivo CSV ou XML no mesmo
formato da exportação; cada linha importada é inserida como um novo registro; restrito a
perfis que podem executar ações (Visualizador não pode importar).
**Atores:** Usuário.
**Prioridade:** Baixa.
**Status:** Implementado.
**Evidência:** `Services/IObjetoDetectadoExportService.cs` (`ImportCsv`, `ImportXml`),
`ViewModels/ObjetosDetectadosViewModel.cs` (`PodeImportar`).

**RF16 – Auditoria de ações realizadas**
**Descrição:** cada tentativa de acionamento deve ser gravada para consulta posterior,
exclusivamente em modo de inserção (sem edição/remoção).
**Atores:** Sistema, Usuário (consulta).
**Prioridade:** Média.
**Status:** Parcial — o registro já é gravado automaticamente a cada tentativa de acionamento
(ver RF06); a tela dedicada de consulta ainda não foi implementada (permanece como item de
navegação "em construção"; ver `Docs/CONTEXTO_PROJETO.md`, seção 3).
**Evidência:** `Repositories/IAcaoRealizadaRepository.cs`, `Models/AcaoRealizada.cs`.

**RF17 – Auditoria de alterações de modo**
**Descrição:** cada troca de modo de operação (RF08) deve ser gravada para consulta posterior.
**Atores:** Sistema, Usuário (consulta).
**Prioridade:** Média.
**Status:** Parcial — o registro já é gravado automaticamente a cada troca de modo; a tela
dedicada de consulta ainda não foi implementada. Mesma ressalva de RF16.
**Evidência:** `Repositories/IAlteracaoModoRepository.cs`, `Models/AlteracaoModo.cs`.

**RF18 – Gerenciamento de usuários**
**Descrição:** um Administrador deve poder criar, editar e inativar contas de usuário.
**Atores:** Usuário (Administrador).
**Prioridade:** Média.
**Status:** Planejado — existem o contrato de repositório e a checagem de permissão que
restringiria a função a Administradores, mas nenhuma tela ou ViewModel consome esse
repositório hoje; não há, atualmente, nenhuma forma de criar/editar/inativar usuário pela
interface.
**Evidência:** `Repositories/IUsuarioRepository.cs`, `Services/IPermissionService.cs`
(`PodeGerenciarUsuarios`).

**RF19 – Preferências de usuário (idioma, tema, barra lateral)**
**Descrição:** o sistema deve salvar e restaurar, por usuário, o idioma preferido, o tema e o
estado recolhido/expandido da barra lateral.
**Atores:** Usuário.
**Prioridade:** Baixa.
**Status:** Implementado.
**Evidência:** `Models/PreferenciasUsuario.cs`, `Repositories/CsvPreferenciasUsuarioRepository.cs`,
`Services/ILocalizationService.cs`, `Services/IThemeService.cs`.

**RF20 – Troca de idioma da interface**
**Descrição:** o usuário deve poder alterar o idioma da interface entre português (Brasil) e
inglês (EUA).
**Atores:** Usuário.
**Prioridade:** Baixa.
**Status:** Implementado.
**Evidência:** `Services/LocalizationService.cs`, `Localization/LocExtension.cs`.
*(Característica de qualidade associada — troca em tempo de execução sem reiniciar — descrita
em RNF12, sem repetir este requisito.)*

**RF21 – Troca de tema visual**
**Descrição:** o usuário deve poder selecionar o tema visual da interface entre claro, escuro
ou acompanhar a configuração do Windows.
**Atores:** Usuário.
**Prioridade:** Baixa.
**Status:** Implementado.
**Evidência:** `Services/ThemeService.cs`, `Services/IThemeService.cs`,
`Models/PreferenciasUsuario.cs` (`TemaPreferido`).
*(Característica de qualidade associada — consistência visual entre temas — descrita em RNF13,
sem repetir este requisito.)*

**RF22 – Abertura de chamado de ajuda/suporte**
**Descrição:** o usuário deve poder abrir um chamado de suporte (título, descrição, categoria,
módulo relacionado, mensagem de erro), com usuário e data preenchidos automaticamente.
**Atores:** Usuário.
**Prioridade:** Baixa.
**Status:** Implementado.
**Evidência:** `Models/ChamadoAjuda.cs`, `ViewModels/HelpDeskFormViewModel.cs`,
`Views/Shell/HelpDeskFormWindow.xaml.cs`.

**RF23 – Tratamento administrativo de chamados de ajuda**
**Descrição:** um Administrador deve poder consultar os chamados abertos e definir situação e
resposta para cada um.
**Atores:** Usuário (Administrador).
**Prioridade:** Baixa.
**Status:** Planejado — o repositório já expõe uma operação de atualização (situação/resposta),
mas nenhuma tela consome essa operação hoje; só o formulário de abertura (RF22) está
implementado.
**Evidência:** `Repositories/IChamadoAjudaRepository.cs` (único repositório do domínio que
expõe `Update`), `Models/ChamadoAjuda.cs`.

**RF24 – Painel principal com cards personalizáveis**
**Descrição:** o usuário deve poder arrastar e redimensionar cards de indicadores no painel
principal; a posição, o tamanho, a visibilidade e a ordem devem ser salvos por usuário e
restaurados no próximo acesso; deve haver um comando para restaurar o layout padrão.
**Atores:** Usuário.
**Prioridade:** Baixa.
**Status:** Implementado.
**Evidência:** `Views/Shared/DashboardCanvas.cs`, `Views/Shared/DashboardCard.xaml.cs`,
`Services/DashboardLayoutRepository.cs`, `Models/DashboardCardLayout.cs`,
`Docs/ARQUITETURA.md`, seção 5.2.

**RF25 – Removido da especificação funcional**
**Motivo:** a responsividade do layout a mudanças de resolução/tamanho de janela é uma
característica de qualidade (usabilidade/compatibilidade), não uma função acionada por um
ator. Consolidado em **RNF11**, sem duplicar aqui.

**RF26 – Console de eventos fixável na lateral**
**Descrição:** o usuário deve poder fixar o card de console de eventos na borda direita da tela
de Monitoramento, saindo do canvas arrastável; o estado fixado/não fixado deve ser persistido.
**Atores:** Usuário.
**Prioridade:** Baixa.
**Status:** Implementado.
**Evidência:** `Models/DashboardCardLayout.cs` (`IsPinnedRight`),
`Views/MonitoramentoView.xaml.cs` (`SetLogPinned`), `Docs/ARQUITETURA.md`, seção 5.4.

**RF27 – Gestão de zonas mortas (áreas de exclusão)**
**Descrição:** um Administrador deve poder criar, ativar/desativar e remover zonas (por
quadrante ou por faixa de distância) onde alvos não recebem torre nem podem ser acionados,
embora continuem visíveis/rastreados; demais perfis devem visualizar a lista somente leitura.
**Atores:** Usuário (Administrador), Sistema.
**Prioridade:** Alta.
**Status:** Implementado.
**Evidência:** `Models/DeadZone.cs`, `Services/IDeadZoneService.cs`,
`Services/IDeadZoneRepository.cs`, `Services/IPermissionService.cs`
(`PodeGerenciarZonasMortas`), `Docs/ARQUITETURA.md`, seção 5.3.

**RF28 – Detecção do Arduino CLI**
**Descrição:** o sistema deve localizar o executável do Arduino CLI no computador (caminho
salvo, pasta do aplicativo, `PATH`, locais comuns de instalação), sem baixar nada
automaticamente, e exibir a versão detectada.
**Atores:** Usuário, Sistema.
**Prioridade:** Média.
**Status:** Implementado.
**Evidência:** `Services/ArduinoCliLocatorService.cs`, `Services/IArduinoCliLocatorService.cs`,
`Docs/COMUNICACAO_ARDUINO.md`, seção 8.1.

**RF29 – Compilação de sketch Arduino pela interface**
**Descrição:** o usuário deve poder selecionar um sketch `.ino` e uma placa, compilar via
Arduino CLI como processo assíncrono e cancelável, acompanhando a saída em tempo real; o
resultado (sucesso/falha) deve ser decidido pelo código de saída do processo.
**Atores:** Usuário, Sistema.
**Prioridade:** Média.
**Status:** Implementado.
**Evidência:** `Services/ArduinoCompilerService.cs`, `Services/IArduinoCompilerService.cs`,
`Docs/COMUNICACAO_ARDUINO.md`, seção 8.3.

**RF30 – Monitor serial pela aba Configurações do Arduino**
**Descrição:** o usuário deve poder acompanhar mensagens da porta serial diretamente nesta aba,
reutilizando a mesma conexão da tela de Monitoramento (nunca duas portas abertas
concorrentemente); em caso de conflito de parâmetros, o sistema deve pedir confirmação antes de
reconectar.
**Atores:** Usuário, Sistema, Arduino.
**Prioridade:** Média.
**Status:** Implementado.
**Evidência:** `ViewModels/ArduinoSettingsViewModel.cs`, `Docs/COMUNICACAO_ARDUINO.md`, seção 8.4.

**RF31 – Persistência de preferências da aba Arduino**
**Descrição:** caminho do CLI, último sketch, placa, porta/baud e preferências do console devem
ser salvos e restaurados entre sessões.
**Atores:** Sistema.
**Prioridade:** Baixa.
**Status:** Implementado.
**Evidência:** `Services/ArduinoSettingsRepository.cs`, `Services/IArduinoSettingsRepository.cs`.

**RF32 – Removido da especificação funcional**
**Motivo:** instalação, empacotamento self-contained e preservação de configurações em upgrade
são características de portabilidade/instalação/manutenibilidade, não uma função acionada por
um ator do sistema em operação. Consolidado em **RNF17** e **RNF18**, sem duplicar aqui.

---

## Tabela consolidada

| ID | Requisito Funcional | Prioridade | Status | Ator | Evidência |
|---|---|---|---|---|---|
| RF01 | Comunicação serial com o Arduino | Alta | Implementado | Usuário, Arduino | `SerialCommunicationService.cs` |
| RF02 | Recepção e validação de leituras de alvo | Alta | Implementado | Arduino, Sistema | `SerialProtocolParser.cs` |
| RF03 | Rastreamento de alvos em tempo real | Alta | Implementado | Sistema | `TargetTrackingService.cs` |
| RF04 | Exibição do radar circular | Alta | Implementado | Usuário | `RadarControl.xaml.cs` |
| RF05 | Seleção automática de torre | Alta | Implementado | Sistema | `TowerSelectionService.cs` |
| RF06 | Acionamento demonstrativo automático | Alta | Implementado (ver D1) | Sistema | `FireControlService.cs` |
| RF07 | *Removido — modo de simulação não é requisito do produto* | — | Removido | — | — |
| RF08 | Modos de operação (Verde/Amarelo/Vermelho) | Alta | Parcial (ver D1) | Usuário | `SystemState.cs`, `AlteracaoModo.cs` |
| RF09 | Autenticação multiusuário | Alta | Implementado | Usuário | `AuthService.cs`, `PasswordHasher.cs` |
| RF10 | Controle de acesso por perfil | Alta | Implementado | Sistema, Usuário | `PermissionService.cs` |
| RF11 | Troca de senha | Média | Implementado | Usuário | `IAuthService.cs` |
| RF12 | Registro histórico de detecções | Média | Implementado | Sistema | `CsvObjetoDetectadoRepository.cs` |
| RF13 | Visualização de objetos detectados | Média | Implementado | Usuário | `ObjetosDetectadosViewModel.cs` |
| RF14 | Exportação de objetos detectados | Baixa | Implementado | Usuário | `ObjetoDetectadoExportService.cs` |
| RF15 | Importação de objetos detectados | Baixa | Implementado | Usuário | `IObjetoDetectadoExportService.cs` |
| RF16 | Auditoria de ações realizadas | Média | Parcial (registro ok, tela pendente) | Sistema, Usuário | `IAcaoRealizadaRepository.cs` |
| RF17 | Auditoria de alterações de modo | Média | Parcial (registro ok, tela pendente) | Sistema, Usuário | `IAlteracaoModoRepository.cs` |
| RF18 | Gerenciamento de usuários | Média | Planejado | Usuário (Admin) | `IUsuarioRepository.cs` |
| RF19 | Preferências de usuário | Baixa | Implementado | Usuário | `PreferenciasUsuario.cs` |
| RF20 | Troca de idioma da interface | Baixa | Implementado | Usuário | `LocalizationService.cs` |
| RF21 | Troca de tema visual | Baixa | Implementado | Usuário | `ThemeService.cs` |
| RF22 | Abertura de chamado de ajuda | Baixa | Implementado | Usuário | `HelpDeskFormViewModel.cs` |
| RF23 | Tratamento administrativo de chamados | Baixa | Planejado | Usuário (Admin) | `IChamadoAjudaRepository.cs` |
| RF24 | Painel com cards personalizáveis | Baixa | Implementado | Usuário | `DashboardCanvas.cs` |
| RF25 | *Removido — responsividade é RNF11* | — | Removido | — | — |
| RF26 | Console de eventos fixável | Baixa | Implementado | Usuário | `MonitoramentoView.xaml.cs` |
| RF27 | Gestão de zonas mortas | Alta | Implementado | Usuário (Admin), Sistema | `DeadZone.cs`, `IDeadZoneService.cs` |
| RF28 | Detecção do Arduino CLI | Média | Implementado | Usuário, Sistema | `ArduinoCliLocatorService.cs` |
| RF29 | Compilação de sketch pela interface | Média | Implementado | Usuário, Sistema | `ArduinoCompilerService.cs` |
| RF30 | Monitor serial na aba Arduino | Média | Implementado | Usuário, Sistema, Arduino | `ArduinoSettingsViewModel.cs` |
| RF31 | Persistência de preferências da aba Arduino | Baixa | Implementado | Sistema | `ArduinoSettingsRepository.cs` |
| RF32 | *Removido — instalação é RNF17/RNF18* | — | Removido | — | — |

**D1** = divergência entre especificação e implementação atual, ver `Limitacoes_Conhecidas.md`.
