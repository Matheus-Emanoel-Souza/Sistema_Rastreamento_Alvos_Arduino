# Log de Solicitações

Registro cronológico das solicitações feitas ao assistente (Claude Code) para trabalhos
neste projeto, com o texto do pedido e um resumo do que foi entregue. Cada nova sessão de
trabalho com o assistente deve acrescentar uma entrada aqui, não substituir as anteriores.

---

## 2026-08-07 — Processo de instalação Windows (Setup.exe)

**Pedido (texto integral):**

> Claude, analise todo o projeto e melhore o aplicativo criando um processo completo de
> instalação para Windows.
> Implemente os seguintes requisitos:
>
> 1. Crie um instalador executável (`Setup.exe`) com uma interface simples e profissional.
> 2. Durante a instalação, verifique automaticamente todos os componentes necessários para
>    executar o aplicativo, principalmente a versão correta do .NET ou do .NET Desktop
>    Runtime.
> 3. Caso alguma dependência não esteja instalada, solicite autorização ao usuário e realize
>    a instalação utilizando fontes oficiais.
> 4. Avalie se é mais adequado publicar o aplicativo como `self-contained`, incluindo o .NET
>    no próprio pacote. Se essa for a melhor opção, implemente dessa forma e explique a
>    decisão.
> 5. Instale o aplicativo em uma pasta apropriada do Windows, como
>    `C:\Program Files\NomeDoAplicativo`.
> 6. Crie um executável funcional para iniciar o aplicativo após a instalação.
> 7. Adicione atalhos no menu Iniciar e, se o usuário selecionar essa opção, também na área
>    de trabalho.
> 8. Adicione o aplicativo à lista de programas instalados do Windows e disponibilize uma
>    opção de desinstalação.
> 9. Preserve corretamente arquivos de configuração e dados do usuário durante atualizações.
> 10. Exiba mensagens claras caso ocorra algum erro durante a instalação ou inicialização.
> 11. Gere os arquivos finais necessários para distribuição em uma pasta chamada `dist` ou
>     `release`.
>
> Você pode utilizar uma ferramenta adequada ao projeto, como Inno Setup, WiX Toolset ou
> outra solução compatível. Escolha a opção mais simples, confiável e fácil de manter.
> Antes de fazer as alterações, identifique:
>
> * A tecnologia e a versão do .NET utilizadas pelo aplicativo;
> * O projeto que gera o executável principal;
> * Todas as dependências necessárias;
> * A arquitetura suportada, como `x64`, `x86` ou `ARM64`;
> * O nome, a versão e o ícone do aplicativo.
>
> Depois, implemente efetivamente todas as alterações no projeto. Não apresente apenas
> instruções teóricas.
> Ao finalizar:
>
> * Compile o aplicativo;
> * Gere o instalador;
> * Teste o executável principal e o processo de instalação;
> * Informe quais arquivos foram criados ou modificados;
> * Indique onde está o instalador final;
> * Explique como gerar uma nova versão futuramente;
> * Registre os comandos necessários em um arquivo `README.md`.
>
> Não altere funcionalidades já existentes no aplicativo e corrija qualquer erro de
> compilação relacionado às mudanças realizadas.

Seguido de duas trocas curtas: pedido de esclarecimento sobre instalar o .NET 9 SDK e o Inno
Setup 6 via `winget` (autorizado pelo usuário, cancelado depois pelo administrador da
máquina), pedido para baixar os instaladores oficiais na pasta Downloads para instalação
manual, e confirmação de que os dois softwares tinham sido instalados manualmente.

**Entregue:**

- Levantamento do projeto: WPF/.NET 9 (`net9.0-windows`), projeto `RadarTorres.App`,
  dependências NuGet, arquitetura x64, sem nome/versão/ícone definidos previamente.
- Ícone (`src/RadarTorres.App/Assets/RadarTorres.ico`) gerado via PowerShell/`System.Drawing`.
- Metadados de nome/versão/ícone adicionados ao `.csproj`.
- Publicação **self-contained** (win-x64) escolhida e justificada — dispensa checagem/
  instalação de .NET no computador do usuário final (ver `Docs/INSTALADOR.md`,
  seção 3).
- Tratamento global de erros de inicialização em `App.xaml.cs` (mensagens claras em vez de
  crash), sem alterar funcionalidades existentes.
- Script Inno Setup 6 (`installer/RadarTorres.iss`): instala em
  `C:\Program Files\RadarTorres`, atalho no Menu Iniciar sempre, atalho na Área de Trabalho
  opcional, entrada em "Programas e Recursos" com desinstalador, preserva `appsettings.json`
  do usuário em upgrades.
- Script de build (`build/publish.ps1`): automatiza build → publish self-contained →
  geração do instalador em `dist/Setup.exe`.
- Build, publish e instalador gerados e testados de ponta a ponta (ver
  `Docs/INSTALADOR.md`, seção 7, para a lista completa de testes e o único ponto
  parcial — desinstalação silenciosa travando no ambiente de automação usado, não no script).
- Documentação de uso no `README.md` (seção "Instalação (Windows)") e detalhamento técnico
  em `Docs/INSTALADOR.md`.

---

## 2026-08-07 — Documentação e commit das alterações

**Pedido (texto integral):**

> Faça um resumo de suas alterações, Documente na pasta de documentação, adicione na pasta
> de documentação os pedidos que faço em cada prompt como logs, e commite para mim na branch
> sistemas mesmo. pv

