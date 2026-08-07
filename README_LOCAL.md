# Sistema de Detecção e Seleção de Torres — TCC

Aplicativo desktop em C#/WPF que se comunica via porta serial (USB) com um Arduino
responsável por sensores de detecção de alvos ao redor de uma base. O software localiza os
alvos em um radar gráfico, determina automaticamente o quadrante de cada um, seleciona a
torre demonstrativa mais próxima/adequada e pode acionar um indicador demonstrativo
(laser de baixa potência / LED / simulação — **nunca armamento real**).

> Projeto desenvolvido como base de Trabalho de Conclusão de Curso (TCC).

---

## 1. Objetivo do sistema

Fornecer uma plataforma de software completa para:

1. Receber, de forma assíncrona e não bloqueante, leituras de sensores enviadas por um
   Arduino via serial (protocolo texto simples, documentado em
   [`Documentation/COMUNICACAO_ARDUINO.md`](Documentation/COMUNICACAO_ARDUINO.md)).
2. Converter as leituras (ângulo + distância) em posição cartesiana e exibi-las em tempo
   real em um radar circular, dividido em quatro quadrantes.
3. Selecionar automaticamente, entre um conjunto configurável de torres demonstrativas
   posicionadas ao redor da base, qual delas está mais próxima/melhor posicionada para
   cada alvo (algoritmo documentado em
   [`Documentation/ALGORITMO_SELECAO_TORRE.md`](Documentation/ALGORITMO_SELECAO_TORRE.md)).
4. Permitir um modo de acionamento **demonstrativo** (indicador/laser de baixa potência ou
   simulação puramente em software), sempre respeitando uma distância mínima de segurança.
5. Funcionar de ponta a ponta mesmo sem nenhum Arduino conectado, através de um modo de
   simulação embutido — essencial para desenvolvimento contínuo e para as demonstrações do TCC.

## 2. Arquitetura

O projeto segue uma separação clássica de responsabilidades (inspirada em MVVM), com toda a
lógica de negócio isolada da interface gráfica:

```
Views (WPF/XAML)  <-->  ViewModels (MainViewModel)  <-->  Services (regras de negócio)
                                                                 |
                                                          Models (entidades)
```

* **Views** não contêm lógica alguma além de bindings e pequenos encaminhamentos de eventos
  de UI (ex.: clique em um alvo no radar).
* **MainViewModel** orquestra os serviços e expõe apenas propriedades/comandos simples.
* **Services** contêm 100% das regras: parsing do protocolo serial, rastreamento de alvos,
  seleção de torres, controle de acionamento e simulação. Nenhum deles referencia WPF.
* **Models** são entidades de domínio simples (`Target`, `Tower`, ...), com
  `INotifyPropertyChanged` para permitir binding direto e atualização em tempo real sem
  recriar a interface.

Detalhes completos de cada classe estão em
[`Documentation/DOCUMENTACAO_TECNICA.md`](Documentation/DOCUMENTACAO_TECNICA.md) e o racional
arquitetural em [`Documentation/ARQUITETURA.md`](Documentation/ARQUITETURA.md).

## 3. Estrutura das pastas

```
TCC/
├── README.md                          (este arquivo)
├── Arduino/
│   └── ArduinoSimulation.ino          (firmware de teste, sem sensores reais)
├── Documentation/
│   ├── ARQUITETURA.md
│   ├── COMUNICACAO_ARDUINO.md
│   ├── ALGORITMO_SELECAO_TORRE.md
│   └── DOCUMENTACAO_TECNICA.md
└── src/
    └── RadarTorres.App/
        ├── RadarTorres.App.csproj
        ├── appsettings.json            (configuração: portas, torres, distâncias...)
        ├── App.xaml / App.xaml.cs
        ├── Models/                     (Target, Tower, SensorReading, SystemState, LogEntry)
        ├── Configuration/              (AppSettings, AppConfig)
        ├── Helpers/                    (CoordinateConverter, DistanceCalculator, QuadrantHelper, RelayCommand)
        ├── Services/                   (Serial*, Target/TowerSelection, FireControl, Simulation, Logging)
        ├── Converters/                 (conversores de binding usados no XAML)
        ├── ViewModels/                 (MainViewModel, ViewModelBase)
        └── Views/                      (MainWindow, RadarControl)
```

## 4. Tecnologias utilizadas

| Camada              | Tecnologia                                          |
|---------------------|------------------------------------------------------|
| Linguagem            | C# 13 / .NET 9 (LTS mais recente disponível)         |
| Interface            | WPF (Windows Presentation Foundation)                |
| Comunicação serial    | `System.IO.Ports` (pacote NuGet)                     |
| Configuração          | `Microsoft.Extensions.Configuration` + `appsettings.json` |
| Firmware de teste     | Arduino (C/C++, Arduino IDE)                          |

**Por que WPF em vez de WinForms?** WPF foi escolhido porque oferece data-binding real,
gráficos vetoriais 2D de alta qualidade (essenciais para o radar circular), separação
MVVM natural e melhor desempenho de redesenho em tempo real — todos requisitos centrais
deste projeto.

## 5. Como executar

