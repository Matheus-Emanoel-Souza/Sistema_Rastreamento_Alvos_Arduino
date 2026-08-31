# Comunicação Serial PC ↔ Arduino

Este documento especifica o protocolo textual usado entre o aplicativo C# (`RadarTorres.App`)
e o Arduino, implementado em `Services/SerialProtocolParser.cs` (interpretação) e
`Services/SerialCommunicationService.cs` (transporte).

## 1. Visão geral

* **Transporte:** porta serial USB (`System.IO.Ports.SerialPort`).
* **Baud Rate padrão:** 9600 (configurável; a UI oferece 9600 / 19200 / 38400 / 57600 / 115200).
* **Codificação:** ASCII.
* **Terminador de linha:** `\n` (LF). O Arduino deve usar `Serial.println(...)`, que já
  termina cada mensagem com `\r\n` — o `\r` é ignorado pelo lado do PC.
* **Formato geral de uma mensagem:**

  ```
  TIPO;CHAVE1=VALOR1;CHAVE2=VALOR2;...
  ```

  Um `TIPO` em maiúsculas seguido de pares `CHAVE=VALOR` separados por `;`. O parser
  (`SerialProtocolParser.TryParse`) é tolerante a espaços em excesso e a campos ausentes
  (nesse caso, gera uma mensagem de erro em vez de lançar exceção).

## 2. Mensagens Arduino → PC

| Mensagem | Campos | Exemplo | Significado |
|---|---|---|---|
| `TARGET` | `ID` (int), `ANGLE` (0–360, graus), `DIST` (metros, ≥ 0) | `TARGET;ID=1;ANGLE=45;DIST=2.80` | Leitura de um alvo detectado. |
| `STATUS` | `SYSTEM` (`ONLINE`\|`OFFLINE`) | `STATUS;SYSTEM=ONLINE` | Status geral relatado pelo firmware. |
| `ACK` | `CMD` (comando ecoado) | `ACK;CMD=SYSTEM;ON` | Confirmação de que um comando foi recebido/executado. |
| `ERROR` | `REASON` (texto livre) | `ERROR;REASON=SENSOR TIMEOUT` | Erro reportado pelo próprio firmware. |

Qualquer linha que não se encaixe em nenhum desses formatos é tratada como
`UnknownMessage` e apenas registrada como aviso no console de eventos — **nunca** derruba
a aplicação.

### Validações aplicadas a `TARGET` (do lado do PC)

O parser rejeita e converte em `ERROR` (nunca em exceção) leituras com:

* `ID`, `ANGLE` ou `DIST` ausentes ou não numéricos;
* `ANGLE` = `NaN`/infinito ou fora do intervalo `[0, 360)`;
* `DIST` = `NaN`/infinito ou negativo.

Essas mensagens de erro aparecem no console de eventos com o motivo específico
(ex.: `"DISTÂNCIA NEGATIVA PARA ALVO 3 (-1.2)"`), permitindo depurar rapidamente um sensor
com defeito sem que isso afete os demais alvos.

## 3. Mensagens PC → Arduino

| Comando | Exemplo | Efeito esperado no firmware |
|---|---|---|
| `SYSTEM;ON` | `SYSTEM;ON` | Liga o sistema de detecção. |
| `SYSTEM;OFF` | `SYSTEM;OFF` | Desliga o sistema de detecção. |
| `MODE;DETECTION` | `MODE;DETECTION` | Modo somente localização (sem seleção automática). |
| `MODE;AUTO` | `MODE;AUTO` | Modo automático (seleção de torre, e opcionalmente acionamento, controlados pelo PC). |
| `SET;MIN_DISTANCE=` | `SET;MIN_DISTANCE=1.50` | Define a distância mínima de segurança (metros). |
| `SET;MAX_DISTANCE=` | `SET;MAX_DISTANCE=6.00` | Define a distância máxima de detecção (metros). |
| `FIRE;TOWER=;TARGET=` | `FIRE;TOWER=2;TARGET=1` | Solicita acionamento demonstrativo da torre indicada sobre o alvo indicado. |

Esses comandos são gerados por `SerialProtocolParser.Build*` — **nunca** montados como
strings soltas em outras partes do código, para manter o protocolo centralizado em um único
lugar.

## 4. Exemplo de sessão