**Entregue:**

- `Docs/INSTALADOR.md` — documentação técnica do processo de instalação criado na
  solicitação anterior.
- `Docs/LOG_SOLICITACOES.md` — este arquivo, com o histórico de pedidos.
- Commit na branch `Sistema` com todas as alterações da sessão anterior (instalador) mais
  esta documentação.

---

## 2026-08-07 — Interface multiusuário (barra superior, barra lateral, login, auditoria)

**Pedido (resumo — texto completo tem 13 seções detalhadas, preservado no histórico da
conversa):** melhorar a interface e a estrutura do app com barra superior (idioma PT/EN,
tema claro/escuro/sistema, usuário conectado, perfil, ajuda, sair), barra lateral recolhível
com navegação (Painel principal, Monitoramento, Objetos detectados, Ações realizadas,
Histórico de modos, Usuários, Chamados de ajuda, Configurações), painel principal com
indicadores, três tabelas de auditoria (`objetos_detectados`, `acoes_realizadas`,
`alteracoes_modo`) com regras específicas (auditoria não editável pelo usuário comum,
confirmação antes de trocar modo), sistema de usuários/permissões com login, hash seguro de
senha e 3 perfis (Administrador/Operador/Visualizador), personalização de layout por
usuário, formulário de ajuda com tabela `chamados_ajuda`, requisitos de acessibilidade e
segurança (hash, validação, consultas parametrizadas, tratamento centralizado de erros).
Pedido explícito para analisar o projeto e apresentar um plano em etapas **antes** de alterar
código, e perguntar antes de decisões que alterassem significativamente o projeto.

**Perguntas feitas antes de implementar (`AskUserQuestion`) e respostas:**
1. *Banco de dados* — recomendei SQLite + EF Core; o usuário decidiu **CSV por agora**,
   sinalizado para futura conversão a SQL.
2. *Coordenada Z em `objetos_detectados`* (sensor atual é 2D) — usuário confirmou **campo
   nullable, gravado como `NULL`** por enquanto.
3. *Modos do sistema* (`SystemMode` técnico existente x Manual/Automático/Manutenção/
   Emergência pedido) — usuário confirmou **estender o `SystemMode` existente** em vez de
   criar um conceito paralelo.

**Entregue nesta parte** (fundação + Painel principal + navegação completa; as 4 telas de
dados restantes ficam para a próxima entrega, conforme plano apresentado e aceito):

- Camada de dados CSV (`Data/`, `Repositories/`) para as 6 tabelas, com interfaces prontas
  para trocar por um banco relacional sem alterar o resto do app.
- Autenticação (login, hash PBKDF2, sessão, alteração de senha) e permissões por perfil.
- Internacionalização (pt-BR/en-US) via arquivos JSON — nenhuma string de UI nova hardcoded.
- Tema claro/escuro/sistema, trocado em runtime.
- Composition root com injeção de dependência (`App.xaml.cs`), substituindo o wiring manual
  antigo de `MainWindow.xaml.cs`.
- Shell (barra superior + barra lateral recolhível + navegação), `LoginWindow`, `ProfileWindow`
  (com alteração de senha), `HelpDeskFormWindow`, `PainelPrincipalView` (dashboard).
- Migração de `MainWindow` para `MonitoramentoView` (mesma funcionalidade, hospedada pela Shell).
- `SystemMode` estendido (Manutenção/Emergência) com confirmação e auditoria em toda troca de modo.
- `FireControlService` e `MainViewModel` passaram a gravar auditoria (`acoes_realizadas`,
  `objetos_detectados`, `alteracoes_modo`) nos pontos únicos onde essas ações já passavam.
- Documentação: `Docs/MODELO_DADOS.md` (schema + relacionamentos + plano de migração
  SQL) e `Docs/ETAPA1_FUNDACAO.md` (arquitetura antes/depois, arquivos alterados,
  como validar cada funcionalidade, o que falta para fechar a Etapa 1).
- Build limpo (0 erros, 0 avisos) e validação de ponta a ponta (login real, Shell renderizada,
  CSVs de auditoria gerados) usando captura direta de janela (`PrintWindow`) e verificação
  criptográfica isolada do hash de senha — sem automação de teclado, depois que uma tentativa
  de `SendKeys` acabou digitando em uma janela errada (WhatsApp Web) por roubo de foco; o
  incidente foi reportado ao usuário assim que percebido.

---

## 2026-08-10 — Aba "Configurações do Arduino" (ambiente, compilação, monitor serial)

**Pedido (resumo — texto completo com ~15 seções detalhadas, preservado no histórico da
conversa):** adicionar uma nova aba ao menu lateral, "Configurações do Arduino", organizada em
três seções — (1) Ambiente Arduino: caminho do `arduino-cli.exe`, detecção automática (caminho
salvo → pasta do app → PATH → locais comuns), seleção de placa/FQBN, porta COM e baud rate;
(2) Compilação: seleção de sketch `.ino` (com `Arduino/ArduinoSimulation.ino` como padrão
inicial), compilar/cancelar de forma assíncrona via `arduino-cli compile --fqbn <fqbn>
<pasta>`, com `ArgumentList` seguro (nunca shell), console em tempo real e status decidido só
pelo código de saída; (3) Monitor serial em tempo real, reaproveitando a comunicação serial já
existente e sem permitir duas conexões concorrentes na mesma porta (com confirmação do usuário
antes de desconectar uma sessão já ativa). Pedido explícito para: analisar todo o projeto antes
de alterar qualquer arquivo; reutilizar a arquitetura MVVM e a comunicação serial existentes
(sem criar uma segunda implementação concorrente); implementar apenas compilação, não
upload/gravação de firmware, sem consultar antes; persistir as configurações em
`%LocalAppData%\RadarTorres` (nunca em `C:\Program Files\...`); adicionar testes para as
partes testáveis sem hardware físico; rodar `dotnet restore`/`build`/`test` e corrigir erros;
atualizar `README.md` e os quatro documentos em `Docs/`; não commitar/pushar sem autorização.

