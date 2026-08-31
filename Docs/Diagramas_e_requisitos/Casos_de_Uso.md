# Casos de Uso — RadarTorres

## 1. Objetivo

Este documento especifica os casos de uso do sistema **RadarTorres**, complementando o
`Diagrama_Casos_de_Uso.puml` com o detalhamento textual de cada caso de uso: objetivo, atores,
pré-condições, fluxo principal, fluxos alternativos/exceções e pós-condições. Serve de ponte
entre `Requisitos_Funcionais.md`/`Requisitos_Nao_Funcionais.md` (o que o sistema deve fazer) e
o comportamento observável do sistema (como um ator interage com ele para atingir um objetivo),
para uso na documentação e na defesa do Trabalho de Conclusão de Curso.

Levantado a partir dos requisitos funcionais revisados, da `Matriz_de_Rastreabilidade.md`, de
`Docs/CONTEXTO_PROJETO.md` e de leitura direta do código-fonte para confirmar atores e regras de
permissão (`Services/PermissionService.cs`). Nenhum caso de uso foi incluído sem lastro em um
requisito funcional válido; o modo de simulação (RF07, removido da especificação funcional) não
aparece como caso de uso oficial.

## 2. Atores

| Ator | Descrição | Responsabilidades |
|---|---|---|
| **Usuário** *(genérico)* | Ator abstrato que representa qualquer perfil autenticado no sistema. Usado no diagrama por generalização (`Administrador`, `Operador` e `Visualizador` herdam dele) para não repetir, em cada caso de uso comum aos três perfis, três associações idênticas. Nenhuma pessoa é literalmente "Usuário genérico" — todo login exige um dos três perfis concretos. | Autenticar-se, alterar a própria senha, visualizar painel e radar, consultar/exportar histórico de objetos detectados e de auditoria, ajustar idioma/tema/layout pessoal, abrir chamado de ajuda, usar a aba de configuração do Arduino. |
| **Administrador** | Perfil com acesso administrativo completo, além de tudo que `Usuário` já cobre. | Gerenciar usuários (RF18), gerenciar zonas mortas (RF27), gerenciar chamados de ajuda (RF23), e — por também satisfazer `PodeExecutarAcoes` — alterar modo de operação (RF08) e importar objetos detectados (RF15), no mesmo nível do Operador. |
| **Operador** | Perfil operacional, sem acesso às telas administrativas. | Tudo que `Usuário` cobre, mais alterar modo de operação (RF08) e importar objetos detectados (RF15) — ambos liberados por `PermissionService.PodeExecutarAcoes`, que retorna verdadeiro para Administrador **e** Operador. |
| **Visualizador** | Perfil somente-consulta. | Restrito ao conjunto herdado de `Usuário` (visualização, exportação, preferências pessoais, abertura de chamado, aba do Arduino — ver observação de inconsistência abaixo). **Não** pode alterar modo de operação nem importar dados (`PodeExecutarAcoes` retorna falso), nem gerenciar usuários/zonas mortas/chamados. |
| **Arduino** | Ator externo (hardware), não um usuário do sistema. Representado só nas duas interações reais via porta serial: envio de leituras de sensores e tráfego observado no monitor serial da aba de configuração. | Enviar leituras de ângulo/distância pelo protocolo serial (RF01/RF02); ser a origem das mensagens exibidas em "Monitorar comunicação serial" (RF30). |

**Observação de inconsistência encontrada:** a descrição de RF10 define o Visualizador como
"somente consulta", mas `ArduinoSettingsViewModel` não tem nenhuma checagem de
`PodeExecutarAcoes` (ou equivalente) nas ações de compilar sketch/detectar CLI — hoje um
Visualizador consegue compilar firmware e usar o monitor serial pela interface, o que extrapola
"somente consulta". O diagrama reflete o **código real** (associação `Usuário` → UC25–UC28,
herdada por todos os perfis), não a descrição textual de RF10; a divergência é registrada aqui
para decisão futura (não corrigida nesta tarefa, que é documental).

## 3. Diagrama de Casos de Uso

Fonte: [`Diagrama_Casos_de_Uso.puml`](Diagrama_Casos_de_Uso.puml) (PlantUML).

Não foi gerada versão `.svg`/`.png` nesta tarefa: não há PlantUML nem um `plantuml.jar` instalado
localmente neste ambiente (só o JDK), e a instrução foi explícita em não instalar ferramentas
novas só para essa conversão. Para gerar a imagem quando desejar, qualquer uma destas opções
funciona sem alterar o projeto:

* Extensão "PlantUML" do VS Code (renderiza direto do `.puml`, exporta PNG/SVG).
* [www.plantuml.com/plantuml](https://www.plantuml.com/plantuml/uml/) — colar o conteúdo do
  arquivo.
* Localmente, se um `plantuml.jar` for baixado manualmente: `java -jar plantuml.jar -tsvg
  Diagrama_Casos_de_Uso.puml`.

O diagrama representa o sistema como uma única fronteira "Sistema RadarTorres", subdividida em
6 agrupamentos visuais para manter a legibilidade (28 casos de uso ao todo): **Conta e Acesso**,
**Monitoramento e Operação**, **Histórico e Dados**, **Administração**, **Preferências** e
**Arduino e Comunicação**. Casos de uso puramente automáticos (reação do sistema, sem ator
humano nem Arduino diretamente ligados) — Rastrear alvos, Selecionar torre automaticamente,
Acompanhar alvo pelas torres, Executar acionamento demonstrativo automático, Validar regras de
segurança — não têm linha própria de ator: são alcançados só via `<<include>>`/`<<extend>>` a
partir de "Monitorar radar" (Usuário) e "Receber/detectar alvos" (Arduino), reforçando que não
há acionamento manual no sistema.

## 4. Especificação dos Casos de Uso

### UC01 — Autenticar-se

**Objetivo:** permitir que um usuário cadastrado acesse o sistema com suas credenciais.
**Atores:** Usuário (Administrador, Operador ou Visualizador).
**Pré-condições:** existir um usuário ativo cadastrado (o usuário `admin` é semeado
automaticamente no primeiro uso do sistema).
**Fluxo principal:**
1. O sistema exibe a tela de login ao iniciar.
2. O usuário informa nome de usuário e senha.
3. O sistema valida a senha comparando o hash (PBKDF2-HMACSHA256) com o valor armazenado.
4. O sistema inicia a sessão e libera a navegação conforme o perfil do usuário.
**Fluxos alternativos/exceções:**
1. Credenciais inválidas — o sistema exibe mensagem de erro e mantém a tela de login.
2. Usuário inativo — o acesso é negado com mensagem específica.
**Pós-condições:** sessão autenticada ativa; menu e permissões carregados conforme o perfil.
**Requisitos relacionados:** RF09.
**Status:** Implementado.

### UC02 — Alterar senha

**Objetivo:** permitir que o usuário logado troque a própria senha.
**Atores:** Usuário.
**Pré-condições:** sessão autenticada ativa (UC01).
**Fluxo principal:**
1. O usuário acessa "Perfil > Alterar senha".
2. Informa a senha atual e a nova senha.
3. O sistema valida a senha atual e grava o novo hash.
**Fluxos alternativos/exceções:**
1. Senha atual incorreta — o sistema rejeita a troca e exibe mensagem de erro.
**Pós-condições:** nova senha vigente a partir do próximo login.
**Requisitos relacionados:** RF11.
**Status:** Implementado.

### UC03 — Visualizar painel

**Objetivo:** apresentar ao usuário um painel principal com indicadores do sistema.
**Atores:** Usuário.
**Pré-condições:** sessão autenticada ativa.
**Fluxo principal:**
1. O usuário acessa o item "Painel Principal" do menu.
2. O sistema carrega o layout salvo do usuário (ou o layout padrão, na primeira vez) e exibe os
   cards de indicadores.
**Fluxos alternativos/exceções:** nenhum relevante.
**Pós-condições:** painel exibido com os indicadores correntes.
**Requisitos relacionados:** RF24.
**Status:** Implementado.

### UC04 — Monitorar radar

**Objetivo:** acompanhar em tempo real, visualmente, os alvos detectados ao redor da base.
**Atores:** Usuário.
**Pré-condições:** sessão autenticada ativa.
**Fluxo principal:**
1. O usuário acessa a tela de Monitoramento.
2. O sistema exibe o radar circular dividido em 4 quadrantes.
3. O sistema reposiciona os alvos ativos continuamente (a cada ciclo de atualização), incluindo
   os já sendo rastreados (UC06).
4. O usuário pode selecionar um alvo no radar para ver seus dados.
**Fluxos alternativos/exceções:**
1. Nenhum Arduino conectado — o radar permanece vazio até que leituras cheguem (não há erro).
**Pós-condições:** estado do radar refletindo os alvos ativos no momento.
**Requisitos relacionados:** RF04.
**Status:** Implementado.

### UC05 — Receber/detectar alvos

**Objetivo:** interpretar as leituras de sensores enviadas pelo Arduino e validá-las antes de
alimentar o rastreamento.
**Atores:** Arduino (ator principal, envia a leitura); Sistema (interpreta).
**Pré-condições:** conexão serial estabelecida (porta e baud rate configurados e conectados).
**Fluxo principal:**
1. O Arduino envia uma mensagem de leitura de alvo pela porta serial.
2. O sistema interpreta os campos (identificador, ângulo, distância).
3. O sistema valida os campos (numéricos, ângulo em `[0,360)`, distância ≥ 0).
4. A leitura válida segue para o rastreamento (UC06).
**Fluxos alternativos/exceções:**
1. Mensagem malformada ou campo inválido — o sistema registra um aviso no console de eventos e
   descarta a leitura, sem interromper a aplicação (RNF28).
**Pós-condições:** leitura válida disponível para criar/atualizar um alvo rastreado.
**Requisitos relacionados:** RF01, RF02.
**Status:** Implementado.

### UC06 — Rastrear alvos

**Objetivo:** manter o conjunto de alvos ativos atualizado a partir das leituras recebidas.
**Atores:** nenhum ator direto — acionado internamente via `<<include>>` a partir de UC04
(Monitorar radar) e UC05 (Receber/detectar alvos).
**Pré-condições:** existir uma leitura válida (UC05) ou um alvo já rastreado.
**Fluxo principal:**
1. O sistema recebe uma leitura válida.
2. Se o identificador do alvo é inédito, cria um novo alvo com a posição da leitura.
3. Se o identificador já existe, atualiza a posição do alvo existente.
4. O sistema expira automaticamente alvos sem leitura recente, após o tempo limite configurado.
**Fluxos alternativos/exceções:** nenhum relevante (a expiração por timeout já é o próprio
tratamento de "alvo perdido").
**Pós-condições:** coleção de alvos ativos consistente com as leituras mais recentes.
**Requisitos relacionados:** RF03.
**Status:** Implementado.

### UC07 — Selecionar torre automaticamente

**Objetivo:** escolher, para cada alvo ativo, a torre demonstrativa mais adequada.
**Atores:** nenhum ator direto — acionado internamente via `<<include>>` a partir de UC06
(Rastrear alvos).
**Pré-condições:** existir ao menos um alvo ativo e ao menos uma torre disponível configurada.
**Fluxo principal:**
1. O sistema identifica o quadrante do alvo.
2. O sistema prioriza torres disponíveis no mesmo quadrante do alvo; se nenhuma, considera
   todas as disponíveis.
3. O sistema calcula a distância euclidiana do alvo a cada torre candidata.
4. O sistema seleciona a torre de menor distância e associa ao alvo.
**Fluxos alternativos/exceções:**
1. Alvo dentro de uma zona morta ativa — nenhuma torre é selecionada para esse alvo (RF27).
2. Nenhuma torre disponível — o alvo permanece sem torre selecionada.
**Pós-condições:** alvo associado a uma torre (ou sem torre, se bloqueado/indisponível).
**Requisitos relacionados:** RF05.
**Status:** Implementado.

### UC08 — Alterar modo de operação (Verde / Amarelo / Vermelho)

**Objetivo:** trocar o estado operacional do sistema, que determina se as torres acompanham e/ou
acionam automaticamente os alvos.
**Atores:** Administrador, Operador.
**Pré-condições:** sessão autenticada com perfil que satisfaça `PodeExecutarAcoes` (Visualizador
não pode).
**Fluxo principal:**
1. O usuário seleciona o modo desejado (Verde, Amarelo ou Vermelho).
2. O sistema solicita confirmação antes de aplicar a troca.
3. O usuário confirma.
4. O sistema aplica o novo modo e registra a troca em auditoria (sucesso).
**Fluxos alternativos/exceções:**
1. Usuário cancela a confirmação — o modo permanece o anterior; a tentativa é registrada em
   auditoria como cancelada.
2. Perfil Visualizador tenta alterar — o sistema recusa e registra aviso, sem alterar o modo.
**Pós-condições:** modo de operação atualizado, refletido em UC09/UC10 conforme a regra:
* **Verde** — nenhum rastreamento/acionamento funcional habilitado.
* **Amarelo** — habilita UC09 (acompanhamento automático), sem acionamento.
* **Vermelho** — habilita UC09 e a extensão UC10 (acionamento automático, sujeito a UC11).
**Requisitos relacionados:** RF08.
**Status:** Parcial — o comportamento de "só acompanhar" e "acompanhar e acionar" já existe no
sistema, mas rotulado com uma nomenclatura de modos diferente da especificada (ver
`Limitacoes_Conhecidas.md`, divergência D1); não há hoje um único modo equivalente a "Verde".

### UC09 — Acompanhar alvo pelas torres

**Objetivo:** manter a torre selecionada apontada/orientada para o alvo enquanto ele estiver
ativo, nos modos Amarelo e Vermelho.
**Atores:** nenhum ator direto — acionado internamente via `<<include>>` a partir de UC07
(Selecionar torre automaticamente), condicionado ao modo definido em UC08.
**Pré-condições:** modo de operação Amarelo ou Vermelho; alvo com torre selecionada (UC07).
**Fluxo principal:**
1. O sistema mantém a torre associada ao alvo enquanto ele permanecer ativo.
2. Se o alvo se move, a associação/orientação é recalculada a cada ciclo.
**Fluxos alternativos/exceções:**
1. Modo Verde — este caso de uso não ocorre.
2. Alvo expira (UC06) — o acompanhamento cessa junto com o alvo.
**Pós-condições:** torre orientada para o alvo, sem acionamento (a menos que o modo seja
Vermelho, ver UC10).
**Requisitos relacionados:** RF06, RF08.
**Status:** Implementado (ver divergência D1 em `Limitacoes_Conhecidas.md`).

### UC10 — Executar acionamento demonstrativo automático

**Objetivo:** realizar automaticamente o acionamento demonstrativo (laser de baixa potência,
LED ou simulação — nunca armamento real) sobre um alvo, exclusivamente no modo Vermelho.
**Atores:** nenhum ator direto — é uma extensão (`<<extend>>`) de UC09, condicionada ao modo
Vermelho; inclui (`<<include>>`) UC07 (precisa de torre selecionada) e UC11 (precisa passar na
validação de segurança).
**Pré-condições:** modo de operação Vermelho; alvo com torre selecionada e sendo acompanhado
(UC09).
**Fluxo principal:**
1. O sistema identifica que um alvo acompanhado está no modo Vermelho.
2. O sistema executa UC11 (validação de segurança) para esse alvo.
3. Se autorizado, o sistema executa o acionamento demonstrativo através da torre selecionada.
4. O sistema registra a tentativa (autorizada e executada) em auditoria.
**Fluxos alternativos/exceções:**
1. Validação de segurança reprova (UC11) — o acionamento é bloqueado e a tentativa é registrada
   em auditoria com o motivo (ex.: "alvo dentro da distância mínima", "alvo em zona morta").
2. Alvo deixa de estar ativo antes do acionamento — a tentativa é cancelada.
**Pós-condições:** acionamento demonstrativo executado (ou bloqueado, com motivo registrado);
registro de auditoria criado em todos os casos.
**Requisitos relacionados:** RF06.
**Status:** Implementado — ver divergência D1: o código ainda expõe, adicionalmente, um caminho
de acionamento manual (`MainViewModel.ManualFireCommand`) não previsto nesta especificação, sem
checagem de modo em `FireControlService.Authorize`.

### UC11 — Validar regras de segurança

**Objetivo:** autorizar ou bloquear uma tentativa de acionamento demonstrativo, aplicando todas
as regras de segurança do sistema. Extraído como caso de uso próprio (não estava na lista
original de nomes, mas decorre diretamente da descrição de RF06/RNF04/RNF05/RF27) para permitir
um `<<include>>` limpo a partir de UC10, em vez de descrever as quatro checagens dentro dele.
**Atores:** nenhum ator direto — subfluxo obrigatório de UC10.
**Pré-condições:** existir uma tentativa de acionamento em curso (UC10).
**Fluxo principal:**
1. Verifica se o alvo ainda está ativo.
2. Verifica se o alvo está dentro de uma zona morta ativa (RF27) — se sim, bloqueia.
3. Verifica se há torre selecionada para o alvo — se não, bloqueia.
4. Verifica se a distância do alvo é maior ou igual à distância mínima de segurança configurada
   — se não, bloqueia.
5. Se todas as checagens passarem, autoriza o acionamento.
**Fluxos alternativos/exceções:** cada checagem reprovada (passos 2–4) é, em si, um desfecho de
bloqueio com motivo específico, sempre registrado em auditoria por UC10.
**Pós-condições:** resultado de autorização (autorizado/bloqueado) com motivo, devolvido a UC10.
**Requisitos relacionados:** RF06, RNF04, RNF05, RF27.
**Status:** Implementado.

### UC12 — Visualizar objetos detectados

**Objetivo:** consultar o histórico de detecções em uma tela de tabela.
**Atores:** Usuário.
**Pré-condições:** sessão autenticada ativa.
**Fluxo principal:**
1. O usuário acessa "Objetos Detectados".
2. O sistema carrega e exibe o histórico de registros.
**Fluxos alternativos/exceções:** nenhum relevante.
**Pós-condições:** tabela exibida com o histórico atual.
**Requisitos relacionados:** RF12, RF13.
**Status:** Implementado.

### UC13 — Exportar objetos detectados

**Objetivo:** gerar um arquivo (CSV, XML ou PDF) com a lista de objetos detectados.
**Atores:** Usuário.
**Pré-condições:** tela de Objetos Detectados aberta (UC12).
**Fluxo principal:**
1. O usuário escolhe o formato de exportação.
2. O sistema gera o arquivo no formato escolhido e o salva onde o usuário indicar.
**Fluxos alternativos/exceções:** nenhum relevante.
**Pós-condições:** arquivo exportado disponível no destino escolhido.
**Requisitos relacionados:** RF14.
**Status:** Implementado.

### UC14 — Importar objetos detectados

**Objetivo:** inserir registros de objetos detectados a partir de um arquivo CSV ou XML externo.
**Atores:** Administrador, Operador.
**Pré-condições:** tela de Objetos Detectados aberta; perfil com `PodeExecutarAcoes` (Visualizador
não pode importar).
**Fluxo principal:**
1. O usuário seleciona um arquivo CSV ou XML no formato de exportação.
2. O sistema lê o arquivo e insere cada linha como um novo registro.
**Fluxos alternativos/exceções:**
1. Perfil Visualizador tenta importar — a ação fica indisponível na interface.
2. Arquivo em formato inválido — a importação é rejeitada com mensagem de erro.
**Pós-condições:** novos registros inseridos no histórico de objetos detectados.
**Requisitos relacionados:** RF15.
**Status:** Implementado.

### UC15 — Consultar ações realizadas

**Objetivo:** consultar o histórico de tentativas de acionamento (autorizadas, bloqueadas ou com
erro).
**Atores:** Usuário.
**Pré-condições:** sessão autenticada ativa.
**Fluxo principal:**
1. O usuário acessa "Ações Realizadas".
2. O sistema exibiria a lista de registros de auditoria de acionamento.
**Fluxos alternativos/exceções:** não aplicável no estado atual (ver Status).
**Pós-condições:** consulta exibida (quando implementada).
**Requisitos relacionados:** RF16.
**Status:** Parcial — o registro em si já é gravado a cada tentativa de acionamento (UC10); a
tela de consulta ainda é um item de menu "em construção" (`PlaceholderView`).

### UC16 — Consultar histórico de modos

**Objetivo:** consultar o histórico de trocas de modo de operação.
**Atores:** Usuário.
**Pré-condições:** sessão autenticada ativa.
**Fluxo principal:**
1. O usuário acessa "Histórico de Modos".
2. O sistema exibiria a lista de registros de troca de modo.
**Fluxos alternativos/exceções:** não aplicável no estado atual (ver Status).
**Pós-condições:** consulta exibida (quando implementada).
**Requisitos relacionados:** RF17.
**Status:** Parcial — o registro já é gravado a cada troca de modo (UC08); a tela de consulta
ainda é um item de menu "em construção".

### UC17 — Gerenciar usuários

**Objetivo:** criar, editar e inativar contas de usuário.
**Atores:** Administrador.
**Pré-condições:** perfil Administrador (`PodeGerenciarUsuarios`).
**Fluxo principal:**
1. O Administrador acessaria "Usuários".
2. O sistema exibiria a lista de contas, com opções de criar/editar/inativar.
**Fluxos alternativos/exceções:** não aplicável no estado atual (ver Status).
**Pós-condições:** conta criada/editada/inativada (quando implementado).
**Requisitos relacionados:** RF18.
**Status:** Planejado — existem o contrato de repositório e a checagem de permissão, mas nenhuma
tela consome esse repositório hoje; o item de menu é um `PlaceholderView`.

### UC18 — Gerenciar zonas mortas

**Objetivo:** criar, ativar/desativar e remover áreas de exclusão onde alvos não recebem torre
nem podem ser acionados, embora continuem visíveis/rastreados.
**Atores:** Administrador (gerencia); demais perfis herdam de `Usuário` a visualização
somente-leitura da lista (não modelada como caso de uso próprio, para não sobrecarregar o
diagrama — é a mesma tela, com os controles de edição ocultos).
**Pré-condições:** perfil Administrador (`PodeGerenciarZonasMortas`); tela de Monitoramento
aberta.
**Fluxo principal:**
1. O Administrador ativa o modo de desenho de zona morta.
2. O Administrador desenha a área no radar (clique/arraste) definindo quadrante e/ou faixa de
   distância.
3. O sistema cria a zona e a persiste.
4. O Administrador pode ativar, desativar ou remover qualquer zona existente.
**Fluxos alternativos/exceções:**
1. Perfil sem permissão tenta editar — os controles de gestão ficam indisponíveis, a lista
   continua visível.
**Pós-condições:** zona morta criada/atualizada, imediatamente considerada por UC07 (seleção de
torre) e UC11 (validação de segurança) para qualquer alvo dentro dela.
**Requisitos relacionados:** RF27.
**Status:** Implementado.

### UC19 — Alterar idioma

**Objetivo:** trocar o idioma da interface entre português (Brasil) e inglês (EUA).
**Atores:** Usuário.
**Pré-condições:** sessão autenticada ativa.
**Fluxo principal:**
1. O usuário seleciona o idioma desejado.
2. O sistema aplica o idioma imediatamente, sem reiniciar, e persiste a preferência.
**Fluxos alternativos/exceções:** nenhum relevante.
**Pós-condições:** interface exibida no idioma escolhido nas próximas sessões.
**Requisitos relacionados:** RF19, RF20.
**Status:** Implementado.

### UC20 — Alterar tema

**Objetivo:** trocar o tema visual da interface (claro, escuro, ou seguir o Windows).
**Atores:** Usuário.
**Pré-condições:** sessão autenticada ativa.
**Fluxo principal:**
1. O usuário seleciona o tema desejado.
2. O sistema aplica o tema imediatamente, sem reiniciar, e persiste a preferência.
**Fluxos alternativos/exceções:** nenhum relevante.
**Pós-condições:** interface exibida no tema escolhido nas próximas sessões.
**Requisitos relacionados:** RF19, RF21.
**Status:** Implementado.

### UC21 — Personalizar painel

**Objetivo:** ajustar a posição, o tamanho e a visibilidade dos cards do painel principal.
**Atores:** Usuário.
**Pré-condições:** tela do Painel Principal aberta (UC03).
**Fluxo principal:**
1. O usuário arrasta e/ou redimensiona um card.
2. O sistema impede a sobreposição (recusa o gesto que colidiria com outro card).
3. O sistema salva o novo layout por usuário ao final do gesto.
**Fluxos alternativos/exceções:**
1. O usuário aciona "Restaurar layout padrão" — o sistema descarta o layout salvo e volta à
   grade padrão.
**Pós-condições:** layout persistido, restaurado no próximo acesso do mesmo usuário.
**Requisitos relacionados:** RF24.
**Status:** Implementado.

### UC22 — Fixar/desafixar console

**Objetivo:** fixar o card de console de eventos na borda direita da tela de Monitoramento, fora
do canvas arrastável.
**Atores:** Usuário.
**Pré-condições:** tela de Monitoramento aberta (UC04).
**Fluxo principal:**
1. O usuário aciona a opção de fixar o console.
2. O sistema realoca o console para a faixa lateral fixa e persiste o estado.
**Fluxos alternativos/exceções:**
1. O usuário desafixa — o console volta ao canvas arrastável.
**Pós-condições:** estado fixado/não fixado persistido para o usuário.
**Requisitos relacionados:** RF26.
**Status:** Implementado.

### UC23 — Abrir chamado de ajuda

**Objetivo:** registrar um chamado de suporte descrevendo um problema ou dúvida.
**Atores:** Usuário.
**Pré-condições:** sessão autenticada ativa.
**Fluxo principal:**
1. O usuário abre o formulário de chamado (acessível pela barra superior).
2. Preenche título, descrição, categoria, módulo relacionado e, opcionalmente, mensagem de erro.
3. O sistema preenche usuário e data automaticamente e grava o chamado.
**Fluxos alternativos/exceções:**
1. Campos obrigatórios não preenchidos — o sistema impede o envio até completá-los.
**Pós-condições:** chamado registrado, disponível para tratamento administrativo (UC24, quando
implementado).
**Requisitos relacionados:** RF22.
**Status:** Implementado.

### UC24 — Gerenciar chamados

**Objetivo:** consultar os chamados abertos e definir situação e resposta para cada um.
**Atores:** Administrador.
**Pré-condições:** perfil Administrador (inferido — ainda não há checagem de permissão dedicada,
por não existir tela).
**Fluxo principal:**
1. O Administrador acessaria a lista de chamados abertos.
2. Selecionaria um chamado e definiria situação/resposta.
**Fluxos alternativos/exceções:** não aplicável no estado atual (ver Status).
**Pós-condições:** chamado atualizado (quando implementado).
**Requisitos relacionados:** RF23.
**Status:** Planejado — o repositório já expõe uma operação de atualização (situação/resposta),
mas nenhuma tela a consome hoje; o item de menu é um `PlaceholderView`.

### UC25 — Configurar Arduino

**Objetivo:** reunir, em uma aba dedicada, a configuração do ambiente de desenvolvimento Arduino
(caminho do CLI, placa, porta/baud) usada pelas ações de detecção, compilação e monitoramento.
**Atores:** Usuário.
**Pré-condições:** sessão autenticada ativa.
**Fluxo principal:**
1. O usuário acessa a aba "Configurações do Arduino".
2. O sistema carrega as preferências salvas (caminho do CLI, último sketch, placa, porta/baud).
3. A partir daqui, o usuário pode estender para UC26 (detectar CLI), UC27 (compilar) ou UC28
   (monitorar serial).
**Fluxos alternativos/exceções:** nenhum relevante neste caso de uso "guarda-chuva" — as exceções
específicas estão em UC26–UC28.
**Pós-condições:** ambiente configurado disponível para as ações da aba.
**Requisitos relacionados:** RF31.
**Status:** Implementado.

### UC26 — Detectar Arduino CLI

**Objetivo:** localizar o executável do Arduino CLI no computador.
**Atores:** Usuário. **Extensão de:** UC25.
**Pré-condições:** aba "Configurações do Arduino" aberta.
**Fluxo principal:**
1. O usuário aciona "Detectar automaticamente" (ou informa o caminho manualmente).
2. O sistema procura, em ordem, o caminho salvo, a pasta do próprio aplicativo, o `PATH` do
   Windows e locais comuns de instalação — nunca baixa nada automaticamente.
3. O sistema exibe a versão detectada.
**Fluxos alternativos/exceções:**
1. CLI não encontrado em nenhum local — o sistema informa que não foi encontrado, sem travar a
   aplicação.
**Pós-condições:** caminho do CLI configurado (ou permanece vazio, se não encontrado).
**Requisitos relacionados:** RF28.
**Status:** Implementado.

### UC27 — Compilar sketch

**Objetivo:** compilar um sketch `.ino` para a placa selecionada, acompanhando a saída em tempo
real.
**Atores:** Usuário. **Extensão de:** UC25. **Inclui:** UC26 (precisa do CLI já localizado).
**Pré-condições:** CLI detectado (UC26); sketch e placa (FQBN) selecionados.
**Fluxo principal:**
1. O usuário seleciona o arquivo `.ino` e a placa.
2. O usuário aciona "Compilar".
3. O sistema executa a compilação como processo filho assíncrono, exibindo a saída em tempo
   real.
4. O sistema determina sucesso/falha pelo código de saída do processo.
**Fluxos alternativos/exceções:**
1. O usuário cancela a compilação em andamento — o sistema encerra a árvore de processos e
   registra o cancelamento.
**Pós-condições:** resultado da compilação (sucesso, falha ou cancelamento) exibido no console.
**Requisitos relacionados:** RF29.
**Status:** Implementado.

### UC28 — Monitorar comunicação serial

**Objetivo:** acompanhar, na própria aba de configuração do Arduino, as mensagens trocadas pela
porta serial.
**Atores:** Usuário; Arduino (origem das mensagens). **Extensão de:** UC25.
**Pré-condições:** porta serial disponível; se já em uso pela tela de Monitoramento com
parâmetros diferentes, o sistema pede confirmação antes de reconectar.
**Fluxo principal:**
1. O usuário abre o monitor serial na aba de configuração.
2. O sistema reutiliza a mesma conexão da tela de Monitoramento (nunca abre uma segunda porta
   concorrente).
3. As mensagens enviadas pelo Arduino são exibidas em tempo real.
**Fluxos alternativos/exceções:**
1. Conflito de parâmetros com uma conexão já ativa — o sistema pergunta antes de desconectar e
   reconectar, nunca derruba a sessão silenciosamente.
**Pós-condições:** tráfego serial visível durante a sessão do monitor.
**Requisitos relacionados:** RF30.
**Status:** Implementado.

## 5. Matriz Caso de Uso × Requisito

| Caso de Uso | Requisito(s) relacionado(s) | Ator | Status |
|---|---|---|---|
| UC01 – Autenticar-se | RF09 | Usuário | Implementado |
| UC02 – Alterar senha | RF11 | Usuário | Implementado |
| UC03 – Visualizar painel | RF24 | Usuário | Implementado |
| UC04 – Monitorar radar | RF04 | Usuário | Implementado |
| UC05 – Receber/detectar alvos | RF01, RF02 | Arduino | Implementado |
| UC06 – Rastrear alvos | RF03 | — *(automático)* | Implementado |
| UC07 – Selecionar torre automaticamente | RF05 | — *(automático)* | Implementado |
| UC08 – Alterar modo de operação | RF08 | Administrador, Operador | Parcial |
| UC09 – Acompanhar alvo pelas torres | RF06, RF08 | — *(automático)* | Implementado (ver D1) |
| UC10 – Executar acionamento demonstrativo automático | RF06 | — *(automático)* | Implementado (ver D1) |
| UC11 – Validar regras de segurança | RF06, RNF04, RNF05, RF27 | — *(automático)* | Implementado |
| UC12 – Visualizar objetos detectados | RF12, RF13 | Usuário | Implementado |
| UC13 – Exportar objetos detectados | RF14 | Usuário | Implementado |
| UC14 – Importar objetos detectados | RF15 | Administrador, Operador | Implementado |
| UC15 – Consultar ações realizadas | RF16 | Usuário | Parcial |
| UC16 – Consultar histórico de modos | RF17 | Usuário | Parcial |
| UC17 – Gerenciar usuários | RF18 | Administrador | Planejado |
| UC18 – Gerenciar zonas mortas | RF27 | Administrador | Implementado |
| UC19 – Alterar idioma | RF19, RF20 | Usuário | Implementado |
| UC20 – Alterar tema | RF19, RF21 | Usuário | Implementado |
| UC21 – Personalizar painel | RF24 | Usuário | Implementado |
| UC22 – Fixar/desafixar console | RF26 | Usuário | Implementado |
| UC23 – Abrir chamado de ajuda | RF22 | Usuário | Implementado |
| UC24 – Gerenciar chamados | RF23 | Administrador | Planejado |
| UC25 – Configurar Arduino | RF31 | Usuário | Implementado |
| UC26 – Detectar Arduino CLI | RF28 | Usuário | Implementado |
| UC27 – Compilar sketch | RF29 | Usuário | Implementado |
| UC28 – Monitorar comunicação serial | RF30 | Usuário, Arduino | Implementado |

Todos os 28 casos de uso têm ao menos um requisito funcional válido associado; nenhum caso de
uso foi criado sem essa ligação. UC06, UC07, UC09, UC10 e UC11 não têm um ator humano/Arduino
associado por serem reações internas do sistema, mas continuam ligados a requisitos reais.

**RF10 (Controle de acesso por perfil) é o único requisito funcional válido sem um caso de uso
próprio** — deliberadamente: RF10 não é uma ação que um ator realiza, é a regra que decide
*quais* casos de uso cada ator vê/executa. Ele está representado estruturalmente no diagrama —
nas associações diferenciadas por perfil (ex.: só Administrador/Operador ligados a UC08/UC14; só
Administrador ligado a UC17/UC18/UC24) — em vez de como uma bolha própria, o que evitaria
duplicar, como caso de uso, algo que já é a explicação de por que os outros casos de uso têm os
atores que têm.

## 6. Estado atual versus sistema planejado

**Implementados (22):** UC01, UC02, UC03, UC04, UC05, UC06, UC07, UC09*, UC10*, UC11, UC12,
UC13, UC14, UC18, UC19, UC20, UC21, UC22, UC23, UC25, UC26, UC27, UC28.
*(UC09/UC10 implementados no sentido comportamental — acompanhar/acionar automaticamente já
funciona — mas rotulado nos modos antigos do sistema, não nos três estados Verde/Amarelo/
Vermelho da especificação revisada; ver D1.)*

**Parciais (3):** UC08 (modos ainda com nomenclatura antiga), UC15 e UC16 (registro de dados
funciona, tela de consulta não existe).

**Planejados (2):** UC17 (Gerenciar usuários) e UC24 (Gerenciar chamados) — nenhuma interface
implementada, só a base de dados/permissão que os sustentaria.

Nenhuma tela hoje em `PlaceholderView` (Ações Realizadas, Histórico de Modos, Usuários,
Chamados/Ajuda, Configurações) é representada como "Implementado" neste documento nem na matriz
acima — consistente com `Requisitos_Funcionais.md` e `Limitacoes_Conhecidas.md`.
