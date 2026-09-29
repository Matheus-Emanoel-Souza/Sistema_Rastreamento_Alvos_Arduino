# Contexto do Projeto — RadarTorres (TCC)

> Documento-resumo pensado para ser colado/anexado em **outra IA** (ChatGPT, Gemini, uma nova
> sessão do Claude, etc.) como contexto rápido do projeto, sem precisar enviar o código-fonte
> completo. Cobre: a ideia, como foi construído, o estado atual e o que falta. Para detalhes
> técnicos linha a linha, os links para `Docs/*.md` no final apontam para as fontes completas.
>
> Última atualização: 2026-08-30.

---

## 1. O que é o projeto

**RadarTorres** é um aplicativo desktop **C#/WPF (.NET 9)** desenvolvido como base de
**Trabalho de Conclusão de Curso (TCC) em Engenharia da Computação**, por Matheus Emanoel
Souza.

O software se comunica via porta serial (USB) com um **Arduino** responsável por sensores de
detecção de alvos ao redor de uma base. Ele:

1. Recebe leituras de sensores (ângulo + distância) via protocolo texto simples pela serial.
2. Converte cada leitura em posição cartesiana e exibe em tempo real num radar circular
   dividido em 4 quadrantes.
3. Seleciona automaticamente, entre um conjunto configurável de torres demonstrativas
   posicionadas ao redor da base, qual está mais próxima/adequada para cada alvo.
4. Permite um modo de acionamento **demonstrativo** (laser de baixa potência / LED —
   **nunca armamento real**), respeitando uma distância mínima de segurança.

Repositório: `Matheus-Emanoel-Souza/Sistema_Rastreamento_Alvos_Arduino` (GitHub).

---

## 2. Como foi construído

### 2.1 Stack e decisões técnicas

| Camada | Escolha | Por quê |
|---|---|---|
| Linguagem/Runtime | C# 13 / .NET 9 | — |
| Interface | WPF | Data-binding real, gráficos vetoriais 2D (radar), MVVM natural, bom desempenho de redesenho em tempo real |
| MVVM | Implementado à mão (`ViewModelBase`, `RelayCommand`, ~60 linhas), sem framework externo (Prism/CommunityToolkit.Mvvm) | Projeto didático (TCC) — mantém o mecanismo de binding 100% explicável na defesa |
| Comunicação serial | `System.IO.Ports` | — |
| Configuração | `Microsoft.Extensions.Configuration` + `appsettings.json` | Torres, portas, distâncias configuráveis sem recompilar |
| Injeção de dependência | `Microsoft.Extensions.DependencyInjection`, composition root em `App.xaml.cs` | — |
| Persistência | **SQLite** (`%AppData%\RadarTorres\Data\radartorres.db`) | Migrada de CSV para SQLite — DI em `App.xaml.cs:141-146` registra `Sqlite*Repository` para as 5 tabelas; falhas de escrita são enfileiradas em `pending-writes.sql` e reaplicadas no próximo start. Único uso de CSV remanescente é o botão manual "Exportar CSV" da tela de Objetos Detectados |
| Empacotamento | Inno Setup 6 (`installer/RadarTorres.iss`) | Instalador `Setup.exe`, self-contained (embute o .NET 9 Desktop Runtime — usuário final não precisa instalar nada) |

### 2.2 Arquitetura (camadas)

```
Views (WPF/XAML)  <-->  ViewModels (MainViewModel, ...)  <-->  Services (regras de negócio)
                                                                       |
                                                                Models (entidades)
```

- **Views**: só XAML + pequenos encaminhamentos de eventos de UI.
- **ViewModels**: orquestram serviços, sem regra de negócio própria.
- **Services**: 100% da lógica (protocolo serial, rastreamento, seleção de torre,
  acionamento, auth, permissões, i18n, tema, layout do painel). Nenhum referencia
  WPF diretamente — interfaces `I*Service` permitem trocar implementação (ex.: testes).
- **Models**: entidades simples com `INotifyPropertyChanged`.

### 2.3 Concorrência