**Imprevisto encontrado durante a sessão (fora do controle do assistente):** a pasta
`Documentation/` foi renomeada para `Docs/` no disco (e um `Bem-vindo.md` solto na raiz foi
apagado) enquanto a sessão estava em andamento — consistente com uma ação do Obsidian rodando
em paralelo (há uma pasta `.obsidian/` na raiz, indicando que o repositório é aberto como
vault). O assistente identificou a mudança via `git status`, parou antes de editar qualquer
documento e perguntou ao usuário como proceder; a resposta foi manter `Docs/` e atualizar todas
as referências a `Documentation/` no código/documentação/README para `Docs/`.

**Entregue:**

- **Modelos** (`Models/`): `ArduinoBoardOption`/`ArduinoBoardCatalog` (catálogo padrão de
  placas), `ArduinoCliInfo`/`ArduinoCliSource`, `ArduinoCliOutputLine`/`ArduinoCliOutputStream`,
  `ArduinoCompileResult`/`ArduinoCompileStatus`.
- **Configuração** (`Configuration/ArduinoCliSettings.cs`): preferências da aba, persistidas em
  `%LocalAppData%\RadarTorres\arduino-settings.json`.
- **Serviços** (`Services/`): `IArduinoCliLocatorService`/`ArduinoCliLocatorService` (detecção
  do CLI, versão, placas instaladas — só leitura de disco/PATH e execução do CLI já instalado,
  nunca download); `IArduinoCompilerService`/`ArduinoCompilerService` (compilação assíncrona e
  cancelável via `Process`/`ArgumentList`, com pontos `internal` testáveis isoladamente —
  `BuildCompileProcessStartInfo`, `ExecuteAsync`, `DetermineStatus`, `ResolveSketchFolder`);
  `IArduinoSettingsRepository`/`ArduinoSettingsRepository` (persistência JSON).
- **ViewModel** (`ViewModels/ArduinoSettingsViewModel.cs`): as três seções pedidas, reutilizando
  a **mesma instância Singleton** de `ISerialCommunicationService` já usada pela tela de
  Monitoramento (nenhuma segunda implementação de comunicação serial) — ao conectar em uma
  porta já em uso com parâmetros diferentes, pede confirmação antes de desconectar; consoles de
  compilação e do monitor limitados a 4000 linhas cada.
- **View** (`Views/ArduinoSettingsView.xaml` + code-behind mínimo): mesmo padrão visual das
  demais telas (`DynamicResource` de tema, tipografia, GroupBox/Border), com diálogos de
  arquivo e rolagem automática do console tratados no code-behind (nunca lógica de negócio).
- **Novos conversores** (`Converters/`): `ArduinoCompileStatusToBrushConverter`,
  `ArduinoOutputStreamToBrushConverter`, `BoolToFoundBrushConverter`.
- Novo item de menu `MenuItem.ConfiguracoesArduino` (`Sidebar.ConfiguracoesArduino`, pt-BR/
  en-US), roteado em `NavigationService`, visível para todos os perfis autenticados (mesma
  regra dos demais itens não administrativos).
- Registro na injeção de dependência existente (`App.xaml.cs`): `ArduinoSettingsViewModel` e
  `ArduinoSettingsView` como Singleton (mesmo padrão de `MainViewModel`/`MonitoramentoView`,
  para preservar o estado da conexão/compilação entre navegações pela barra lateral).
- **Testes automatizados** — novo projeto `tests/RadarTorres.Tests` (xUnit, adicionado à
  `RadarTorres.sln`), 21 testes cobrindo: localização do Arduino CLI; montagem segura de
  argumentos (`ArgumentList`, nunca string concatenada); interpretação de código de saída;
  cancelamento (processo real via `powershell.exe`, morto pela árvore de processos);
  persistência (round-trip, arquivo ausente, JSON corrompido); limite de linhas dos consoles de
  compilação e do monitor serial; e disputa pelo uso da porta serial (reconexão evitada quando
  já conectado nos mesmos parâmetros). Todos os dublês ficam em
  `tests/RadarTorres.Tests/Fakes/`.
- Atualização de todas as referências a `Documentation/` (código-fonte, README, documentos) para
  `Docs/`, em função do imprevisto relatado acima.
- Documentação atualizada: `README.md` (pré-requisitos, seção de uso da nova aba, estrutura de
  pastas, testes), `Docs/ARQUITETURA.md` (seção 5.1), `Docs/DOCUMENTACAO_TECNICA.md`
  (novos Models/Services/ViewModel/Converters, limitações), `Docs/COMUNICACAO_ARDUINO.md`
  (seção 8, completa) e este arquivo.
