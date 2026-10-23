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
  instalação de .NET no computador do usuário final (ver `Docs/Projeto/INSTALADOR.md`,
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
  `Docs/Projeto/INSTALADOR.md`, seção 7, para a lista completa de testes e o único ponto
  parcial — desinstalação silenciosa travando no ambiente de automação usado, não no script).
- Documentação de uso no `README.md` (seção "Instalação (Windows)") e detalhamento técnico
  em `Docs/Projeto/INSTALADOR.md`.

---

## 2026-08-07 — Documentação e commit das alterações

**Pedido (texto integral):**

> Faça um resumo de suas alterações, Documente na pasta de documentação, adicione na pasta
> de documentação os pedidos que faço em cada prompt como logs, e commite para mim na branch
> sistemas mesmo. pv

**Entregue:**

- `Docs/Projeto/INSTALADOR.md` — documentação técnica do processo de instalação criado na
  solicitação anterior.
- `Docs/Projeto/LOG_SOLICITACOES.md` — este arquivo, com o histórico de pedidos.
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
- Documentação: `Docs/Tecnica/MODELO_DADOS.md` (schema + relacionamentos + plano de migração
  SQL) e `Docs/Projeto/ETAPA1_FUNDACAO.md` (arquitetura antes/depois, arquivos alterados,
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
  pastas, testes), `Docs/Tecnica/ARQUITETURA.md` (seção 5.1), `Docs/Tecnica/DOCUMENTACAO_TECNICA.md`
  (novos Models/Services/ViewModel/Converters, limitações), `Docs/Tecnica/COMUNICACAO_ARDUINO.md`
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
- Documentação atualizada: `Docs/Tecnica/ARQUITETURA.md` (seção 5.2), `Docs/Tecnica/DOCUMENTACAO_TECNICA.md`
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
  desktop interativo) foram documentados em `Docs/Tecnica/DOCUMENTACAO_TECNICA.md`, nova seção "Bugs
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
  `Docs/Projeto/CONTEXTO_PROJETO.md` (commit `2839e25`, "atualização").
- **2026-08-14** — Branch `TESTE` → `main` (merge `45bf83d`): feature de **zonas mortas**
  completa (`DeadZone`, persistência JSON, serviço de avaliação, bloqueio de torre/disparo,
  permissão restrita a Administrador, criação por clique/arraste no radar, card dedicado) +
  ajustes de UX no radar (torres como quadrado, console de eventos fixado). Mesma data, branch
  `Tela_de_logs` → `main` (merge `f34527d`): tela de **Objetos Detectados** sai do placeholder
  (tabela + exportar/importar CSV/XML/PDF).
- **2026-08-20** — Commit `929d2b5` em `origin/main` (não puxado para o `main` local até
  2026-08-30, ver entrada abaixo): `Docs/Documentos_Entregaveis/Diagramas_e_requisitos/` — diagrama de classes, de
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
resolução de `appsettings.json`/pasta de localização); `Docs/Projeto/CONTEXTO_PROJETO.md` desatualizado
desde 2026-08-12 (não refletia zonas mortas, Objetos Detectados nem o levantamento de RF/RNF);
`Docs/Projeto/LOG_SOLICITACOES.md` sem entradas desde 2026-08-11; branch local `TESTE` órfã (upstream
`origin/TESTE` já apagado).

**Entregue:**

- `dotnet build` (0 erros/avisos) e `dotnet test` (21/21 aprovados) confirmados antes de
  qualquer alteração, validando o estado da working tree.
- `git pull origin main --ff-only` — trouxe `Docs/Documentos_Entregaveis/Diagramas_e_requisitos/` sem conflito (só
  arquivos novos).
- `Docs/Projeto/CONTEXTO_PROJETO.md` atualizado: linha do tempo (itens 7–10: zonas mortas, Objetos
  Detectados, formulário de Chamado de Ajuda, levantamento de RF/RNF/diagramas), estado atual,
  lista de placeholders restantes (confirmada lendo `NavigationService.cs`: Ações realizadas,
  Histórico de modos, Usuários, listagem de Chamados/Ajuda, Configurações), roadmap e tabela de
  documentação — data de "Última atualização" para 2026-08-30.
- Esta entrada retroativa (2026-08-12 a 2026-08-20) e esta entrada, adicionadas a
  `Docs/Projeto/LOG_SOLICITACOES.md`.
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
    documentado em `Docs/Tecnica/DOCUMENTACAO_TECNICA.md` e `README.md`.
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
  catalogados em `Docs/Tecnica/DOCUMENTACAO_TECNICA.md`, sem duplicar), e a divergência **D1**:
  `SystemMode` no código ainda usa os 6 valores antigos (não Verde/Amarelo/Vermelho), e
  `MainViewModel.ManualFireCommand`/`FireControlService.Authorize` ainda permitem acionamento
  manual sem checagem de modo — documentado, nenhum código alterado.
- `Matriz_de_Rastreabilidade.md` reescrita: coluna `Status` adicionada à tabela de RF; RF07/
  RF25/RF32 marcados como removidos com o novo destino; novas tabelas para RNF reformulados,
  para as Decisões Arquiteturais (DA01–DA05) e para Limitações/Divergências (L01–L03, D1).
- `Docs/Documentos_Entregaveis/Diagramas_e_requisitos/README.md` atualizado: tabela de arquivos com os 2 novos
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
- `Docs/Documentos_Entregaveis/Diagramas_e_requisitos/README.md` e `Matriz_de_Rastreabilidade.md` atualizados com
  ponteiros para os dois novos arquivos, sem duplicar conteúdo.
- Nenhuma alteração de código-fonte. Nenhum commit/push feito — aguardando revisão do usuário.

---

## 2026-08-31 — Documentação acadêmica do TCC: UML e Requisitos do Sistema (pasta Docs/Documentos_Entregaveis/)

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

- `Docs/Documentos_Entregaveis/UML_RadarTorres.md` **(novo, pasta nova)**: introdução acadêmica à UML (definição,
  independência de linguagem, diagramas estruturais vs. comportamentais), UML aplicada ao
  RadarTorres com nota explícita corrigindo MVC→MVVM, os três diagramas (casos de uso, classes,
  implantação) com explicação conceitual + aplicação real, seção dedicada aos modos
  Verde/Amarelo/Vermelho (com a divergência de nomenclatura do `SystemMode` registrada), e
  seção final relacionando UML e MVVM (`View ↔ ViewModel ↔ Services ↔ Models/Persistência`).
  Onde uma definição exigiria referência bibliográfica sem fonte já existente no projeto, foi
  usado o marcador `[REFERÊNCIA BIBLIOGRÁFICA A INSERIR]` em vez de inventar autor/ano.
- `Docs/Documentos_Entregaveis/Requisitos_do_Sistema_RadarTorres.md` **(novo)**: introdução à Engenharia de
  Requisitos, definição de RF/RNF, os 32 RFs (tabela resumida + especificação individual sem
  citar classe/método — evidência técnica fica só na matriz de rastreabilidade existente),
  RNFs reorganizados por categoria com as reformulações já decididas em sessão anterior (RNF12/
  13/16/24 reformulados, RNF20–23/29/30 movidos), seção própria de Decisões e Restrições
  Arquiteturais, seção de Limitações atuais, seção relacionando requisitos e UML, e matriz
  resumida final (RF+RNF × categoria/prioridade/status, sem evidência técnica).
- `Docs/Documentos_Entregaveis/Diagramas/Casos_de_Uso_RadarTorres.puml`: mesmo modelo de 28 casos de uso já validado
  em `Docs/Documentos_Entregaveis/Diagramas_e_requisitos/Diagrama_Casos_de_Uso.puml`, reproduzido como artefato
  independente do pacote Docs/Documentos_Entregaveis/.
- `Docs/Documentos_Entregaveis/Diagramas/Classes_RadarTorres.puml` **(novo)**: diagrama de classes conceitual —
  `Target`, `Tower`, `DeadZone`, `SensorReading`, `Usuario`, `ObjetoDetectado`, `AcaoRealizada`,
  `AlteracaoModo`, `ChamadoAjuda`, enums `SystemMode`/`PerfilUsuario`, e as interfaces de serviço
  centrais (`ITargetTrackingService`, `ITowerSelectionService`, `IFireControlService`,
  `IDeadZoneService`, `ISerialCommunicationService`, `IAuthService`, `IPermissionService`), com
  atributos/operações lidos diretamente do código. Nota explícita: não existe uma classe
  `SystemState` no código (o arquivo `Models/SystemState.cs` só agrupa enums) — o diagrama
  representa `SystemMode` diretamente, em vez de inventar uma classe inexistente.
- `Docs/Documentos_Entregaveis/Diagramas/Implantacao_RadarTorres.puml` **(novo)**: computador Windows
  (RadarTorres.App + .NET 9 Desktop Runtime + armazenamento local CSV/JSON) ↔ USB/Serial ↔
  Arduino ↔ sensores/torres demonstrativas. Sem nenhum nó de servidor/web/cloud.
- Sem SVG: confirmado novamente que não há PlantUML/`plantuml.jar` instalado neste ambiente (só
  o JDK); nenhuma ferramenta foi instalada, conforme instrução.
