# Diagrama de Implantação

> Base desta análise: `installer/RadarTorres.iss` (instalador), `src/RadarTorres.App/App.xaml.cs`
> (composition root), `src/RadarTorres.App/RadarTorres.App.csproj` (versões de dependências),
> `Arduino/RadarTorres_Firmware/RadarTorres_Firmware.ino` (firmware real) e
> `Docs/Tecnica/COMUNICACAO_ARDUINO.md` (protocolo serial). `Arduino/ArduinoSimulation.ino` é
> apenas um sketch auxiliar de teste do protocolo serial, sem sensores/motores reais.
> Nenhum nó, artefato ou protocolo foi inventado.

## 1. Nós e elementos

| Nó | Elemento | Papel |
|---|---|---|
| Computador do Usuário (Windows 10/11, 64-bit) | `RadarTorres.App` (.exe) | Único processo, self-contained (.NET 9), instalado por `installer/RadarTorres.iss` |
| ↳ dentro do processo | Apresentação (`Views` + `ViewModels`) | Captura interação do operador, binding declarativo (`ViewModelBase`, `RelayCommand`) |
| ↳ dentro do processo | Serviços (regra de negócio) | `ISerialCommunicationService`, `ITargetTrackingService`, `ITowerSelectionService`, `IFireControlService`, `IZonaMortaService`, `IAuthService`, `IPermissionService` |
| ↳ dentro do processo | Persistência local | Leitura/escrita do banco SQLite via `Microsoft.Data.Sqlite` (`SqliteConnectionFactory`/`Sqlite*Repository`) |
| ↳ dentro do processo | .NET 9 Desktop Runtime | Embutido pelo instalador — sem dependência externa a instalar |
| Arduino MEGA 2560 (microcontrolador) | Firmware (`RadarTorres_Firmware.ino`) | Controla 13 motores de passo (varredura central + 8 pan/tilt de câmeras + 4 pan de torres), lê 2 sensores ultrassônicos HC-SR04, aciona 5 LEDs, envia leituras de alvo e recebe comandos do PC |
| Armazenamento local (disco do usuário) | `radartorres.db` (SQLite) | Usuários, auditoria, objetos detectados, preferências e demais dados cadastrais; CSV permanece apenas como exportação manual da tela de Objetos Detectados |
| Sensores ultrassônicos (2x HC-SR04) | — | Montados a 180° no eixo da varredura central; juntos cobrem 360°, alimentam o Arduino |
| Torres de decisão (4x, eixo PAN) / indicador | — | Laser de baixa potência ou LED, nunca armamento real; posicionamento via motores de passo próprios |

## 2. Diagrama de Implantação

![Diagrama de Implantação](UML/Implantacao_RadarTorres.png)

Diagrama fonte: [`Implantacao_RadarTorres.puml`](UML/Implantacao_RadarTorres.puml).

## 3. Protocolo de comunicação (USB/Serial)

O único canal entre os dois nós físicos é a porta serial sobre USB, com mensagens em texto
simples (ver `Docs/Tecnica/COMUNICACAO_ARDUINO.md` para a especificação completa):

| Direção | Formato | Finalidade |
|---|---|---|
| Computador → Arduino | `TIPO;CHAVE=VALOR;...` | Comandos (troca de modo, acionamento, configuração) |
| Arduino → Computador | `TARGET;ID=;ANGLE=;DIST=` | Leituras de alvo (ângulo + distância detectados) |

Não há reconhecimento criptográfico nem autenticação no link serial — a segurança do sistema
depende do controle de acesso físico ao cabo USB e do fato de o Arduino ser um periférico
dedicado, sem acesso a rede.

## 4. Explicação

O diagrama representa apenas **dois nós físicos reais**: o computador do usuário e o Arduino,
conectados exclusivamente por **USB/Serial**. Não há servidor, componente web ou nuvem nessa
topologia — decisão deliberada, para não representar infraestrutura que o RadarTorres não possui.

O diagrama usa a notação UML padrão de implantação: `Computador do Usuário` e `Arduino` são nós
físicos marcados com o estereótipo **«device»**; dentro do nó do computador, o processo
`RadarTorres.App.exe` é um nó de ambiente de execução que contém quatro **«artifact»**
(Apresentação, Serviços, Persistência local, .NET 9 Runtime) — artefatos de software do mesmo
processo, não componentes distribuídos em máquinas ou processos separados. O banco de dados
(`radartorres.db`) é representado com o ícone de cilindro (**database**), reservado para dados
persistidos pelo sistema. Os dois acoplamentos mais relevantes do processo saem da caixa para
fora: Serviços comunica-se com o Arduino pela porta serial, e a camada de Persistência lê/grava o
banco SQLite local em disco.

Do lado do Arduino (MEGA 2560), o Firmware concentra as responsabilidades físicas do dispositivo:
controlar os 13 motores de passo (varredura central, pan/tilt das câmeras e pan das torres de
decisão), ler os 2 sensores ultrassônicos HC-SR04, acionar os 5 LEDs, enviar leituras de alvo ao
computador e receber comandos deste.

A escolha por um único processo desktop self-contained, sem servidor, é coerente com o argumento
de segurança/leveza já discutido para o SGBD (ver
[`Modelo_Banco_de_Dados.md`](Modelo_Banco_de_Dados.md)): menor superfície de
ataque (nenhuma porta de rede exposta), zero administração de infraestrutura e instalação
totalmente local, adequadas ao perfil de nó único do protótipo.

## 5. Validação

* Nós, artefato e protocolo confirmados em `installer/RadarTorres.iss`, `App.xaml.cs` e
  `Docs/Tecnica/COMUNICACAO_ARDUINO.md`.
* Driver de persistência (`Microsoft.Data.Sqlite` 9.0.0) confirmado em
  `src/RadarTorres.App/RadarTorres.App.csproj:50`.
* A segregação em quatro blocos internos (Apresentação/Serviços/Persistência/Runtime) é
  **didática**, não uma leitura de componentes fisicamente distribuídos — ver Seção 6 de
  [`UML_RadarTorres.md`](UML_RadarTorres.md).

## Ver também

- [[UML_RadarTorres]] — Seção 6, discussão conceitual deste diagrama.
- [[COMUNICACAO_ARDUINO]] — protocolo que trafega na ligação USB/serial representada aqui.
- [[Diagrama_Eletrico]] — detalhe físico/pinagem do nó Arduino.
- [[Modelo_Banco_de_Dados]] — especificação completa do SGBD usado na camada de Persistência.