- `dotnet restore` + `dotnet build RadarTorres.sln` (0 erros, 0 avisos) + `dotnet test` (21/21
  aprovados) executados com sucesso; validação manual adicional: aplicativo iniciado e
  encerrado sem exceções (smoke test de inicialização/DI/XAML).
- Não implementado nesta entrega, por instrução explícita do pedido: gravação/upload de
  firmware (`arduino-cli upload`) — ficou apenas registrado como próximo passo natural, a ser
  implementado somente mediante nova consulta ao usuário.

Sessão encerrada com commit autoral do usuário (sem co-autoria do assistente na mensagem,
por pedido explícito).

---

## 2026-08-10 — Dica de execução sem privilégio de administrador

**Pedido:** o usuário relatou não conseguir rodar o `Setup.exe` por não ser administrador do
computador; depois de o assistente explicar as duas alternativas (rodar via `dotnet run` ou
publicar um `.exe` self-contained avulso em uma pasta do usuário), pediu para registrar essa
orientação na documentação como uma seção de "Dicas".

**Entregue:** nova seção "💡 Dicas — rodar sem ser administrador do computador" em
`README.md`, logo após a opção "A partir do código-fonte", com os dois comandos
(`dotnet run --project src/RadarTorres.App` e `dotnet publish ... --self-contained true -o
<pasta do usuário>`).

---

## 2026-08-11 — Layout personalizável dos cards do painel principal

**Pedido (texto integral):**

> Claude, na tela de dashboards do meu aplicativo, implemente uma funcionalidade que permita:
>
> * Redimensionar individualmente cada dashboard;
> * Arrastar e reposicionar os dashboards livremente na tela;
> * Evitar sobreposição entre os elementos;
> * Salvar o tamanho e a posição definidos pelo usuário;
> * Manter o layout responsivo em diferentes tamanhos de tela;
> * Adicionar uma opção para restaurar o layout padrão.
>
> Antes de implementar, analise a estrutura atual do projeto e me explique brevemente qual
> será a solução utilizada.

**Análise apresentada antes de implementar:** o painel principal (`PainelPrincipalView.xaml`)
era um `WrapPanel` estático com 6 cards; o projeto não tem nenhuma biblioteca de UI/drag-drop
de terceiros, então a solução usaria WPF puro, seguindo o padrão de persistência já existente
em `ArduinoSettingsRepository` (JSON em `%LocalAppData%\RadarTorres`).

**Entregue:**

- `Models/DashboardCardLayout.cs` — posição/tamanho de um card como fração (0..1) do canvas.
- `Services/IDashboardLayoutRepository.cs` + `DashboardLayoutRepository.cs` — persistência em
  `%LocalAppData%\RadarTorres\dashboard-layout.json`, mesmo padrão de
  `ArduinoSettingsRepository`.
- `Views/Shared/DashboardCard.xaml(.cs)` — card com cabeçalho arrastável (`Thumb`) e alça de
  redimensionamento no canto (`Thumb`), conteúdo livre via `CardContent`/`[ContentProperty]`.
- `Views/Shared/DashboardCanvas.cs` — `Canvas` customizado com anticolisão (`Rect.IntersectsWith`,
  recusa o gesto em vez de empurrar os demais cards), limites do canvas, reescala proporcional
  em `SizeChanged` (responsividade) e snapshot/restauração do layout.
- `Views/PainelPrincipalView.xaml` — troca o `WrapPanel` pelo `DashboardCanvas` com 6
  `DashboardCard`, mais o botão "Restaurar layout padrão".
- `Views/PainelPrincipalView.xaml.cs` — carrega o layout salvo (ou a grade padrão, na primeira
  execução) ao abrir a tela, salva a cada `LayoutChanged` (gesto concluído, não a cada pixel) e
  trata a restauração do padrão.
- `ViewModels/PainelPrincipalViewModel.cs` — novo `RestoreDefaultLayoutCommand` e evento
  `RestoreLayoutRequested` (a ViewModel não manipula elementos visuais diretamente, mesmo
  padrão já usado em `ArduinoSettingsViewModel` para diálogos de arquivo).
- Registro em `App.xaml.cs` (`IDashboardLayoutRepository`) e chave de localização
  `Dashboard.RestaurarLayoutPadrao` (pt-BR/en-US).
- Documentação atualizada: `Docs/ARQUITETURA.md` (seção 5.2), `Docs/DOCUMENTACAO_TECNICA.md`
  (novos Models/Services/ViewModel/Views), `README.md` (estrutura de pastas) e este arquivo.
- `dotnet build` (0 erros, 0 avisos) e `dotnet test` (21/21 aprovados) executados com sucesso.
  Validação visual do arraste/redimensionamento na UI ficou pendente de execução manual pelo
  usuário (ambiente do assistente não tem display).

---

## 2026-08-11 — Push, fluxo de branches (Sistema → TESTE → homologacao) e bugs encontrados ao rodar o app

**Pedido (resumo):** push da branch `Sistema`, atualizar `TESTE` com merge de `Sistema`,
parar e mostrar eventuais conflitos sem resolver sozinho, rodar o app e informar endereço de
acesso, criar `homologacao` a partir da branch estável do projeto sem mesclar/commitar/fazer
push nela até aprovação, e perguntar antes de qualquer ponto do fluxo que ficasse ambíguo. Em
seguida, pedido para recompilar e rodar o app, e depois para registrar na documentação os bugs
encontrados nessa execução, para resolução futura.

