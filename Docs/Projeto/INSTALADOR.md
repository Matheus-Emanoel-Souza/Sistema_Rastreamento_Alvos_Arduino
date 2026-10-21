# Processo de Instalação (Windows)

Este documento registra o processo de instalação Windows criado para o RadarTorres: as
decisões tomadas, os arquivos envolvidos, como gerar uma nova versão e os testes realizados.

## 1. Levantamento inicial

Antes de implementar, o projeto foi mapeado para confirmar tecnologia, versão e dependências:

| Item | Valor |
|---|---|
| Tecnologia | WPF (C#) |
| Framework alvo | **.NET 9** (`net9.0-windows`) |
| Projeto principal | `src/RadarTorres.App/RadarTorres.App.csproj` |
| Dependências (NuGet) | `System.IO.Ports`, `Microsoft.Extensions.Configuration`, `.Json`, `.Binder` — todas gerenciadas, sem instalador nativo próprio |
| Arquitetura suportada | x64 (única necessária; não há uso de API específica de x86/ARM64) |
| Nome/versão/ícone | Não existiam antes desta implementação — definidos como parte deste trabalho (ver seção 3) |
| Dados de usuário existentes | Só `appsettings.json` (portas, torres, distâncias). Não há logs em disco nem banco de dados — o console de eventos (`LoggingService`) é só em memória |

Na máquina de desenvolvimento usada, não havia nenhum .NET SDK instalado (só o runtime 8.0) e
o Inno Setup não estava presente — ambos precisaram ser instalados antes do primeiro build
(ver seção 6).

## 2. Ferramenta escolhida: Inno Setup 6

Optou-se pelo [Inno Setup 6](https://jrsoftware.org/isinfo.php) em vez de WiX Toolset ou
MSIX por ser a opção mais simples de manter para um projeto único (não uma suíte de
produtos): script único e legível (`.iss`), sem necessidade de XML verboso, com todos os
recursos pedidos (atalhos, registro em Programas e Recursos, preservação de arquivos em
upgrade, mensagens de erro nativas) disponíveis via diretivas diretas.

## 3. Decisão de publicação: self-contained

O `.csproj` publica o app como **self-contained** (`dotnet publish -r win-x64
--self-contained true`), ou seja, o **.NET 9 Desktop Runtime vai embutido dentro do próprio
instalador**. A máquina do usuário final não precisa ter nenhuma versão do .NET instalada.

Motivos (dado o objetivo de um processo "simples, confiável e fácil de manter"):

- **Confiabilidade em demonstração**: o instalador funciona em qualquer Windows 10/11 x64
  "limpo", sem depender de internet no momento da instalação nem de detectar/baixar a versão
  certa do .NET Desktop Runtime na hora H (ex.: banca de TCC).
- **Menos pontos de falha**: dispensa lógica extra no instalador para checar versão
  instalada, baixar o instalador oficial da Microsoft e rodá-lo silenciosamente antes de
  instalar o app — o Inno Setup só copia arquivos.
- **Trade-off aceito**: o instalador fica maior (~44 MB comprimido) do que uma publicação
  framework-dependent (poucos MB) que dependeria do .NET já estar no PC do usuário. Para um
  aplicativo desktop único distribuído por instalador, o aumento de tamanho foi considerado
  um custo razoável frente à simplicidade e confiabilidade obtidas.

Como consequência direta dessa escolha, os requisitos de "detectar e instalar o .NET se
ausente" tornam-se desnecessários — o próprio pacote já contém tudo. O Inno Setup ainda
valida nativamente (com mensagens claras) SO mínimo (Windows 10+) e arquitetura (x64) antes
de instalar.

Se no futuro for necessário reduzir o tamanho do instalador e for aceitável exigir o .NET 9
Desktop Runtime já instalado na máquina de destino, é possível trocar para
framework-dependent (`--self-contained false` em `build/publish.ps1`) — nesse caso será
necessário reintroduzir alguma forma de checagem/instalação do .NET Desktop Runtime.

## 4. Arquivos criados

| Arquivo | Função |
|---|---|
| `src/RadarTorres.App/Assets/RadarTorres.ico` | Ícone do app (radar verde sobre fundo escuro, gerado via PowerShell + `System.Drawing`, combinando com a paleta já usada na UI) |
| `installer/RadarTorres.iss` | Script do Inno Setup 6 |
| `build/publish.ps1` | Pipeline de build: `dotnet publish` self-contained → compila o instalador → gera `dist/Setup.exe` |
| `Docs/Projeto/INSTALADOR.md` | Este documento |
| `Docs/Projeto/LOG_SOLICITACOES.md` | Log das solicitações feitas ao assistente ao longo do projeto |

### O que o `installer/RadarTorres.iss` faz

- Instala em `C:\Program Files\RadarTorres` (`{autopf}\RadarTorres`).
- Cria atalho no Menu Iniciar sempre; atalho na Área de Trabalho é **opcional**, via checkbox
  desmarcado por padrão no assistente (`Tasks: desktopicon`, `Flags: unchecked`).
- Registra o app em "Programas e Recursos" do Windows (nome, versão, publisher, ícone,
  string de desinstalação) — automático via `[Setup]`/Inno Setup.
- **Preserva configuração do usuário em atualizações**: todo o conteúdo publicado é copiado
  normalmente, exceto `appsettings.json`, que usa `Flags: onlyifdoesntexist` — só é gravado
  na primeira instalação; se o usuário já tiver customizado porta COM/torres/distâncias, o
  arquivo existente não é sobrescrito numa reinstalação/upgrade.
- Mensagens de erro claras: o Inno Setup já cobre nativamente SO/arquitetura incompatível,
  espaço em disco insuficiente, falta de permissão de administrador e arquivo em uso durante
  a cópia (pede para fechar o programa e tenta de novo) — sem necessidade de código
  customizado adicional.
- `AppId` fixo (GUID) garante que instalações futuras com versão maior sejam tratadas como
  **upgrade** da mesma instalação, não como um programa novo e separado.

### O que o `App.xaml.cs` ganhou (sem alterar funcionalidade existente)

- `DispatcherUnhandledException` (thread de UI) e `AppDomain.CurrentDomain.UnhandledException`
  (threads de fundo, ex.: leitura serial) — exibem uma `MessageBox` com mensagem clara em vez
  de o aplicativo fechar sem explicação.
- Carregamento do `appsettings.json` agora é resiliente: se o arquivo não existir ou estiver
  corrompido/inválido, o app segue com valores padrão e avisa o usuário, em vez de crashar na
  inicialização.

## 5. Como gerar uma nova versão

1. Atualize a versão em **um único lugar**: `<Version>` em
   `src/RadarTorres.App/RadarTorres.App.csproj` (ex.: `1.1.0`).
2. Rode:
   ```powershell
   powershell -ExecutionPolicy Bypass -File build\publish.ps1
   ```
3. O script lê a versão do `.csproj`, publica self-contained e gera `dist\Setup.exe` já
   versionado, compatível com upgrade sobre instalações existentes (mesmo `AppId`).
4. Se o ícone mudar, regenere-o e salve como `src/RadarTorres.App/Assets/RadarTorres.ico`
   antes de publicar.

Comandos e solução de problemas detalhados estão no [`README.md`](../README.md#instalação-windows)
da raiz do projeto (é o local pedido para registrar os comandos).

## 6. Ferramentas instaladas na máquina de desenvolvimento

Nenhuma delas é necessária na máquina do usuário final (ver seção 3):

- **.NET 9 SDK** (`Microsoft.DotNet.SDK.9`, versão 9.0.316) — necessário porque o projeto
  alvo é `net9.0-windows` e a máquina só tinha o runtime 8.0.
- **Inno Setup 6** (`JRSoftware.InnoSetup`, versão 6.7.3) — necessário para compilar
  `installer\RadarTorres.iss` em `Setup.exe`.

A instalação automática via `winget` foi cancelada pelo administrador da máquina; os dois
instaladores oficiais foram então baixados para a pasta Downloads do usuário
(`dotnet-sdk-9.0.316-win-x64.exe`, fonte `builds.dotnet.microsoft.com`, e
`innosetup-6.7.3.exe`, fonte oficial no GitHub `jrsoftware/issrc`) para instalação manual com
privilégios de administrador. Depois de instalados, o restante do processo (build, publish,
geração do instalador) foi executado normalmente.

## 7. Testes realizados

| Teste | Resultado |
|---|---|
| Build limpo (`dotnet build`, do zero) | OK — 0 avisos, 0 erros |
| `dotnet publish` self-contained win-x64 | OK — pacote de ~127 MB (antes da compressão do instalador) |
| Executável publicado abre sem erros | OK — janela principal renderiza corretamente (radar, torres, console de eventos) |
| Compilação do instalador (`ISCC.exe`) | OK — `dist\Setup.exe` de ~44 MB |
| Instalação silenciosa (`/VERYSILENT`) | OK — arquivos, atalho de Menu Iniciar e atalho de Desktop (task `desktopicon`) criados corretamente |
| Entrada em "Programas e Recursos" | OK — nome, versão, publisher e string de desinstalação corretos no registro |
| **Preservação de `appsettings.json` em upgrade** | OK — valor customizado (`DefaultPort` alterado manualmente) sobreviveu a uma reinstalação por cima |
| Executável instalado (fora da pasta de publish) roda | OK |
| Desinstalação silenciosa (`unins000.exe`) | ⚠️ Parcial — o processo removeu arquivos/registro parcialmente, mas travou com "Acesso negado" no passo em que o Inno Setup se autocopia para uma pasta temporária e relança a si mesmo (mecanismo padrão do instalador). Esse relançamento de processo foi bloqueado pelo sandbox da ferramenta de automação usada para gerar este projeto, não é um defeito do script `.iss`. Resíduos do teste foram limpos manualmente. **Recomenda-se validar manualmente uma vez**, clicando em "Desinstalar RadarTorres" no Menu Iniciar ou em "Programas e Recursos". |

## 8. Referências relacionadas

- [`README.md`](../README.md) — seção "Instalação (Windows)": comandos de build, geração de
  nova versão e solução de problemas.
- [`ARQUITETURA.md`](../Tecnica/ARQUITETURA.md) — arquitetura do aplicativo em si (não do instalador).
- [`Docs/Projeto/LOG_SOLICITACOES.md`](LOG_SOLICITACOES.md) — histórico das solicitações
  feitas ao assistente para este e outros trabalhos no projeto.