```
PC   -> SYSTEM;ON
ARD  -> ACK;CMD=SYSTEM;ON
ARD  -> STATUS;SYSTEM=ONLINE
PC   -> MODE;AUTO
ARD  -> ACK;CMD=MODE;AUTO
ARD  -> TARGET;ID=1;ANGLE=45.0;DIST=2.80
ARD  -> TARGET;ID=2;ANGLE=220.0;DIST=4.30
PC   -> SET;MIN_DISTANCE=1.50
ARD  -> ACK;CMD=SET;MIN_DISTANCE=1.50
PC   -> FIRE;TOWER=1;TARGET=1
ARD  -> ACK;CMD=FIRE;TOWER=1;TARGET=1
```

## 5. Tratamento de erros de comunicação

Implementado em `SerialCommunicationService`:

| Situação | Como é tratada |
|---|---|
| Porta COM inexistente | `FileNotFoundException` capturada → estado `Error`, evento logado, aplicação segue rodando. |
| Porta ocupada por outro programa | `UnauthorizedAccessException` capturada → mesma tratativa acima. |
| Cabo desconectado durante a leitura | `IOException` no laço de leitura → `Disconnect()` automático + log `"Porta serial fechada inesperadamente"`. |
| Nenhum dado por período prolongado | *Watchdog* (`Timer` interno, `ConnectionWatchdogMs`) detecta silêncio prolongado e força desconexão/estado de erro, mesmo sem exceção do SO. |
| Mensagem malformada / incompleta | Parser retorna `ErrorMessage`; é apenas logada, o laço de leitura continua normalmente. |
| Envio de comando sem conexão ativa | `SendCommandAsync` retorna `false` e loga aviso, sem lançar exceção. |

Em nenhum desses casos a aplicação fecha ou trava — o objetivo explícito do design é que
falhas de hardware/serial sejam **degradação graciosa**, não crash.

## 6. Reconexão

O usuário pode clicar em **CONECTAR** novamente a qualquer momento após uma queda de
conexão; `ConnectAsync` sempre desconecta qualquer sessão anterior antes de abrir uma nova,
evitando handles de porta duplicados. Uma futura evolução natural (ver README, seção
"Próximos passos") é adicionar reconexão automática com backoff, usando o campo
`SerialSettings.ReconnectAttempts` já presente na configuração.

## 7. Testando sem hardware físico

Duas formas:

1. **Modo de simulação interno** (recomendado para desenvolvimento do software): não usa a
   porta serial, gera leituras diretamente em memória via `SimulationService`.
2. **`Arduino/ArduinoSimulation.ino`**: grave este sketch em um Arduino real (ou em um
   emulador serial) para validar a camada de comunicação de ponta a ponta, incluindo a
   própria porta USB, antes de existir qualquer sensor físico.

## 8. Aba "Configurações do Arduino" — compilar o firmware e monitorar a serial pela própria interface