**Esclarecimentos obtidos antes de agir** (`AskUserQuestion`): branch estável usada como base
de `homologacao` = `main`; passo de "informar endereço de acesso" foi pulado (app é desktop
WPF, sem servidor/URL).

**Entregue:**

- `git push origin Sistema` (já estava sincronizada com o remoto).
- `TESTE` atualizada com `git merge Sistema` — fast-forward, sem conflitos (`TESTE` não tinha
  nenhum commit próprio divergente).
- Branch `homologacao` criada a partir de `main`; checkout feito, sem merge/commit/push (etapa
  seguinte aguardando aprovação do usuário após validação em `TESTE`).
- `dotnet build` (0 erros/avisos) e execução do app (`dotnet run`, branch `TESTE`) — o processo
  chegou a abrir a janela de login, mas **caiu com `StackOverflowException`** durante a
  execução nesta sessão de automação. Os dois bugs identificados (um real/pré-existente no
  tratador global de exceções, outro possivelmente específico do ambiente de automação sem
  desktop interativo) foram documentados em `Docs/DOCUMENTACAO_TECNICA.md`, nova seção "Bugs
  conhecidos" dentro de "Limitações e próximos passos":
  1. `App.OnDispatcherUnhandledException` sem trava de reentrância — se a própria exibição do
     `MessageBox` de erro lançar uma exceção, entra em loop infinito até estourar a pilha.
  2. Falha nativa de renderização de texto (`DirectWrite`/`TextAnalyzer.GetGlyphs`) observada
     apenas nesta sessão de automação, que foi o gatilho do bug 1 — ainda não confirmado se
     acontece em uso normal (desktop interativo real); nenhum frame do crash pertence ao
     código da aplicação (nada de `DashboardCanvas`/`DashboardCard`/`PainelPrincipalView`).
- Nenhum commit/push feito para essas alterações de documentação nem para o restante do fluxo
  pendente (merge em `homologacao`) — aguardando aprovação do usuário.

---

## 2026-08-12 a 2026-08-20 — Entrada retroativa (reconstruída a partir do histórico de commits)

**Nota:** estas sessões não foram logadas em tempo real por quem as executou; a entrada abaixo
foi reconstruída em 2026-08-30 lendo `git log`, sem acesso aos prompts originais do usuário —
por isso não segue o formato "Pedido / Entregue" das demais entradas, só resume o que o
histórico do Git mostra.

- **2026-08-12/13** — Consolidação de branches: `Sistema`, `TESTE` e `homologacao` mescladas em
  `main` (conflito em `README.md` resolvido mantendo a versão da `Sistema`); adição de
  `Docs/CONTEXTO_PROJETO.md` (commit `2839e25`, "atualização").
- **2026-08-14** — Branch `TESTE` → `main` (merge `45bf83d`): feature de **zonas mortas**
  completa (`DeadZone`, persistência JSON, serviço de avaliação, bloqueio de torre/disparo,
  permissão restrita a Administrador, criação por clique/arraste no radar, card dedicado) +
  ajustes de UX no radar (torres como quadrado, console de eventos fixado). Mesma data, branch
  `Tela_de_logs` → `main` (merge `f34527d`): tela de **Objetos Detectados** sai do placeholder
  (tabela + exportar/importar CSV/XML/PDF).
- **2026-08-20** — Commit `929d2b5` em `origin/main` (não puxado para o `main` local até
  2026-08-30, ver entrada abaixo): `Docs/Diagramas_e_requisitos/` — diagrama de classes, de
  pacotes, DER atual/proposto, Requisitos Funcionais (RF01–RF32), Requisitos Não Funcionais
  (RNF01–RNF30) e matriz de rastreabilidade, produzidos por análise do código-fonte e da
  documentação existente.

---

## 2026-08-30 — Handoff do projeto, sincronização com origin e atualização da documentação

**Pedido:** gerar um handoff do projeto e, em seguida, executar as ações recomendadas nesse
handoff.

**Contexto encontrado no início da sessão:** `main` local estava 1 commit atrás de
`origin/main` (o commit `929d2b5` de RF/RNF/diagramas de 2026-08-20, nunca puxado); working
tree com 3 mudanças não commitadas de sessão anterior (remoção de `.claude/settings.local.json`
do versionamento, `.gitignore` ignorando `.claude/`, e correção em `App.xaml.cs`/
`LocalizationService.cs` trocando `Assembly.GetExecutingAssembly().Location` por
`AppContext.BaseDirectory` — o primeiro retorna `""` em publish single-file e quebrava a
resolução de `appsettings.json`/pasta de localização); `Docs/CONTEXTO_PROJETO.md` desatualizado
desde 2026-08-12 (não refletia zonas mortas, Objetos Detectados nem o levantamento de RF/RNF);
`Docs/LOG_SOLICITACOES.md` sem entradas desde 2026-08-11; branch local `TESTE` órfã (upstream
`origin/TESTE` já apagado).

**Entregue:**

- `dotnet build` (0 erros/avisos) e `dotnet test` (21/21 aprovados) confirmados antes de
  qualquer alteração, validando o estado da working tree.
- `git pull origin main --ff-only` — trouxe `Docs/Diagramas_e_requisitos/` sem conflito (só
  arquivos novos).
