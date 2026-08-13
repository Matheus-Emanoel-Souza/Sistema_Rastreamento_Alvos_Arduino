# Contexto do Projeto — RadarTorres (TCC)

> Documento-resumo pensado para ser colado/anexado em **outra IA** (ChatGPT, Gemini, uma nova
> sessão do Claude, etc.) como contexto rápido do projeto, sem precisar enviar o código-fonte
> completo. Cobre: a ideia, como foi construído, o estado atual e o que falta. Para detalhes
> técnicos linha a linha, os links para `Docs/*.md` no final apontam para as fontes completas.
>
> Última atualização: 2026-08-12.

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
4. Permite um modo de acionamento **demonstrativo** (laser de baixa potência / LED /
   simulação — **nunca armamento real**), respeitando uma distância mínima de segurança.
5. Funciona de ponta a ponta mesmo **sem Arduino conectado**, via modo de simulação embutido
   (essencial para desenvolvimento e demonstrações do TCC).

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
| Persistência | **CSV** (`%AppData%\RadarTorres\Data\*.csv`) | Decisão explícita do usuário: por ora CSV, sinalizado (`TODO(SQL)`) para futura migração a banco relacional (SQLite/EF Core) sem alterar ViewModels/Services — cada tabela já tem uma interface de repositório dedicada |
| Empacotamento | Inno Setup 6 (`installer/RadarTorres.iss`) | Instalador `Setup.exe`, self-contained (embute o .NET 9 Desktop Runtime — usuário final não precisa instalar nada) |
| Firmware de teste | Arduino (`Arduino/ArduinoSimulation.ino`) | Sem sensores reais, só para desenvolvimento |

### 2.2 Arquitetura (camadas)

```
Views (WPF/XAML)  <-->  ViewModels (MainViewModel, ...)  <-->  Services (regras de negócio)
                                                                       |
                                                                Models (entidades)
```

- **Views**: só XAML + pequenos encaminhamentos de eventos de UI.
- **ViewModels**: orquestram serviços, sem regra de negócio própria.
- **Services**: 100% da lógica (protocolo serial, rastreamento, seleção de torre,
  acionamento, simulação, auth, permissões, i18n, tema, layout do painel). Nenhum referencia
  WPF diretamente — interfaces `I*Service` permitem trocar implementação (ex.: testes).
- **Models**: entidades simples com `INotifyPropertyChanged`.

### 2.3 Concorrência

Leitura serial roda em `Task.Run` dedicado; timers (`System.Threading.Timer` /
`DispatcherTimer`) cuidam de watchdog de conexão, simulação e expiração de alvos. Regra geral:
qualquer classe com coleção/evento vinculado à UI é responsável por despachar para o
`Dispatcher` internamente (captura o Dispatcher no construtor).

### 2.4 Como o app evoluiu (linha do tempo funcional)

1. **Base inicial**: app único (`MainWindow`), lógica de radar/serial/torres/simulação.
2. **Instalador Windows**: `Setup.exe` (Inno Setup, self-contained), launcher avulso, ícone,
   tratamento global de erros de inicialização.
3. **Fundação multiusuário** (Etapa 1, parte A): login, 3 perfis (Administrador/Operador/
   Visualizador), hash de senha (PBKDF2-HMACSHA256), permissões, i18n (pt-BR/en-US), tema
   claro/escuro/sistema, Shell (barra superior + barra lateral + navegação), painel principal
   com indicadores, auditoria (`objetos_detectados`, `acoes_realizadas`, `alteracoes_modo`),
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

---

## 3. Estado atual (o que já funciona)

- Radar em tempo real, seleção automática de torre, modo de acionamento demonstrativo, modo
  de simulação sem hardware — funcionalidade original, validada.
- Login multiusuário, 3 perfis, troca de senha, auditoria de ações/modos/detecções gravada em
  CSV.
- Internacionalização (pt-BR/en-US) e tema (claro/escuro/sistema) trocáveis em runtime.
- Painel principal com cards de indicadores, arrastáveis/redimensionáveis, layout persistido
  por usuário e responsivo a mudanças de tamanho de janela.