Leitura serial roda em `Task.Run` dedicado; timers (`System.Threading.Timer` /
`DispatcherTimer`) cuidam de watchdog de conexão e expiração de alvos. Regra geral:
qualquer classe com coleção/evento vinculado à UI é responsável por despachar para o
`Dispatcher` internamente (captura o Dispatcher no construtor).

### 2.4 Como o app evoluiu (linha do tempo funcional)

1. **Base inicial**: app único (`MainWindow`), lógica de radar/serial/torres.
2. **Instalador Windows**: `Setup.exe` (Inno Setup, self-contained), launcher avulso, ícone,
   tratamento global de erros de inicialização.
3. **Fundação multiusuário** (Etapa 1, parte A): login, 3 perfis (Administrador/Operador/
   Visualizador), hash de senha (PBKDF2-HMACSHA256), permissões, i18n (pt-BR/en-US), tema
   claro/escuro/sistema, Shell (barra superior + barra lateral + navegação), painel principal
   com indicadores, auditoria (`objetos_detectados`, `acoes_realizadas`, `modo_atual_torre`),
   persistência CSV. `MainWindow` virou `MonitoramentoView`, um item de menu dentro da Shell.
4. **Aba "Configurações do Arduino"**: detecção do `arduino-cli`, compilação de sketch `.ino`
   assíncrona/cancelável (via `Process`/`ArgumentList`, nunca shell), monitor serial reaproveitando
   a mesma conexão da tela de Monitoramento (sem duas portas concorrentes). Primeiro projeto de
   testes automatizados do repositório (`tests/RadarTorres.Tests`, xUnit, 21 testes).
5. **Painel principal com layout personalizável**: cards arrastáveis/redimensionáveis
   (`DashboardCanvas`/`DashboardCard`, WPF puro, sem lib de terceiros), posição/tamanho
   guardados como fração (0..1) do canvas (responsivo), anticolisão por rejeição, persistidos
   por usuário em JSON.
6. **Consolidação de branches (2026-08-12)**: as branches `Sistema`, `TESTE` e `homologacao`
   foram todas mescladas na `main` (uma por vez, com merge commit, histórico preservado). Um
   conflito real em `README.md` (a `main` havia excluído o arquivo numa limpeza antiga; a
   `Sistema` o reescreveu por completo, unificando com `README_LOCAL.md`) foi resolvido
   mantendo a versão reescrita da `Sistema`, por decisão do usuário. Build (0 erros/avisos) e
   os 21 testes automatizados validados após o merge. `Sistema` e `homologacao` foram
   excluídas (local + remoto); `main` e `TESTE` permanecem.
7. **Zonas mortas** (via branch `TESTE`, mesclada em `main`): modelo de domínio `ZonaMorta`,
   persistência em JSON único da instalação, serviço de negócio que avalia zonas ativas e
   bloqueia seleção de torre/disparo dentro delas, gestão restrita ao perfil Administrador,
   criação por clique/arraste direto no radar, card dedicado "ZONAS MORTAS" com sombreamento
   visual. Ajustes de UX no mesmo ciclo: torres desenhadas como quadrado no radar, console de
   eventos fixado na faixa lateral direita.
8. **Tela de Objetos Detectados** (via branch `Tela_de_logs`, mesclada em `main`): a tela sai
   do placeholder e vira uma tabela real (ViewModel + View), com serviço de
   exportação/importação em CSV, XML e PDF.
9. **Formulário de Chamado de Ajuda**: `ChamadoAjuda` (model), `CsvChamadoAjudaRepository`,
   `HelpDeskFormViewModel`/`HelpDeskFormWindow`, acionado pela TopBar — permite abrir um
   ticket. Era só o lado de *criação*; nunca ganhou tela de listagem/gestão — o módulo inteiro
   foi **removido** do projeto em etapa posterior (ver seção 3, "Módulo removido").
10. **Levantamento de engenharia de software (2026-08-20)**: `Docs/Documentos_Entregaveis/Diagramas_e_requisitos/`
    adicionado por análise do código-fonte e da documentação existente — diagrama de classes,
    diagrama de pacotes, DER atual (CSV) e proposto (SQL), Requisitos Funcionais (RF01–RF32),
    Requisitos Não Funcionais (RNF01–RNF30) e matriz de rastreabilidade requisito → arquivo/
    classe/função. Pontos não confirmáveis por código foram sinalizados como inferência no
    próprio documento (ver `Docs/Documentos_Entregaveis/Diagramas_e_requisitos/README.md`).