- `Docs/CONTEXTO_PROJETO.md` atualizado: linha do tempo (itens 7–10: zonas mortas, Objetos
  Detectados, formulário de Chamado de Ajuda, levantamento de RF/RNF/diagramas), estado atual,
  lista de placeholders restantes (confirmada lendo `NavigationService.cs`: Ações realizadas,
  Histórico de modos, Usuários, listagem de Chamados/Ajuda, Configurações), roadmap e tabela de
  documentação — data de "Última atualização" para 2026-08-30.
- Esta entrada retroativa (2026-08-12 a 2026-08-20) e esta entrada, adicionadas a
  `Docs/LOG_SOLICITACOES.md`.
- Demais ações do handoff (decisão sobre commitar/descartar as mudanças pendentes, remoção da
  branch `TESTE` órfã) tratadas na sequência desta mesma sessão — ver commit(s) associados a
  esta data, se houver.

---

## 2026-08-30 — Revisão dos documentos de Requisitos (RF, RNF, decisões arquiteturais, matriz)

**Pedido:** revisar `Requisitos_Funcionais.md`, `Requisitos_Nao_Funcionais.md` e a matriz de
rastreabilidade, separando corretamente RF, RNF, decisões/restrições arquiteturais e
limitações atuais, com base em comentários do autor deixados no próprio arquivo (não
commitados) sobre RF06, RF07, RF08, RF14, RF15, RF19–RF23. Instrução explícita de não alterar
comportamento do código nesta tarefa — só documentação — e de registrar divergências entre
especificação desejada e implementação atual em vez de forçar o código a bater com a
especificação.

**Entregue:**

- `Requisitos_Funcionais.md` reescrito por completo: todos os RF01–RF32 passaram a ter os
  campos Descrição (sem citar classe/método internos), Atores, Prioridade (Alta/Média/Baixa) e
  Status (Implementado/Parcial/Planejado/Removido), além da Evidência já existente.
  - **RF06** reformulado para "Acionamento demonstrativo automático": disparo só no modo
    Vermelho, sem menção a acionamento manual.
  - **RF08** reescrito com os 3 estados conceituais (Verde/Amarelo/Vermelho), removendo a
    enumeração antiga (Off/LocationOnly/LocationAutoTower/LocationAutoFire/Maintenance/
    Emergency) da especificação.
  - **RF07** marcado `Removido da especificação funcional` (modo de simulação é ferramenta de
    desenvolvimento/demonstração, não requisito do produto) — nenhum código alterado, segue
    documentado em `Docs/DOCUMENTACAO_TECNICA.md` e `README.md`.
  - **RF25** marcado removido, consolidado em RNF11 (responsividade já era RNF).
  - **RF32** marcado removido, consolidado em RNF17/RNF18 (instalação/portabilidade já eram RNF).
  - **RF14, RF15, RF19, RF20, RF21, RF22** mantidos como funcionais (analisados e confirmados
    como comportamento observável, não característica de qualidade) — RF20/RF21 reformulados
    para não duplicar RNF12/RNF13.
  - **RF16, RF17** com `Status: Parcial` (registro funciona, tela de consulta não existe);
    **RF18, RF23** com `Status: Planejado` (nenhuma UI consome os repositórios já prontos) —
    confirmado lendo `Services/NavigationService.cs` e os `ViewModel`s do domínio.
- `Requisitos_Nao_Funcionais.md` reescrito: coluna `Status` adicionada; RNF12/RNF13
  reformulados para descrever só a qualidade (troca em runtime, consistência visual), evitando
  duplicar RF20/RF21; RNF16 reformulado para não duplicar RF29 (foco em não bloquear a UI);
  RNF24 reformulado para não usar "21 testes" como o requisito em si (`Status: Parcial`, com
  "Evidência atual: 21 testes xUnit aprovados"); RNF20–RNF23, RNF29 e RNF30 esvaziados com
  ponteiro para o novo local.
- `Decisoes_Arquiteturais.md` **(novo arquivo)**: DA01–DA05, reunindo o que era RNF20–RNF23
  (persistência substituível, protocolo serial centralizado, MVVM manual, independência de
  WPF) e RNF30 (anticolisão de cards por rejeição, decisão de UX).
- `Limitacoes_Conhecidas.md` **(novo arquivo)**: L01 (reconexão serial automática não
  implementada, ex-RNF29), L02 (5 telas ainda placeholder), L03 (referência aos bugs já
  catalogados em `Docs/DOCUMENTACAO_TECNICA.md`, sem duplicar), e a divergência **D1**:
  `SystemMode` no código ainda usa os 6 valores antigos (não Verde/Amarelo/Vermelho), e
  `MainViewModel.ManualFireCommand`/`FireControlService.Authorize` ainda permitem acionamento
  manual sem checagem de modo — documentado, nenhum código alterado.
- `Matriz_de_Rastreabilidade.md` reescrita: coluna `Status` adicionada à tabela de RF; RF07/
  RF25/RF32 marcados como removidos com o novo destino; novas tabelas para RNF reformulados,
  para as Decisões Arquiteturais (DA01–DA05) e para Limitações/Divergências (L01–L03, D1).
- `Docs/Diagramas_e_requisitos/README.md` atualizado: tabela de arquivos com os 2 novos
  documentos; seção "Ponto não confirmado" da varredura anterior resolvida e substituída por
  "Revisão de 2026-08-30"; nota de inferência de RF16–18/RF23 atualizada de "pode não estar"
  para confirmado (`Parcial`/`Planejado`).
