# Limitações Conhecidas e Divergências entre Especificação e Implementação

Este documento reúne dois tipos de conteúdo que **não** devem aparecer como requisito atendido
em `Requisitos_de_Sistema/Requisitos_RadarTorres.pdf`:

1. **Limitações conhecidas** — comportamento que o sistema deveria ter, mas ainda não tem.
2. **Divergências** — pontos em que a especificação revisada (o que o sistema *deveria* fazer)
   ainda não bate com o que o código *hoje* faz. Nenhuma mudança de código foi feita para
   "empurrar" o comportamento atual até bater artificialmente com a especificação — isso fica
   para uma tarefa separada, com autorização explícita.

## Limitações conhecidas

**L01 — Reconexão automática após queda de conexão serial não implementada** *(ex-RNF29)*
Hoje a reconexão após queda de cabo/porta é manual — o usuário precisa clicar em "Conectar"
novamente. A configuração `SerialSettings.ReconnectAttempts` já existe em `appsettings.json`,
expressando a intenção do requisito, mas nenhuma lógica de retry automático foi implementada
ainda. **Evidência:** `appsettings.json`, `Docs/Tecnica/DOCUMENTACAO_TECNICA.md` (seção "Limitações e
próximos passos").

**L02 — (Resolvida) Telas de consulta/gestão que estavam pendentes**
Não há mais nenhuma tela em `PlaceholderView` no sistema — essa classe não existe mais no
código-fonte. **Ações realizadas**, **Histórico de modos** e **Usuários** (RF15–RF17) são telas
implementadas e roteadas normalmente em `Services/NavigationService.cs`
(`AcoesRealizadasView`/`ViewModel`, `HistoricoModosView`/`ViewModel`, `UsuariosView`/`ViewModel`).
O item de menu "Configurações" (genérico, distinto da aba "Configurações do Arduino") foi
removido do sistema — não existe mais nem como entrada de menu (`Services/IPermissionService.cs`,
enum `MenuItem`, não tem esse valor), nem como placeholder. O módulo de Chamados de Ajuda também
foi removido por completo, então não há mais item de menu associado a ele. Não há, no momento,
nenhuma limitação conhecida desse tipo. **Evidência:** `Services/NavigationService.cs`,
`Services/IPermissionService.cs`.

**L03 — Bugs conhecidos não corrigidos**
Já catalogados em `Docs/Tecnica/DOCUMENTACAO_TECNICA.md` (seção "Limitações e próximos passos"), não
duplicados aqui: (1) `App.OnDispatcherUnhandledException` sem trava de reentrância pode causar
`StackOverflowException`; (2) falha nativa de renderização de texto (DirectWrite) observada só
em ambiente de automação sem desktop interativo real, não confirmada em uso normal.

## Divergências entre especificação revisada e implementação atual

**D1 — RF06/RF08: comando de acionamento manual ainda não restrito por modo de operação**

A especificação revisada (RF06, RF08) define acionamento **exclusivamente automático** no modo
Vermelho, e três estados de operação (Verde/Amarelo/Vermelho). O enum `SystemMode`
(`Models/SystemState.cs`) já reflete corretamente esses três estados — `LigadoApenas` (Verde),
`AcompanharAlvos` (Amarelo) e `Disparar` (Vermelho) —, então esse ponto específico já está
resolvido. A divergência que permanece é outra:

* O código ainda expõe um comando de **acionamento manual**
  (`MainViewModel.ManualFireCommand` → `ManualFireAsync` →
  `FireControlService.TryFireAsync(..., OrigemAcao.Manual)`), habilitado sempre que há um alvo
  selecionado e o perfil satisfaz `PodeExecutarAcoes` — **sem checagem do `SystemMode` atual**,
  incluindo modos onde a especificação revisada não permite acionamento algum (o equivalente a
  "Amarelo"/`AcompanharAlvos`). `FireControlService.Authorize` também não verifica `SystemMode`
  — só checa alvo ativo, zona morta, torre selecionada e distância mínima. Ou seja, hoje é
  tecnicamente possível disparar manualmente mesmo em um modo que só deveria acompanhar o alvo.

**Nenhum código foi alterado nesta revisão.** Esta divergência é só documentada, conforme
solicitado — a remoção do caminho de acionamento manual (ou sua restrição ao modo `Disparar`) e
a adição da checagem de modo em `Authorize` ficam para uma tarefa de implementação separada, a
ser autorizada explicitamente.
