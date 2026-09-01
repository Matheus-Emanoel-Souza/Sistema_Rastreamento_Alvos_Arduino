# Decisões e Restrições Arquiteturais

Escolhas internas de **como** o sistema foi construído — não são requisitos do produto (o
usuário não percebe/aciona nenhuma delas diretamente) e por isso não devem aparecer como RF nem
RNF. Cada item aqui listado foi movido de `Requisitos_Nao_Funcionais.md` nesta revisão, com o
ID original preservado como referência cruzada.

| ID | Decisão | Categoria | Descrição | Evidência |
|---|---|---|---|---|
| DA01 *(ex-RNF20)* | Persistência substituível sem alterar camadas superiores | Manutenibilidade / Escalabilidade | Cada tabela CSV tem uma interface de repositório dedicada, preparada para troca futura por banco relacional sem tocar ViewModels/Services | `Repositories/I*Repository.cs`, comentários `TODO(SQL)`, `Docs/Tecnica/MODELO_DADOS.md`, seção 1 |
| DA02 *(ex-RNF21)* | Protocolo serial centralizado em um único componente | Manutenibilidade | Toda a interpretação/montagem de mensagens Arduino↔PC passa por `SerialProtocolParser`, nunca strings soltas em outras classes | `SerialProtocolParser.cs`, `Docs/Tecnica/COMUNICACAO_ARDUINO.md`, seção 3 |
| DA03 *(ex-RNF22)* | MVVM implementado manualmente, sem framework externo | Manutenibilidade | `ViewModelBase`/`RelayCommand` implementados à mão (~60 linhas) em vez de Prism/CommunityToolkit.Mvvm — decisão didática, mantém o mecanismo de binding 100% explicável na defesa do TCC | `ViewModelBase.cs`, `RelayCommand.cs`, `Docs/Tecnica/ARQUITETURA.md`, seção 2 |
| DA04 *(ex-RNF23)* | Nenhuma classe de Services/Models depende de WPF | Manutenibilidade / Testabilidade | Interfaces `I*Service` permitem trocar implementação (ex.: dublê de teste) sem tocar a camada de UI | `Docs/Tecnica/ARQUITETURA.md`, seção 1 |
| DA05 *(ex-RNF30)* | Anticolisão de cards do painel por rejeição, não por reposicionamento em cascata | Interação / UX | `DashboardCanvas` recusa o gesto de arraste/redimensionamento que colidiria com outro card, em vez de empurrar os demais — comportamento mais previsível, mas é uma escolha de interação, sem requisito de negócio explícito exigindo especificamente esse comportamento em vez de outro | `DashboardCanvas.cs`, `Docs/Tecnica/ARQUITETURA.md`, seção 5.2 |

## Por que estes itens não são requisitos

Um requisito (funcional ou não funcional) descreve uma característica **observável do produto
pelo seu usuário/ator** — o que o sistema faz, ou quão bem faz. Os itens acima descrevem
**escolhas de construção interna**: trocar qualquer um deles (ex.: usar EF Core em vez de CSV,
usar Prism em vez de MVVM manual, deixar Services referenciarem WPF) não mudaria nada que um
usuário observe funcionando — mudaria apenas o quão fácil é manter/estender o código, que é uma
preocupação de engenharia, não do produto entregue. Por isso ficam documentados à parte, sem
poluir a rastreabilidade de requisitos com decisões de implementação.

Quando uma decisão arquitetural também sustenta diretamente um requisito não funcional real
(por exemplo, DA01 sustenta a futura evolução do sistema, mas não é em si um RNF), a referência
cruzada é feita pelo ID `DA0x`, não pela duplicação do texto.