---

## 3. Estado atual (o que já funciona)

- Radar em tempo real, seleção automática de torre, modo de acionamento demonstrativo —
  funcionalidade original, validada.
- Login multiusuário, 3 perfis, troca de senha, auditoria de ações/modos/detecções gravada em
  SQLite.
- Internacionalização (pt-BR/en-US) e tema (claro/escuro/sistema) trocáveis em runtime.
- Painel principal com cards de indicadores, arrastáveis/redimensionáveis, layout persistido
  por usuário e responsivo a mudanças de tamanho de janela.
- Aba de Configurações do Arduino: detectar `arduino-cli`, compilar sketch, monitor serial.
- Instalador Windows completo (`Setup.exe`), self-contained, com upgrade preservando
  configurações do usuário.
- `dotnet build`: 0 erros/avisos. O projeto `tests/RadarTorres.Tests` (xUnit, cobria a aba do
  Arduino CLI) foi removido do repositório — atualmente **não há projeto de testes
  automatizados**.
- **Todas as branches de trabalho consolidadas na `main`** — é o branch canônico agora.
- Zonas mortas: criação por clique/arraste no radar, bloqueio de torre/disparo, restrito a
  Administrador (ver item 7 da linha do tempo).
- Objetos detectados: tela de tabela real, com exportar/importar (CSV/XML/PDF).
- Ações realizadas: tela de tabela real (auditoria, somente leitura) — `AcoesRealizadasView`/
  `AcoesRealizadasViewModel` implementados e roteados.
- Histórico de modos: tela de tabela real (auditoria, somente leitura) — `HistoricoModosView`/
  `HistoricoModosViewModel` implementados e roteados.
- Usuários: CRUD completo (criar/editar/inativar/resetar senha), restrito a Administrador —
  `UsuariosView`/`UsuariosViewModel` implementados e roteados.

### Módulo removido

O módulo `ChamadoAjuda` (model, ViewModel, repositório, tela de formulário e entrada de menu na
TopBar) foi **removido inteiramente** do projeto — não existe mais nenhuma referência a ele em
`src/`.

### Telas ainda "em construção" (placeholder, navegação já funciona)

- Configurações — ainda não há um item de menu genérico "Configurações" planejado; o único
  módulo de configuração implementado hoje é a aba "Configurações do Arduino"
  (`MenuItem.ConfiguracoesArduino`, já concluída — ver linha "Aba de Configurações do Arduino"
  acima).

### Bugs conhecidos (documentados em `Docs/Tecnica/DOCUMENTACAO_TECNICA.md`, seção "Bugs conhecidos")

1. `App.OnDispatcherUnhandledException` sem trava de reentrância — se o próprio `MessageBox`
   de erro lançar uma exceção, pode entrar em loop até estourar a pilha (`StackOverflowException`).
2. Falha nativa de renderização de texto (`DirectWrite`) observada uma vez em ambiente de
   automação (sem confirmação se ocorre em uso normal/desktop interativo real).

---

## 4. O que falta / próximos passos (roadmap)

Conforme plano em `Docs/Projeto/ETAPA1_FUNDACAO.md`, seção 6:

- ~~Fechar a Etapa 1: implementar as 4 telas de dados completas~~ — **concluído**: Ações
  realizadas, Histórico de modos e Usuários já estão implementadas (ver seção 3); o módulo de
  Chamados de Ajuda foi removido do projeto em vez de ganhar tela de listagem.
- **Etapa 2**: indicadores gráficos avançados, personalização completa de layout, filtros
  avançados, notificações.
- **Etapa 3 ("Qualidade")**: recriar um projeto de testes automatizados (o anterior,
  `tests/RadarTorres.Tests`, foi removido), revisão de segurança, ajustes de desempenho.
- ~~Migração de persistência: CSV → banco relacional~~ — **concluído**: persistência é SQLite
  (ver seção 2.1, item "Persistência").
