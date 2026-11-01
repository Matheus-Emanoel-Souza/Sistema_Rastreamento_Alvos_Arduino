Funcionalidade: apresenta o diagrama de arquitetura geral do sistema (camadas, componentes e integrações).

# Diagrama de Arquitetura Geral

Diagrama visual (`.drawio`) da arquitetura de implantação completa do RadarTorres — PC +
Arduino + sensores + câmeras + torres. Encontrado solto na pasta sem nenhum `.md`/link
associado durante a reorganização do vault (2026-10-26); este documento existe só para
indexá-lo e descrever o que ele contém, sem reabrir nenhuma decisão de arquitetura.

Arquivo fonte: [`Diagrama_Arquitetura_Geral.drawio`](Diagrama_Arquitetura_Geral.drawio) (abrir em
[draw.io](https://app.diagrams.net/) ou no plugin drawio do Obsidian).

## Conteúdo do diagrama

- **PC Windows 10/11** (`<<Computador do Operador>>`) — ambiente `.NET 9 Desktop Runtime`,
  executando `RadarTorres.exe` (WPF), com os módulos: Autenticação, Rastreamento de Alvos,
  Seleção de Torre, Serviço Serial, Acionamento Demonstrativo, Simulação, persistência em
  dados (CSV) e `appsettings.json`. Também lista `arduino-cli` como dependência do PC.
- **4 câmeras** (`<<Dispositivo>>` Câmera 1–4), conectadas via USB ao PC.
- **Arduino** (`<<Microcontrolador>>`) — executa o `Firmware (.ino)`, comunica com o PC via
  `Protocolo Serial` sobre USB/Serial (UART).
- **2 sensores ultrassônicos**, conectados ao Arduino via GPIO (Trig/Echo).
- **5 torres** (servos) + lasers de baixa potência (≤ 5 mW, via relés), conectados ao Arduino via
  PWM/Digital.

## Relação com outros documentos

Este diagrama cobre, num único artefato visual, uma visão mais ampla (inclui câmeras e contagem
de torres/lasers) do que o [[Diagrama_de_Implantacao]] (foco PC ↔ Arduino, PlantUML) e do que o
[[Diagrama_Eletrico]] (foco pinagem real do firmware atual, Arduino MEGA 2560 com 13 motores de
passo). Os três documentos descrevem a mesma arquitetura física em níveis de detalhe e épocas de
atualização diferentes — se houver divergência entre eles (ex.: contagem de sensores/torres),
[[Diagrama_Eletrico]] é a fonte mais recente, por ter sido revisado diretamente contra o
firmware real em 2026-10-26.

Ver também: [[Registro_index|Registro]].
