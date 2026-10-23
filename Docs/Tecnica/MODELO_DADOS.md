# Modelo de Dados

Este documento descreve as "tabelas" criadas para a Etapa 1 (fundação multiusuário):
estrutura, tipos, relacionamentos e o plano de migração para um banco relacional.

## 1. Tecnologia atual: SQLite local (único armazenamento)

Todo registro do sistema grava exclusivamente em **SQLite** (`radartorres.db`, um arquivo só —
`src/RadarTorres.App/Data/SqliteConnectionFactory.cs`), sem fallback para CSV. Local do
arquivo:

```
%AppData%\RadarTorres\Data\
└── radartorres.db   (SQLite — todas as tabelas)
```

`%AppData%` (por usuário do Windows, sem precisar de administrador) foi escolhido em vez da
pasta de instalação (`C:\Program Files\...`, somente leitura para usuários comuns) — ver
`src/RadarTorres.App/Data/AppDataPaths.cs`.

O único lugar do sistema que ainda produz um arquivo CSV é a exportação manual da tela
"Objetos Detectados" (`IObjetoDetectadoExportService.ExportCsv`, botão "Exportar CSV") — uma
ação explícita do usuário para gerar um arquivo à parte, não persistência (`CsvTableStore<T>`
em `src/RadarTorres.App/Data/CsvTableStore.cs` continua existindo só para esse fim).

Se o banco estiver momentaneamente inacessível (arquivo bloqueado, removido, sem permissão
etc.), cada `Sqlite*Repository` não perde a escrita nem trava a operação: registra o
INSERT/UPDATE equivalente em `pending-writes.sql` (mesma pasta do banco, ver
`src/RadarTorres.App/Data/PendingWriteQueue.cs`) e reaplica tudo automaticamente no próximo
início do app (`App.xaml.cs`, antes de qualquer outra leitura/escrita).

### Como cada tabela é criada

Não existe uma migration tradicional: cada `Sqlite*Repository`
(`src/RadarTorres.App/Repositories/`) cria sua tabela com `CREATE TABLE IF NOT EXISTS` na
primeira operação (`EnsureTable`, dentro do próprio repositório). Adicionar uma coluna =
adicionar uma propriedade no modelo + o campo correspondente no `Sqlite*Repository`.

### Evolução de esquema (colunas acrescentadas depois)

`CREATE TABLE IF NOT EXISTS` não faz nada numa tabela que já existe, então coluna nova nunca
apareceria no banco de quem já usava o sistema. Em vez de introduzir um framework de migração,
cada repositório declara o que passou a precisar e `Data/SqliteSchema.cs` aplica o que falta
(`PRAGMA table_info` + `ALTER TABLE ADD COLUMN` / `CREATE INDEX IF NOT EXISTS`), de forma
idempotente, a cada abertura de conexão. Nenhum dado é descartado: linhas antigas ficam com as
colunas novas nulas e com o valor padrão (`Estado = 'Detectado'`). É o caminho usado pelas
colunas de vínculo/ciclo de vida de `objetos_detectados` e pelas tabelas `ocorrencia_estados` e
`cameras`.

O JSON dos perfis de alvo (`perfis-alvo.json`) tem o equivalente em
`PerfilAlvoRepository.MigrarFotoUnica`: perfis gravados quando havia **uma** foto de referência
por alvo são convertidos, na leitura, para a lista de fotografias atual — sem reescrever o
arquivo até a próxima alteração, de modo que voltar à versão anterior do aplicativo continua
funcionando.

### Próxima etapa: sincronização para a nuvem

O SQLite local é a fonte da verdade para operação em tempo real (nunca depende de rede). A
etapa seguinte adiciona uma tabela de outbox (`sync_outbox`) e um serviço em segundo plano que
envia em lote, de forma assíncrona, os registros de auditoria (`objetos_detectados`,
`acoes_realizadas`, `modo_atual_torre`) para um banco gerenciado na nuvem via API HTTPS —
`usuarios` e `preferencias_usuario` permanecem só locais. Ver proposta de arquitetura
discutida com o time.

## 2. Diagrama de relacionamento

