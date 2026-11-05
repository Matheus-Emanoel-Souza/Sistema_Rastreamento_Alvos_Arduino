Funcionalidade: define o design da interface do RadarTorres — identidade visual (cores,
tipografia, ícones) e diretriz de UX (padrão de ações e formulários).

# Design da Interface

> Consolida `IDENTIDADE_VISUAL.md` (tokens de cor, tipografia, ícones) e `DIRETRIZ_UX.md` (padrão
> de interação) em um único documento. `Guia_do_Site.md` (guia de build/deploy do site estático)
> tem finalidade distinta — operação de um subprojeto, não design — e por isso permanece separado
> em `Software/Design/Guia_do_Site.md` (ver nota em "Pendências" no fim deste documento).

## Parte 1 — Identidade visual

Referência viva da identidade visual do RadarTorres (tokens de cor, tipografia, ícones) — criada
na sessão de 2026-10-17 (ver [[HISTORICO_ALTERACOES]]) para documentar o que antes só existia no
pedido original em texto corrido. Atualize este documento sempre que um token, fonte ou ícone
mudar; não deixe o pedido original ser a única fonte de verdade.

### 1.1 Tokens de cor

`src/RadarTorres.App/Themes/Dark.xaml` e `Themes/Light.xaml` definem as mesmas chaves nos dois
temas — nenhuma tela sabe qual tema está ativo, todas consomem via `DynamicResource`. A troca em
runtime é feita por `ThemeService.ApplyTheme`, que substitui o `ResourceDictionary` mesclado em
`Application.Resources` (ver `App.xaml.cs`).

| Token | Uso | Escuro | Claro |
|---|---|---|---|
| `Bg.Window` | Fundo da janela/cabeçalho | `#0B1B1E` | `#F1F7F5` |
| `Bg.Sidebar` | Menu lateral, fundo do radar | `#102A2E` | `#E4F0EC` |
| `Bg.Card` | Cards, painéis, popups | `#15353A` | `#FFFFFF` |
| `Border.Card` | Borda de card (só visível no claro) | transparente | `#D3E5DE` |
| `Border.Divider` | Linhas de cabeçalho/menu/tabela | `#1E4045` | `#CFE3DC` |
| `Border.Control` | Borda de botão secundário/campo | `#2A5A5C` | `#A9CBC2` |
| `Selection.Subtle` | Item ativo do menu, hover | `#1A6F5C` 33% | `#12A87B` 20% |
| `Selection.Solid` | Avatar, seletor segmentado | `#1A6F5C` | `#BFE9DB` |
| `Text.Primary` | Texto principal | `#EEF7F3` | `#0F2A2E` |
| `Text.Secondary` | Texto secundário | `#B7D2CA` | `#35524D` |
| `Text.Muted` | Rótulos de card/tabela | `#8FB3AA` | `#4F716A` |
| `Text.Neutral` | Estado neutro, rótulo de grupo | `#6F948B` | `#5E827A` |
| `Accent.Fill` | Botão principal, torre, anéis do radar | `#2BE5A6` | `#12A87B` |
| `Accent.Text` | Links, item ativo, texto "ATIVO" | `#2BE5A6` | `#0B7A5A` |
| `OnAccent` | Texto sobre `Accent.Fill`/`Target.Fill`/`Danger.Fill` — **nunca branco fixo** | `#0B1B1E` | `#0B1B1E` |
| `Target.Fill` | Alvo, Modo Amarelo | `#FFAE1F` | `#FFAE1F` |
| `Warning.Text` | Texto âmbar | `#FFAE1F` | `#9A5A00` |
| `Danger.Fill` | Disparo, erro | `#FF5A4E` | `#D93B2F` |
| `Danger.Text` | Texto vermelho | `#FF8077` | `#B3261E` |
| `Radar.Quadrant1Fill` | Preenchimento permanente do quadrante Q1 | `#1A6F5C` 50% | `#12A87B` 28% |
| `Radar.Quadrant3Fill` | Preenchimento permanente do quadrante Q3 | `#134037` 90%\* | `#12A87B` 14% |

\* O valor "90%" do pedido original é a opacidade do token em si (a cor de base já é escura), não
uma mistura adicional — ver o `Color` com canal alfa em `Themes/Dark.xaml`.

**Regra de significado, não decorativa:** âmbar (`Target.Fill`/`Warning.Text`) = alvo ou Modo
Amarelo; mint (`Accent.*`) = torre, ação principal, estado OK; vermelho (`Danger.*`) = disparo.
Não reutilizar essas cores para outro propósito. O roxo que antes marcava torres foi removido por
completo.

### 1.2 Tipografia

Três famílias OFL (Google Fonts), embutidas como `Resource` em
`src/RadarTorres.App/Resources/Fonts/` (licenças em `*-OFL.txt` ao lado de cada fonte) e expostas
como `FontFamily` nomeadas em `Styles/Typography.xaml`:

| Recurso | Fonte | Fallback | Uso |
|---|---|---|---|
| `Font.Display` | Chakra Petch (Bold) | Segoe UI | Título de página (28px), logo "RadarTorres" (20px), "Q1..Q4" do radar (22px), "N alvos no raio" (24px) |
| `Font.Body` | IBM Plex Sans (variável, 400/500/600) | Segoe UI | Toda a interface: menus, botões, crachás, rótulos de card |
| `Font.Mono` | JetBrains Mono (variável, 400/500) | Consolas | Números/dados: valores grandes de card, posição/ângulo/distância, data/hora, rótulos de grupo do menu, rótulos de distância do radar, registro de eventos |

O fallback funciona nativamente: `FontFamily="./Resources/Fonts/#Chakra Petch, Segoe UI"` — se a
fonte embutida não carregar, o WPF cai para o segundo nome da lista sem nenhum código extra.

### 1.3 Ícones

`src/RadarTorres.App/Resources/Icons.xaml` — um `GeometryGroup`/`PathGeometry` por ícone
(`Icon.PainelPrincipal`, `Icon.Monitoramento`, `Icon.ObjetosDetectados`, `Icon.AcoesRealizadas`,
`Icon.HistoricoModos`, `Icon.Cameras`, `Icon.PerfisAlvo`, `Icon.ConfiguracoesArduino`,
`Icon.Usuarios`, `Icon.Configuracoes`, `Icon.LogoTorre`), todos grade 24x24, traço (sem
preenchimento), resolvidos em runtime a partir da chave string de `SidebarMenuEntry.IconKey` via
`Converters/ResourceKeyToGeometryConverter`. Mesclado estaticamente em `App.xaml` (não varia por
tema).

### 1.4 Shell

- `Views/Shell/SidebarView.xaml` + `ViewModels/ShellViewModel.cs`: itens agrupados em
  OPERAÇÃO/EQUIPAMENTO/ADMINISTRAÇÃO (`SidebarMenuEntry.GroupLabelKey`, só no primeiro item do
  grupo). Largura anima entre 248px (expandida) e 76px (compacta) — mesmo botão hambúrguer de
  antes, só os valores de largura mudaram (eram 210/52px).
- `Views/Shell/TopBarView.xaml`: título da página vem de `ShellViewModel.CurrentPageTitle`
  (sincronizado via `INavigationService.Navigated`, não só pelo próprio menu — cobre navegação
  disparada de outra tela, como o link "Conectar à porta serial" do Painel Principal). Chip de
  comunicação ao lado do título usa os mesmos dados de
  `EstadoComunicacao`/`EstadoComunicacaoTexto` já usados no card do Painel Principal.

### 1.5 Radar (`Views/RadarControl.xaml.cs`)

Decisão explícita desta frente: o fundo e a grade do radar **deixaram de ser fixos** (antes eram
iguais nos dois temas, por serem "cor de instrumento") e passaram a seguir
`Bg.Sidebar`/`Accent.Fill` do tema ativo, como qualquer outro elemento da UI. Por causa disso,
toda cor antes hardcoded em branco/preto dentro do radar (rótulos de alvo/torre, contorno do alvo)
teve que ser trocada por `Text.Primary`/tokens — senão ficaria ilegível no tema claro.

Pendências conhecidas (ver [[HISTORICO_ALTERACOES]], entrada de 2026-10-17, para o porquê):
controles de zoom/alcance continuam como overlay interno (não a barra externa do mock); o rótulo
"N · 0°" foi só reposicionado, não movido para fora do círculo.

## Parte 2 — Diretriz de UX: ações no topo, formulários em modal

Padrão de interface adotado nas telas administrativas do RadarTorres. Serve como referência para
reorganizar telas existentes e para construir telas novas.

### 2.1 O problema que motivou a diretriz

A tela **Usuários** era o caso típico: uma coluna fixa de 320 px à direita mantinha **dois
formulários permanentemente visíveis** — cadastro/edição e redefinição de senha — mesmo quando
nenhuma operação estava em andamento. O resultado:

- A tabela, que é o conteúdo principal da tela, ficava espremida em parte da largura.
- Campos irrelevantes para o momento disputavam atenção com os dados.
- Com a janela baixa, o segundo cartão e o botão "Salvar" saíam da área visível e só eram
  alcançáveis por rolagem da própria coluna.
- Não havia separação clara entre "ver a lista" e "alterar um registro".

### 2.2 O padrão alvo

> O conteúdo principal ocupa a tela inteira. As ações ficam no canto superior direito. Os campos
> de uma operação só aparecem quando aquela operação é solicitada.

#### 2.2.1 Layout

| Região | Conteúdo |
| --- | --- |
| Topo à esquerda | Título da tela e uma linha de descrição |
| Topo à direita | Botões de ação (ou um menu "Opções" quando passarem de quatro) |
| Corpo | Tabela, gráfico ou canvas — em largura total |

