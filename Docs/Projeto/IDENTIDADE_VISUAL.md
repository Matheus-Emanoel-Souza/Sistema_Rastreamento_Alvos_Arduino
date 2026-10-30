Funcionalidade: define a identidade visual (cores, estilo) usada na interface do RadarTorres.

# Identidade visual

Referência viva da identidade visual do RadarTorres (tokens de cor, tipografia, ícones) —
criada na sessão de 2026-10-17 (ver [[LOG_SOLICITACOES]]) para documentar o que antes só
existia no pedido original em texto corrido. Atualize este documento sempre que um token,
fonte ou ícone mudar; não deixe o pedido original ser a única fonte de verdade.

## 1. Tokens de cor

`src/RadarTorres.App/Themes/Dark.xaml` e `Themes/Light.xaml` definem as mesmas chaves nos dois
temas — nenhuma tela sabe qual tema está ativo, todas consomem via `DynamicResource`. A troca
em runtime é feita por `ThemeService.ApplyTheme`, que substitui o `ResourceDictionary` mesclado
em `Application.Resources` (ver `App.xaml.cs`).

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

\* O valor "90%" do pedido original é a opacidade do token em si (a cor de base já é escura),
não uma mistura adicional — ver o `Color` com canal alfa em `Themes/Dark.xaml`.

**Regra de significado, não decorativa:** âmbar (`Target.Fill`/`Warning.Text`) = alvo ou Modo
Amarelo; mint (`Accent.*`) = torre, ação principal, estado OK; vermelho (`Danger.*`) =
disparo. Não reutilizar essas cores para outro propósito. O roxo que antes marcava torres foi
removido por completo.

## 2. Tipografia

Três famílias OFL (Google Fonts), embutidas como `Resource` em
`src/RadarTorres.App/Resources/Fonts/` (licenças em `*-OFL.txt` ao lado de cada fonte) e
expostas como `FontFamily` nomeadas em `Styles/Typography.xaml`:

| Recurso | Fonte | Fallback | Uso |
|---|---|---|---|
| `Font.Display` | Chakra Petch (Bold) | Segoe UI | Título de página (28px), logo "RadarTorres" (20px), "Q1..Q4" do radar (22px), "N alvos no raio" (24px) |
| `Font.Body` | IBM Plex Sans (variável, 400/500/600) | Segoe UI | Toda a interface: menus, botões, crachás, rótulos de card |
| `Font.Mono` | JetBrains Mono (variável, 400/500) | Consolas | Números/dados: valores grandes de card, posição/ângulo/distância, data/hora, rótulos de grupo do menu, rótulos de distância do radar, registro de eventos |

O fallback funciona nativamente: `FontFamily="./Resources/Fonts/#Chakra Petch, Segoe UI"` — se
a fonte embutida não carregar, o WPF cai para o segundo nome da lista sem nenhum código extra.

## 3. Ícones

`src/RadarTorres.App/Resources/Icons.xaml` — um `GeometryGroup`/`PathGeometry` por ícone
(`Icon.PainelPrincipal`, `Icon.Monitoramento`, `Icon.ObjetosDetectados`,
`Icon.AcoesRealizadas`, `Icon.HistoricoModos`, `Icon.Cameras`, `Icon.PerfisAlvo`,
`Icon.ConfiguracoesArduino`, `Icon.Usuarios`, `Icon.Configuracoes`, `Icon.LogoTorre`), todos
grade 24x24, traço (sem preenchimento), resolvidos em runtime a partir da chave string de
`SidebarMenuEntry.IconKey` via `Converters/ResourceKeyToGeometryConverter`. Mesclado
estaticamente em `App.xaml` (não varia por tema).

## 4. Shell

- `Views/Shell/SidebarView.xaml` + `ViewModels/ShellViewModel.cs`: itens agrupados em
  OPERAÇÃO/EQUIPAMENTO/ADMINISTRAÇÃO (`SidebarMenuEntry.GroupLabelKey`, só no primeiro item do
  grupo). Largura anima entre 248px (expandida) e 76px (compacta) — mesmo botão hambúrguer de
  antes, só os valores de largura mudaram (eram 210/52px).
- `Views/Shell/TopBarView.xaml`: título da página vem de `ShellViewModel.CurrentPageTitle`
  (sincronizado via `INavigationService.Navigated`, não só pelo próprio menu — cobre navegação
  disparada de outra tela, como o link "Conectar à porta serial" do Painel Principal). Chip de
  comunicação ao lado do título usa os mesmos dados de `EstadoComunicacao`/`EstadoComunicacaoTexto`
  já usados no card do Painel Principal.

## 5. Radar (`Views/RadarControl.xaml.cs`)

Decisão explícita desta frente: o fundo e a grade do radar **deixaram de ser fixos** (antes
eram iguais nos dois temas, por serem "cor de instrumento") e passaram a seguir
`Bg.Sidebar`/`Accent.Fill` do tema ativo, como qualquer outro elemento da UI. Por causa disso,
toda cor antes hardcoded em branco/preto dentro do radar (rótulos de alvo/torre, contorno do
alvo) teve que ser trocada por `Text.Primary`/tokens — senão ficaria ilegível no tema claro.

Pendências conhecidas (ver [[LOG_SOLICITACOES]], entrada de 2026-10-17, para o porquê):
controles de zoom/alcance continuam como overlay interno (não a barra externa do mock); o
rótulo "N · 0°" foi só reposicionado, não movido para fora do círculo.

## Ver também

- [[LOG_SOLICITACOES]] — pedido original completo e decisões tomadas durante a implementação.
- [[ETAPA1_FUNDACAO]] — onde `ThemeService`/`ILocalizationService` foram introduzidos.