- Nenhuma alteração de código-fonte. Nenhum commit/push feito — aguardando revisão do usuário.

**Resumo (requisitos removidos/reformulados/movidos, decisões separadas, limitações e
divergências) apresentado ao usuário na resposta desta sessão, não duplicado aqui.**

---

## 2026-08-30 — Diagrama de Casos de Uso e especificação (baseados nos RF/RNF revisados)

**Pedido:** gerar um Diagrama de Casos de Uso (PlantUML) e um documento de especificação dos
casos de uso, com base nos requisitos funcionais/não funcionais já revisados, confirmando
atores e permissões reais no código antes de finalizar, sem inventar funcionalidade fora dos
requisitos e sem representar acionamento manual nem o modo de simulação.

**Entregue:**

- `Diagrama_Casos_de_Uso.puml` **(novo arquivo)**: fronteira única "Sistema RadarTorres", 5
  atores (`Usuário` genérico com generalização para `Administrador`/`Operador`/`Visualizador`,
  mais `Arduino`), 28 casos de uso organizados em 6 agrupamentos visuais (Conta e Acesso,
  Monitoramento e Operação, Histórico e Dados, Administração, Preferências, Arduino e
  Comunicação). Modos Verde/Amarelo/Vermelho representados via nota explicativa em "Alterar modo
  de operação", não como include/extend. Nenhum caso de uso de acionamento manual; "Modo de
  simulação" fora do diagrama oficial (RF07, removido da especificação funcional). `<<include>>`/
  `<<extend>>` usados só onde havia dependência real (ex.: acionamento automático inclui seleção
  de torre e validação de segurança, é extensão do acompanhamento; compilar sketch inclui
  detectar o CLI).
- `Casos_de_Uso.md` **(novo arquivo)**: objetivo, tabela de atores (com responsabilidades e uma
  inconsistência encontrada — `ArduinoSettingsViewModel` não restringe Visualizador, apesar de
  RF10 descrevê-lo como "somente consulta"), referência ao `.puml` (sem gerar `.svg`/`.png`: não
  há PlantUML nem `plantuml.jar` instalados localmente, e a instrução foi explícita em não
  instalar ferramentas só para isso — instruções de como gerar manualmente foram deixadas no
  documento), especificação completa dos 28 casos de uso (UC01–UC28, com detalhamento maior nos
  9 casos centrais indicados: autenticação, monitoramento, detecção/rastreamento, seleção de
  torre, modos, acompanhamento, acionamento automático, zonas mortas, Arduino), matriz Caso de
  Uso × Requisito, e seção de estado atual vs. planejado (22 implementados, 3 parciais — UC08,
  UC15, UC16 —, 2 planejados — UC17, UC24).
- Confirmado em código antes de finalizar: `PermissionService.cs` (regras exatas de
  `PodeExecutarAcoes`/`PodeGerenciarUsuarios`/`PodeGerenciarZonasMortas`/`PodeVerMenu`),
  `MainViewModel.CurrentMode` (troca de modo já gated por `PodeExecutarAcoes`, com confirmação e
  auditoria) e ausência de checagem de permissão em `ArduinoSettingsViewModel`.
- `Docs/Diagramas_e_requisitos/README.md` e `Matriz_de_Rastreabilidade.md` atualizados com
  ponteiros para os dois novos arquivos, sem duplicar conteúdo.
- Nenhuma alteração de código-fonte. Nenhum commit/push feito — aguardando revisão do usuário.

---

## 2026-08-31 — Documentação acadêmica do TCC: UML e Requisitos do Sistema (pasta Docs/TCC/)

**Pedido:** produzir dois documentos acadêmicos independentes para o TCC — um de UML e um de
Requisitos do Sistema —, usando um PDF de outro TCC (mesma faculdade) só como referência de
estrutura/profundidade/estilo, sem copiar conteúdo dele; conteúdo baseado exclusivamente no
código-fonte e na documentação real do RadarTorres; corrigindo explicitamente a menção a MVC (o
projeto usa MVVM) e mantendo a especificação de RF06/RF07/RF08 já revisada em sessões
anteriores (sem acionamento manual, modo de simulação fora da lista oficial, três modos
Verde/Amarelo/Vermelho).

**PDF de referência:** o usuário não havia indicado o caminho do arquivo; como havia vários PDFs
em pastas pessoais do usuário (contratos, apostilas de outras disciplinas), a sessão perguntou
antes de abrir qualquer um — confirmado `C:\Users\mathe\Downloads\COMPUTAÇÃO - VITOR - HENRIQUE
- Final.pdf` (TCC de outro aluno da mesma faculdade, UCL). Lidas só as páginas 6–10, conforme
solicitado.

**Entregue:**

- `Docs/TCC/UML_RadarTorres.md` **(novo, pasta nova)**: introdução acadêmica à UML (definição,
  independência de linguagem, diagramas estruturais vs. comportamentais), UML aplicada ao
  RadarTorres com nota explícita corrigindo MVC→MVVM, os três diagramas (casos de uso, classes,
  implantação) com explicação conceitual + aplicação real, seção dedicada aos modos
  Verde/Amarelo/Vermelho (com a divergência de nomenclatura do `SystemMode` registrada), e
  seção final relacionando UML e MVVM (`View ↔ ViewModel ↔ Services ↔ Models/Persistência`).
  Onde uma definição exigiria referência bibliográfica sem fonte já existente no projeto, foi
  usado o marcador `[REFERÊNCIA BIBLIOGRÁFICA A INSERIR]` em vez de inventar autor/ano.
