# Etapa 1 (parte A) — Fundação multiusuário

Este documento registra a primeira parte da Etapa 1 do plano de melhorias de interface e
estrutura: a infraestrutura (banco CSV, autenticação, permissões, i18n, tema) e a "casca" da
aplicação (login, barra superior, barra lateral, navegação, painel principal, formulário de
ajuda). As quatro telas de dados completas (Objetos detectados, Ações realizadas, Histórico
de modos, Usuários) ficam para a próxima parte — ver seção 6.

## 1. Por que dividir a Etapa 1 em partes

O pedido original já reconhece que a Etapa 1 é grande (login + usuários + 3 telas de
auditoria + shell completa). Implementar tudo de uma vez, sem checkpoint, teria dois riscos:
se a fundação (banco, autenticação, navegação) tivesse um problema de design, todas as 4
telas de dados construídas em cima dela precisariam ser refeitas. Por isso esta parte entrega
primeiro a fundação + uma tela completa de exemplo (Painel principal) + navegação
funcionando ponta a ponta para todos os itens do menu (com telas "em construção" onde a tela
final ainda não existe), e a próxima parte foca só nas 4 telas de dados restantes,
reaproveitando a base já validada aqui.

## 2. Arquitetura antes x depois

| Antes | Depois |
|---|---|
| `MainWindow` era a única janela do app | `LoginWindow` → `ShellWindow` (barra superior + barra lateral + área de conteúdo navegável); o conteúdo antigo virou `MonitoramentoView`, um item do menu |
| Serviços instanciados manualmente em `MainWindow.xaml.cs` | Composition root com `Microsoft.Extensions.DependencyInjection` em `App.xaml.cs` (evolução já prevista no comentário original de `AppConfig.cs`) |
| Sem persistência (tudo em memória) | 6 "tabelas" CSV em `%AppData%\RadarTorres\Data\` — ver `Docs/MODELO_DADOS.md` |
| Sem conceito de usuário | Login obrigatório, 3 perfis (Administrador/Operador/Visualizador), sessão em memória |
| 100% português hardcoded | `ILocalizationService` + JSON (`Resources/Localization/pt-BR.json`, `en-US.json`) + `{loc:Loc Chave}` no XAML |
| Cores fixas em `App.xaml` | `Themes/Dark.xaml` / `Themes/Light.xaml`, trocados em runtime; app já usava `DynamicResource` em tudo, então nenhuma tela precisou ser redesenhada |
| `SystemMode`: Off/LocationOnly/LocationAutoTower/LocationAutoFire | Mesmo enum + `Maintenance`/`Emergency`, com confirmação e auditoria em toda troca |

## 3. Arquivos criados

Mais de 50 arquivos novos. Os grupos principais:

| Pasta | Conteúdo |
|---|---|
| `Data/` | `AppDataPaths`, `CsvTableStore<T>`, `CsvColumn<T>`, `CsvConvert`, `DataSeeder` |
| `Repositories/` | 6 pares de interface + implementação CSV (`IUsuarioRepository`/`CsvUsuarioRepository`, etc.) |
| `Models/` (novos) | `Usuario`, `ObjetoDetectado`, `AcaoRealizada`, `AlteracaoModo`, `PreferenciasUsuario`, `ChamadoAjuda`, `Auditoria.cs` (enums) |
| `Services/` (novos) | `IPasswordHasher`/`PasswordHasher`, `IAuthService`/`AuthService`, `IPermissionService`/`PermissionService`, `ILocalizationService`/`LocalizationService`, `IThemeService`/`ThemeService`, `INavigationService`/`NavigationService` |
| `Localization/` | `LocExtension` (markup extension XAML) |
| `Resources/Localization/` | `pt-BR.json`, `en-US.json` |
| `Themes/` | `Dark.xaml`, `Light.xaml` |
| `Views/` (novos) | `LoginWindow`, `PainelPrincipalView`, `MonitoramentoView` (migrada de `MainWindow`) |
| `Views/Shell/` | `ShellWindow`, `TopBarView`, `SidebarView`, `ProfileWindow`, `HelpDeskFormWindow`, `PlaceholderView` |
| `ViewModels/` (novos) | `LoginViewModel`, `ShellViewModel`, `SidebarMenuEntry`, `PainelPrincipalViewModel`, `ProfileViewModel`, `HelpDeskFormViewModel` |

## 4. Arquivos modificados (sem alterar funcionalidade existente)

- **`RadarTorres.App.csproj`**: + `Microsoft.Extensions.DependencyInjection`; cópia dos JSON de tradução para a saída do build.
- **`App.xaml` / `App.xaml.cs`**: cores movidas para `Themes/*.xaml`; sem mais `StartupUri` (fluxo login → shell controlado por código); composition root com DI.
- **`Models/SystemState.cs`**: `SystemMode` ganhou `Maintenance` e `Emergency`.
- **`Services/IFireControlService.cs` / `FireControlService.cs`**: `TryFireAsync` ganhou o parâmetro `OrigemAcao` (Manual/Automática) e agora grava cada tentativa em `acoes_realizadas`.
- **`ViewModels/MainViewModel.cs`**:
  - grava `alteracoes_modo` a cada troca de modo (com confirmação e possibilidade de cancelar);
  - grava `objetos_detectados` na primeira detecção de cada alvo (não a cada atualização);
  - bloqueia ações (`ManualFireCommand`, troca de modo) para o perfil Visualizador.
- **`Views/MainWindow.xaml(.cs)` → removidos**, substituídos por `Views/MonitoramentoView.xaml(.cs)` (mesmo conteúdo, hospedado pela Shell).

Nenhuma lógica de detecção, rastreamento, seleção de torre, protocolo serial ou simulação foi
alterada — só os pontos de gancho para auditoria/permissão, todos aditivos.

## 5. Como validar cada funcionalidade

Pré-requisito: `dotnet build src\RadarTorres.App\RadarTorres.App.csproj -c Debug` (0 erros, 0
avisos confirmado) e rodar `RadarTorres.App.exe`.

| Funcionalidade | Como validar |
|---|---|
| Login | Tela abre pedindo usuário/senha. Primeiro acesso: `admin` / `admin123` (dica já exibida na tela). Usuário/senha errados mostram mensagem clara sem derrubar o app. |
| Usuário/perfil semeado | Após o primeiro login, `%AppData%\RadarTorres\Data\usuarios.csv` tem 1 linha, `Perfil=Administrador`, `SenhaHash`/`SenhaSalt` preenchidos (nunca a senha em texto puro). |
| Barra superior | Menu "Opções" > Idioma (Português/Inglês) e Tema (Claro/Escuro/Seguir sistema) — troca instantânea, sem reiniciar. Nome + selo do perfil visíveis. Botões Perfil/Ajuda/Sair funcionam. |
| Alteração de senha | Perfil > preencher senha atual + nova + confirmar > mensagem de sucesso/erro clara. |
| Barra lateral | Clique no ☰ recolhe/expande (texto vira tooltip). Itens navegam para a tela correspondente; item ativo fica destacado. "Usuários" só aparece para Administrador. |
| Painel principal | Cards de objetos detectados, ações realizadas, modo atual, última alteração de modo, usuário responsável, estado da comunicação e última atualização — todos com dado real (ou "—"/contagem 0 quando não há histórico ainda). |
| Monitoramento | Idêntico ao app anterior (radar, conexão serial, modos, simulação) — agora dentro da Shell. Visualizador vê aviso "não permite executar ações" e os botões ficam bloqueados. |
| Auditoria de modo | Trocar o modo pede confirmação; aceitar ou cancelar grava uma linha em `alteracoes_modo.csv` (`Resultado=Sucesso` ou `Erro`). |
| Auditoria de objetos | Ativar o modo de simulação gera alvos; a primeira detecção de cada um grava uma linha em `objetos_detectados.csv`. |
| Auditoria de ações | "Acionamento manual demonstrativo" (ou automático, no modo 4) grava uma linha em `acoes_realizadas.csv`, incluindo bloqueios de segurança (`Resultado=Cancelada`). |
| Formulário de ajuda | Botão "Ajuda" abre o formulário; enviar sem título/descrição mostra erro de validação; enviar com dados grava em `chamados_ajuda.csv` com usuário e data preenchidos automaticamente. |
| Logout | Botão "Sair" volta para a tela de login sem fechar o processo; logar de novo funciona. |
| Preferências por usuário | Trocar tema/idioma e fechar o app; no próximo login, o tema/idioma escolhido é restaurado automaticamente (gravado em `preferencias_usuario.csv`). |
| Telas "em construção" | Objetos detectados, Ações realizadas, Histórico de modos, Usuários e Configurações mostram uma tela placeholder traduzida — a navegação já funciona, só a tela completa (tabela/filtros) chega na próxima parte. |

## 6. O que falta para fechar a Etapa 1 (próxima entrega)

Conforme o próprio pedido definiu como escopo da Etapa 1, ainda faltam as 4 telas de dados
completas — cada uma reaproveitando a fundação já pronta aqui (repositórios, permissões,
i18n, tema, navegação):

1. **Objetos detectados**: tabela com busca, filtros (data/objeto/quadrante/dispositivo),
   ordenação, paginação e representação gráfica das coordenadas.
2. **Ações realizadas**: tabela de auditoria (somente leitura para todos os perfis).
3. **Histórico de modos**: tabela de auditoria (somente leitura).
4. **Usuários**: CRUD completo (criar/editar/inativar), restrito ao Administrador, com
   validação de campos.

A Etapa 2 (indicadores gráficos avançados, personalização completa de layout, filtros
avançados, exportação CSV, notificações) e a Etapa 3 (testes automatizados, revisão de
segurança, ajustes de desempenho) seguem depois, como já planejado no pedido original.

## 7. Limitações conhecidas desta parte

- Não há projeto de testes automatizados no repositório ainda — "executar os testes
  disponíveis" (item 13 do pedido) não se aplica por não existirem; fica reservado para a
  Etapa 3 ("Qualidade"), que já lista "Testes" como item.
- A validação visual interativa (clicar em botões, digitar) não foi feita via automação de
  UI nesta sessão — o ambiente tinha várias outras janelas do usuário abertas e uma tentativa
  de automação de teclado acabou digitando em uma janela errada (ver aviso dado na conversa).
  Em vez disso, a validação usou: build sem erros/avisos, captura direta da janela via
  `PrintWindow` (sem roubar foco), inspeção dos arquivos CSV gerados de fato pela aplicação
  rodando, e verificação criptográfica isolada de que o hash de senha semeado bate com
  `admin123`. Recomenda-se um teste manual de clique adicional antes de considerar a etapa
  definitivamente fechada.
