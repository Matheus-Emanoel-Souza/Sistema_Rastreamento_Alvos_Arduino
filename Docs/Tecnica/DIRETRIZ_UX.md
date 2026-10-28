# Diretriz de UX — Ações no topo, formulários em modal

Padrão de interface adotado nas telas administrativas do RadarTorres. Serve como referência
para reorganizar telas existentes e para construir telas novas.

## 1. O problema que motivou a diretriz

A tela **Usuários** era o caso típico: uma coluna fixa de 320 px à direita mantinha **dois
formulários permanentemente visíveis** — cadastro/edição e redefinição de senha — mesmo quando
nenhuma operação estava em andamento. O resultado:

- A tabela, que é o conteúdo principal da tela, ficava espremida em parte da largura.
- Campos irrelevantes para o momento disputavam atenção com os dados.
- Com a janela baixa, o segundo cartão e o botão "Salvar" saíam da área visível e só eram
  alcançáveis por rolagem da própria coluna.
- Não havia separação clara entre "ver a lista" e "alterar um registro".

## 2. O padrão alvo

> O conteúdo principal ocupa a tela inteira. As ações ficam no canto superior direito.
> Os campos de uma operação só aparecem quando aquela operação é solicitada.

### 2.1 Layout

| Região | Conteúdo |
| --- | --- |
| Topo à esquerda | Título da tela e uma linha de descrição |
| Topo à direita | Botões de ação (ou um menu "Opções" quando passarem de quatro) |
| Corpo | Tabela, gráfico ou canvas — em largura total |

### 2.2 Ações

- **Ação principal** (criar) recebe a cor de acento; as demais ficam neutras, para não
  competirem entre si.
- Ações que operam sobre **um registro** (`Editar`, `Redefinir senha`, `Excluir`) ficam
  desabilitadas enquanto não houver linha selecionada — desabilitado comunica melhor que um
  clique que não faz nada.
- **Duplo clique na linha** é atalho para a ação de edição.
- Quando houver mais de quatro ações, agrupe as secundárias num menu "Opções" e mantenha
  visíveis apenas as de uso frequente.

### 2.3 Modal x drawer

- **Modal centralizado** — formulários curtos e operações pontuais. É o caso padrão.
- **Drawer lateral temporário** — só quando o conteúdo precisa ficar visível junto do
  conteúdo principal (ex.: ajuste que exige ver o efeito na tela ao mesmo tempo).

### 2.4 Regras de interação

- Sempre um botão primário (confirma) e um secundário (`Cancelar`, com `IsCancel="True"` para
  responder ao `Esc`).
- O modal **fecha sozinho quando a operação dá certo** e **permanece aberto quando dá errado**,
  exibindo a mensagem de validação dentro dele.
- Validação e gravação continuam na ViewModel. O modal não duplica regra de negócio.
- Permissões continuam valendo: o modal não é um atalho para contornar `IPermissionService`.

## 3. Componentes reutilizáveis

| Componente | Arquivo | Papel |
| --- | --- | --- |
| Estilos do modal | `src/RadarTorres.App/Styles/Modal.xaml` | `Modal.Titulo`, `Modal.Subtitulo`, `Modal.Rotulo`, `Modal.Campo`, `Modal.Combo`, `Modal.BotaoPrimario`, `Modal.BotaoSecundario`, `Modal.Mensagem` |
| Abertura padronizada | `src/RadarTorres.App/Views/Shared/JanelaModal.cs` | `Abrir(janela, origem)` define `Owner` e centraliza; `FecharQuando(janela, propriedade)` fecha no sucesso |

`Styles/Modal.xaml` está registrado em `App.xaml`, então os estilos valem em qualquer janela
sem import adicional. Todas as cores vêm de `DynamicResource` do tema (`Themes/Dark.xaml` e
`Themes/Light.xaml`), nunca fixas — o modal acompanha a troca de tema em runtime.

### 3.1 Por que compartilhar a ViewModel da tela

Os modais recebem a **mesma instância** de ViewModel da tela que os abriu. Com isso:

- Os campos continuam ligados às mesmas propriedades (`FormNome`, `FormEmail`, ...).
- Os botões continuam disparando os mesmos comandos (`SalvarCommand`, `RedefinirSenhaCommand`).
- Nenhuma lógica de validação ou gravação é duplicada nem reescrita.

A contrapartida: como a ViewModel é singleton no contêiner de DI, **o estado sobrevive ao
fechamento do modal**. Antes de abrir, limpe o que for de uma operação anterior — ver
`UsuariosViewModel.LimparRedefinicaoSenha()`. Sem isso, o modal reabre exibindo a mensagem de
sucesso antiga e o fechamento automático não dispara na operação seguinte.

## 4. Referência de implementação

A tela **Usuários** é o exemplo canônico do padrão:

- `Views/UsuariosView.xaml` — cabeçalho com ações à direita, tabela em largura total
- `Views/UsuariosView.xaml.cs` — abertura dos modais e recarga da tabela depois da operação
- `Views/UsuarioEdicaoWindow.xaml` — cadastro e edição no mesmo modal (`IsNovoUsuario` alterna
  o título e a presença dos campos de senha)
- `Views/RedefinirSenhaWindow.xaml` — operação isolada, só os dois campos necessários

## 5. Quando NÃO aplicar

Não reorganize uma tela só por uniformidade. O padrão resolve **formulário permanente
competindo com conteúdo principal**. Telas cujo painel lateral é parte do trabalho contínuo —
controles que o usuário ajusta enquanto observa o efeito no conteúdo — devem mantê-lo.
