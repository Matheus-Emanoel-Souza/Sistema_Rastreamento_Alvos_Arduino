# Sistema_Rastreamento_Alvos_Arduino
Sistema de rastreamento e monitoramento de alvos desenvolvido como parte de um Trabalho de Conclusão de Curso em Engenharia da Computação.

Documentação técnica completa em [`README_LOCAL.md`](README_LOCAL.md) e [`Documentation/`](Documentation/).

## Login e multiusuário

Desde a Etapa 1 (fundação multiusuário), o app pede login ao abrir. No primeiro uso:

```
Usuário: admin
Senha:   admin123
```

Troque a senha padrão em **Perfil > Alterar senha** assim que possível. Detalhes completos
(arquitetura, tabelas, como validar cada funcionalidade) em
[`Documentation/ETAPA1_FUNDACAO.md`](Documentation/ETAPA1_FUNDACAO.md) e
[`Documentation/MODELO_DADOS.md`](Documentation/MODELO_DADOS.md).

## Instalação (Windows)

O RadarTorres é distribuído como um instalador Windows único, `Setup.exe`, gerado com o
[Inno Setup 6](https://jrsoftware.org/isinfo.php).

**Para o usuário final:** baixe `dist/Setup.exe`, execute e siga o assistente. Não é
necessário instalar o .NET separadamente — o instalador já inclui tudo o que o aplicativo
precisa para rodar (ver "Por que self-contained?" abaixo). É pedida elevação de administrador
porque o app é instalado em `C:\Program Files\RadarTorres` e registrado em
"Programas e Recursos".

O instalador:
- Cria atalho no Menu Iniciar (sempre) e, opcionalmente, na Área de Trabalho (checkbox
  desmarcado por padrão no assistente).
- Registra o RadarTorres em "Aplicativos instalados" do Windows, com opção de desinstalar.
- Em atualizações (reinstalar por cima de uma versão existente), preserva o
  `appsettings.json` já configurado pelo usuário (porta COM, torres, distâncias etc.) —
  só grava o arquivo padrão na primeira instalação.

### Requisitos para gerar o instalador (máquina de desenvolvimento)

Só é necessário na máquina que **compila** o projeto, nunca na do usuário final:

| Ferramenta | Versão usada | Fonte oficial |
|---|---|---|
| .NET SDK | 9.0.316+ | https://dotnet.microsoft.com/download/dotnet/9.0 |
| Inno Setup | 6.7.3+ | https://jrsoftware.org/isdl.php |

Verifique se estão disponíveis:

```powershell
dotnet --version
```

```powershell
Test-Path "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
```

### Gerar o instalador (comando único)

```powershell
powershell -ExecutionPolicy Bypass -File build\publish.ps1
```

Esse script (`build/publish.ps1`) automatiza o pipeline completo:

1. `dotnet restore` + `dotnet build -c Release`
2. `dotnet publish -c Release -r win-x64 --self-contained true` (inclui o .NET Desktop
   Runtime no pacote — ver publish self-contained abaixo)
3. Compila `installer\RadarTorres.iss` com o Inno Setup (`ISCC.exe`)
4. Gera o instalador final em **`dist\Setup.exe`**

Para só compilar/publicar o app sem gerar o instalador (ex.: Inno Setup não instalado ainda):

```powershell
powershell -ExecutionPolicy Bypass -File build\publish.ps1 -SkipInstaller
```

Se o Inno Setup estiver em um local não padrão:

```powershell
powershell -ExecutionPolicy Bypass -File build\publish.ps1 -InnoSetupPath "C:\caminho\ISCC.exe"
```

### Gerar uma nova versão no futuro

1. Atualize a versão em **um único lugar**:
   [`src/RadarTorres.App/RadarTorres.App.csproj`](src/RadarTorres.App/RadarTorres.App.csproj),
   propriedade `<Version>` (ex.: `1.1.0`). O script de build lê esse valor automaticamente e
   propaga para o `.exe`, para o instalador (`AppVersion`) e para o nome exibido em
   "Programas e Recursos".
2. Rode `build\publish.ps1` novamente.
3. O novo `dist\Setup.exe` já instala como upgrade sobre uma instalação existente (mesmo
   `AppId` fixo no `.iss`), preservando o `appsettings.json` do usuário.
4. (Opcional) Se o ícone do aplicativo mudar, regenere-o e salve como
   `src/RadarTorres.App/Assets/RadarTorres.ico` antes de publicar.

### Testar manualmente antes de distribuir

- **App**: rode `src\RadarTorres.App\bin\Release\net9.0-windows\win-x64\publish\RadarTorres.App.exe`
  diretamente e confirme que a janela abre sem erros.
- **Instalador**: rode `dist\Setup.exe`, confira o assistente, os atalhos criados e a
  entrada em "Programas e Recursos" (`appwiz.cpl`). Para testar sem precisar de admin (ex.:
  em CI), use `/CURRENTUSER /VERYSILENT /DIR="C:\pasta\qualquer"`.

### Por que self-contained?

O `.csproj` está configurado para publicar como **self-contained** (`--self-contained true`
+ `RuntimeIdentifiers=win-x64`), ou seja, o .NET 9 Desktop Runtime vai embutido dentro do
próprio pacote instalado — o usuário final não precisa ter nenhuma versão do .NET instalada
antes de rodar o RadarTorres.

Motivo da escolha, dado o objetivo de ter um processo "simples, confiável e fácil de manter":

- **Confiabilidade em demonstração/banca de TCC**: o instalador funciona em qualquer Windows
  10/11 x64 "limpo", sem depender de conexão com a internet no momento da instalação nem de
  detectar/baixar a versão certa do .NET Desktop Runtime na hora.
- **Menos pontos de falha**: não há necessidade de lógica extra no instalador para checar
  versão instalada, baixar o instalador oficial da Microsoft e rodá-lo silenciosamente antes
  de instalar o app — o Inno Setup só copia arquivos.
- **Custo aceito**: o instalador fica maior (~42 MB, compactado) do que uma publicação
  framework-dependent (poucos MB) que dependeria do .NET já estar no PC do usuário. Para um
  aplicativo desktop único distribuído por instalador, esse aumento de tamanho é um trade-off
  razoável frente à simplicidade e confiabilidade obtidas.

Se um dia for necessário reduzir o tamanho do instalador e for aceitável exigir o .NET 9
Desktop Runtime já instalado na máquina de destino, publique como framework-dependent trocando,
em `build/publish.ps1`, `--self-contained true` por `--self-contained false` — nesse caso será
necessário reintroduzir alguma forma de checagem/instalação do .NET Desktop Runtime, hoje
desnecessária.

### Arquitetura suportada

Apenas **x64** (Windows 10/11 64-bit). O aplicativo não usa nenhuma API específica de
x86/ARM64; x64 foi escolhido por ser a arquitetura padrão de praticamente todo PC Windows
atual. Para suportar ARM64 nativamente no futuro, adicione `win-arm64` a
`RuntimeIdentifiers` no `.csproj` e gere uma publicação/instalador separados para esse RID.

### Solução de problemas

| Sintoma | Causa provável | Solução |
|---|---|---|
| `dotnet publish` falha com `NETSDK1047` | RID não incluído no restore | Confirme `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>` no `.csproj` e rode `dotnet restore` de novo |
| `ISCC.exe não encontrado` | Inno Setup não instalado ou em local não padrão | Instale via https://jrsoftware.org/isdl.php ou use `-InnoSetupPath` |
| Mensagem "appsettings.json não encontrado" ao abrir o app | Instalação corrompida/incompleta | Reinstale o RadarTorres |
| Erro inesperado ao iniciar o app | Exibido em uma caixa de mensagem clara (não crash silencioso) | Ver `App.xaml.cs`, tratamento global de exceções (`DispatcherUnhandledException` / `AppDomain.UnhandledException`) |
