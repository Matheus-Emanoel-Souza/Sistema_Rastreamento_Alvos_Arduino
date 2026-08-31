# 🛰️ RadarTorres — Sistema de Detecção e Seleção de Torres

![Plataforma](https://img.shields.io/badge/plataforma-Windows%2010%2F11%20x64-0078D6?logo=windows&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)
![Linguagem](https://img.shields.io/badge/linguagem-C%23%2013-239120?logo=csharp&logoColor=white)
![UI](https://img.shields.io/badge/UI-WPF-blue)
![Versão](https://img.shields.io/badge/versão-1.0.0-brightgreen)
![Licença](https://img.shields.io/badge/licença-EPL--2.0-orange)
[![Download](https://img.shields.io/badge/⬇️_download-Setup.exe-success)](https://github.com/Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino/releases/latest)

Aplicativo desktop em **C#/WPF** que se comunica via porta serial (USB) com um **Arduino**
responsável por sensores de detecção de alvos ao redor de uma base. O software localiza os
alvos em um radar gráfico, determina automaticamente o quadrante de cada um, seleciona a
torre demonstrativa mais próxima/adequada e pode acionar um indicador demonstrativo
(laser de baixa potência / LED / simulação — **nunca armamento real**).

> 🎓 Projeto desenvolvido como base de Trabalho de Conclusão de Curso (TCC) em Engenharia da
> Computação.

---

## 📋 Índice

- [Objetivo do sistema](#-objetivo-do-sistema)
- [Pré-requisitos](#️-pré-requisitos)
- [Instalação](#-instalação)
- [Como usar](#-como-usar)
- [Como gerar o instalador (build)](#️-como-gerar-o-instalador-build)
- [Arquitetura](#-arquitetura)
- [Estrutura de pastas](#-estrutura-de-pastas)
- [Tecnologias utilizadas](#️-tecnologias-utilizadas)
- [Documentação adicional](#-documentação-adicional)
- [Licença](#-licença)
- [Créditos](#-créditos)

---

## 🎯 Objetivo do sistema

Fornecer uma plataforma de software completa para:

1. Receber, de forma assíncrona e não bloqueante, leituras de sensores enviadas por um
   Arduino via serial (protocolo texto simples, documentado em
   [`Docs/Tecnica/COMUNICACAO_ARDUINO.md`](Docs/Tecnica/COMUNICACAO_ARDUINO.md)).
2. Converter as leituras (ângulo + distância) em posição cartesiana e exibi-las em tempo
   real em um radar circular, dividido em quatro quadrantes.
3. Selecionar automaticamente, entre um conjunto configurável de torres demonstrativas
   posicionadas ao redor da base, qual delas está mais próxima/melhor posicionada para
   cada alvo (algoritmo documentado em
   [`Docs/Tecnica/ALGORITMO_SELECAO_TORRE.md`](Docs/Tecnica/ALGORITMO_SELECAO_TORRE.md)).
4. Permitir um modo de acionamento **demonstrativo** (indicador/laser de baixa potência ou
   simulação puramente em software), sempre respeitando uma distância mínima de segurança.
5. Funcionar de ponta a ponta mesmo sem nenhum Arduino conectado, através de um modo de
   simulação embutido — essencial para desenvolvimento contínuo e para as demonstrações do TCC.

---

## ⚙️ Pré-requisitos

### Para usar o aplicativo (usuário final)

| Requisito | Detalhe |
|---|---|
| Sistema operacional | Windows 10 ou 11, **64-bit (x64)** |
| Espaço em disco | ~250 MB livres (instalação self-contained, inclui o runtime do .NET) |
| Memória RAM | Sem requisito especial além do mínimo do próprio Windows — aplicativo leve, sem processamento pesado |
| Runtime externo | **Nenhum** — o instalador já inclui o .NET 9 Desktop Runtime (ver [self-contained](#-como-gerar-o-instalador-build)) |
| Hardware opcional | Arduino (ou compatível) conectado via USB — dispensável, já que há [modo de simulação](#modo-de-simulação) embutido |

### Para desenvolver/compilar a partir do código-fonte

| Ferramenta | Versão usada | Fonte oficial |
|---|---|---|
| .NET SDK | 9.0.316+ | https://dotnet.microsoft.com/download/dotnet/9.0 |
| Inno Setup *(só para gerar o instalador)* | 6.7.3+ | https://jrsoftware.org/isdl.php |
| Visual Studio 2022+ *(opcional)* | com workload ".NET desktop development" | https://visualstudio.microsoft.com/ |
| Arduino CLI *(opcional — só para compilar sketches pela aba **Configurações do Arduino**)* | qualquer versão recente | https://arduino.github.io/arduino-cli/latest/installation/ |

---

## 📦 Instalação

### Opção 1 — Instalador Windows (recomendado para usuário final)

O RadarTorres é distribuído como um instalador único, `Setup.exe`, gerado com o
[Inno Setup 6](https://jrsoftware.org/isinfo.php) e publicado nas
[Releases do repositório](https://github.com/Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino/releases).

➡️ **[Baixar a versão mais recente (Setup.exe)](https://github.com/Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino/releases/latest)**
&nbsp;·&nbsp; [v1.0.0 diretamente](https://github.com/Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino/releases/tag/v1.0.0)

1. Baixe o `Setup.exe` da versão mais recente.
2. Execute e siga o assistente (será pedida elevação de administrador, necessária porque o
   app é instalado em `C:\Program Files\RadarTorres` e registrado em "Programas e Recursos").
3. Ao final, use a opção "Executar o RadarTorres" para abrir o app imediatamente, ou os
   atalhos criados no Menu Iniciar / Área de Trabalho.

Não é necessário instalar o .NET separadamente — o instalador já inclui tudo o que o
aplicativo precisa para rodar. O que o instalador faz:

- Cria atalho no **Menu Iniciar** (sempre) e, opcionalmente, na **Área de Trabalho** (checkbox
  desmarcado por padrão no assistente).
- Registra o RadarTorres em **"Programas e Recursos"** do Windows, com desinstalador
  (`unins000.exe`) gerado automaticamente.
- Em atualizações (reinstalar por cima de uma versão existente), **preserva o
  `appsettings.json`** já configurado pelo usuário (porta COM, torres, distâncias etc.) — só
  grava o arquivo padrão na primeira instalação.

#### Launcher avulso (opcional)

Para quem já tem o RadarTorres instalado e quer um atalho único e estável para abri-lo — sem
depender do Menu Iniciar ou de ter marcado o ícone de Área de Trabalho na instalação — existe
um executável separado, `RadarTorres-Launcher.exe` (projeto
[`src/RadarTorres.Launcher`](src/RadarTorres.Launcher)): localiza o app instalado (pasta padrão
ou, se customizada, via registro do Windows) e o abre. Pode ser copiado para a Área de
Trabalho, fixado na barra de tarefas etc. Não precisa do .NET instalado (self-contained,
arquivo único). Gerar:

```powershell
dotnet publish src\RadarTorres.Launcher -c Release -r win-x64
```

O `.exe` fica em `src\RadarTorres.Launcher\bin\Release\net9.0-windows\win-x64\publish\`.

### Opção 2 — A partir do código-fonte (desenvolvimento)

```bash
git clone https://github.com/Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino.git
cd Sistema_Rastreamento_Alvos_Arduino/src/RadarTorres.App
dotnet build
dotnet run
```

Ou abra `RadarTorres.sln` (na raiz do projeto) no Visual Studio 2022+ e pressione F5.

### 💡 Dicas — rodar sem ser administrador do computador

O `Setup.exe` pede elevação de administrador porque instala em `C:\Program Files`. Se você
não tem esse privilégio na máquina, dá para rodar o RadarTorres sem ele, de duas formas:

**1. Rodar direto do código-fonte** (mais simples, se já tiver o .NET SDK instalado):

```powershell
dotnet run --project src/RadarTorres.App
```

Não grava nada fora da sua pasta de usuário (`%AppData%\RadarTorres` para os dados).

**2. Gerar um `.exe` avulso, sem instalar nada** — publica tudo (incluindo o runtime do .NET)
em uma pasta comum, tipo Desktop ou Documentos, e funciona como um programa portátil:

```powershell
dotnet publish src/RadarTorres.App -c Release -r win-x64 --self-contained true -o "$env:USERPROFILE\Desktop\RadarTorres"
```

Depois é só abrir `RadarTorres.App.exe` dentro dessa pasta — sem instalador e sem precisar de
privilégio de administrador.

---

## 🚀 Como usar

### Login e multiusuário

O app pede login ao abrir. No primeiro uso:

```text
Usuário: admin
Senha:   admin123
```

Troque a senha padrão em **Perfil > Alterar senha** assim que possível. Detalhes completos
(arquitetura, tabelas, como validar cada funcionalidade) em
[`Docs/Projeto/ETAPA1_FUNDACAO.md`](Docs/Projeto/ETAPA1_FUNDACAO.md) e
[`Docs/Tecnica/MODELO_DADOS.md`](Docs/Tecnica/MODELO_DADOS.md).

### Conectar ao Arduino

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
automaticamente e registra o evento no console, sem travar ou fechar a aplicação — detalhes em
[`Docs/Tecnica/COMUNICACAO_ARDUINO.md`](Docs/Tecnica/COMUNICACAO_ARDUINO.md).

### Modo de simulação

Não é necessário nenhum Arduino para testar o sistema por completo:

1. Marque a caixa **"Modo de simulação"** no painel de ações (bloqueado enquanto houver uma
   conexão serial real ativa, e vice-versa).
2. O `SimulationService` passa a gerar alvos fictícios automaticamente (posição inicial
   aleatória, quantidade configurável em `appsettings.json` →
   `SimulationSettings.DefaultTargetCount`), movendo-os a cada ciclo.
3. Selecione o modo "3. Localização + seleção automática de torre" ou "4. Localização +
   acionamento demonstrativo automático" para observar o algoritmo de seleção e (no modo 4)
   o acionamento demonstrativo agindo sobre os alvos simulados.
4. Desmarque a caixa para parar a geração de novos alvos a qualquer momento.

### Configurações do Arduino (compilar sketches e monitor serial pela interface)

Além da tela de Monitoramento, a barra lateral tem uma aba **Configurações do Arduino** para
configurar o [Arduino CLI](https://arduino.github.io/arduino-cli/), compilar um sketch `.ino`
e acompanhar a saída em tempo real, sem sair do aplicativo:

1. **Ambiente Arduino:** informe o caminho de `arduino-cli.exe` (botão **Procurar…**) ou clique
   em **Detectar automaticamente** (procura o caminho salvo, a pasta do próprio app, o `PATH`
   do Windows e locais comuns de instalação, nessa ordem — nunca baixa nada
   automaticamente). Escolha a placa/FQBN, a porta COM e o Baud Rate.
2. **Compilação:** clique em **Selecionar código .ino…** (por padrão, se disponível, já vem
   pré-selecionado `Arduino/ArduinoSimulation.ino`) e depois em **Compilar**. A saída do
   `arduino-cli compile` aparece em tempo real no console, com status final de sucesso, erro ou
   cancelamento (botão **Cancelar compilação** disponível durante o processo).
3. **Monitor serial:** reaproveita a mesma conexão serial da tela de Monitoramento — se a porta
   já estiver em uso com parâmetros diferentes, o app pergunta antes de desconectar e
   reconectar, nunca derruba uma sessão ativa silenciosamente.

Esta aba implementa apenas **compilação**; gravação/upload de firmware para a placa não está
incluída. Detalhes completos em
[`Docs/Tecnica/COMUNICACAO_ARDUINO.md`](Docs/Tecnica/COMUNICACAO_ARDUINO.md), seção 8.

### Configurar as torres

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

---

## 🏗️ Como gerar o instalador (build)

Comando único, executado na raiz do projeto:

```powershell
powershell -ExecutionPolicy Bypass -File build\publish.ps1
```

Esse script (`build/publish.ps1`) automatiza o pipeline completo:

1. `dotnet restore` + `dotnet build -c Release`
2. `dotnet publish -c Release -r win-x64 --self-contained true` (inclui o .NET Desktop
   Runtime no pacote — ver "Por que self-contained?" abaixo)
3. Compila `installer\RadarTorres.iss` com o Inno Setup (`ISCC.exe`)
4. Gera o instalador final em **`dist\Setup.exe`**

```powershell
# Só compilar/publicar o app, sem gerar o instalador (ex.: Inno Setup ainda não instalado)
powershell -ExecutionPolicy Bypass -File build\publish.ps1 -SkipInstaller

# Se o Inno Setup estiver em um local não padrão
powershell -ExecutionPolicy Bypass -File build\publish.ps1 -InnoSetupPath "C:\caminho\ISCC.exe"
```

### Gerar uma nova versão

1. Atualize a versão em **um único lugar**:
   [`src/RadarTorres.App/RadarTorres.App.csproj`](src/RadarTorres.App/RadarTorres.App.csproj),
   propriedade `<Version>` (ex.: `1.1.0`). O script de build lê esse valor automaticamente e
   propaga para o `.exe`, para o instalador (`AppVersion`) e para o nome exibido em
   "Programas e Recursos".
2. Rode `build\publish.ps1` novamente.
3. O novo `dist\Setup.exe` já instala como upgrade sobre uma instalação existente (mesmo
   `AppId` fixo no `.iss`), preservando o `appsettings.json` do usuário.
4. *(Opcional)* Se o ícone do aplicativo mudar, regenere-o e salve como
   `src/RadarTorres.App/Assets/RadarTorres.ico` antes de publicar.

### Por que self-contained?

O `.csproj` publica como **self-contained** (`--self-contained true` +
`RuntimeIdentifiers=win-x64`): o .NET 9 Desktop Runtime vai embutido no pacote instalado, e o
usuário final não precisa ter nenhuma versão do .NET instalada antes de rodar o RadarTorres.
Isso prioriza confiabilidade em demonstração (funciona em qualquer Windows 10/11 x64 "limpo",
sem depender de internet) e simplicidade (o instalador só copia arquivos), ao custo de um
pacote maior (~42 MB comprimido). Racional completo em
[`Docs/Projeto/INSTALADOR.md`](Docs/Projeto/INSTALADOR.md).

### Solução de problemas

| Sintoma | Causa provável | Solução |
|---|---|---|
| `dotnet publish` falha com `NETSDK1047` | RID não incluído no restore | Confirme `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>` no `.csproj` e rode `dotnet restore` de novo |
| `ISCC.exe não encontrado` | Inno Setup não instalado ou em local não padrão | Instale via https://jrsoftware.org/isdl.php ou use `-InnoSetupPath` |
| Mensagem "appsettings.json não encontrado" ao abrir o app | Instalação corrompida/incompleta | Reinstale o RadarTorres |
| Erro inesperado ao iniciar o app | Exibido em uma caixa de mensagem clara (não crash silencioso) | Ver `App.xaml.cs`, tratamento global de exceções (`DispatcherUnhandledException` / `AppDomain.UnhandledException`) |

---

## 🧱 Arquitetura

O projeto segue uma separação clássica de responsabilidades (inspirada em MVVM), com toda a
lógica de negócio isolada da interface gráfica:

```text
Views (WPF/XAML)  <-->  ViewModels (MainViewModel)  <-->  Services (regras de negócio)
                                                                 |
                                                          Models (entidades)
```

- **Views** não contêm lógica alguma além de bindings e pequenos encaminhamentos de eventos
  de UI (ex.: clique em um alvo no radar).
- **ViewModels** orquestram os serviços e expõem apenas propriedades/comandos simples.
- **Services** contêm 100% das regras: parsing do protocolo serial, rastreamento de alvos,
  seleção de torres, controle de acionamento e simulação. Nenhum deles referencia WPF.
- **Models** são entidades de domínio simples (`Target`, `Tower`, ...), com
  `INotifyPropertyChanged` para permitir binding direto e atualização em tempo real sem
  recriar a interface.

Detalhes completos de cada classe estão em
[`Docs/Tecnica/DOCUMENTACAO_TECNICA.md`](Docs/Tecnica/DOCUMENTACAO_TECNICA.md) e o racional
arquitetural em [`Docs/Tecnica/ARQUITETURA.md`](Docs/Tecnica/ARQUITETURA.md).

### Como funciona o radar (resumo)

O `RadarControl` desenha círculos concêntricos de distância, os quadrantes (Q1–Q4), a base no
centro, as torres (destacadas quando selecionadas/acionadas) e os alvos, com atualização por
timer (`RadarSettings.RefreshRateMs`, 150 ms por padrão) que reposiciona elementos já
existentes em vez de recriar a árvore visual — mantendo a interface fluida.

### Seleção automática de torre (resumo)

1. Cada leitura `ANGLE`/`DIST` é convertida em coordenadas cartesianas (X, Y) relativas à base.
2. O quadrante do alvo é determinado (Q1–Q4) a partir do sinal de X e Y.
3. Entre as torres **disponíveis**, o sistema prioriza as que pertencem ao mesmo quadrante do
   alvo; se nenhuma estiver disponível, considera todas as disponíveis.
4. Calcula a distância Euclidiana entre o alvo e cada torre candidata e seleciona a de menor
   distância, registrando a decisão no console de eventos.
5. Dependendo do modo de operação, o sistema apenas informa a seleção (modo 3) ou também
   envia o comando de acionamento demonstrativo (modo 4) — sempre respeitando a distância
   mínima de segurança.

Matemática completa em
[`Docs/Tecnica/ALGORITMO_SELECAO_TORRE.md`](Docs/Tecnica/ALGORITMO_SELECAO_TORRE.md).

---

## 📁 Estrutura de pastas

```text
Sistema_Rastreamento_Alvos_Arduino/
├── README.md                          (este arquivo)
├── LICENSE
├── RadarTorres.sln
├── Arduino/
│   └── ArduinoSimulation.ino          (firmware de teste, sem sensores reais)
├── build/
│   └── publish.ps1                    (pipeline: build → publish → instalador)
├── installer/
│   └── RadarTorres.iss                (script do Inno Setup 6)
├── dist/                              (gerado pelo build — Setup.exe; não versionado)
├── Docs/
│   ├── Tecnica/                        (arquitetura, protocolo Arduino, modelo de dados, algoritmo, referência de classes)
│   ├── Projeto/                        (contexto do TCC, etapas, instalador, log de solicitações)
│   └── Documentos_Entregaveis/         (documentos acadêmicos entregáveis: UML e Requisitos do Sistema)
│       └── Diagramas_e_requisitos/     (requisitos, casos de uso, diagramas, matriz de rastreabilidade)
├── tests/
│   └── RadarTorres.Tests/             (xUnit — Arduino CLI, compilação, persistência, portas)
└── src/
    ├── RadarTorres.App/
    │   ├── RadarTorres.App.csproj
    │   ├── appsettings.json            (configuração: portas, torres, distâncias...)
    │   ├── App.xaml / App.xaml.cs
    │   ├── Assets/                     (ícone do app)
    │   ├── Configuration/              (AppSettings, AppConfig, ArduinoCliSettings)
    │   ├── Converters/                 (conversores de binding usados no XAML)
    │   ├── Data/ · Repositories/       (persistência em CSV)
    │   ├── Helpers/                    (CoordinateConverter, DistanceCalculator, QuadrantHelper, RelayCommand)
    │   ├── Localization/ · Resources/  (pt-BR / en-US)
    │   ├── Models/                     (Target, Tower, SensorReading, SystemState, LogEntry, Arduino*, DashboardCardLayout, ...)
    │   ├── Services/                   (Serial*, Target/TowerSelection, FireControl, Simulation, Auth, Logging, Arduino*, DashboardLayout*)
    │   ├── Themes/                     (Light.xaml, Dark.xaml)
    │   ├── ViewModels/                 (MainViewModel, ArduinoSettingsViewModel, PainelPrincipalViewModel, ViewModelBase, ...)
    │   └── Views/                      (MainWindow, RadarControl, ArduinoSettingsView, PainelPrincipalView,
    │       Shell/..., Shared/ — DashboardCanvas + DashboardCard: cards do painel principal
    │       arrastáveis/redimensionáveis, sem sobreposição, com layout persistido por usuário)
    └── RadarTorres.Launcher/          (launcher avulso — ver seção "Instalação")
```

---

## 🛠️ Tecnologias utilizadas

| Camada | Tecnologia |
|---|---|
| Linguagem | C# 13 / .NET 9 |
| Interface | WPF (Windows Presentation Foundation) |
| Comunicação serial | `System.IO.Ports` (NuGet) |
| Configuração | `Microsoft.Extensions.Configuration` + `appsettings.json` |
| Injeção de dependência | `Microsoft.Extensions.DependencyInjection` |
| Persistência local | CSV (`Data/`, `Repositories/`) |
| Empacotamento/instalador | [Inno Setup 6](https://jrsoftware.org/isinfo.php) |
| Firmware de teste | Arduino (C/C++, Arduino IDE) |

### ✅ Testes automatizados

```powershell
dotnet restore
dotnet build RadarTorres.sln
dotnet test RadarTorres.sln
```

O projeto `tests/RadarTorres.Tests` (xUnit) cobre hoje a aba **Configurações do Arduino**:
localização do Arduino CLI, montagem segura dos argumentos de compilação, interpretação do
código de saída, cancelamento, persistência das preferências e limite de linhas dos consoles,
além da disputa pelo uso da porta serial compartilhada com a tela de Monitoramento.

**Por que WPF em vez de WinForms?** WPF foi escolhido por oferecer data-binding real, gráficos
vetoriais 2D de alta qualidade (essenciais para o radar circular), separação MVVM natural e
melhor desempenho de redesenho em tempo real — todos requisitos centrais deste projeto.

---

## 📚 Documentação adicional

> Índice completo (com a pasta `Documentos_Entregaveis/`, que inclui `Diagramas_e_requisitos/`) em [`Docs/README.md`](Docs/README.md).

| Documento | Conteúdo |
|---|---|
| [`Docs/Tecnica/ARQUITETURA.md`](Docs/Tecnica/ARQUITETURA.md) | Decisões arquiteturais e diagramas |
| [`Docs/Tecnica/DOCUMENTACAO_TECNICA.md`](Docs/Tecnica/DOCUMENTACAO_TECNICA.md) | Referência de cada classe/serviço |
| [`Docs/Tecnica/COMUNICACAO_ARDUINO.md`](Docs/Tecnica/COMUNICACAO_ARDUINO.md) | Protocolo serial completo |
| [`Docs/Tecnica/ALGORITMO_SELECAO_TORRE.md`](Docs/Tecnica/ALGORITMO_SELECAO_TORRE.md) | Matemática do radar e da seleção de torres |
| [`Docs/Projeto/INSTALADOR.md`](Docs/Projeto/INSTALADOR.md) | Processo de criação do instalador: decisões, arquivos, testes realizados |
| [`Docs/Projeto/ETAPA1_FUNDACAO.md`](Docs/Projeto/ETAPA1_FUNDACAO.md) | Fundação multiusuário: arquitetura, tabelas, validação |
| [`Docs/Tecnica/MODELO_DADOS.md`](Docs/Tecnica/MODELO_DADOS.md) | Modelo de dados |
| [`Docs/Projeto/LOG_SOLICITACOES.md`](Docs/Projeto/LOG_SOLICITACOES.md) | Histórico das solicitações feitas ao assistente ao longo do projeto |

---

## 📄 Licença

Distribuído sob a **Eclipse Public License 2.0**. Veja [`LICENSE`](LICENSE) para o texto
completo.

---

## 👥 Créditos

Desenvolvido por **Matheus Emanoel Souza** como Trabalho de Conclusão de Curso (TCC) em
Engenharia da Computação.

---

<sub>📝 **Nota sobre este arquivo:** até a versão anterior, o projeto tinha dois arquivos na
raiz (`README.md` e `README_LOCAL.md`) com propósitos sobrepostos — um focado em
distribuição/instalação, o outro em arquitetura/uso técnico — sem conflito real de
informação entre eles, apenas escopos complementares. Este `README.md` é o resultado da
fusão dos dois em um único documento na raiz do projeto, conforme convenção do GitHub (que
só renderiza `README.md` automaticamente). O `README_LOCAL.md` foi removido; nenhum conteúdo
de valor foi perdido — tudo foi incorporado às seções acima ou já vivia em
`Docs/`.</sub>
