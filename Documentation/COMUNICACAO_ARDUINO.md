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