```mermaid
erDiagram
    OBJETOS_DETECTADOS ||--o{ ACOES_REALIZADAS : "originou (coordenadas)"
    OBJETOS_DETECTADOS ||--o{ OCORRENCIA_ESTADOS : "transita"
    CAMERAS ||--o{ OBJETOS_DETECTADOS : "detectou (quadrante)"
    USUARIOS ||--o{ MODO_ATUAL_TORRE : "confirma"
    USUARIOS ||--o{ CHAMADOS_AJUDA : "abre"
    USUARIOS ||--o| PREFERENCIAS_USUARIO : "tem"

    USUARIOS {
        int Id PK
        string Nome
        string Login UK
        string SenhaHash
        string SenhaSalt
        string Perfil
        bool Ativo
        datetime DataCriacao
        datetime UltimoAcesso "nullable"
    }

    OBJETOS_DETECTADOS {
        int Id PK
        string Tipo
        double X
        double Y
        double Z "nullable"
        string Quadrante
        datetime DataHora
        string Dispositivo
        double NivelConfianca "nullable"
        string Observacao "nullable"
        string ReferenciaImagem "nullable"
        int PerfilAlvoId "nullable - alvo cadastrado"
        string CameraDispositivoId "nullable"
        string CameraNome "nullable"
        string RastreamentoId "nullable - identidade da ocorrencia"
        string Estado "Detectado|AbatePendente|Abatido"
        datetime DataHoraUltimaDeteccao "nullable"
    }

    OCORRENCIA_ESTADOS {
        int Id PK
        int ObjetoDetectadoId FK
        string Estado "Detectado|AbatePendente|Abatido"
        datetime DataHora
        string Usuario "nullable - login de quem decidiu"
        string Observacao "nullable"
    }

    CAMERAS {
        int Id PK
        string DispositivoId UK "moniker DirectShow"
        string Nome
        string Quadrante UK "Q1..Q4"
        datetime AtualizadoEm
    }

    ACOES_REALIZADAS {
        int ID_ACAO PK
        int ID_TORRE "Id da torre (ver appsettings.json > Towers)"
        int ID_OBJETO FK "nullable — chave para as coordenadas em OBJETOS_DETECTADOS"
        datetime DH_ACAO
        string OBSERVACAO "nullable"
    }

    MODO_ATUAL_TORRE {
        int ID_MODO PK
        string MODOATUAL
        string MODOANTERIOR
        int ID_USER FK "nullable"
        datetime DHALTERACAO
    }

    HISTORICO_TREINAMENTO_GERACOES {
        int ID PK
        int GERACAO
        double MELHOR_FITNESS
        double FITNESS_MEDIO
        int ALVOS_ABATIDOS
        int ALVOS_ESCAPADOS
        datetime DH_REGISTRO
    }

    PREFERENCIAS_USUARIO {
        int UsuarioId PK-FK
        string Idioma
        string Tema "Claro | Escuro | Sistema"
        bool SidebarRecolhida
        string TelaInicial "nullable"
        int RegistrosPorPagina
    }

    CHAMADOS_AJUDA {
        int Id PK
        int UsuarioId FK
        string UsuarioNome
        string Titulo
        string Descricao
        string Categoria
        string ModuloRelacionado "nullable"
        string MensagemErro "nullable"
        datetime DataHoraEnvio
        string Status "Aberto | EmAnalise | Resolvido | Cancelado"
        string RespostaAdmin "nullable"
        datetime DataResolucao "nullable"
    }
```

**Nota sobre chaves estrangeiras**: `ID_OBJETO` e `ID_USER` são referências por **Id**, mas sem
`FOREIGN KEY` declarada nas tabelas SQLite atuais — a consistência é garantida pelo código
(`IAuthService.CurrentUser`), não pelo armazenamento.

## 3. Detalhe de cada tabela

### `usuarios`
Contas de acesso ao aplicativo (login independente da conta do Windows).
- **Perfil**: `Administrador` | `Operador` | `Visualizador` (ver Requisito 7).
- **SenhaHash/SenhaSalt**: PBKDF2-HMACSHA256, 100.000 iterações, salt de 128 bits por
  usuário (`src/RadarTorres.App/Services/PasswordHasher.cs`). Nunca a senha em texto puro.
- Semeado automaticamente no primeiro uso com `admin` / `admin123`
  (`src/RadarTorres.App/Data/DataSeeder.cs`) — **troque a senha padrão em produção** via
  "Perfil > Alterar senha".

### `objetos_detectados`
Histórico de **ocorrências** de detecção (Requisito 4). Uma linha por ocorrência — nunca por
quadro de vídeo nem por alvo cadastrado:

- **Radar/Arduino**: uma linha na **primeira detecção** de um alvo (não a cada atualização de
  posição — ver `MainViewModel.OnTargetCreated`), para não inflar a tabela a cada ciclo de
  leitura (~150ms).
- **Câmera**: uma linha por **acompanhamento** (`RastreamentoId`) em cada quadrante. Enquanto o
  mesmo objeto continua sendo acompanhado pela mesma câmera, nada é inserido — só
  `DataHoraUltimaDeteccao` avança (`OcorrenciaDeteccaoService`). Um único alvo cadastrado pode,
  assim, originar até quatro ocorrências, uma por quadrante, sem duplicar dentro de um deles.

Colunas de vínculo e ciclo de vida:

- **PerfilAlvoId** — o alvo CADASTRADO (`perfis-alvo.json`) que originou a ocorrência; `null`
  nas detecções do radar/simulador, que não classificam o objeto. É este vínculo, e não o texto
  em `Tipo`, que mantém o histórico associado ao alvo quando o cadastro é editado.
- **CameraDispositivoId / CameraNome** — a câmera responsável. O nome é redundante de propósito:
  a listagem continua legível com a câmera desconectada.