Pré-requisitos: Windows 10/11 e [.NET SDK 9.0+](https://dotnet.microsoft.com/download).

```bash
cd src/RadarTorres.App
dotnet build
dotnet run
```

Ou abra `RadarTorres.sln` (na raiz do projeto) no Visual Studio 2022+ e pressione F5.

## 6. Como conectar ao Arduino

1. Grave o firmware [`Arduino/ArduinoSimulation.ino`](Arduino/ArduinoSimulation.ino) no seu
   Arduino (ou o firmware definitivo dos sensores, desde que siga o mesmo protocolo).
2. Conecte o Arduino via USB e anote a porta COM atribuída pelo Windows (Gerenciador de
   Dispositivos).
3. No painel **CONTROLE DO SISTEMA**, clique em "⟳" para atualizar a lista de portas,
   selecione a porta correta e o Baud Rate (9600 por padrão, igual ao firmware de teste).
4. Clique em **CONECTAR**. O indicador de status deve ficar verde ("Connected") e o console
   de eventos deve mostrar `Arduino conectado em COMx @ 9600 bps`.
5. Selecione um dos quatro modos de operação para começar a receber/processar alvos.

Se a conexão cair (cabo desconectado, porta ocupada, etc.), o software detecta a falha
automaticamente (ver seção de tratamento de erros em
[`Documentation/COMUNICACAO_ARDUINO.md`](Documentation/COMUNICACAO_ARDUINO.md)) e registra o
evento no console, sem travar ou fechar a aplicação.

## 7. Como utilizar o modo de simulação

Não é necessário nenhum Arduino para testar o sistema por completo:

1. Marque a caixa **"Modo de simulação"** no painel de ações (isso é bloqueado enquanto
   houver uma conexão serial real ativa, e vice-versa).
2. O `SimulationService` passa a gerar alvos fictícios automaticamente (posição inicial
   aleatória, quantidade configurável em `appsettings.json` →
   `SimulationSettings.DefaultTargetCount`), movendo-os a cada ciclo.
3. Selecione o modo "3. Localização + seleção automática de torre" ou "4. Localização +
   acionamento demonstrativo automático" para observar o algoritmo de seleção e (no modo 4)
   o acionamento demonstrativo agindo sobre os alvos simulados.
4. Desmarque a caixa para parar a geração de novos alvos a qualquer momento.

## 8. Como configurar as torres

A quantidade e a posição das torres **não são fixas no código** — são lidas de
`src/RadarTorres.App/appsettings.json`:

```json
"Towers": [
  { "Id": 1, "Name": "Torre 1", "X": 3.0, "Y": 3.0 },
  { "Id": 2, "Name": "Torre 2", "X": -3.0, "Y": 3.0 },
  { "Id": 3, "Name": "Torre 3", "X": -3.0, "Y": -3.0 },
  { "Id": 4, "Name": "Torre 4", "X": 3.0, "Y": -3.0 }
]
```

Para adicionar, remover ou reposicionar uma torre, basta editar esta lista e reiniciar o
aplicativo — nenhuma alteração de código é necessária (ver
`TowerSelectionService.LoadTowersFromConfig`).

## 9. Como funciona o radar

O `RadarControl` (`Views/RadarControl.xaml` + `.xaml.cs`) desenha:

* Círculos concêntricos de distância (quantidade configurável, `RadarSettings.DistanceRingCount`).
* Linhas dos quadrantes (eixos X/Y) e rótulos Q1–Q4.
* Indicação de Norte / 0° no topo.
* A base (origem) no centro.
* Torres (triângulos roxos, ficam verdes quando selecionadas para algum alvo, vermelhas
  durante o acionamento demonstrativo).
* Alvos (círculos azuis; o alvo selecionado na interface fica em destaque dourado e maior).
* Uma linha tracejada ligando o alvo à torre escolhida para ele, quando houver.

A posição de cada alvo é calculada a partir de ângulo/distância recebidos
(`CoordinateConverter.PolarToCartesian`) e depois convertida para pixels de tela
(`CoordinateConverter.WorldToScreen`). A atualização visual ocorre em um timer próprio
(`RadarSettings.RefreshRateMs`, 150 ms por padrão) que **reposiciona elementos gráficos já
existentes** em vez de recriar toda a árvore visual a cada leitura — fundamental para manter
a interface fluida mesmo com muitos alvos.

## 10. Como funciona a seleção automática da torre

Resumo (detalhamento matemático completo em
[`Documentation/ALGORITMO_SELECAO_TORRE.md`](Documentation/ALGORITMO_SELECAO_TORRE.md)):

1. Cada leitura `ANGLE`/`DIST` é convertida em coordenadas cartesianas (X, Y) relativas à base.
2. O quadrante do alvo é determinado (Q1–Q4) a partir do sinal de X e Y.
3. Entre as torres **disponíveis**, o sistema prioriza as que "pertencem" ao mesmo quadrante
   do alvo; se nenhuma estiver disponível, considera todas as disponíveis.
4. Calcula a distância Euclidiana entre o alvo e cada torre candidata.
5. Seleciona a torre de menor distância, registra a decisão no console de eventos e atualiza
   o radar (linha tracejada + torre destacada em verde).
6. Dependendo do modo de operação, o sistema apenas informa a seleção (modo 3) ou também
   envia o comando de acionamento demonstrativo (modo 4) — sempre respeitando a distância
   mínima de segurança.

---

## Documentação adicional

* [`Documentation/ARQUITETURA.md`](Documentation/ARQUITETURA.md) — decisões arquiteturais e diagramas.
* [`Documentation/DOCUMENTACAO_TECNICA.md`](Documentation/DOCUMENTACAO_TECNICA.md) — referência de cada classe/serviço.
* [`Documentation/COMUNICACAO_ARDUINO.md`](Documentation/COMUNICACAO_ARDUINO.md) — protocolo serial completo.
* [`Documentation/ALGORITMO_SELECAO_TORRE.md`](Documentation/ALGORITMO_SELECAO_TORRE.md) — matemática do radar e da seleção de torres.

## Limitações atuais e próximos passos

Ver a seção final da resposta que acompanha a entrega deste projeto (ou o arquivo
`DOCUMENTACAO_TECNICA.md`, seção "Limitações e próximos passos") para o roteiro de
integração com sensores reais.