- **Upload/gravação de firmware** pelo Arduino CLI (hoje só compila) — próximo passo natural
  da aba de Configurações do Arduino, mas só deve ser implementado mediante nova consulta ao
  usuário.
- Corrigir os dois bugs conhecidos listados acima.
- Validação manual (interativa, em desktop real) do arraste/redimensionamento dos cards do
  painel principal — só foi validada por build, não por clique manual (não há mais testes
  automatizados no projeto).

---

## 5. Como trabalhar neste projeto (convenções e preferências do usuário)

Relevante para qualquer IA/assistente que for continuar o trabalho:

- **Analisar antes de codar**: em pedidos grandes/ambíguos, apresentar um plano ou fazer
  perguntas de esclarecimento *antes* de alterar código — especialmente em decisões que
  afetam arquitetura ou dados existentes.
- **Nunca resolver conflitos de merge sozinho**: parar e perguntar ao usuário qual versão
  manter.
- **Nunca commitar/dar push sem autorização explícita** do usuário na sessão.
- **Nunca usar comandos destrutivos** (`git push --force`, `reset --hard` etc.) sem consultar
  antes.
- **Documentar cada sessão**: toda sessão de trabalho relevante deve ganhar uma entrada em
  `Docs/Projeto/LOG_SOLICITACOES.md` (pedido + resumo do entregue) — não substituir entradas
  anteriores, só acrescentar.
- **Não quebrar funcionalidade existente**: mudanças devem ser aditivas quando possível;
  qualquer remoção/alteração de comportamento já existente deve ser sinalizada.
- **Validar de ponta a ponta**: `dotnet build` (0 erros/avisos) e `dotnet test` antes de
  considerar uma entrega concluída; documentar quando a validação visual/manual ficar
  pendente (ex.: ambiente sem display interativo).
- O repositório é aberto como **vault do Obsidian** (`.obsidian/` na raiz) — arquivos podem
  ser renomeados/tocados por fora do Git enquanto uma sessão está em andamento; se isso
  acontecer, identificar via `git status` e perguntar como proceder antes de seguir.

---

## 6. Onde encontrar mais detalhes

| Documento | Conteúdo |
|---|---|
| [`README.md`](../README.md) | Visão geral, instalação, uso, estrutura de pastas |
| [`Docs/Tecnica/ARQUITETURA.md`](../Tecnica/ARQUITETURA.md) | Decisões arquiteturais e diagramas (mermaid) |
| [`Docs/Tecnica/DOCUMENTACAO_TECNICA.md`](../Tecnica/DOCUMENTACAO_TECNICA.md) | Referência de cada classe/serviço, limitações e bugs conhecidos |
| [`Docs/Tecnica/COMUNICACAO_ARDUINO.md`](../Tecnica/COMUNICACAO_ARDUINO.md) | Protocolo serial completo |
| [`Docs/Tecnica/ALGORITMO_SELECAO_TORRE.md`](../Tecnica/ALGORITMO_SELECAO_TORRE.md) | Matemática do radar e da seleção de torres |
| [`Docs/Projeto/INSTALADOR.md`](INSTALADOR.md) | Processo de criação do instalador |
| [`Docs/Projeto/ETAPA1_FUNDACAO.md`](ETAPA1_FUNDACAO.md) | Fundação multiusuário: arquitetura, tabelas, validação, roadmap |
| [`Docs/Tecnica/MODELO_DADOS.md`](../Tecnica/MODELO_DADOS.md) | Schema das tabelas CSV, relacionamentos, plano de migração SQL |
| [`Docs/Documentos_Entregaveis/Diagramas_e_requisitos/`](../Documentos_Entregaveis/Diagramas_e_requisitos/README.md) | RF01–RF32, RNF01–RNF30, diagrama de classes, de pacotes, DER atual/proposto, matriz de rastreabilidade |
| [`Docs/Projeto/LOG_SOLICITACOES.md`](LOG_SOLICITACOES.md) | Histórico cronológico dos pedidos feitos à IA (pode não refletir ainda as sessões mais recentes, ex.: migração SQLite, remoção do `ChamadoAjuda`, remoção dos testes) |