- Aba de Configurações do Arduino: detectar `arduino-cli`, compilar sketch, monitor serial.
- Instalador Windows completo (`Setup.exe`), self-contained, com upgrade preservando
  configurações do usuário.
- 21 testes automatizados (xUnit) cobrindo a aba do Arduino CLI.
- **Todas as branches de trabalho consolidadas na `main`** — é o branch canônico agora.

### Telas ainda "em construção" (placeholder, navegação já funciona)

- Objetos detectados (tabela com filtros/paginação/gráfico) — **pendente**.
- Ações realizadas (tabela de auditoria, somente leitura) — **pendente**.
- Histórico de modos (tabela de auditoria, somente leitura) — **pendente**.
- Usuários (CRUD completo, restrito a Administrador) — **pendente**.

### Bugs conhecidos (documentados em `Docs/DOCUMENTACAO_TECNICA.md`, seção "Bugs conhecidos")

1. `App.OnDispatcherUnhandledException` sem trava de reentrância — se o próprio `MessageBox`
   de erro lançar uma exceção, pode entrar em loop até estourar a pilha (`StackOverflowException`).
2. Falha nativa de renderização de texto (`DirectWrite`) observada uma vez em ambiente de
   automação (sem confirmação se ocorre em uso normal/desktop interativo real).

---

## 4. O que falta / próximos passos (roadmap)

Conforme plano em `Docs/ETAPA1_FUNDACAO.md`, seção 6:

- **Fechar a Etapa 1**: implementar as 4 telas de dados completas listadas acima
  (Objetos detectados, Ações realizadas, Histórico de modos, Usuários), reaproveitando a
  fundação já pronta (repositórios, permissões, i18n, tema, navegação).
- **Etapa 2**: indicadores gráficos avançados, personalização completa de layout, filtros
  avançados, exportação CSV, notificações.
- **Etapa 3 ("Qualidade")**: testes automatizados mais amplos, revisão de segurança, ajustes
  de desempenho.
- **Migração de persistência**: CSV → banco relacional (SQLite/EF Core) quando fizer sentido —
  interfaces de repositório já preparadas para isso (`TODO(SQL)` marcado no código).
- **Upload/gravação de firmware** pelo Arduino CLI (hoje só compila) — próximo passo natural
  da aba de Configurações do Arduino, mas só deve ser implementado mediante nova consulta ao
  usuário.
- Corrigir os dois bugs conhecidos listados acima.
- Validação manual (interativa, em desktop real) do arraste/redimensionamento dos cards do
  painel principal — só foi validada por build + testes automatizados, não por clique manual.

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
  `Docs/LOG_SOLICITACOES.md` (pedido + resumo do entregue) — não substituir entradas
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
| [`Docs/ARQUITETURA.md`](ARQUITETURA.md) | Decisões arquiteturais e diagramas (mermaid) |
| [`Docs/DOCUMENTACAO_TECNICA.md`](DOCUMENTACAO_TECNICA.md) | Referência de cada classe/serviço, limitações e bugs conhecidos |
| [`Docs/COMUNICACAO_ARDUINO.md`](COMUNICACAO_ARDUINO.md) | Protocolo serial completo |
| [`Docs/ALGORITMO_SELECAO_TORRE.md`](ALGORITMO_SELECAO_TORRE.md) | Matemática do radar e da seleção de torres |
| [`Docs/INSTALADOR.md`](INSTALADOR.md) | Processo de criação do instalador |
| [`Docs/ETAPA1_FUNDACAO.md`](ETAPA1_FUNDACAO.md) | Fundação multiusuário: arquitetura, tabelas, validação, roadmap |
| [`Docs/MODELO_DADOS.md`](MODELO_DADOS.md) | Schema das tabelas CSV, relacionamentos, plano de migração SQL |
| [`Docs/LOG_SOLICITACOES.md`](LOG_SOLICITACOES.md) | Histórico cronológico completo de todos os pedidos feitos à IA |