- Nenhuma alteração de código-fonte, nenhuma alteração na pasta
  `Docs/Documentos_Entregaveis/Diagramas_e_requisitos/` (a matriz de rastreabilidade técnica já estava correta e não
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

- `Docs/Documentos_Entregaveis/UML_RadarTorres.pdf` (6 páginas) e `Docs/Documentos_Entregaveis/Requisitos_do_Sistema_RadarTorres.pdf`
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

---

## 2026-09-11 — Site oficial do projeto (documentação/TCC/portfólio) via GitHub Pages

**Pedido:** criar um site oficial hospedado no GitHub Pages, funcionando como documentação
técnica, apresentação do TCC, portfólio e central dos documentos existentes — stack preferida
React + TypeScript + Tailwind CSS + Vite, com 9 páginas (Home, Projeto, Arquitetura,
Tecnologias, Visão Computacional, Documentação, Testes, Galeria, Sobre), demonstração
interativa do radar, deploy automático via GitHub Actions, sem duplicar documentos existentes.

**Antes de codar:** repositório analisado (README, `Docs/Projeto/CONTEXTO_PROJETO.md` como
"handoff", `Docs/Tecnica/*`, `Docs/Documentos_Entregaveis/*`); branch de trabalho atual
(`tratamento-de-imagens`) identificada como WIP local não relacionado e não pushada — nova
branch `feature/site-documentacao` criada a partir de `main` para este trabalho. Plano
apresentado e aprovado antes da implementação (1 pergunta de esclarecimento sobre estratégia
de roteamento: HashRouter escolhido).

**Entregue:**

- `site/` — projeto Vite + React 19 + TypeScript + Tailwind CSS v4, `base` configurado para
  `/Sistema_Rastreamento_Alvos_Arduino/`. Pasta `docs/` evitada de propósito: colidiria
  case-insensitive com `Docs/` já existente no Windows.
- 9 páginas em `site/src/pages/`, componentes reutilizáveis (`TechCard`, `Timeline`,
  `Diagram`, `DocumentationCard`, `RadarSimulation`) em `site/src/components/`, dados
  estruturados (tecnologias, documentação, testes, timeline) em `site/src/data/`.
- `RadarSimulation` (Canvas HTML, `site/src/components/radar/`): reproduz em TypeScript a
  lógica real de `CoordinateConverter`/`QuadrantHelper`/`TowerSelectionService`
  (`radarMath.ts`) — arrastar o alvo ou usar os sliders recalcula quadrante, distância e
  torre selecionada ao vivo, não é decorativo.
- Página Documentação linka os arquivos reais do repositório via URL do GitHub (nenhum
  documento duplicado dentro do site).
- Roteamento com `HashRouter` (react-router-dom) — zero configuração extra necessária no
  GitHub Pages.
- `.github/workflows/deploy.yml`: build (`npm ci && npm run build` em `site/`) e publicação
  via `actions/upload-pages-artifact` + `actions/deploy-pages` a cada push em `main` que
  toque `site/**`; `site/dist` nunca é commitado.
- Build (`npm run build`) e lint (`npm run lint`, oxlint) validados sem erros/warnings;
  smoke test do `npm run preview` confirmado (HTTP 200 no path base correto).
- **Pendência que depende do usuário:** em Settings → Pages do repositório no GitHub, mudar
  "Source" para "GitHub Actions" — não é possível fazer isso via Git.
- Commits locais feitos na branch `feature/site-documentacao`; **push não realizado** sem
  autorização explícita na sessão.

## 2026-10-12 — Planejamento do módulo de visão computacional + tela "Perfis de Alvo"

**Pedido (em duas partes, mesma sessão):** (1) verificação somente-leitura do repositório
(branches locais vs. GitHub) e plano de arquitetura para o módulo de detecção por câmera
(YOLO) com envio de posição/distância ao Arduino — sem código, só documento de projeto; (2)
depois de aprovado, uma tela nova para cadastrar o que a detecção deve "travar": cor (apenas
as primárias), formato (quadrado/redondo/retangular) e tamanho, mais uma opção de tirar foto
do objeto para tratar depois.

**Antes de codar (parte 2):** 4 perguntas de esclarecimento feitas e respondidas: cores =
RGB; tamanho = numérico em cm; vários perfis salvos, um ativo por vez; implementar agora (não
só documentar). Módulo "Câmeras" (visualização) e modelo `tiny-yolov3-11.onnx` já existentes
na branch `tratamento-de-imagens` foram inspecionados antes de desenhar a integração.

**Entregue:**

- `Docs/Projeto/ETAPA2_VISAO_COMPUTACIONAL.md`: documento de planejamento (arquitetura
  recomendada — C# in-process com ONNX Runtime em vez de serviço Python separado —, fluxo de
  dados, extensão aditiva do protocolo serial com `DISTANCE`/`AIM`/`AIMED`, interfaces
  propostas e perguntas abertas). Nenhum código deste documento foi implementado ainda.
- Tela "Perfis de Alvo" (`MenuItem.PerfilAlvo`, visível a todos os perfis exceto
  Visualizador — `IPermissionService.PodeGerenciarPerfisAlvo`): `Models/CorAlvo.cs`,
  `Models/FormatoAlvo.cs`, `Models/PerfilAlvo.cs`; `Services/IPerfilAlvoRepository.cs` +
  `PerfilAlvoRepository.cs` (JSON em `%LocalAppData%\RadarTorres\perfis-alvo.json`, mesmo
  padrão de `ZonaMortaRepository`); `Services/IPerfilAlvoService.cs` + `PerfilAlvoService.cs`
  (só um perfil ativo por vez, sem edição de campos após criado — igual Zonas Mortas);
  `ViewModels/PerfilAlvoViewModel.cs` + `Views/PerfilAlvoView.xaml(.cs)`.
- Captura de "foto de referência": reaproveita `ICameraCaptureServiceFactory`/
  `ICameraDeviceEnumerator` já existentes do módulo "Câmeras" (sessão de captura própria,
  independente das já abertas em outras telas); salva PNG em
  `%LocalAppData%\RadarTorres\PerfisAlvo\Fotos\` e grava o caminho no perfil. Não participa da
  detecção automática — é só para consulta manual posterior, como pedido.
- Chaves de localização pt-BR/en-US adicionadas (`Sidebar.PerfilAlvo`, `PerfilAlvo.*`).
- `dotnet build`: 0 erros (avisos restantes são `NU1701` pré-existentes do pacote AForge, não
  relacionados a esta mudança). Sem `dotnet test` — não há projeto de testes no repositório.
- **Suposição assumida e não confirmada com o usuário:** quem pode gerenciar perfis de alvo é
  Operador+Administrador (mesmo critério de `PodeExecutarAcoes`), não restrito só a
  Administrador como Zonas Mortas/Usuários — sinalizar se precisar ser mais restritivo.
- **Pendência:** validação visual/manual da tela (arraste de RadioButtons, captura de foto
  real com webcam) não foi feita — ambiente sem display interativo nesta sessão.

## 2026-10-13 — Preview ao vivo na foto de referência + detecção por câmera (YOLO) em Monitoramento

**Pedido (em duas partes):** (1) na tela "Perfis de Alvo", a câmera escolhida para a foto de
referência deveria ativar a visualização imediatamente, para centralizar o objeto antes de
capturar (antes só tirava a foto direto, sem mostrar o quadro ao vivo); (2) na tela
Monitoramento, abrir uma câmera e, se o modo de operação estiver Amarelo ou Vermelho e um
objeto ativo for identificado, disparar um alarme sonoro e informar a distância do alvo.

**Antes de codar (parte 2):** fiz perguntas de esclarecimento, porque a identificação "real"
por câmera depende do pipeline de visão
computacional inteiro (YOLO), que só tinha sido planejado (`ETAPA2_VISAO_COMPUTACIONAL.md`),
nunca implementado. Respostas do usuário: detecção deve filtrar pelo Perfil de Alvo ativo
(cor/formato/tamanho, não classes genéricas do YOLO); distância anunciada em texto E por voz;
distância estimada pela imagem (tamanho da caixa × campo de visão da câmera), não pelo
Arduino. O contrato real do modelo `tiny-yolov3-11.onnx` (entradas/saídas, shapes) foi
confirmado carregando o arquivo com `Microsoft.ML.OnnxRuntime` num projeto console descartável
antes de escrever o serviço — não foi suposição.

**Entregue:**

- Preview ao vivo em "Perfis de Alvo": `PerfilAlvoViewModel` agora liga a câmera assim que
  selecionada (antes só ao clicar "Tirar foto"); "Tirar foto" passou a salvar o quadro atual
  da pré-visualização em vez de iniciar uma captura nova. Bug corrigido no caminho: ao
  voltar à tela depois de navegar para outra, o preview não religava (mesma referência de
  câmera não disparava o `set`) — corrigido em `OnNavigatedTo`.
- Novo card "Detecção por Câmera" em Monitoramento (`DeteccaoCameraViewModel`, exposto por
  `MainViewModel.DeteccaoCamera`): seleção de câmera + preview ao vivo + status + distância.
- Pipeline de visão computacional novo (primeira implementação real da Etapa 2, bem reduzida
  em escopo — só o necessário para este alerta, não o fluxo completo de mira pan/tilt do
  documento de planejamento): `Models/VisionDetection.cs`, `Models/AlvoVisualResultado.cs`,
  `Services/IObjectDetectionService.cs` + `OnnxYoloDetectionService.cs` (inferência ONNX sobre
  `tiny-yolov3-11.onnx`, ignora a classe COCO — só usa as caixas como região candidata),
  `Services/IAlvoVisualService.cs` + `AlvoVisualService.cs` (cor por canal dominante, formato
  por proporção da caixa + preenchimento, distância por campo de visão), `Services/
  IAlertaSonoroService.cs` + `AlertaSonoroService.cs` (`Console.Beep` + `System.Speech`/SAPI).
- Novos pacotes: `Microsoft.ML.OnnxRuntime` 1.20.1, `System.Speech` 9.0.0.
- `Configuration/AppSettings.cs` + `appsettings.json`: seção nova `VisionSettings` com todos
  os limiares (confiança mínima, intervalo de processamento, campo de visão da câmera,
  limiares de formato/cor, debounce do alerta) — nenhum valor calibrado com hardware real.
- `dotnet build` (Debug e Release): 0 erros (os 9 avisos restantes são `NU1701` pré-existentes
  do AForge). App testado abrindo (processo estável, sem crash na inicialização).
- **Limitação explícita, sem suposição de que funciona de ponta a ponta:** não há câmera nem
  monitor neste ambiente para validar visualmente detecção real, cor/formato classificados
  corretamente, precisão da distância estimada, ou se alguma voz pt-BR está instalada para o
  TTS funcionar. Os limiares em `VisionSettings` são valores de partida (comentados no próprio
  `AppSettings.cs`) e quase certamente precisarão de calibração depois de testar com a câmera
  e o objeto físico reais — pedir para o usuário testar e reportar o comportamento antes de
  considerar este card "pronto", só "implementado".

## 2026-10-13 (continuação) — Correções reportadas em Monitoramento (binding, crash fatal, sobreposição de cards)

**Pedidos, nesta ordem, reportando problemas reais ao testar:** (1) scroll em "Perfis de
Alvo" cortava conteúdo; (2) cards sobrepostos em Monitoramento + pedido para fixar o arranjo
como padrão do sistema; (3) erro de binding ao abrir Monitoramento; (4) aplicativo fechando
sozinho ao abrir Monitoramento.

**Diagnosticado e corrigido:**

- **Scroll cortado** em `PerfilAlvoView.xaml`: coluna direita (Definição de Alvo/Perfil
  selecionado/Foto de referência) não tinha `ScrollViewer` — adicionado.
- **Sobreposição de cards em Monitoramento** — causa raiz real, não só o card novo: o usuário
  tinha um layout salvo (`dashboard-layout-monitoramento-1.json`) de uma versão antiga do app
  (cards `Acoes`/`Distancias`, que não existem mais). `DashboardCanvas.ApplyLayoutSnapshot`
  tinha um bug genuíno: cards sem entrada no layout salvo (ex.: o card novo) eram posicionados
  por `ArrangeDefaultFlow`, que **nunca verifica contra os cards já restaurados do layout
  salvo** — o card novo caía "no vácuo" em cima do Radar. Corrigido para usar `FindFreeSlot`
  (já existia, usado por `ShowCard`), que verifica contra todos os cards visíveis no estado
  atual — corrige o bug para qualquer card adicionado no futuro, não só este. Removido o
  arquivo de layout pessoal obsoleto do usuário de teste, para ele voltar a usar o arranjo
  padrão do sistema (recalculado automaticamente, sempre sem sobreposição por construção).
- **Erro de binding** ("TwoWay/OneWayToSource não pode funcionar em propriedade somente
  leitura `UltimaDistanciaMetros`"): `Run.Text` tem `BindingMode` padrão **TwoWay** (diferente
  de `TextBlock.Text`, que é OneWay) — armadilha clássica do WPF; o resto do projeto já
  conhecia e sempre especificava `Mode=OneWay`/`OneTime` em bindings de `Run`, só o trecho
  novo não seguiu o padrão. Trocado por um único `TextBlock.Text` com `StringFormat`.
- **Crash fatal (fechava o app sozinho)** — o mais sério: `0x80131506` (erro fatal da CLR,
  acesso de memória inválido, não uma exceção .NET normal) dentro de
  `AlvoVisualService.CalcularFracaoCor`. Causa: `Bitmap.LockBits` com um retângulo recortado
  (a caixa detectada) sobre um `Bitmap` cujo `PixelFormat` já bate com o pedido não copia
  dados — `Scan0`/`Stride` continuam sendo os da imagem INTEIRA, só com `Scan0` deslocado até
  o canto do recorte. O código copiava `data.Stride` (largura da imagem inteira) por linha em
  vez de só `area.Width * 3` (largura do recorte) — perto da última linha da imagem, isso lia
  além do fim do buffer alocado. Corrigido para copiar só os bytes de cada linha do recorte.
  Verificado que os outros 2 lugares do projeto (novo e pré-existente) que fazem
  `LockBits`/`Marshal.Copy` sempre travam a imagem INTEIRA (nunca um recorte), então não têm
  esse problema.
- `dotnet build` (Release): 0 erros em cada correção. App reaberto depois de cada mudança e
  confirmado que o processo permanece de pé (sem crash) — ainda sem câmera/objeto real para
  validar visualmente a detecção em si.

## 2026-10-13 (continuação 2) — Diagnóstico granular: alarme não disparava ao passar o alvo

**Pedido:** "o sistema não está apitando quando o alvo passa em frente a câmera" — pediu para
corrigir.

**Investigado, sem conseguir reproduzir (sem câmera/objeto físico neste ambiente):** a cadeia
`DeteccaoCameraViewModel → AlvoVisualService → IObjectDetectionService` tem vários pontos que
podem silenciosamente não disparar o alarme por design (sem ser bug): nenhum perfil de alvo
selecionado como ativo; modo de operação em Verde (alarme só dispara em Amarelo/Vermelho,
por pedido original); YOLO não propor nenhuma caixa candidata para o objeto usado no teste
(o modelo é treinado nas 80 classes do COCO — um objeto de demonstração arbitrário pode não
ser reconhecido); ou caixa candidata encontrada mas sem bater o limiar de cor/formato do
perfil. Antes dessa mudança, `StatusDeteccao` colapsava todos esses casos na mesma mensagem
genérica ("nenhum alvo identificado"), tornando impossível diferenciar causa sem acesso à
máquina/depurador.

**Entregue:** `IAlvoVisualService.AvaliarFrame` passou a devolver `AlvoVisualAvaliacao`
(`Models/AlvoVisualAvaliacao.cs`) — não só se bateu, mas quantas caixas o YOLO propôs no
quadro e a melhor fração de pixels na cor esperada entre elas, mesmo quando nada bate.
`DeteccaoCameraViewModel.AplicarResultado` agora mostra, na própria tela de Monitoramento,
qual das 4 causas está ocorrendo: sem perfil ativo / YOLO sem candidatos / candidatos sem
bater cor-formato (com o "quão perto" chegou) / alvo identificado mas modo Verde suprime o
alarme / alarme disparado normalmente. `dotnet build` (Release): 0 erros.

**Pendência que depende do usuário:** preciso que ele teste de novo e me diga **exatamente o
texto** que aparece no card "Detecção por Câmera" ao passar o objeto na frente da câmera —
isso aponta com certeza qual limiar ajustar (ou confirma se é a troca de modo que falta) em
vez de eu continuar tentando adivinhar sem poder reproduzir.

## 2026-10-13 (continuação 3) — Reconhecimento por comparação com a foto de referência

**Esclarecimento que motivou a mudança:** o usuário esperava que a foto de referência tirada
em "Perfis de Alvo" fosse usada para RECONHECER o objeto (não só um registro manual, como
implementado antes) — confirmado objetivamente por pergunta antes de codar: "comparar com a
foto tirada" (não "continuar só por Cor+Formato+Tamanho").

**Entregue:** `AlvoVisualService` agora, quando o perfil ativo tem foto de referência, compara
cada caixa candidata do YOLO diretamente com a foto — não mais só cor dominante + proporção da
caixa. Comparação: reduz tanto a foto quanto cada candidata a uma grade 8×8 de cor média
(`ExtrairAssinatura`) e mede a diferença média por canal (`CalcularSimilaridade`, 0–1, limiar
`VisionSettings.LimiarSimilaridadeFoto`, padrão 0,65 — valor de partida, não calibrado). Antes
de comparar, roda a mesma detecção do YOLO **na própria foto de referência** para recortar só
o objeto (não a foto inteira com fundo); sem detecção na foto, assume que o usuário centralizou
o objeto ao tirar a foto (preview ao vivo já existente) e usa um recorte central como
aproximação. **Perfis sem foto continuam funcionando pelo método anterior (cor/formato)** —
mudança aditiva, sem regressão para quem não tirou foto ainda.
`Models/AlvoVisualAvaliacao.cs` ganhou `UsouFotoReferencia`/`MelhorPontuacao` (renomeado de
`MelhorFracaoCor`) para a mensagem de diagnóstico no card dizer "melhor similaridade: X%"
(comparação por foto) em vez de "melhor correspondência de cor" quando for o caso.
`dotnet build` (Release): 0 erros. App reaberto, processo estável.

**Pendência, mesma de sempre nesta frente:** limiar de similaridade (0,65) é chute inicial —
iluminação/ângulo diferentes entre a foto de cadastro e a cena ao vivo tendem a derrubar a
similaridade bastante; se continuar não reconhecendo, o texto do card agora informa a melhor
similaridade encontrada, o que diz exatamente quanto abaixar o limiar.

## 2026-10-16 — Marcação manual do objeto dentro da foto de referência

**Pedido:** "assim que tirar a foto do alvo, eu possa selecionar na foto qual é o objeto a ser
observado".

**Por que importa:** até aqui, a região do objeto DENTRO da foto era adivinhada pelo
`AlvoVisualService` — pela cor cadastrada no perfil e, quando nenhuma região tinha cor
suficiente, por um recorte central. Era a fonte documentada de dois erros: a assinatura podia
sair da pessoa que segurava o objeto, e objetos de cor diferente da cadastrada caíam no recorte
central. Com a marcação do usuário, não há mais o que adivinhar.

**Entregue:**
- `Models/PerfilAlvo.cs`: novo `RecorteAlvo(X, Y, Largura, Altura)` (pixels da própria foto) em
  `PerfilAlvo.RecorteFotoReferencia` — persiste junto no `perfis-alvo.json` existente; perfis
  antigos leem `null` e seguem funcionando.
- `IPerfilAlvoService.DefinirRecorteFotoReferencia` (grava/limpa a marcação).
  `DefinirFotoReferencia` passou a descartar a marcação anterior — recorte é em pixels da foto
  antiga, não vale para a nova.
- `AlvoVisualService.EncontrarAreaDoObjeto`: com marcação válida, usa exatamente ela e não
  calcula mais nada; sem marcação, o caminho automático anterior continua igual. A chave do
  cache de assinatura passou a incluir o recorte (sem isso, remarcar o objeto na mesma foto
  continuaria usando a assinatura antiga). Marcação que não cai dentro da foto cai no caminho
  automático com aviso no log, em vez de devolver região vazia.
- `PerfilAlvoViewModel`: `FotoLargura`/`FotoAltura`, `RecorteSelecionado` (+ `RecorteX/Y/...`
  para o desenho), `MarcarObjeto(x, y, w, h)` com clamp nos limites da foto e descarte de
  arrasto sem área, e `LimparRecorteCommand`.
- `PerfilAlvoView`: a foto salva virou área de marcação (altura 140 → 220). A foto é desenhada
  num `Canvas` do tamanho EXATO da imagem em pixels dentro de um `Viewbox` — assim a posição do
  mouse já chega em pixels da foto, sem conversão de escala nem cálculo de "letterbox" para
  errar. Retângulo cheio = marcação salva; tracejado = arrasto em curso. O arrasto fica no
  code-behind (entrada de ponteiro); a ViewModel recebe só o retângulo final.
- Localização pt-BR/en-US: `PerfilAlvo.Foto.MarcarObjeto`, `PerfilAlvo.Foto.LimparMarcacao`;
  `PerfilAlvo.Foto.Descricao`/`UltimaSalva` reescritas — o texto antigo ainda dizia que a foto
  "não faz parte da detecção automática", o que deixou de ser verdade em 2026-10-13.
- `dotnet build` (solution): 0 erros (só os `NU1701` pré-existentes do AForge). Sem `dotnet test`
  — não há projeto de testes no repositório.

**Pendência que depende do usuário:** validação visual na máquina dele — tirar a foto, arrastar
sobre o objeto, conferir que o retângulo fica onde foi desenhado, reabrir a tela e ver se a
marcação voltou, e checar no `.log` a linha "usando o objeto marcado pelo usuário".

## 2026-10-16 (continuação) — Edição de alvos, 3 fotos, altura×largura, antiduplicidade por quadrante e câmeras por quadrante

**Pedido (8 frentes):** editar alvos cadastrados; exigir no mínimo 3 fotografias válidas no
cadastro; separar altura e largura para objetos retangulares; impedir que o mesmo alvo gere
vários registros enquanto continua sendo rastreado; deixar o card "Detecção por câmera"
responsivo; associar cada câmera a um quadrante; classificar as ocorrências por quadrante; e
revisar o modelo de dados — com testes automatizados dos 13 critérios de aceitação.

**Decisões do usuário (perguntadas antes de implementar, porque mudavam a arquitetura):**

1. **Abatido** — não existia nenhum conceito de abate no código. Decidido: acionamento
   bem-sucedido deixa a ocorrência em `AbatePendente` e o **operador confirma ou descarta** na
   tela "Objetos Detectados". Nenhum automatismo abate nada.
2. **Quadrante por câmera** — exatamente **um** quadrante por câmera, com unicidade (atribuir um
   quadrante ocupado transfere a responsabilidade).
3. **Multi-câmera** — a detecção passa a rodar **nos 4 painéis da tela "Câmeras"**, em paralelo,
   cada um no seu quadrante (o card de Monitoramento continua existindo, com uma câmera).
4. **3 fotos** — exigência vale **só na criação**; na edição, perfis antigos (criados quando o
   sistema aceitava uma foto) continuam podendo ser corrigidos.

**O que o diagnóstico mudou em relação ao enunciado:** o projeto é WPF desktop — não há
frontend web, CSS nem API HTTP. "Validação no backend" virou validação na camada de serviço
(`PerfilAlvoService`), que recusa a operação independentemente da tela; "responsividade" virou
layout elástico ao tamanho do card; "migração de banco" virou `Data/SqliteSchema.cs` (não havia
nenhum mecanismo de migração — `CREATE TABLE IF NOT EXISTS` não altera tabela existente).

**Entregue — cadastro de alvos (frentes 1, 2 e 3):**
- `Models/PerfilAlvo.cs`: lista `Fotos` (`FotoReferenciaAlvo`: caminho + recorte + data) no lugar
  da foto única; `LarguraCm`/`AlturaCm` para retangulares, com `MaiorDimensaoCm` e
  `DimensoesDescricao` derivados; `MinimoFotos = 3`. Tudo em CENTÍMETROS do objeto real — a
  medida em pixels continua sendo só a caixa detectada (`VisionDetection`), e as duas
  representações se encontram apenas na estimativa de distância.
- `Helpers/ValidacaoFotoAlvo.cs`: foto válida é a que existe, **abre como imagem** e tem pelo
  menos 64×64 px. Abrir o arquivo (em vez de olhar a extensão) é o que descarta PNG truncado,
  que só quebraria depois, dentro do reconhecimento.
- `IPerfilAlvoService`: `Add`/`Atualizar` devolvem `ResultadoPerfilAlvo` (padrão de `AuthResult`)
  e recebem `DimensoesAlvo`. `Atualizar` altera o objeto já presente na coleção, preservando o
  `Id` — é ele que liga o alvo ao histórico (`ObjetoDetectado.PerfilAlvoId`), então recriar o
  registro desvincularia silenciosamente as detecções anteriores.
- `PerfilAlvoViewModel`/`PerfilAlvoView`: formulário único para criar e editar, trabalhando
  sobre uma CÓPIA das fotos (`ClonarFotos`) — desistir no meio não deixa o perfil num estado
  intermediário. Botão desabilitado com menos de 3 fotos e mensagem dizendo quantas faltam;
  galeria de fotos com remoção e indicador de "objeto já marcado"; marcação do objeto por foto.
- `AlvoVisualService`: compara o quadro ao vivo com **todas** as fotos do perfil e fica com a
  melhor similaridade (basta uma bater — exigir média faria a foto mais desfavorável derrubar o
  reconhecimento, o oposto do motivo de haver várias). Cache de assinaturas passou a ter na
  chave a lista de fotos + recortes. Distância usa `MaiorDimensaoCm`.
- `PerfilAlvoRepository.MigrarFotoUnica`: perfis no formato antigo (uma foto na raiz) são
  convertidos na leitura, sem reescrever o arquivo até a próxima alteração — voltar à versão
  anterior do app continua funcionando.
- Remoção de foto **desvincula sem apagar o arquivo**: ocorrências já gravadas guardam o caminho
  em `ReferenciaImagem`, e apagar deixaria o histórico apontando para arquivo inexistente.

**Entregue — ocorrências, rastreamento e abate (frentes 4, 7 e 8):**
- `Models/ObjetoDetectado.cs`: a tabela passa a ser de OCORRÊNCIAS, com
  `PerfilAlvoId`, `CameraDispositivoId`, `CameraNome`, `RastreamentoId`, `Estado`
  (`Detectado`/`AbatePendente`/`Abatido`) e `DataHoraUltimaDeteccao`; nova
  `OcorrenciaEstadoTransicao` (tabela `OcorrenciaEstados`) para o histórico de transições.
- `Services/RastreamentoVisualService.cs` (novo): identidade dos objetos entre quadros por
  continuidade espacial (IoU + resgate por "centro dentro da caixa anterior", que cobre o objeto
  se aproximando). Justificativa de ser um serviço novo e não `TargetTrackingService`: lá o
  identificador **vem pronto** no protocolo do Arduino; a câmera não fornece nenhum.
  Tolerância configurável a oclusão/perda (padrão 10s) — e encerrar identidade **nunca** é abate.
- `Services/OcorrenciaDeteccaoService.cs` (novo): enquanto o mesmo acompanhamento segue no mesmo
  quadrante, não há linha nova — só `DataHoraUltimaDeteccao` avança. Ocorrência só abre depois de
  N confirmações consecutivas (padrão 2), para a oscilação de pontuação entre quadros não sujar o
  histórico. O quadrante vem da CÂMERA; sem quadrante atribuído, cai no cálculo geométrico
  anterior.
- `SqliteObjetoDetectadoRepository`: `AdicionarSeNaoExistir` com `ON CONFLICT … DO NOTHING` sobre
  índice **único parcial** `(RastreamentoId, Quadrante) WHERE RastreamentoId IS NOT NULL` —
  defesa final contra duplicidade com as câmeras processando em paralelo; o parcial preserva o
  radar, que não tem rastreamento visual e pode repetir quadrante. Também `AtualizarEstado`,
  `RegistrarDeteccaoContinuada`, `GetById` e `ObterHistoricoEstados`.
- `FireControlService`: acionamento efetivado marca a ocorrência como `AbatePendente`;
  `ObjetosDetectadosViewModel`/`View` ganharam "Confirmar abate"/"Descartar abate" (só para quem
  pode executar ações e só em ocorrência pendente) e a faixa com o histórico de estados.
- **Mudança de comportamento deliberada:** o registro da detecção por câmera saiu de dentro do
  debounce do alerta e deixou de depender do modo de operação. Antes era uma linha a cada alerta
  (5s) e nenhuma no modo Verde; agora é uma ocorrência por acompanhamento, em qualquer modo — o
  modo continua decidindo apenas se o **alarme** toca. O que motivava o acoplamento (evitar
  dezenas de linhas por minuto) é exatamente o que o rastreamento resolve melhor.

**Entregue — câmeras e quadrantes (frentes 5 e 6):**
- `Models/CameraQuadrante.cs`, `Repositories/SqliteCameraQuadranteRepository.cs` (tabela
  `Cameras`, índices únicos em `DispositivoId` e `Quadrante`, transferência na mesma transação) e
  `Services/CameraQuadranteService.cs`.
- `CameraSlotViewModel`/`CameraPanelView`: seletor de quadrante por painel (Q1..Q4 ou nenhum) e
  detecção própria rodando em paralelo (um `Helpers/AnalisadorQuadros` por painel). Sem quadrante
  atribuído a detecção fica desligada — sem quadrante não há onde registrar, e rodar inferência à
  toa tiraria CPU dos painéis configurados.
- `Helpers/AnalisadorQuadros.cs` (novo): extraído de `DeteccaoCameraViewModel` quando a detecção
  passou a rodar em 5 lugares — concentra limite de frequência, rodada única por vez e a
  marshalização do buffer da câmera, que são justamente as partes delicadas de duplicar.
- Card "DETECÇÃO POR CÂMERA" responsivo: `Grid` com o vídeo na única linha `*` (antes `StackPanel`
  com altura fixa de 130px, que era cortado ao redimensionar), `Stretch="Uniform"` preservando a
  proporção, textos com `TextWrapping` e os blocos inferiores num `ScrollViewer` vertical — sem
  nenhuma rolagem horizontal. Mostra também o quadrante da câmera selecionada.

**Modelo de dados / migração:**
- `Data/SqliteSchema.cs` (novo): `PRAGMA table_info` + `ALTER TABLE ADD COLUMN` /
  `CREATE INDEX IF NOT EXISTS`, idempotente, chamado pelos repositórios. Nenhum dado é
  descartado: linhas antigas ficam com as colunas novas nulas e `Estado = 'Detectado'`.
- Documentação atualizada: `Docs/Tecnica/MODELO_DADOS.md`,
  `Docs/Documentos_Entregaveis/Banco_de_Dados/schema.sql`, `Modelo_Banco_de_Dados.dbml` e
  `Modelo_Banco_de_Dados.md` (DER, colunas novas, tabelas `ocorrencia_estados` e `cameras`).
  **Pendência:** o PDF/TeX do entregável (`Modelo_Banco_Dados_RadarTorres.pdf`) precisa ser
  regerado a partir dos fontes atualizados.

**Testes (frente 9):** criado `tests/RadarTorres.Tests` (xunit) — o projeto era referenciado pelo
`InternalsVisibleTo` do .csproj mas **nunca existiu**. 68 testes cobrindo os 13 critérios:
mínimo de fotos (incluindo arquivo corrompido, vazio, minúsculo e repetido), edição sem criar
registro e preservando o vínculo do histórico, largura×altura e perfil retangular legado,
alvo acompanhado sem gerar linha por quadro, perda temporária que não vira abate, ocorrência por
quadrante, controle no mesmo quadrante, câmera/quadrante de cada ocorrência, concorrência de 4
câmeras, migração de tabela antiga e unicidade no SQLite de verdade.
`SqliteConnectionFactory.UsarBancoDeTeste` (internal) redireciona para um banco temporário — sem
isso os testes escreveriam no banco real do usuário em `%AppData%`.

**Dois defeitos encontrados pelos próprios testes, ambos reais e corrigidos:**
1. O `INSERT` tinha `;` no fim da constante, então o `ON CONFLICT` anexado depois produzia SQL
   inválido — **toda** ocorrência de câmera cairia na fila offline (`PendingWriteQueue`) com Id
   negativo, e a antiduplicidade nunca funcionaria.
2. SQLite só aceita índice **parcial** como alvo de `ON CONFLICT` se o predicado for repetido na
   cláusula; sem `WHERE RastreamentoId IS NOT NULL`, o comando falhava com "no unique index
   matching the conflict target".

**Verificação:** `dotnet build` da solution — 0 erros (restam os `NU1701` pré-existentes do
AForge). `dotnet test` — 68/68 aprovados.

**Pendências que dependem do usuário:** validação visual na máquina dele (ver o relatório final
da conversa) — cadastro com 3 fotos, edição, atribuição de quadrantes nos 4 painéis com câmeras
reais, redimensionamento do card de detecção e o ciclo acionamento → confirmar abate.

## 2026-10-17 — Termos de Uso no app (com PDF), rolagem em Usuários, quadrante como identidade da câmera e cadastro de alvo em modal

**Pedido (7 frentes):** corrigir a cláusula 1.1 dos Termos de Uso; apresentá-los com hierarquia
visual; botão para baixar o documento integral em PDF; corrigir a rolagem da tela "Usuários";
usar o quadrante como identificação da câmera; impedir duas câmeras no mesmo quadrante; e trocar
o formulário lateral de "Perfis de Alvo" por um modal com etapas.

**Decisões do usuário (perguntadas antes de implementar):**

1. **Onde os termos aparecem** — "no ato da instalação e na tela de configurações". Não houve
   criação de item na barra lateral: a seção fica em Configurações, que abre a janela de leitura.
2. **Página de termos do instalador** — mantida em texto puro. Converter para RTF formatado
   criaria um segundo arquivo para manter em sincronia, exatamente o que o pedido manda evitar.

**Diagnóstico que mudou o escopo:** não existia tela de Termos de Uso no aplicativo. Eles só
apareciam na página de licença do instalador (`LicenseFile=TERMOS_DE_USO.txt`) e no atalho do
Menu Iniciar, que abre o `.txt` no Bloco de Notas — e um botão de download não cabe no
instalador. A tela foi criada do zero.

**Entregue — Termos de Uso (frentes 1, 2 e 3):**
- `installer/TERMOS_DE_USO.txt`: cláusula 1.1 passa a identificar Matheus Emanoel de Souza e
  Gabriel Vasconcellos de Morais, estudantes de Engenharia da Computação da UCL, sob orientação
  de Anker Loss. Conteúdo alterado ⇒ versão **1.0 → 1.1** (vigente a partir de 16/10/2026), para
  a regra de aceite já existente — toda instalação reexibe a página — passar a valer sobre esta
  redação. Nenhuma outra cláusula foi tocada.
- **Fonte única da verdade:** o MESMO arquivo é embutido no executável
  (`<EmbeddedResource>` no .csproj) e lido por `TermosDeUsoService`. Instalador, tela e PDF leem
  a mesma redação; não há cópia do texto em código, XAML nem banco.
- `Models/TermosDeUso.cs` + parser em `TermosDeUsoService`: classifica o texto puro em blocos
  (título, versão, cláusula, parágrafo numerado, alínea, item do resumo) e desfaz a quebra em 78
  colunas do arquivo. Os blocos alimentam tela E PDF — por construção não há como divergirem.
- `Views/TermosDeUsoWindow`: leitura com hierarquia visual (número da cláusula em etiqueta,
  título destacado, parágrafos com entrelinha), coluna de leitura limitada a 760px e
  centralizada, tema claro/escuro e janela redimensionável.
- `ConfiguracoesView`: seção "Termos de Uso" com a versão vigente, "Ler os Termos de Uso" e
  "Baixar Termos de Uso em PDF" (atalho que dispensa abrir a janela).
- PDF com PDFsharp (dependência que o projeto já tinha): título, hierarquia, quebra de linha
  medida, quebra de página sem cláusula órfã, rodapé com versão e "Página X de Y", acentuação
  preservada. Nome sugerido `RadarTorres_Termos_de_Uso.pdf`. Geração fora da thread de interface,
  com o botão desabilitado e rotulado "Gerando o PDF…" enquanto roda.

**Bug pré-existente encontrado pelos testes:** o PDFsharp 6 não acompanha fonte alguma e não
consulta o sistema sozinho — sem um `IFontResolver` registrado, `new XFont("Verdana", …)` lança
`InvalidOperationException`. Ou seja, **a exportação de "Objetos Detectados" em PDF nunca
funcionou** nesta versão da biblioteca. Criado `ResolvedorFontesPdf` (lê Verdana/Segoe UI/Arial
de `%WINDIR%\Fonts`, com fallback entre elas) e registrado nos dois geradores.

**Entregue — Usuários (frente 4):** a coluna direita (cartões de cadastro/edição e de redefinição
de senha) vivia solta numa linha de altura `*`, sem `ScrollViewer`: em janela mais baixa o
segundo cartão e o botão "Salvar" ficavam cortados, sem como alcançá-los — a grade da esquerda
rola sozinha, a coluna não rolava. Passou a usar o mesmo arranjo das outras telas (rolagem
vertical, horizontal desabilitada).

**Entregue — câmeras e quadrantes (frentes 5 e 6):**
- O cabeçalho do painel passa a ser a identificação do QUADRANTE ("Quadrante 2") em vez de
  "Câmera N", que era só a posição na grade. Sem atribuição, mostra "Painel N — defina o
  quadrante". O nome do dispositivo continua no seletor logo abaixo: identificação técnica e
  responsabilidade espacial são dados distintos.
- **Quadrante ocupado passa a ser recusado**, não transferido. Antes a atribuição tomava o
  quadrante da outra câmera (deixando-a sem responsabilidade sem ninguém pedir); agora a
  operação é recusada com mensagem nomeando a câmera que já o ocupa, e o seletor volta para o
  valor realmente gravado. Liberar com "Nenhum" continua permitido, e quadrante vago segue sendo
  uma situação válida.
- Concorrência: a checagem do serviço pode perder a corrida entre duas janelas, então a violação
  do índice único do banco é capturada e vira mensagem ("acabou de ser atribuído a outra
  câmera"), com recarga da lista. Os painéis assinam `AssociacoesAlteradas` e se atualizam
  sozinhos quando outro painel muda alguma atribuição.
- Compatibilidade verificada no banco real antes de mudar a regra: duas webcams conhecidas e uma
  única associação (Q1). Nenhum conflito a corrigir.

**Entregue — cadastro de alvo em modal (frente 7):**
- A tela ficou só com a listagem (agora em largura total), as ações do alvo selecionado e o botão
  **"Cadastrar novo alvo"** ao lado do título. O formulário fixo da lateral direita deixou de
  existir.
- Modal como overlay DENTRO da própria view (e não `Window` separada), porque o requisito pede a
  tela de fundo visível sob um escurecimento; de quebra, o fundo fica inerte a clique e a Tab
  sem precisar ser desabilitado (o que o deixaria cinza).
- Assistente em 4 etapas — Informações, Fotografias, Dimensões, Revisão — com indicador de
  progresso, validação por etapa (`PodeAvancar`/`PendenciaEtapa`), navegação que preserva tudo o
  que já foi preenchido e conclusão só na última etapa. O mínimo de 3 fotografias continua valendo
  na tela e no serviço.
- Edição reaproveita o mesmo assistente, preservando o `Id` (e o vínculo com o histórico).
- Esc e "✕" fecham com confirmação quando há dados; clique no fundo escurecido NÃO fecha; foco
  vai para o primeiro campo ao abrir e volta ao botão ao fechar; campos com
  `AutomationProperties.Name` para leitor de tela; `EstaSalvando` impede concluir duas vezes.
- A câmera agora só liga enquanto o modal está aberto — antes a tela segurava a webcam só para
  listar alvos, disputando o dispositivo com o módulo "Câmeras" e o card de detecção.

**Testes:** 105 no total (eram 73). Novos: 10 de Termos de Uso (cláusula 1.1, 15 cláusulas
preservadas, alíneas, versão, PDF com ≥5 páginas abrindo como PDF válido, parser isolado),
6 de câmera×quadrante (recusa com mensagem, uma câmera um quadrante, liberar e reatribuir,
persistência após reiniciar, atribuições simultâneas) e 16 do modal de cadastro (abertura,
validação por etapa, navegação preservando dados, conclusão, duplo clique, falha que mantém o
rascunho, edição pelo mesmo modal). `dotnet build` da solution: 0 erros, 0 avisos CS.

**Observação registrada, sem alteração:** o preâmbulo dos Termos define o termo "AUTOR" no
singular ("o autor identificado na Cláusula 1"). Com dois desenvolvedores, o rigor pediria
"AUTORES" — mas isso propaga por todo o documento, e o pedido foi explícito em não reescrever o
texto jurídico além do necessário. Fica para decisão do usuário.

**Pendência que depende do usuário:** conferência visual das telas novas (janela dos termos, PDF
aberto, modal em resoluções menores) e do fluxo de quadrantes com as duas webcams reais.

## 2026-10-17 — Nova identidade visual (tokens de cor, tipografia, Shell, Painel Principal, Monitoramento)

**Pedido (resumo):** aplicar uma nova identidade visual ao RadarTorres — tokens de cor
semânticos (claro/escuro), 3 fontes (Chakra Petch/IBM Plex Sans/JetBrains Mono, OFL,
embutidas), ícones de traço próprios, Shell reconstruída (Sidebar agrupada com ícones,
248px/76px, cabeçalho com título+chip de comunicação), cards do Painel Principal redesenhados
+ card novo "Radar ao vivo (prévia)", Monitoramento redesenhado (3 botões de modo sempre
visíveis com selos ATIVO/pede confirmação, radar com quadrantes Q1/Q3 translúcidos permanentes,
torres quadradas vazadas/preenchidas, alvos quadrados âmbar) e uma passagem de tokens/tipografia
nas demais telas — regra explícita: **mudança puramente visual**, nenhuma regra de negócio
alterada. Pedido feito em duas partes na mesma sessão: primeiro um plano com perguntas de
aprovação, depois "faça tudo que ficou pendente" confirmando as decisões.

**Branch:** `tratamento-de-imagens`. Commits (nesta ordem, cada um com `dotnet build`/`dotnet
test` passando): tokens de cor → bugfix i18n do estado de comunicação → dicionário de ícones →
fontes embutidas → Shell (sidebar/topbar) → ajuste de alinhamento da sidebar compacta → cards
do Painel Principal + radar ao vivo → Monitoramento + radar → tipografia/tabelas nas demais
telas → contraste (OnAccent em vez de branco fixo sobre cores de destaque).

**Decisões aprovadas pelo usuário (perguntadas antes de codar):** troca direta dos tokens
antigos (sem alias de compatibilidade); hambúrguer da Sidebar alternando 248px/76px em
qualquer tela (era 210/52px); implementar o card "Radar ao vivo" (funcionalidade nova,
reaproveitando `ITargetTrackingService`/`ITowerSelectionService` já existentes, sem duplicar
lógica); fontes OFL podem ser embutidas no instalador.

**Achado que simplificou o escopo:** a confirmação antes de trocar para o modo Vermelho/Disparar
**já existia** (`MainViewModel.CurrentMode`, `MessageBox.Show` de confirmação) — o selo "pede
confirmação" pedido é só visual, não precisou de lógica nova.

**Bug corrigido:** o card "Comunicação" do Painel Principal mostrava o enum em inglês
("Disconnected") mesmo com o app em português — faltava passar pelo dicionário de tradução.

**Itens com fidelidade parcial ao mock (decisão de risco, não implementado por completo):**
- Os controles de zoom/alcance do radar continuam como overlay interno (22x22, cantos
  superiores), não como a barra externa de botões 44x44 descrita no mock — movê-los exigiria
  expor estado interno do `RadarControl` como propriedade pública, risco maior para o tempo
  disponível.
- O rótulo "N · 0°" do radar foi reposicionado (mais à esquerda) para não colidir com o rótulo
  de distância do anel mais externo, mas continua dentro do círculo, não fora dele — colocá-lo
  fora exigiria somar uma margem a toda a matemática de conversão mundo→tela do radar (torres,
  alvos, cliques), risco alto de regressão num componente que não dá para testar visualmente
  nesta sessão.
- Cabeçalhos de coluna das tabelas (`DataGrid`) ganharam cor/fonte/divisória do token novo, mas
  o texto de cada coluna não foi reescrito em maiúsculas (são ~70 strings hardcoded em 5 telas).
- A barra de abas "estilo navegador" do `MainWindow.xaml` (sessões paralelas) não foi alterada —
  ficou pendente uma dúvida não respondida sobre uma suposta aba "Administrador" duplicando o
  cabeçalho, que não foi localizada no código.

**Testes:** 105 no total, todos passando a cada commit. O teste
`PersistenciaSqliteTests.Insercoes_concorrentes_do_mesmo_rastreamento_resultam_numa_ocorrencia`
é instável (falha ~1 a cada poucas execuções) — confirmado via `git stash` que já falhava assim
antes de qualquer alteração desta frente; não foi investigado, por ser camada de persistência/
concorrência, fora do escopo "só visual".

**Pendência que depende do usuário:** validação visual completa nas duas telas, nos dois temas
(claro/escuro) e nos dois idiomas — a sessão não pode abrir o aplicativo para conferir. Também
ficou pendente a resposta sobre a aba "Administrador" citada acima.

**Armadilha de processo encontrada e documentada:** `build.bat`/`run.bat` compilam e abrem o que
estiver *checked out* no momento, sem indicar a branch — rodar numa branch errada (ex.: `main`,
sem estas mudanças) parece "a mudança não funcionou". Conferir `git branch --show-current` antes
de compilar.

## 2026-10-20 — Troca do ícone do item de menu "Desempenho"

**Pedido (texto integral):**

> Preciso trocar o ícone do item de menu "Desempenho" (barra lateral da Shell) por um novo
> ícone. É uma alteração apenas visual, e quero a menor alteração possível.
>
> REGRAS
> - Antes de qualquer coisa: git status, branch atual, git fetch e comparação com o remoto.
>   Me informe a situação. Não faça push.
> - Antes de editar, me apresente um plano curto (arquivos que pretende tocar e o que muda em
>   cada um) e espere minha aprovação.
> - Não altere nada fora do pedido: sem refatoração, sem renomear itens, sem mexer em rota,
>   permissão, texto ou ordem do menu.
> - Siga exatamente o padrão que o projeto já usa para os ícones dos outros itens do menu
>   (dicionário de recursos, Geometry/Path, tamanho, espessura, cor via DynamicResource). Não
>   introduza biblioteca nem outro mecanismo. Se o padrão existente for diferente do que
>   descrevo abaixo, siga o do projeto e me avise.
> - Na dúvida sobre qualquer ponto, pare e pergunte. Não suponha.
> - Não faça merge, rebase, reset, nem crie tags.
>
> O QUE FAZER
> 1. Localizar o item de menu "Desempenho" (se ele ainda aparecer como "Objetos detectados" no
>    código, confirme comigo antes de seguir) e o ícone atual dele.
> 2. Substituir somente o ícone atual por este: gráfico de dispersão (eixos em L + 4 pontos),
>    grade 24x24, traço de 2px, pontas e junções arredondadas, sem preenchimento (geometria SVG
>    fornecida com `M`/`V`/`H`/`A`). Ajuste o nome da chave ao padrão de nomenclatura dos ícones
>    existentes.
> 3. A cor do traço deve vir do estado do item (ativo = Accent.Text, inativo = Text.Secondary),
>    via recurso dinâmico, igual aos demais ícones. Sem cor fixa no código. Confira nos temas
>    claro e escuro.
> 4. Se o ícone antigo ficar sem uso em outro lugar, me avise em vez de apagá-lo.
>
> VALIDAÇÃO: `dotnet build` (0 erros e 0 avisos) e `dotnet test` (21 testes). Se não conseguir
> conferir o resultado visualmente, dizer isso claramente e pedir para o usuário conferir.
>
> COMMIT: só depois de autorização; mensagem em português, verbo no presente, até 72
> caracteres, sem menção a IA/assinatura/trailer.
>
> Pediu também entrada neste log e um relatório final cobrindo situação inicial do repositório,
> sincronismo com o GitHub, arquivos alterados, validações, commits, pendências e riscos.

**Situação do repositório no início (informada antes de editar):** branch atual
`estrutura/cooperacao-torres-adjacentes` (não `main`); `main` local em dia com `origin/main`
(0 ahead/0 behind, confirmado via `git fetch`); a branch atual tinha 1 commit local não
enviado ao remoto e sem upstream configurado; havia uma mudança não commitada de sessão
anterior (`src/RadarTorres.App/Assets/RadarTorres.ico`, do ajuste de ícone do aplicativo, sem
relação com este pedido).

**Divergências do padrão do projeto encontradas e avisadas antes de editar:**
- Os ícones da Sidebar são `<GeometryGroup x:Key="Icon.X">` em `Resources/Icons.xaml`
  combinando formas primitivas (`PathGeometry`, `EllipseGeometry` para pontos), não
  `<Geometry>` solta como no pedido — a geometria SVG fornecida (eixos com `V`/`H`, pontos
  via arco `A`) foi traduzida para esse idioma: uma `PathGeometry` para o eixo em L e uma
  `EllipseGeometry RadiusX="1" RadiusY="1"` por ponto.
- Nenhum ícone do projeto define `StrokeLineJoin`/`StrokeStartLineCap`/`StrokeEndLineCap`
  (todos usam o padrão do WPF — miter/flat) porque o `<Path>` que desenha os ícones é um único
  template compartilhado na Sidebar (data-bound por `IconKey`), não um Path por ícone — não dá
  para arredondar só este sem adicionar lógica por item ou mudar o template geral. Seguido o
  padrão do projeto (sem cantos arredondados), por decisão do usuário.
- `Docs/LOG_SOLICITACOES.md` (caminho citado no pedido) não existe; o arquivo real é
  `Docs/Projeto/LOG_SOLICITACOES.md` — usado o caminho real.

**Pergunta feita antes de editar (`AskUserQuestion`) e resposta:** `Icon.Desempenho` é
compartilhado entre o item "Desempenho" da Sidebar (`ShellViewModel.cs:230`) e o ícone do card
"Monitoramento" no menu "Painéis ocultos" (`DashboardToolbar.xaml.cs:35`) — trocar o recurso
muda os dois lugares. Usuário escolheu trocar o recurso compartilhado (opção recomendada) em
vez de criar uma chave nova só para a Sidebar.

**Entregue:**

- `src/RadarTorres.App/Resources/Icons.xaml` — conteúdo de `Icon.Desempenho` substituído pelo
  gráfico de dispersão pedido (eixo em L + 4 pontos), mesma grade 24x24/traço 2px/sem
  preenchimento dos demais ícones. Único arquivo alterado.
- Confirmado que "Desempenho" já era o nome correto do item no código (`MenuItem.Desempenho`) —
  "Objetos detectados" é um item diferente (`MenuItem.ObjetosDetectados`), não tocado.
- Cor do traço: nenhuma mudança necessária — já vem do `Foreground` do botão pai (herdado via
  `RelativeSource`), que já aplica `Accent.Text` (selecionado) / `Text.Secondary` (padrão) nos
  dois temas, igual aos demais itens.
- Ícone antigo (gráfico de barras): não ficou "sem uso" em outro lugar — era o conteúdo inline
  da própria chave `Icon.Desempenho` (sem um recurso nomeado separado), então foi substituído
  no lugar, não há nada órfão para avisar.
- `dotnet build`: 0 erros; 9 avisos, todos `NU1701` pré-existentes do pacote AForge (mesmos de
  antes desta mudança, não relacionados ao ícone) — portanto não bate com o "0 avisos" pedido,
  mas por uma causa alheia a esta alteração.
- `dotnet test`: 110 testes, 110 aprovados, 0 falhas — não bate com os "21 testes" citados no
  pedido (o projeto de testes cresceu desde a sessão que originou esse número); nenhum teste
  cobre recursos de ícone (confirmado por busca antes de editar), então o resultado não muda
  por causa desta alteração.
- Validação visual: não realizada pela sessão (ambiente sem abrir a UI para este pedido) —
  pendente de conferência pelo usuário nos dois temas.
- Nenhum commit feito — aguardando autorização, conforme pedido.

---

## 2026-10-21 — Redesenho da tela de Login (70% arte / 30% formulário)

**Pedido (texto integral):**

> Tarefa: redesenhar o layout da tela de Login (70% arte / 30% formulário)
>
> REGRAS: antes de qualquer alteração, verificar git status/branch/remoto/fetch e informar a
> situação; apresentar plano e aguardar aprovação antes de alterar código; menor alteração
> possível, sem refatorar fora do escopo, seguindo os padrões existentes (MVVM à mão,
> DynamicResource, i18n); parar e perguntar na dúvida; sem push/merge/rebase/reset/tags sem
> autorização explícita; commits pequenos, um por alteração lógica, formato
> `<tipo>(<escopo>): <descrição>`, sem menção a IA; validar com `dotnet build` (0 erros/avisos)
> e `dotnet test` (21 testes); registrar a sessão neste log; relatório final (situação inicial,
> arquivos alterados, validações, commits, pendências).
>
> OBJETIVO: dividir a tela em painel de arte à esquerda (70%, logo centralizada + nome
> "RadarTorres" abaixo) e painel de formulário à direita (30%, conteúdo centralizado). Só
> visual — sem alterar lógica de autenticação, ViewModel, serviços ou fluxo pós-login; manter
> todos os campos/botões/mensagens já existentes, só reposicionados/estilizados.
>
> ESPECIFICAÇÕES: `Grid` raiz com colunas `7*`/`3*`; coluna direita com `MinWidth` (perguntar o
> valor); formulário com largura máxima e centralizado; painel esquerdo com fundo de tema e logo
> proporcional sem distorcer; painel direito com fundo de tema e controles empilhados; botão
> principal no estilo de destaque "já existente" (Accent.Fill/OnAccent, altura mínima 44, raio
> 10); campos com altura mínima 44, raio 10, borda Border.Control; tudo via DynamicResource, sem
> cor fixa, nos dois temas; fontes Chakra Petch/IBM Plex Sans (perguntar se já embutidas); logo
> = ícone original do app (perguntar qual arquivo); textos via i18n existente.
>
> PERGUNTAR antes de implementar: (1) elementos decorativos no painel esquerdo ou só a logo; (2)
> exibir versão do app; (3) seletor de idioma/tema na tela de login; (4) janela fixa ou
> redimensionável; (5) existe "esqueci minha senha" hoje.

**Situação do repositório no início:** branch `estrutura/cooperacao-torres-adjacentes`,
working tree limpa, sincronizada com `origin/estrutura/cooperacao-torres-adjacentes` (sem
commits locais pendentes de push); `main` local em dia com `origin/main`.

**Levantamento feito antes de perguntar/editar** (evitou perguntas desnecessárias e revelou
divergências do pedido):
- Login não é uma `Window` própria — é `UserControl` hospedado como aba do `MainWindow`
  (mesmo padrão de abas do resto do app); não existe "janela de login" com tamanho
  fixo/redimensionável separado para decidir.
- `LoginViewModel` não tem (nem tinha) comando de "esqueci minha senha" — confirmado que não
  existe, não foi criado.
- Fontes `Font.Display` (Chakra Petch) e `Font.Body` (IBM Plex Sans) já embutidas e com chave de
  recurso pronta (`Styles/Typography.xaml`), com fallback Segoe UI automático.
- Único arquivo de logo é `Assets/RadarTorres.ico` (sem PNG/SVG); quadro de 256px já tem a arte
  completa (torre + mira), mesmo arquivo já usado antes na tela de login.
- Chave de i18n `Login.Title` ("Entrar no RadarTorres"/"Sign in to RadarTorres") já existia nos
  dois idiomas mas não era usada em lugar nenhum — reaproveitada para o título do formulário no
  lugar do texto fixo "RadarTorres" que havia antes.
- O nome "RadarTorres" como marca (não traduzido, dois `Run` coloridos) é o padrão já usado na
  Sidebar — reproduzido exatamente igual abaixo da logo no painel esquerdo.
- Nenhum botão do projeto hoje tem altura mínima 44 + raio 10 juntos, nem nenhum campo de texto
  tem cantos arredondados (bordas retas padrão do WPF em todo o app) — o estilo descrito no
  pedido como "já existente" não existia.

**Perguntas feitas antes de editar (`AskUserQuestion`) e respostas — todas as recomendadas:**
1. Raio/altura inexistentes hoje no projeto → aplicar só nesta tela (campo/botão com
   `ControlTemplate` próprio, escopado por `x:Key`, sem virar padrão implícito do app).
2. Painel esquerdo → só logo + nome, sem anéis/ponto decorativos.
3. Versão do app no rodapé → não mostrar (hoje não aparece em nenhuma tela, só em relatório de
   diagnóstico; evitado adicionar propriedade nova à ViewModel só para isso).
4. Seletor de idioma/tema no login → manter como está (sem esses controles nessa tela).
5. (Pergunta extra, fora da lista original, levantada ao detalhar a implementação) O campo de
   senha usa o `PasswordRevealBox` compartilhado (Usuários/Perfil também usam) sem como mudar a
   borda interna de fora → aceitar a pequena inconsistência (campo de usuário arredondado,
   campo de senha só com altura 44) em vez de tocar no componente compartilhado.

**Entregue:**

- `src/RadarTorres.App/Views/LoginView.xaml` — reescrito: `Grid` raiz 2 colunas (`7*`/`3*`,
  direita com `MinWidth="340"`); painel esquerdo (`Bg.Sidebar`) com a logo
  (`Assets/RadarTorres.ico`, `Stretch="Uniform"`, sem distorcer/recortar) e "RadarTorres"
  abaixo, centralizados; painel direito (`Bg.Window`) com o formulário existente
  (título agora usando `Login.Title`, subtítulo, usuário, senha, mensagem de erro, botão,
  dica de admin padrão) centralizado, `MaxWidth="360"`. Removida a antiga imagem de fundo
  esticada (`UniformToFill` cobrindo a tela toda) que causava o desalinhamento relatado — o
  painel dedicado à esquerda resolve isso por construção.
- Dois estilos novos, escopados só a este arquivo (`x:Key`, não implícitos por tipo):
  `LoginFieldTextBox` (campo de usuário, `ControlTemplate` com `CornerRadius="10"`,
  `MinHeight="44"`) e `LoginSubmitButton` (botão, mesmo tratamento, com gatilhos de
  hover/pressed para repor o feedback visual que o template substitui). O `Foreground` do botão
  trocou de `"White"` fixo para `{DynamicResource OnAccent}`.
- Nenhuma alteração em `LoginViewModel.cs`, `App.xaml.cs`, `PasswordRevealBox.xaml` nem em
  nenhum serviço — bindings e comandos idênticos aos de antes.
- `dotnet build`: 0 erros; 6 avisos, todos `NU1701` pré-existentes do pacote AForge (não
  relacionados a esta mudança) — não bate com "0 avisos" do pedido, por causa alheia a esta
  alteração.
- `dotnet test`: 110 testes, 110 aprovados (não 21 — número desatualizado; nenhum teste cobre
  XAML/layout).
- Aplicativo recompilado e reaberto pela sessão para o usuário validar visualmente nos dois
  temas e dois idiomas — a sessão não decide sozinha se o resultado está bom.
- **Órfão identificado, não removido** (avisado em vez de apagado): `LoginMarcaDaguaOpacidade`
  (`Themes/Dark.xaml`/`Light.xaml`) não é mais referenciado em lugar nenhum, já que a imagem de
  fundo que a usava foi removida — chave morta, mantida para o usuário decidir se remove depois.
- Nenhum commit feito — aguardando a validação visual do usuário antes de pedir autorização de
  commit, conforme o fluxo já estabelecido nesta sessão.

---

## 2026-10-21 — Troca do ícone do item de menu "Objetos detectados"

**Pedido (texto integral):**

> Preciso trocar o ícone do item de menu "Objetos detectados" (barra lateral da Shell) por um
> novo ícone. É uma alteração apenas visual, e quero a menor alteração possível.
>
> REGRAS
> - Antes de qualquer coisa: git status, branch atual, git fetch e comparação com o remoto.
>   Me informe a situação. Não faça push.
> - Antes de editar, me apresente um plano curto (arquivos que pretende tocar e o que muda em
>   cada um) e espere minha aprovação.
> - Não altere nada fora do pedido: sem refatoração, sem renomear itens, sem mexer em rota,
>   permissão, texto ou ordem do menu. O texto do item continua "Objetos detectados".
> - Siga exatamente o padrão que o projeto já usa para os ícones dos outros itens do menu
>   (dicionário de recursos, Geometry/Path, tamanho, espessura, cor via DynamicResource). Não
>   introduza biblioteca nem outro mecanismo. Se o padrão existente for diferente do que
>   descrevo abaixo, siga o do projeto e me avise.
> - Na dúvida sobre qualquer ponto, pare e pergunte. Não suponha.
> - Não faça merge, rebase, reset, nem crie tags.
>
> O QUE FAZER
> 1. Localizar o item de menu "Objetos detectados" e o ícone atual dele (dois quadrados ligados
>    por uma linha).
> 2. Substituir somente o ícone atual pelo ícone "binoculars" (estilo outline) do Tabler Icons
>    (licença MIT). Obter o SVG oficial em
>    https://github.com/tabler/tabler-icons/blob/main/icons/outline/binoculars.svg — não
>    desenhar nem reproduzir o path de memória.
> 3. Converter o SVG em Geometry, mantendo grade 24x24, traço de 2px, pontas e junções
>    arredondadas, sem preenchimento. Descartar o path de fundo "M0 0h24v24H0z" se houver.
>    Ajustar o nome da chave ao padrão existente (sugestão: `Icon.ObjetosDetectados`).
> 4. Se não conseguir acessar o SVG por falta de rede, parar e pedir para colar o conteúdo do
>    arquivo — não improvisar outro desenho.
> 5. A cor do traço deve vir do estado do item (ativo = Accent.Text, inativo = Text.Secondary),
>    via recurso dinâmico, igual aos demais ícones. Sem cor fixa no código. Conferir nos temas
>    claro e escuro.
> 6. Se o ícone antigo ficar sem uso em outro lugar, avisar em vez de apagá-lo.
> 7. Se o SVG do Tabler usar comandos que o WPF não aceita na Geometry (ex.: arcos com flags
>    colados), reescrever o path em sintaxe válida sem mudar o desenho, e avisar.
>
> VALIDAÇÃO: `dotnet build` (0 erros e 0 avisos) e `dotnet test` (21 testes). Se algo não puder
> ser conferido visualmente, dizer isso claramente e pedir para o usuário conferir no app.
>
> COMMIT: só depois de autorização; revisar `git status`/`git diff` e incluir só os arquivos
> desta alteração; mensagem em português, verbo no presente, até 72 caracteres, sem menção a
> IA/assinatura/trailer. Sugestão: `style(menu): troca o ícone do item Objetos detectados`.
>
> Pediu também entrada neste log e relatório final cobrindo situação inicial do repositório,
> sincronismo com o GitHub, arquivos alterados, validações, commits, pendências e riscos.

**Situação do repositório no início:** branch `estrutura/cooperacao-torres-adjacentes`,
working tree limpa, sincronizada com `origin/estrutura/cooperacao-torres-adjacentes` (sem
commits locais pendentes de push); `main` local em dia com `origin/main`.

**SVG obtido:** baixado via `curl` de
`https://raw.githubusercontent.com/tabler/tabler-icons/main/icons/outline/binoculars.svg`
(Tabler Icons v3.6, licença MIT) — conteúdo íntegro, sem o path de fundo "M0 0h24v24H0z"
(não existia neste arquivo).

**Divergências do pedido encontradas e avisadas antes de editar:**
- O SVG usa comandos `h`/`v` em 3 dos 7 `<path>` (as duas "hastes" acima das lentes e o
  retângulo central entre elas) — o próprio projeto já tinha documentado que "WPF não tem H/V
  na mini-linguagem" (comentário em `Icon.LogoTorre`, `Icons.xaml`). Esses 3 trechos foram
  recalculados à mão em coordenadas absolutas (`L`), preservando exatamente o desenho original
  (sem mudar a forma, só a sintaxe do comando).
- Os dois círculos das lentes (desenhados no SVG com arcos `a`) foram convertidos para
  `EllipseGeometry Center="7,16" RadiusX="3" RadiusY="3"` / `Center="17,16"` em vez de manter
  arco de path — nenhum ícone do projeto desenha círculo via arco, todos usam `EllipseGeometry`
  (confirmado em `Icon.Monitoramento`, `Icon.PerfisAlvo`, `Icon.Desempenho`).
- `Icon.ObjetosDetectados` também é usado em `DashboardToolbar.xaml.cs:38` (card
  "ObjetosDetectados" no menu "Painéis ocultos") — mesma situação do `Icon.Desempenho` na
  sessão anterior. Avisado antes de editar; seguida a mesma decisão já tomada pelo usuário
  (trocar o recurso compartilhado, muda nos dois lugares).

**Entregue:**

- `src/RadarTorres.App/Resources/Icons.xaml` — `Icon.ObjetosDetectados` substituído pelo ícone
  "binoculars" do Tabler Icons: 2 `EllipseGeometry` (lentes) + 1 `PathGeometry` combinando as 5
  formas restantes, mesma grade 24x24/traço 2px/sem preenchimento dos demais ícones. Único
  arquivo alterado. Comentário no próprio dicionário registra a fonte (Tabler Icons, MIT, com
  caminho do arquivo) e as duas conversões (h/v→L, arco→Ellipse).
- Cor do traço: nenhuma mudança necessária — herdada do `Foreground` do botão pai, igual aos
  demais itens, nos dois temas.
- Ícone antigo (dois quadrados ligados por linha): não ficou "sem uso" em outro lugar — era
  conteúdo inline da própria chave `Icon.ObjetosDetectados` (sem recurso nomeado separado),
  substituído no lugar.
- `dotnet build`: 0 erros; 9 avisos, todos `NU1701` pré-existentes do pacote AForge (não
  relacionados a esta mudança).
- `dotnet test`: 110 testes, 110 aprovados (não 21 — número desatualizado; nenhum teste cobre
  recursos de ícone).
- Aplicativo recompilado e reaberto pela sessão para o usuário validar visualmente nos dois
  temas.
- Nenhum commit feito — aguardando autorização, conforme pedido.

---

## 2026-10-23 — Remoção do card "Radar ao vivo (prévia)" do Painel Principal

**Pedido (texto integral):**

> Remova o radar ao vivo previa da tela painel principal.

**Entregue:**

- `src/RadarTorres.App/Views/PainelPrincipalView.xaml` — removido o `DashboardCard`
  "RadarAoVivo" (mini `RadarControl` + contagem de alvos no raio + link "Abrir Monitoramento");
  removido o `xmlns:local` (sem mais uso na view).
- `src/RadarTorres.App/Views/PainelPrincipalView.xaml.cs` — removido o handler
  `AbrirMonitoramentoLink_MouseLeftButtonUp` (exclusivo do card removido).
- `src/RadarTorres.App/ViewModels/PainelPrincipalViewModel.cs` — removidos `Targets`/`Towers`,
  `AlvosNoRaio`/`AlvosNoRaioTexto`, `AbrirMonitoramentoCommand`, o handler da coleção de alvos e
  as dependências `ITargetTrackingService`/`ITowerSelectionService` (só existiam para este
  card) — a tela volta a ser puramente de leitura de auditoria/estado, sem ligação com o
  rastreamento ao vivo.
- `src/RadarTorres.App/Views/Shared/DashboardToolbar.xaml.cs` — removida a entrada
  `"RadarAoVivo"` do dicionário de ícones do menu "painéis ocultos" (card não existe mais, não
  há como reabrir).
- `Resources/Localization/pt-BR.json` e `en-US.json` — removidas as chaves
  `Dashboard.RadarAoVivo`, `Dashboard.AlvosNoRaio` e `Dashboard.AbrirMonitoramento` (sem uso em
  nenhum outro lugar).
- `dotnet build`: 0 erros. `dotnet test`: 128 testes, 128 aprovados.
- Nenhum commit feito — aguardando autorização.

---

## 2026-10-23 — Embutir o genoma treinado no build e usá-lo no controlador real

**Pedido (texto integral):**

> Agora quero que sempre que for buildado, pege o genoma que foi antes exportado

**Esclarecido com o usuário (duas perguntas):** (1) "buildado" = no momento do build/publish
(não a cada abertura do app); (2) o genoma carregado deve reger a decisão real do sistema (qual
torre engaja), não só ficar disponível.

**Entregue:**

- `src/RadarTorres.App/Scripts/EmbutirGenomaTreinado.ps1` + target MSBuild
  `EmbutirGenomaTreinado` (`BeforeTargets="BeforeBuild"`) em `RadarTorres.App.csproj`: antes de
  cada build, procura em `%AppData%\RadarTorres\Treinamento\genoma-geracao-*.json` o arquivo
  com maior `MelhorFitness` (empate: mais recente) e o copia para
  `Resources\ControladorTreinado\genoma-controlador.json`, que é embutido no executável
  (`EmbeddedResource`). Sem pasta/arquivos (build limpo, CI, outra máquina): mantém o que já
  está commitado, sem falhar o build.
- Baseline commitado: genoma zerado (74 pesos/bias a zero, arquitetura `[6,8,2]`) — equivale
  matematicamente à heurística pura (ver próximo item), então uma instalação nova nunca se
  comporta de forma "não treinada e aleatória".
- `src/RadarTorres.App/Services/ControladorTreinadoProvider.cs` (+ `IControladorTreinadoProvider`):
  carrega o JSON embutido em `RedeNeural` uma única vez, no mesmo padrão de
  `DashboardLayoutRepository.LoadSystemDefault`.
- `src/RadarTorres.App/Services/TowerSelectionService.cs`: dentro de cada camada da heurística
  (próprio quadrante → adjacente → sobressalente), a escolha entre candidatas agora usa
  `ControladorHibrido.Pontuar` (heurística+rede) em vez de só "menor distância" — cai para a
  heurística pura se não houver controlador treinado embutido. Extraído `ConstruirEntradas` e
  `Pontuar` como métodos `static` em `ControladorHibrido` para a MESMA fórmula valer no
  treinamento e em produção (sem isso o genoma treinado não transferiria corretamente).
- **Limite de segurança deliberado, não pedido explicitamente mas necessário:** o disparo
  continua 100% manual (`FireControlService`/`MainViewModel.ManualFireAsync`, só reage a clique
  do operador) — a rede neural decide SÓ qual torre persegue o alvo, nunca aciona nada por
  conta própria. `ControladorHibrido.DecidirDisparo` (usado no treinamento) não foi ligado a
  nenhum caminho automático do sistema real.
- Limitação conhecida, documentada no código: o sistema real não mede velocidade do alvo (só
  posição), então essa entrada da rede usa um valor neutro fixo em produção — a mesma rede foi
  treinada com velocidade real do ambiente simulado.
- Testes: `TowerSelectionServiceTests` ganhou um teste que prova que um genoma zerado produz
  exatamente a mesma escolha que a heurística antiga; `ControladorTreinadoProviderTests` novo.
  `dotnet build`: 0 erros. `dotnet test`: 131 testes, 131 aprovados (inclui validação real do
  pipeline: nesta máquina já havia um genoma exportado, geração 20130, e o build o embutiu
  automaticamente).
- Nenhum commit feito — aguardando autorização.

---

## 2026-10-23 — Campos de visão das torres, torre de vigilância central e mensagem de exportação

**Pedido (texto integral):**

> Primeiro quero que mostre o campo de visão de cada canhão. e o campo de monitoramento do
> sensores do centro da torre de vigilancia central.
>
> Dê a mensagem de genoma exportado com sucesso quando for em exportar.

**Esclarecido com o usuário (três perguntas):** (1) tela = Treinamento do Controlador (2D), não
o radar real; (2) campo de visão = setor/cone angular (não círculo de alcance puro); (3) "torre
de vigilância central" = o sensor/base já existente na origem (0,0), não uma entidade nova.

**Entregue:**

- `ViewModels/SetorVisualTreinamento.cs` (novo) + `CamposDeVisaoVisuais` em
  `TreinamentoControladorViewModel`: um leque de 90° (`AnguloCampoDeVisaoGraus`) por torre,
  voltado para fora da base (mesmo ângulo da torre vista da origem), raio = alcance de
  engajamento. Estático — calculado uma única vez em `DesenharTorresFixas` (as torres não se
  movem durante o episódio), desenhado como `Polygon` no Canvas.
- Círculo de limite do mundo (já existia) estilizado como o campo de monitoramento da torre de
  vigilância central: preenchimento translúcido + tooltip; adicionado um marcador visual (ponto
  azul) no centro (0,0) representando a própria torre central.
- `TreinamentoControladorViewModel.ExportarMelhorGenoma`: além do log (`_logger.Success`, que
  só aparece no console da tela de Monitoramento), agora mostra uma mensagem visível na própria
  tela de Treinamento (`StatusMensagem`/`StatusSucesso`, banner verde/vermelho abaixo do botão
  Exportar) — antes, exportar não dava nenhum feedback visível para quem estivesse nessa tela.
- `dotnet build`: 0 erros. `dotnet test`: 131 testes, 131 aprovados.
- Nenhum commit feito — aguardando autorização. Validação visual não foi feita (não abro o
  aplicativo sem pedido explícito) — pedido ao usuário testar na tela "Treinamento do
  Controlador".

---

## 2026-10-23 — Tabela de histórico no esquema documentado, tela responsiva, cone acompanhando o alvo

**Pedido (texto integral):**

> Faça uma tabela no banco de dados para os dados do histórico de gerações. Deixe essa tela de
> histórico de gerações responsivo. coloque a visulização do cone de visão acompanhando o alvo.

**Entregue:**

- **Tabela documentada**: `historico_treinamento_geracoes` (criada programaticamente desde a
  rodada anterior) agora também está nos documentos oficiais do modelo de dados —
  `Docs/Documentos_Entregaveis/Banco_de_Dados/schema.sql` e `Modelo_Banco_de_Dados.dbml`, e no
  diagrama/detalhe técnico em `Docs/Tecnica/MODELO_DADOS.md`. Adicionado também o índice
  `idx_historico_treinamento_geracao` (coluna `GERACAO`) ao repositório real, para o schema.sql
  continuar espelhando exatamente o código. **Não regenerados**: `Modelo_Banco_de_Dados.md`/
  `.tex`/`.pdf` e os diagramas DER em PNG (entregáveis acadêmicos formatados, fora do alcance
  desta sessão) — avisar se quiser que eu atualize esses também.
- **Tela responsiva**: layout raiz da tela "Treinamento do Controlador" trocado de `DockPanel`
  fixo para `Grid` com linhas `Auto`/`*`/`Auto`/`220 (min 120, max 420)` + `GridSplitter`
  arrastável entre o conteúdo principal e o histórico — o usuário pode redimensionar a tabela de
  histórico puxando a barra. Colunas do `DataGrid` com larguras proporcionais (`1*`/`2*` nas que
  fazem sentido crescer, fixas só nas numéricas estreitas) e `HorizontalScrollBarVisibility=Auto`,
  em vez de larguras fixas em pixel que deixavam espaço vazio ou cortavam em janelas menores.
- **Cone de visão acompanhando o alvo**: o leque de cada torre (antes estático, sempre voltado
  para fora da base) agora gira a cada quadro para apontar para o alvo que a torre está mirando
  — direção = mesmo ângulo da linha de mira (`BearingCalculator`-equivalente) — e volta para a
  direção padrão quando a torre fica livre. `SetorVisualTreinamento.Pontos` passou a ser mutado
  no lugar (em vez de recriado) para o `Polygon` redesenhar sem precisar de
  `INotifyPropertyChanged`.
- `dotnet build`: 0 erros. `dotnet test`: 131 testes, 131 aprovados.
- Nenhum commit feito — aguardando autorização. Validação visual não foi feita — pedido ao
  usuário testar na tela "Treinamento do Controlador" (iniciar treinamento ou rodar 1 episódio,
  arrastar a divisória da tabela de histórico, observar os cones girando atrás dos alvos).

---

## 2026-10-23 — Diagrama interativo da rede neural (visualização + dúvidas respondidas)

**Contexto**: antes do pedido abaixo, o usuário fez três perguntas sobre o treinamento
(tamanho da população = 60; o que o JSON exportado contém; o que são "melhor fitness" e
"evolução do fitness") — respondidas em texto, sem alteração de código.

**Pedido (texto integral):**

> Quero visulizar essa rede neural enquanto as gerações forem rodando.
> "C:\Users\mathe\Downloads\Neural_network.svg" próximo deste modelo e ao passar o mouse por
> cima quero que mostre qual tomada de atitude aquele neurônio é responsável

**Entregue:**

- `NeuralEvolution/RedeNeural.cs`: novos métodos de só-leitura para visualização —
  `AtivarComCamadas` (ativação de TODAS as camadas, não só a saída final) e `Peso`/
  `TamanhosDasCamadas`/`QuantidadeDeCamadasDePesos` (acesso aos pesos). `Ativar` (usado no laço
  de treinamento/avaliação) não foi tocado — o custo extra de alocar cada camada intermediária
  só existe quando a tela pede.
- `ControladorHibrido.RotulosEntradas`/`RotulosSaidas`: nomes fixos das 6 entradas e 2 saídas,
  reaproveitados tanto no diagrama quanto (implicitamente) em `ExtratorDeRegras`.
- `ViewModels/NeuronioVisualTreinamento.cs` e `ConexaoVisualTreinamento.cs` (novos): nó e
  conexão do diagrama, no mesmo padrão dos outros visuais da tela (mutados no lugar, sem
  recriar a coleção).
- `TreinamentoControladorViewModel`: `ConstruirDiagramaRedeNeural` monta o layout (3 colunas —
  entrada/oculta/saída — nas posições da arquitetura `[6,8,2]`) uma única vez;
  `AtualizarDiagramaRedeNeural` recalcula cor/espessura de cada conexão (peso atual: azul
  positivo, vermelho negativo, espessura ∝ |peso|) e ativação/tooltip de cada neurônio a cada
  geração concluída (e ao rodar um episódio único) — propagando um **cenário de referência
  fixo** (torre do próprio quadrante, alvo a meia distância e velocidade média) para que o que
  muda no diagrama geração a geração sejam os PESOS evoluindo, não ruído de cenários diferentes.
- **Tooltip por neurônio** (o pedido central): entrada mostra seu nome + valor no cenário de
  referência; saída mostra seu nome + ativação + o que ela realmente faz no sistema; neurônio
  oculto mostra a ativação atual e, por não ter um "significado" fixo (é uma matriz de pesos,
  não uma regra simbólica), as 2 entradas que mais o influenciam por peso — uma aproximação
  honesta em vez de inventar um rótulo que a rede não tem.
- XAML inspirado no SVG de referência (`C:\Users\mathe\Downloads\Neural_network.svg`): nós
  verdes (entrada) → azuis (oculta) → amarelos (saída), conexões como linhas finas, tudo dentro
  de um novo card acima do Canvas de simulação.
- Testes novos em `RedeNeuralTests` para `AtivarComCamadas`/`Peso`/`TamanhosDasCamadas`.
  `dotnet build`: 0 erros. `dotnet test`: 135 testes, 135 aprovados.
- Nenhum commit feito — aguardando autorização. Validação visual não foi feita — pedido ao
  usuário abrir a tela e passar o mouse sobre os neurônios para confirmar que o layout e os
  tooltips ficaram como esperado.

---

## 2026-10-23 — Botão de reset e histórico de gerações minimizável/maximizável

**Pedido (texto integral):**

> Coloque um botão de reset, na tela de treinamento do controlador.
>
> ajusta a tela histórico de gerações para minimizar e maximizar

**Entregue:**

- **Botão "Resetar treinamento"**: `TreinamentoControladorViewModel.Resetar` — para o
  treinamento/prévia se estiverem rodando e zera geração, fitness, genoma atual, diagrama da
  rede, Canvas (alvos/miras/regras aprendidas). **Decisão deliberada**: não apaga o histórico
  já salvo em `historico_treinamento_geracoes` — reset é da sessão na tela, não uma limpeza de
  dados persistidos; o botão tem tooltip explicando isso. Se quiser um botão separado para
  também limpar o histórico do banco, é só pedir.
- **Histórico de gerações minimizável/maximizável**: botão "▾ Minimizar"/"▸ Maximizar" no
  cabeçalho do card — minimizado, o card encolhe para só o título (36px) e a tabela fica
  oculta; maximizado, volta para a altura que estava antes de minimizar (lembrada, padrão
  220px). A divisória arrastável (`GridSplitter`, adicionada na rodada anterior) é desativada
  enquanto minimizado. Evento novo `TreinamentoControladorViewModel.Resetado` avisa a View para
  também limpar o histórico bruto do gráfico de evolução do fitness quando o reset é acionado.
- `dotnet build`: 0 erros. `dotnet test`: 135 testes, 135 aprovados (sem testes novos — mudança
  é só de UI/code-behind, sem lógica nova em classes testáveis isoladamente).
- Nenhum commit feito — aguardando autorização. Validação visual não foi feita.

---

## 2026-10-23 — Correção: cone de visão não acompanhava o alvo

**Relato do usuário:**

> Ainda não testei mas visualizar o treinamento do controlador, não estou vendo o cone de
> visão das torres acompanharem os alvos

**Causa**: `SetorVisualTreinamento.Pontos` era mutado NO LUGAR (`Clear()`+`Add()` na mesma
`PointCollection`) a cada quadro, em vez de a propriedade ser substituída — mutar uma
`PointCollection` já atribuída a um `Polygon` via `{Binding}` não garante redesenho (o
`Polygon` só reage de forma confiável quando a propriedade é efetivamente trocada, disparando
`PropertyChanged`). Por isso o cone aparecia na posição inicial (desenhado uma vez, ao montar o
episódio) mas nunca girava atrás do alvo depois. O próprio gráfico de evolução do fitness,
nesta mesma tela, já evitava esse padrão trocando a coleção inteira a cada redesenho — o cone
de visão foi a única peça que ficou com a versão "mutar no lugar".

**Correção**: `SetorVisualTreinamento` passou a herdar de `ViewModelBase`
(`INotifyPropertyChanged`) com `Pontos` como propriedade normal (`SetProperty`);
`PreencherPontosDoSetor` agora monta uma `PointCollection` nova e a atribui via `setor.Pontos =
pontos` em vez de mutar a existente.

- `dotnet build`: 0 erros. `dotnet test`: 135 testes, 135 aprovados.
- Nenhum commit feito — aguardando autorização. Pedido ao usuário testar de novo.

---

## Ver também

- [[CONTEXTO_PROJETO]] — resumo atual do projeto (consequência acumulada destes pedidos).
- [[INSTALADOR]] — detalhamento técnico do processo de instalação Windows.
- [[ETAPA1_FUNDACAO]] — detalhamento técnico da fundação multiusuário.
- [[IDENTIDADE_VISUAL]] — tokens de cor, tipografia e ícones da nova identidade visual.