- **RastreamentoId** — identidade da **ocorrência** (acompanhamento entre quadros), distinta da
  identidade do alvo cadastrado. `null` no radar e nos registros gravados antes deste mecanismo.
- **Estado** — `Detectado` | `AbatePendente` | `Abatido`. Sai de `Detectado` apenas por evento
  explícito: acionamento bem-sucedido (`FireControlService`) leva a `AbatePendente`, e só a
  confirmação de um operador na tela "Objetos Detectados" leva a `Abatido`. **Desaparecer da
  imagem nunca altera o estado** — é oclusão ou falha de detecção, não abate.
- **Z**: sempre `null` hoje — os sensores do Arduino são 2D (ângulo + distância). Campo
  mantido para sensores 3D futuros (decisão confirmada com o usuário).

Índice único **parcial** `(RastreamentoId, Quadrante) WHERE RastreamentoId IS NOT NULL`: é a
garantia de última instância contra duplicidade quando várias câmeras processam em paralelo
(uma thread por câmera) — a inserção usa `ON CONFLICT … DO NOTHING` e devolve a linha vencedora.
Parcial para não limitar o radar, que não tem rastreamento visual e pode repetir quadrante.

### `ocorrencia_estados`
Histórico de transições de `objetos_detectados.Estado` — somente inserção. Existe para o estado
atual nunca ser a única informação disponível: a auditoria precisa de quando a ocorrência foi
aberta, quando o acionamento a deixou pendente e quem confirmou ou descartou o abate (`Usuario`).

### `cameras`
Qual câmera monitora qual quadrante (tela "Câmeras"). `DispositivoId` é o moniker DirectShow —
estável para o mesmo dispositivo na mesma máquina, que é o escopo do banco. Dois índices
**únicos** (`DispositivoId` e `Quadrante`) impõem a regra "uma câmera, um quadrante; um
quadrante, uma câmera"; atribuir um quadrante já ocupado transfere a responsabilidade na mesma
transação, em vez de falhar.

### `acoes_realizadas`
Auditoria de acionamentos (Requisito 5) — **somente inserção**: o repositório
(`IAcaoRealizadaRepository`) não expõe Update/Delete de propósito. Gravado em
`FireControlService.TryFireAsync`, o único ponto do sistema por onde todo acionamento passa,
mas só quando o comando chega a ser efetivamente enviado com sucesso — tentativas bloqueadas
por segurança ou com erro de envio geram apenas log (`ILoggingService`), sem linha aqui: o
schema simplificado (alinhado com o orientador, ver
`Docs/Documentos_Entregaveis/Banco_de_Dados/schema.sql`) não tem coluna de resultado para
distingui-las de um acionamento real. `ID_TORRE` é o Id da torre (`Tower.Id`, ver
`appsettings.json > Towers`), não mais um texto combinando torre+ação.

### `modo_atual_torre`
Auditoria de troca de modo (Requisito 6) — também somente inserção, mas só da troca
confirmada: cancelamento na caixa de confirmação gera apenas log (`MainViewModel.CurrentMode`
setter), sem coluna de resultado no schema simplificado para diferenciar de uma troca real.
`ID_USER` referencia `usuarios.Id` (antes era o `Login` em texto).

### `historico_treinamento_geracoes`
Comportamento do melhor indivíduo de cada geração treinada na tela "Treinamento do
Controlador" (neuroevolução do controlador híbrido heurística+rede neural —
`src/RadarTorres.App/NeuralEvolution/`). Diferente das demais tabelas desta lista, é um
**buffer circular limitado a 300
linhas**: ao inserir a 301ª, `SqliteHistoricoTreinamentoRepository.Registrar` descarta o
registro de `ID` mais antigo na mesma transação (`DELETE ... WHERE ID NOT IN (SELECT ID ...
ORDER BY ID DESC LIMIT 300)`) — não é um TTL por data, é estritamente "os 300 mais recentes".
Uma linha por **geração** (não por indivíduo da população), com `ALVOS_ABATIDOS`/
`ALVOS_ESCAPADOS` do melhor genoma daquela geração.

### `preferencias_usuario`
Uma linha por usuário (Requisito 8) — idioma, tema, sidebar recolhida/expandida. Colunas de
personalização mais granular (ordem dos cartões, colunas por tabela) ficam reservadas para a
Etapa 2.

### `chamados_ajuda`
Chamados de suporte (Requisito 9) — suporta atualização (`Update`) porque um administrador
pode alterar `Status`/`RespostaAdmin` ao tratar o chamado; usuário e data de envio são
preenchidos automaticamente pelo sistema, nunca digitados.

## Ver também

- [[Modelo_Banco_de_Dados]] — versão deste modelo para o entregável acadêmico (DER, schema SQL).
- [[DOCUMENTACAO_TECNICA]] — classes/repositórios que leem e gravam estas tabelas.
- [[Matriz_de_Rastreabilidade]] — requisito → tabela/coluna.
