# Requisitos Não Funcionais

| ID | Requisito Não Funcional | Categoria | Descrição | Evidência |
|---|---|---|---|---|
| RNF01 | Redesenho do radar em ~150 ms | Desempenho / Tempo de resposta | `RadarSettings.RefreshRateMs = 150` controla o ciclo de reposicionamento do radar via `DispatcherTimer` | `appsettings.json` (`RadarSettings`), `Docs/ARQUITETURA.md`, seção 4 |
| RNF02 | Leitura serial não bloqueante | Desempenho / Confiabilidade | Leitura contínua roda em `Task.Run` dedicado com `ReadTimeout` curto, nunca bloqueia a thread de UI | `SerialCommunicationService.cs`, `Docs/ARQUITETURA.md`, seção 4 |
| RNF03 | Degradação graciosa de falhas de hardware/serial | Confiabilidade | Porta inexistente, ocupada, cabo desconectado ou silêncio prolongado (watchdog) nunca derrubam a aplicação — apenas mudam estado e logam | `SerialCommunicationService.cs`, `Docs/COMUNICACAO_ARDUINO.md`, seção 5 |
| RNF04 | Distância mínima de segurança obrigatória no acionamento | Segurança | `FireControlService.Authorize` bloqueia acionamento abaixo de `MinSafetyDistanceMeters` | `FireControlService.cs`, `appsettings.json` (`RadarSettings.MinSafetyDistanceMeters`) |
| RNF05 | Acionamento é só demonstrativo, nunca armamento real | Segurança | Escopo do projeto define acionamento como laser de baixa potência/LED/simulação | `Docs/CONTEXTO_PROJETO.md`, seção 1; `Docs/DOCUMENTACAO_TECNICA.md` (Limitações) |
| RNF06 | Senha de usuário nunca em texto puro | Segurança | PBKDF2-HMACSHA256, 100.000 iterações, salt de 128 bits por usuário | `PasswordHasher.cs` |
| RNF07 | Controle de acesso centralizado por perfil | Segurança | Checagem de perfil concentrada em `IPermissionService`, não espalhada pela UI | `PermissionService.cs`, `Docs/ARQUITETURA.md`, seção 5.3 |
| RNF08 | Zonas mortas só editáveis por Administrador | Segurança | `PodeGerenciarZonasMortas` restringe criação/ativação/remoção; demais perfis só leem | `IPermissionService.cs`, `Docs/ARQUITETURA.md`, seção 5.3 |
| RNF09 | Montagem segura de comandos de processo externo (Arduino CLI) | Segurança | `ProcessStartInfo.ArgumentList` usado em vez de concatenação de string interpretada por shell, ao chamar `arduino-cli` | `ArduinoCompilerService.cs`, `Docs/ARQUITETURA.md`, seção 5.1 |
| RNF10 | Arduino CLI nunca baixado automaticamente | Segurança / Restrição tecnológica | `ArduinoCliLocatorService` só lê disco/PATH e executa binário já instalado, sem acesso à rede | `ArduinoCliLocatorService.cs`, `Docs/COMUNICACAO_ARDUINO.md`, seção 8.1 |
| RNF11 | Interface responsiva a mudanças de resolução/tamanho de janela | Usabilidade | Layout dos cards salvo como fração (0..1) do canvas, reescalado proporcionalmente em `SizeChanged` | `DashboardCanvas.cs`, `Models/DashboardCardLayout.cs` |
| RNF12 | Internacionalização pt-BR / en-US | Usabilidade / Compatibilidade | Idioma trocável em runtime via `ILocalizationService` | `LocalizationService.cs`, `Localization/LocExtension.cs` |
| RNF13 | Tema claro/escuro/acompanha sistema | Usabilidade | `IThemeService` aplica tema em runtime, com opção de seguir o Windows | `ThemeService.cs`, `TemaPreferido` (`PreferenciasUsuario.cs`) |
| RNF14 | Limite de linhas nos consoles (anti-crescimento de memória) | Desempenho / Confiabilidade | Console de eventos limitado a 500 linhas; consoles de compilação e monitor serial limitados a 4000 linhas, descartando as mais antigas | `Docs/ARQUITETURA.md`, seção 5.1 |
| RNF15 | Mutação de coleções vinculadas à UI sempre na thread de UI | Confiabilidade | Regra geral do projeto: qualquer classe com `ObservableCollection<T>`/eventos ligados à UI despacha via `Dispatcher` internamente | `Docs/ARQUITETURA.md`, seção 4 |
| RNF16 | Compilação cancelável do sketch Arduino | Usabilidade / Desempenho | `CompileAsync` aceita `CancellationToken`, mata a árvore de processos ao cancelar | `ArduinoCompilerService.cs`, `Docs/COMUNICACAO_ARDUINO.md`, seção 8.3 |
| RNF17 | Runtime portátil, sem dependência externa para o usuário final | Portabilidade / Instalação | Instalador self-contained embute o .NET 9 Desktop Runtime | `installer/RadarTorres.iss`, `Docs/CONTEXTO_PROJETO.md`, seção 2.1 |
| RNF18 | Upgrade preserva configurações do usuário | Manutenibilidade / Instalação | Instalador Inno Setup mantido para não sobrescrever dados em `%AppData%`/`%LocalAppData%` em upgrades | `installer/RadarTorres.iss`, `Docs/CONTEXTO_PROJETO.md`, seção 3 |
| RNF19 | Persistência gravável sem privilégio de administrador | Manutenibilidade / Restrição tecnológica | Dados do usuário em `%AppData%\RadarTorres\Data\` e `%LocalAppData%\RadarTorres\`, nunca em `C:\Program Files\...` | `Data/AppDataPaths.cs`, `Docs/MODELO_DADOS.md`, seção 1 |
| RNF20 | Persistência substituível sem alterar camadas superiores | Manutenibilidade / Escalabilidade | Cada tabela CSV tem uma interface de repositório dedicada, preparada para troca futura por SQL sem tocar ViewModels/Services | `Repositories/I*Repository.cs`, comentários `TODO(SQL)`, `Docs/MODELO_DADOS.md`, seção 1 |
| RNF21 | Protocolo serial centralizado em um único componente | Manutenibilidade | Toda a interpretação/montagem de mensagens Arduino↔PC passa por `SerialProtocolParser`, nunca strings soltas em outras classes | `SerialProtocolParser.cs`, `Docs/COMUNICACAO_ARDUINO.md`, seção 3 |
| RNF22 | MVVM sem framework externo, mecanismo explicável | Manutenibilidade | `ViewModelBase`/`RelayCommand` implementados manualmente (~60 linhas) em vez de Prism/CommunityToolkit.Mvvm, por ser projeto didático de TCC | `ViewModelBase.cs`, `RelayCommand.cs`, `Docs/ARQUITETURA.md`, seção 2 |
| RNF23 | Nenhuma classe de Services/Models depende de WPF | Manutenibilidade / Testabilidade | Interfaces `I*Service` permitem trocar implementação (ex.: dublê de teste) sem tocar a UI | `Docs/ARQUITETURA.md`, seção 1 |
| RNF24 | Cobertura de testes automatizados da aba Arduino CLI | Confiabilidade | 21 testes xUnit cobrindo localização do CLI, montagem de argumentos, código de saída, cancelamento, persistência e disputa de porta serial | `tests/RadarTorres.Tests/*.cs`, `Docs/CONTEXTO_PROJETO.md`, seção 3 |
| RNF25 | Baud rate configurável | Comunicação / Hardware | UI oferece 9600/19200/38400/57600/115200 bps, padrão 9600 | `Docs/COMUNICACAO_ARDUINO.md`, seção 1 |
| RNF26 | Codificação e terminador de linha fixos no protocolo serial | Comunicação | ASCII, terminador `\n` (LF); `\r` do `Serial.println` é ignorado no lado PC | `Docs/COMUNICACAO_ARDUINO.md`, seção 1 |
| RNF27 | Plataforma-alvo Windows Desktop | Compatibilidade / Restrição tecnológica | WPF/.NET 9, projeto gera executável Windows (`net9.0-windows`) | `RadarTorres.App.csproj`, `installer/RadarTorres.iss` |
| RNF28 | Tolerância a mensagens malformadas do Arduino | Precisão / Confiabilidade | Linha fora do formato esperado vira `UnknownMessage`, apenas registrada como aviso, nunca derruba a aplicação | `SerialProtocolParser.cs`, `Docs/COMUNICACAO_ARDUINO.md`, seção 2 |
| RNF29 | Não há reconexão automática após queda (limitação assumida) | Disponibilidade | `SerialSettings.ReconnectAttempts` existe na configuração, mas a lógica de retry automático ainda não foi implementada — reconexão é manual hoje | `appsettings.json`, `Docs/DOCUMENTACAO_TECNICA.md` (Limitações e próximos passos) |
| RNF30 | Anticolisão de cards por rejeição, não reposicionamento em cascata | Usabilidade | `DashboardCanvas` ignora o delta de arraste/redimensionamento em vez de reorganizar os demais cards — comportamento mais previsível | `DashboardCanvas.cs`, `Docs/ARQUITETURA.md`, seção 5.2 |

## Observações de validação

* Todas as linhas acima têm evidência em código-fonte ou em documentação já existente no
  próprio repositório — nenhum requisito foi extrapolado sem base textual.
* RNF29 é registrado como **limitação atual**, não como requisito cumprido — mantido na tabela
  porque a configuração (`ReconnectAttempts`) já expressa a intenção do requisito, mesmo sem
  implementação completa.
