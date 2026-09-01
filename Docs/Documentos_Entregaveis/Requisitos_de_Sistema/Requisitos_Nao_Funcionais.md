# Requisitos Não Funcionais

Levantados a partir do código-fonte e da documentação existente em `Docs/`. Mantém apenas
características de qualidade, desempenho, segurança, confiabilidade, disponibilidade,
usabilidade, compatibilidade, portabilidade, instalação, manutenibilidade e comunicação/
hardware — decisões internas de implementação foram movidas para
`Decisoes_Arquiteturais.md`, e limitações/comportamentos não implementados foram movidos para
`Limitacoes_Conhecidas.md` (nenhum dos dois deve ser confundido com requisito atendido).

| ID | Requisito Não Funcional | Categoria | Descrição | Status | Evidência |
|---|---|---|---|---|---|
| RNF01 | Redesenho do radar em ~150 ms | Desempenho / Tempo de resposta | `RadarSettings.RefreshRateMs = 150` controla o ciclo de reposicionamento do radar via `DispatcherTimer` | Atendido | `appsettings.json` (`RadarSettings`), `Docs/Tecnica/ARQUITETURA.md`, seção 4 |
| RNF02 | Leitura serial não bloqueante | Desempenho / Confiabilidade | Leitura contínua roda em `Task.Run` dedicado com `ReadTimeout` curto, nunca bloqueia a thread de UI | Atendido | `SerialCommunicationService.cs`, `Docs/Tecnica/ARQUITETURA.md`, seção 4 |
| RNF03 | Degradação graciosa de falhas de hardware/serial | Confiabilidade | Porta inexistente, ocupada, cabo desconectado ou silêncio prolongado (watchdog) nunca derrubam a aplicação — apenas mudam estado e logam | Atendido | `SerialCommunicationService.cs`, `Docs/Tecnica/COMUNICACAO_ARDUINO.md`, seção 5 |
| RNF04 | Distância mínima de segurança obrigatória no acionamento | Segurança | `FireControlService.Authorize` bloqueia acionamento abaixo de `MinSafetyDistanceMeters` | Atendido | `FireControlService.cs`, `appsettings.json` (`RadarSettings.MinSafetyDistanceMeters`) |
| RNF05 | Acionamento é só demonstrativo, nunca armamento real | Segurança | Escopo do projeto define acionamento como laser de baixa potência/LED/simulação | Atendido | `Docs/Projeto/CONTEXTO_PROJETO.md`, seção 1; `Docs/Tecnica/DOCUMENTACAO_TECNICA.md` (Limitações) |
| RNF06 | Senha de usuário nunca em texto puro | Segurança | PBKDF2-HMACSHA256, 100.000 iterações, salt de 128 bits por usuário | Atendido | `PasswordHasher.cs` |
| RNF07 | Controle de acesso centralizado por perfil | Segurança | Checagem de perfil concentrada em `IPermissionService`, não espalhada pela UI | Atendido | `PermissionService.cs`, `Docs/Tecnica/ARQUITETURA.md`, seção 5.3 |
| RNF08 | Zonas mortas só editáveis por Administrador | Segurança | `PodeGerenciarZonasMortas` restringe criação/ativação/remoção; demais perfis só leem | Atendido | `IPermissionService.cs`, `Docs/Tecnica/ARQUITETURA.md`, seção 5.3 |
| RNF09 | Montagem segura de comandos de processo externo (Arduino CLI) | Segurança | `ProcessStartInfo.ArgumentList` usado em vez de concatenação de string interpretada por shell, ao chamar `arduino-cli` | Atendido | `ArduinoCompilerService.cs`, `Docs/Tecnica/ARQUITETURA.md`, seção 5.1 |
| RNF10 | Arduino CLI nunca baixado automaticamente | Segurança / Restrição tecnológica | `ArduinoCliLocatorService` só lê disco/PATH e executa binário já instalado, sem acesso à rede | Atendido | `ArduinoCliLocatorService.cs`, `Docs/Tecnica/COMUNICACAO_ARDUINO.md`, seção 8.1 |
| RNF11 | Interface responsiva a mudanças de resolução/tamanho de janela | Usabilidade / Compatibilidade | Layout dos cards salvo como fração (0..1) do canvas, reescalado proporcionalmente em `SizeChanged`. *(Consolida o antigo RF25.)* | Atendido | `DashboardCanvas.cs`, `Models/DashboardCardLayout.cs` |
| RNF12 | Troca de idioma sem reinício | Usabilidade | A troca de idioma (pt-BR/en-US, ver RF20) deve ser aplicada em tempo de execução, sem necessidade de reiniciar a aplicação | Atendido | `LocalizationService.cs`, `Localization/LocExtension.cs` |
| RNF13 | Consistência visual entre temas | Usabilidade | A interface deve manter os mesmos elementos e a mesma organização visual em qualquer tema suportado (claro, escuro, sistema — ver RF21), aplicado em tempo de execução sem reiniciar | Atendido | `ThemeService.cs`, `Themes/Light.xaml`, `Themes/Dark.xaml` |
| RNF14 | Limite de linhas nos consoles (anti-crescimento de memória) | Desempenho / Confiabilidade | Console de eventos limitado a 500 linhas; consoles de compilação e monitor serial limitados a 4000 linhas, descartando as mais antigas | Atendido | `Docs/Tecnica/ARQUITETURA.md`, seção 5.1 |
| RNF15 | Mutação de coleções vinculadas à UI sempre na thread de UI | Confiabilidade | Regra geral do projeto: qualquer classe com `ObservableCollection<T>`/eventos ligados à UI despacha via `Dispatcher` internamente | Atendido | `Docs/Tecnica/ARQUITETURA.md`, seção 4 |
| RNF16 | Compilação do sketch não bloqueia a interface | Desempenho / Usabilidade | A execução da compilação (RF29) não deve travar a interface do usuário e deve permitir interrupção controlada pelo usuário | Atendido | `ArduinoCompilerService.cs` (`CompileAsync` com `CancellationToken`) |
| RNF17 | Runtime portátil, sem dependência externa para o usuário final | Portabilidade / Instalação | Instalador self-contained embute o .NET 9 Desktop Runtime. *(Consolida parte do antigo RF32.)* | Atendido | `installer/RadarTorres.iss`, `Docs/Projeto/CONTEXTO_PROJETO.md`, seção 2.1 |
| RNF18 | Upgrade preserva configurações do usuário | Manutenibilidade / Instalação | Instalador Inno Setup mantido para não sobrescrever dados em `%AppData%`/`%LocalAppData%` em upgrades. *(Consolida parte do antigo RF32.)* | Atendido | `installer/RadarTorres.iss`, `Docs/Projeto/CONTEXTO_PROJETO.md`, seção 3 |
| RNF19 | Persistência gravável sem privilégio de administrador | Manutenibilidade / Restrição tecnológica | Dados do usuário em `%AppData%\RadarTorres\Data\` e `%LocalAppData%\RadarTorres\`, nunca em `C:\Program Files\...` | Atendido | `Data/AppDataPaths.cs`, `Docs/Tecnica/MODELO_DADOS.md`, seção 1 |
| RNF20 | *Movido para Decisões e Restrições Arquiteturais (DA01)* | — | Persistência substituível por interface de repositório é decisão interna de implementação, não característica observável do produto | — | — |
| RNF21 | *Movido para Decisões e Restrições Arquiteturais (DA02)* | — | Centralização do protocolo serial em um único componente é decisão interna | — | — |
| RNF22 | *Movido para Decisões e Restrições Arquiteturais (DA03)* | — | MVVM manual sem framework externo é decisão tecnológica interna | — | — |
| RNF23 | *Movido para Decisões e Restrições Arquiteturais (DA04)* | — | Independência de Services/Models em relação ao WPF é decisão interna de camadas | — | — |
| RNF24 | Cobertura de testes automatizados dos componentes críticos | Confiabilidade | Os componentes críticos de comunicação serial, integração com o Arduino CLI e persistência devem possuir testes automatizados que validem os principais cenários de sucesso, falha e cancelamento | Parcial — cobertura hoje concentrada só na aba Arduino CLI; comunicação serial de leitura de alvos, rastreamento, seleção de torre e persistência CSV ainda não têm teste automatizado | Evidência atual: 21 testes xUnit aprovados (`tests/RadarTorres.Tests/*.cs`) |
| RNF25 | Baud rate configurável | Comunicação / Hardware | UI oferece 9600/19200/38400/57600/115200 bps, padrão 9600 | Atendido | `Docs/Tecnica/COMUNICACAO_ARDUINO.md`, seção 1 |
| RNF26 | Codificação e terminador de linha fixos no protocolo serial | Comunicação | ASCII, terminador `\n` (LF); `\r` do `Serial.println` é ignorado no lado PC | Atendido | `Docs/Tecnica/COMUNICACAO_ARDUINO.md`, seção 1 |
| RNF27 | Plataforma-alvo Windows Desktop | Compatibilidade / Restrição tecnológica | WPF/.NET 9, projeto gera executável Windows (`net9.0-windows`) | Atendido | `RadarTorres.App.csproj`, `installer/RadarTorres.iss` |
| RNF28 | Tolerância a mensagens malformadas do Arduino | Precisão / Confiabilidade | Linha fora do formato esperado vira mensagem desconhecida, apenas registrada como aviso, nunca derruba a aplicação | Atendido | `SerialProtocolParser.cs`, `Docs/Tecnica/COMUNICACAO_ARDUINO.md`, seção 2 |
| RNF29 | *Movido para Limitações Conhecidas (L01)* | — | "Não há reconexão automática" descreve uma funcionalidade ainda não implementada, não uma característica de qualidade atendida | — | — |
| RNF30 | *Movido para Decisões e Restrições Arquiteturais (DA05)* | — | Anticolisão de cards por rejeição é decisão de interação/UX sem requisito de negócio explícito por trás | — | — |

## Observações de validação

* Todas as linhas com Status **Atendido** têm evidência em código-fonte ou em documentação já
  existente no próprio repositório — nenhum requisito foi extrapolado sem base textual.
* RNF12 e RNF13 foram reformulados nesta revisão para descrever só a característica de
  qualidade (troca em runtime, consistência visual); a existência da funcionalidade em si
  (o usuário poder escolher idioma/tema) é responsabilidade de **RF20**/**RF21**, evitando
  duplicar o mesmo conteúdo nos dois documentos.
* RNF16 foi reformulado para não duplicar **RF29**: RF29 descreve a funcionalidade (compilar
  pela interface, com cancelamento), RNF16 descreve a propriedade de qualidade (não bloquear a
  UI durante a execução).
* RNF20–RNF23, RNF29 e RNF30 permanecem nesta tabela como registros vazios apontando para o
  novo local, para que nenhuma referência antiga de outro documento fique órfã — o conteúdo
  real está em `Decisoes_Arquiteturais.md` e `Limitacoes_Conhecidas.md`.
