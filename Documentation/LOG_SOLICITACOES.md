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
  instalação de .NET no computador do usuário final (ver `Documentation/INSTALADOR.md`,
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
  `Documentation/INSTALADOR.md`, seção 7, para a lista completa de testes e o único ponto
  parcial — desinstalação silenciosa travando no ambiente de automação usado, não no script).
- Documentação de uso no `README.md` (seção "Instalação (Windows)") e detalhamento técnico
  em `Documentation/INSTALADOR.md`.

---

## 2026-08-07 — Documentação e commit das alterações

**Pedido (texto integral):**

> Faça um resumo de suas alterações, Documente na pasta de documentação, adicione na pasta
> de documentação os pedidos que faço em cada prompt como logs, e commite para mim na branch
> sistemas mesmo. pv

**Entregue:**

- `Documentation/INSTALADOR.md` — documentação técnica do processo de instalação criado na
  solicitação anterior.
- `Documentation/LOG_SOLICITACOES.md` — este arquivo, com o histórico de pedidos.
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
- Documentação: `Documentation/MODELO_DADOS.md` (schema + relacionamentos + plano de migração
  SQL) e `Documentation/ETAPA1_FUNDACAO.md` (arquitetura antes/depois, arquivos alterados,
  como validar cada funcionalidade, o que falta para fechar a Etapa 1).
- Build limpo (0 erros, 0 avisos) e validação de ponta a ponta (login real, Shell renderizada,
  CSVs de auditoria gerados) usando captura direta de janela (`PrintWindow`) e verificação
  criptográfica isolada do hash de senha — sem automação de teclado, depois que uma tentativa
  de `SendKeys` acabou digitando em uma janela errada (WhatsApp Web) por roubo de foco; o
  incidente foi reportado ao usuário assim que percebido.