Além da tela de Monitoramento (seção 1–7 acima), o RadarTorres tem uma aba dedicada na barra
lateral, **Configurações do Arduino**, para instalar/configurar o [Arduino
CLI](https://arduino.github.io/arduino-cli/), compilar um sketch `.ino` e acompanhar a saída
serial em tempo real — sem sair do aplicativo. Implementada em
`ViewModels/ArduinoSettingsViewModel.cs` + `Views/ArduinoSettingsView.xaml`.

### 8.1 Instalar e configurar o Arduino CLI

O **Arduino CLI não é instalado nem baixado automaticamente pelo RadarTorres** — é uma
ferramenta externa e opcional, necessária apenas para compilar sketches pela própria
interface (o app funciona normalmente sem ela, tanto em modo simulação quanto conectado a um
Arduino já gravado por fora). Para instalá-la:

1. Baixe o instalador/zip em <https://arduino.github.io/arduino-cli/latest/installation/>
   (seção Windows).
2. Extraia/instale em qualquer pasta de sua preferência.
3. Na aba **Configurações do Arduino**, seção **Ambiente Arduino**:
   - Clique em **Detectar automaticamente** — o RadarTorres procura, nesta ordem, o último
     caminho salvo, uma cópia local na pasta do próprio aplicativo, o `PATH` do Windows e
     locais comuns de instalação (ex.: Arduino IDE 2.x, WinGet); ou
   - Clique em **Procurar…** e aponte manualmente para `arduino-cli.exe`.
4. Quando encontrado, o indicador fica verde e a versão detectada (`arduino-cli version`) é
   exibida. Se não for encontrado, uma mensagem explica que o CLI é necessário para compilar e
   como configurá-lo — a aba continua funcional para o monitor serial mesmo sem ele.

### 8.2 Selecionar a placa (FQBN)

O combo **Placa / FQBN** já vem com um catálogo padrão de placas comuns (Uno, Nano, Mega,
Leonardo, ESP32 Dev Module, NodeMCU) — disponível mesmo sem o Arduino CLI instalado ou sem
nenhum "core" baixado, para nunca depender de uma chamada de rede só para preencher a
interface. O botão **Atualizar placas e portas** complementa essa lista com o resultado real
de `arduino-cli board listall` quando o CLI está disponível (útil se você já instalou cores
adicionais) e também atualiza a lista de portas COM.

### 8.3 Compilar um sketch

1. Clique em **Selecionar código .ino…** e escolha o arquivo — por padrão, se
   `Arduino/ArduinoSimulation.ino` estiver disponível (execução a partir do repositório) e
   nenhum sketch tiver sido usado antes, ele já vem pré-selecionado.
2. Confirme a placa/FQBN selecionada na seção de compilação.
3. Clique em **Compilar**. Internamente, o RadarTorres executa (via
   `IArduinoCompilerService`/`ArduinoCompilerService`, `System.Diagnostics.Process` com
   `ProcessStartInfo.ArgumentList` — nunca uma linha de comando concatenada para shell):

   ```powershell
   arduino-cli compile --fqbn <FQBN> <pasta-do-sketch>
   ```

   O CLI espera a **pasta** do sketch, não o `.ino` isolado — se você selecionar um arquivo
   `.ino`, o RadarTorres resolve automaticamente para a pasta que o contém.
4. A saída (stdout/stderr) aparece linha a linha no console de compilação, em tempo real,
   sem travar a interface (processo assíncrono, com `CancellationToken`). Clique em **Cancelar
   compilação** a qualquer momento para encerrar o processo (e sua árvore de processos) e
   liberar os recursos.
5. Ao final, o status mostra sucesso, cancelamento ou falha — **decidido exclusivamente pelo
   código de saída do processo** (0 = sucesso) e pelo cancelamento explícito do usuário; texto
   em `stderr` (usado pelo `arduino-cli` também para avisos de compilador) nunca é interpretado
   sozinho como erro.

Esta etapa cobre apenas **compilação**. Gravação/upload do firmware compilado para a placa
(`arduino-cli upload`) não foi implementada — ver `Docs/Projeto/LOG_SOLICITACOES.md` para o
racional.

### 8.4 Monitor serial da aba Configurações do Arduino

A seção **Monitor serial** desta aba usa o **mesmo** `ISerialCommunicationService` (Singleton
via injeção de dependência) já usado pela tela de Monitoramento — não existe uma segunda
implementação de comunicação serial nem duas conexões concorrentes na mesma porta. Na
prática:

- Se a porta **já está livre**, **Conectar** simplesmente abre a conexão normalmente.
- Se a porta **já está em uso** (por esta aba ou pela tela de Monitoramento) com a mesma
  porta/baud rate solicitados, o RadarTorres reaproveita a conexão existente sem reabrir nada.
- Se a porta já está em uso com **parâmetros diferentes**, o RadarTorres pergunta antes de
  agir: *"A porta serial já está em uso em '&lt;porta&gt;' @ &lt;baud&gt; bps (possivelmente
  pela tela de Monitoramento). Deseja desconectar e reconectar com os parâmetros selecionados
  aqui?"* — só desconecta a sessão existente se o usuário confirmar.
- Desconexão do cabo, porta inexistente e acesso negado usam o mesmo tratamento descrito na
  seção 5 acima (nunca travam nem fecham o aplicativo).

O console do monitor mostra cada mensagem recebida (com opção de exibir horário e rolagem
automática), com um contador de mensagens recebidas e um limite de 4000 linhas — as mais
antigas são descartadas automaticamente para não crescer a memória indefinidamente em sessões
longas. O console de compilação segue o mesmo limite.

### 8.5 Onde as preferências desta aba são salvas

Caminho do Arduino CLI, último sketch usado, FQBN selecionado, última porta COM, baud rate e
preferências do console (rolagem automática, exibir horário) são persistidos em
`%LocalAppData%\RadarTorres\arduino-settings.json` (`IArduinoSettingsRepository` /
`ArduinoSettingsRepository`) — uma pasta gravável pelo usuário comum, nunca dentro de
`C:\Program Files\...`. É um arquivo separado das preferências de usuário (tema/idioma, em CSV
via `PreferenciasUsuario`) porque representa configuração de máquina/instalação de uma
ferramenta externa, não uma preferência por conta de usuário do RadarTorres.
