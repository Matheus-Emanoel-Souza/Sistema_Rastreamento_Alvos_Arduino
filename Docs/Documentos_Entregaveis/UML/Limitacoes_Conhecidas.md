# Limitações Conhecidas e Divergências entre Especificação e Implementação

Este documento reúne dois tipos de conteúdo que **não** devem aparecer como requisito atendido
em `Requisitos_Funcionais.md`/`Requisitos_Nao_Funcionais.md`:

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

**L02 — Telas de consulta/gestão ainda pendentes**
Cinco itens de menu navegam para uma tela "em construção" (`PlaceholderView`) hoje: **Ações
realizadas** e **Histórico de modos** (o registro já é gravado — RF16/RF17 — só falta a tela de
consulta), **Usuários** e **Gestão/listagem de chamados de ajuda** (nem o registro tem
interface de gestão — RF18/RF23), e **Configurações** (nunca chegou a ser escopado).
**Evidência:** `Services/NavigationService.cs`, `Docs/Projeto/CONTEXTO_PROJETO.md`, seção 3.

**L03 — Bugs conhecidos não corrigidos**
Já catalogados em `Docs/Tecnica/DOCUMENTACAO_TECNICA.md` (seção "Limitações e próximos passos"), não
duplicados aqui: (1) `App.OnDispatcherUnhandledException` sem trava de reentrância pode causar
`StackOverflowException`; (2) falha nativa de renderização de texto (DirectWrite) observada só
em ambiente de automação sem desktop interativo real, não confirmada em uso normal.

## Divergências entre especificação revisada e implementação atual

**D1 — RF06/RF08: modelo de 3 estados (Verde/Amarelo/Vermelho) ainda não implementado no código**

A especificação revisada (RF06, RF08) define acionamento **exclusivamente automático** no modo
Vermelho, e três estados de operação (Verde/Amarelo/Vermelho). O código hoje:

* Usa um enum `SystemMode` com **6 valores** (`Off`, `LocationOnly`, `LocationAutoTower`,
  `LocationAutoFire`, `Maintenance`, `Emergency`) — não os 3 estados conceituais da
  especificação. `LocationAutoTower` é o mais próximo de "Amarelo" e `LocationAutoFire` o mais
  próximo de "Vermelho", mas não há um valor único claramente equivalente a "Verde" (ligado,
  porém sem operação funcional) — o candidato mais próximo seria `LocationOnly`, que na
  descrição atual já mostra alvos no radar (portanto tem alguma operação funcional visível,
  diferente do "Verde" da especificação).
* Expõe um comando de **acionamento manual** (`MainViewModel.ManualFireCommand` →
  `ManualFireAsync` → `FireControlService.TryFireAsync(..., OrigemAcao.Manual)`), disponível
  sempre que `CurrentMode != SystemMode.Off`, **incluindo modos onde a especificação revisada
  não permite acionamento algum** (o equivalente a "Amarelo"). `FireControlService.Authorize`
  também não verifica `SystemMode` — só checa alvo ativo, zona morta, torre selecionada e
  distância mínima. Ou seja, hoje é tecnicamente possível disparar manualmente mesmo em um modo
  que só deveria acompanhar o alvo.

**Nenhum código foi alterado nesta revisão.** Esta divergência é só documentada, conforme
solicitado — o ajuste do enum `SystemMode`, a remoção do caminho de acionamento manual e a
adição da checagem de modo em `Authorize` ficam para uma tarefa de implementação separada, a
ser autorizada explicitamente.