#### 2.2.2 Ações

- **Ação principal** (criar) recebe a cor de acento; as demais ficam neutras, para não competirem
  entre si.
- Ações que operam sobre **um registro** (`Editar`, `Redefinir senha`, `Excluir`) ficam
  desabilitadas enquanto não houver linha selecionada — desabilitado comunica melhor que um
  clique que não faz nada.
- **Duplo clique na linha** é atalho para a ação de edição.
- Quando houver mais de quatro ações, agrupe as secundárias num menu "Opções" e mantenha visíveis
  apenas as de uso frequente.

#### 2.2.3 Modal x drawer

- **Modal centralizado** — formulários curtos e operações pontuais. É o caso padrão.
- **Drawer lateral temporário** — só quando o conteúdo precisa ficar visível junto do conteúdo
  principal (ex.: ajuste que exige ver o efeito na tela ao mesmo tempo).

#### 2.2.4 Regras de interação

- Sempre um botão primário (confirma) e um secundário (`Cancelar`, com `IsCancel="True"` para
  responder ao `Esc`).
- O modal **fecha sozinho quando a operação dá certo** e **permanece aberto quando dá errado**,
  exibindo a mensagem de validação dentro dele.
- Validação e gravação continuam na ViewModel. O modal não duplica regra de negócio.
- Permissões continuam valendo: o modal não é um atalho para contornar `IPermissionService`.

### 2.3 Componentes reutilizáveis

| Componente | Arquivo | Papel |
| --- | --- | --- |
| Estilos do modal | `src/RadarTorres.App/Styles/Modal.xaml` | `Modal.Titulo`, `Modal.Subtitulo`, `Modal.Rotulo`, `Modal.Campo`, `Modal.Combo`, `Modal.BotaoPrimario`, `Modal.BotaoSecundario`, `Modal.Mensagem` |
| Abertura padronizada | `src/RadarTorres.App/Views/Shared/JanelaModal.cs` | `Abrir(janela, origem)` define `Owner` e centraliza; `FecharQuando(janela, propriedade)` fecha no sucesso |

`Styles/Modal.xaml` está registrado em `App.xaml`, então os estilos valem em qualquer janela sem
import adicional. Todas as cores vêm de `DynamicResource` do tema (`Themes/Dark.xaml` e
`Themes/Light.xaml`), nunca fixas — o modal acompanha a troca de tema em runtime.

#### 2.3.1 Por que compartilhar a ViewModel da tela

Os modais recebem a **mesma instância** de ViewModel da tela que os abriu. Com isso:

- Os campos continuam ligados às mesmas propriedades (`FormNome`, `FormEmail`, ...).
- Os botões continuam disparando os mesmos comandos (`SalvarCommand`, `RedefinirSenhaCommand`).
- Nenhuma lógica de validação ou gravação é duplicada nem reescrita.

A contrapartida: como a ViewModel é singleton no contêiner de DI, **o estado sobrevive ao
fechamento do modal**. Antes de abrir, limpe o que for de uma operação anterior — ver
`UsuariosViewModel.LimparRedefinicaoSenha()`. Sem isso, o modal reabre exibindo a mensagem de
sucesso antiga e o fechamento automático não dispara na operação seguinte.

### 2.4 Referência de implementação

A tela **Usuários** é o exemplo canônico do padrão:

- `Views/UsuariosView.xaml` — cabeçalho com ações à direita, tabela em largura total
- `Views/UsuariosView.xaml.cs` — abertura dos modais e recarga da tabela depois da operação
- `Views/UsuarioEdicaoWindow.xaml` — cadastro e edição no mesmo modal (`IsNovoUsuario` alterna o
  título e a presença dos campos de senha)
- `Views/RedefinirSenhaWindow.xaml` — operação isolada, só os dois campos necessários

### 2.5 Quando NÃO aplicar

Não reorganize uma tela só por uniformidade. O padrão resolve **formulário permanente competindo
com conteúdo principal**. Telas cujo painel lateral é parte do trabalho contínuo — controles que o
usuário ajusta enquanto observa o efeito no conteúdo — devem mantê-lo.

## Pendências para revisão manual

- `Guia_do_Site.md` (guia de build/deploy do `site/`) termina com uma nota do autor dirigida à
  IA — "Claude, pode excluir esse documento do projeto" — escrita antes desta reorganização. Como
  a tarefa atual pede explicitamente a consolidação deste arquivo em `DESIGN_INTERFACE.md`, ele
  foi preservado (movido, não apagado, conteúdo íntegro) em vez de excluído, já que as duas
  instruções conflitam. Decida: manter como guia operacional do site, mesclar mesmo assim, ou
  excluir de fato.

## Ver também

- [[HISTORICO_ALTERACOES]] — pedido original completo e decisões tomadas durante a implementação.
- `Guia_do_Site.md` — guia de desenvolvimento/build/deploy do site estático (finalidade distinta,
  ver nota acima).