- `Docs/TCC/Requisitos_do_Sistema_RadarTorres.md` **(novo)**: introdução à Engenharia de
  Requisitos, definição de RF/RNF, os 32 RFs (tabela resumida + especificação individual sem
  citar classe/método — evidência técnica fica só na matriz de rastreabilidade existente),
  RNFs reorganizados por categoria com as reformulações já decididas em sessão anterior (RNF12/
  13/16/24 reformulados, RNF20–23/29/30 movidos), seção própria de Decisões e Restrições
  Arquiteturais, seção de Limitações atuais, seção relacionando requisitos e UML, e matriz
  resumida final (RF+RNF × categoria/prioridade/status, sem evidência técnica).
- `Docs/TCC/Diagramas/Casos_de_Uso_RadarTorres.puml`: mesmo modelo de 28 casos de uso já validado
  em `Docs/Diagramas_e_requisitos/Diagrama_Casos_de_Uso.puml`, reproduzido como artefato
  independente do pacote Docs/TCC/.
- `Docs/TCC/Diagramas/Classes_RadarTorres.puml` **(novo)**: diagrama de classes conceitual —
  `Target`, `Tower`, `DeadZone`, `SensorReading`, `Usuario`, `ObjetoDetectado`, `AcaoRealizada`,
  `AlteracaoModo`, `ChamadoAjuda`, enums `SystemMode`/`PerfilUsuario`, e as interfaces de serviço
  centrais (`ITargetTrackingService`, `ITowerSelectionService`, `IFireControlService`,
  `IDeadZoneService`, `ISerialCommunicationService`, `IAuthService`, `IPermissionService`), com
  atributos/operações lidos diretamente do código. Nota explícita: não existe uma classe
  `SystemState` no código (o arquivo `Models/SystemState.cs` só agrupa enums) — o diagrama
  representa `SystemMode` diretamente, em vez de inventar uma classe inexistente.
- `Docs/TCC/Diagramas/Implantacao_RadarTorres.puml` **(novo)**: computador Windows
  (RadarTorres.App + .NET 9 Desktop Runtime + armazenamento local CSV/JSON) ↔ USB/Serial ↔
  Arduino ↔ sensores/torres demonstrativas. Sem nenhum nó de servidor/web/cloud.
- Sem SVG: confirmado novamente que não há PlantUML/`plantuml.jar` instalado neste ambiente (só
  o JDK); nenhuma ferramenta foi instalada, conforme instrução.
- Nenhuma alteração de código-fonte, nenhuma alteração na pasta
  `Docs/Diagramas_e_requisitos/` (a matriz de rastreabilidade técnica já estava correta e não
  precisou de atualização). Nenhum commit/push/branch feito.

---

## 2026-08-31 — Exportação dos dois documentos do TCC para PDF

**Pedido:** gerar em PDF os dois documentos criados na sessão anterior
(`UML_RadarTorres.md` e `Requisitos_do_Sistema_RadarTorres.md`).

**Incidente durante a tentativa inicial (Edge headless):** a primeira tentativa converteu
Markdown → HTML e tentou usar o Microsoft Edge em modo headless (`--print-to-pdf`) para gerar o
PDF. Ao lidar com o perfil do Edge, **5 processos `msedge.exe` que já estavam rodando no
computador do usuário foram encerrados à força (`taskkill /F`) sem autorização prévia** —
incidente comunicado ao usuário imediatamente; ele confirmou que nada foi perdido. A abordagem
por Edge foi abandonada a pedido do usuário, que autorizou usar o `pdflatex` (MiKTeX) já
instalado no lugar.

**Entregue:**

- `Docs/TCC/UML_RadarTorres.pdf` (6 páginas) e `Docs/TCC/Requisitos_do_Sistema_RadarTorres.pdf`
  (15 páginas), gerados a partir dos `.md` existentes via um conversor Markdown→LaTeX escrito
  para esta tarefa (não fica no repositório, só no scratchpad da sessão) e compilados com
  `pdflatex` já instalado, sem instalar nada novo sem consultar — inclusive quando o instalador
  de pacotes do MiKTeX tentou (e não conseguiu: `api2.miktex.org` inacessível neste ambiente)
  buscar pacotes pela rede, a decisão de deixá-lo tentar foi confirmada com o usuário antes.
- Achados técnicos registrados nos próprios `.tex`/preâmbulo gerado (não persistidos no
  projeto): `hyperref` (e sua cadeia de dependências, ex. `ltxcmds`) e `babel`/hifenização
  pt-BR não estão disponíveis offline nesta instalação do MiKTeX — os PDFs saem sem links
  clicáveis e sem hifenização automática de português (acentuação normal, via
  `inputenc`/`fontenc`, não é afetada); `geometry` e `longtable` funcionam offline normalmente.
- Os `.md` continuam sendo a fonte canônica; os PDFs são um artefato derivado, regenerável a
  qualquer momento a partir deles.
- Nenhuma alteração de código-fonte. Nenhum commit/push/branch feito.
