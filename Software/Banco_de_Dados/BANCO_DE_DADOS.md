Funcionalidade: documenta o modelo de dados do RadarTorres (schema, tabelas, chaves, DER) — visão
técnica interna e visão acadêmica/entregável, consolidadas em um único documento.

# Banco de Dados

> Consolida `MODELO_DADOS.md` (enquadramento de desenvolvimento) e `Modelo_Banco_de_Dados.md`
> (enquadramento acadêmico/entregável, com o DER) em um único documento, eliminando a duplicação
> do diagrama de relacionamento entre eles.

## 1. Situação real hoje: SQLite é a única persistência

O projeto usa **SQLite** como único mecanismo de persistência. A migração CSV → SQL, descrita em
versões anteriores deste documento como plano futuro, **já foi implementada**: o contêiner de DI
em `App.xaml.cs:141-146` registra `Sqlite*Repository` diretamente para as 5 tabelas (`Usuario`,
`ObjetoDetectado`, `AcaoRealizada`, `ModoAtualTorre`, `PreferenciasUsuario`). Não existem mais
classes `Csv*Repository`/`Resilient*Repository` de persistência no repositório.

Local do arquivo (por usuário do Windows, sem precisar de administrador — ver
`src/RadarTorres.App/Data/AppDataPaths.cs`, escolhido em vez de `C:\Program Files\...`, somente
leitura para usuários comuns):

```
%AppData%\RadarTorres\Data\
└── radartorres.db   (SQLite — todas as tabelas)
```

Se uma gravação falhar (banco temporariamente indisponível, arquivo bloqueado, sem permissão
etc.), cada `Sqlite*Repository` não perde a escrita nem trava a operação: registra o
INSERT/UPDATE equivalente em `pending-writes.sql` (mesma pasta do banco, ver
`src/RadarTorres.App/Data/PendingWriteQueue.cs`) e reaplica tudo automaticamente no próximo início
do app (`App.xaml.cs`, antes de qualquer outra leitura/escrita) — não há perda silenciosa de
dados.

O único lugar do sistema que ainda produz um arquivo CSV é a exportação manual da tela "Objetos
Detectados" (`IObjetoDetectadoExportService.ExportCsv`, botão "Exportar CSV") — uma ação explícita
do usuário para gerar um arquivo à parte, não persistência (`CsvTableStore<T>` em
`src/RadarTorres.App/Data/CsvTableStore.cs` continua existindo só para esse fim).

**Integridade referencial** NÃO é garantida pelo app real. Nenhum `Sqlite*Repository.cs` em
`src/RadarTorres.App/Repositories/` executa `PRAGMA foreign_keys = ON`, e a maioria das colunas
que funcionam como chave lógica — `ID_OBJETO` (`acoes_realizadas`) e `ID_USER`
(`modo_atual_torre`) — são declaradas como `INTEGER NULL` simples, sem cláusula `REFERENCES`
alguma. As únicas exceções são `OcorrenciaEstados.ObjetoDetectadoId` e
`classificacoes_alvo.ObjetoDetectadoId`, que declaram `REFERENCES objetos_detectados (Id)`
inline — mas, sem `PRAGMA foreign_keys = ON`, o SQLite não aplica essa restrição mesmo assim; é só
documentação embutida no DDL, não enforcement. Chaves estrangeiras reais e com
`PRAGMA foreign_keys = ON` existem apenas no schema **acadêmico/idealizado**
`Software/Banco_de_Dados/schema.sql`, que não é o schema usado pelo app em produção.

### Como cada tabela é criada

Não existe uma migration tradicional: cada `Sqlite*Repository`
(`src/RadarTorres.App/Repositories/`) cria sua tabela com `CREATE TABLE IF NOT EXISTS` na primeira
operação (`EnsureTable`, dentro do próprio repositório). Adicionar uma coluna = adicionar uma
propriedade no modelo + o campo correspondente no `Sqlite*Repository`.

### Evolução de esquema (colunas acrescentadas depois)

`CREATE TABLE IF NOT EXISTS` não faz nada numa tabela que já existe, então coluna nova nunca
apareceria no banco de quem já usava o sistema. Em vez de introduzir um framework de migração,
cada repositório declara o que passou a precisar e `Data/SqliteSchema.cs` aplica o que falta
(`PRAGMA table_info` + `ALTER TABLE ADD COLUMN` / `CREATE INDEX IF NOT EXISTS`), de forma
idempotente, a cada abertura de conexão. Nenhum dado é descartado: linhas antigas ficam com as
colunas novas nulas e com o valor padrão (`Estado = 'Detectado'`). É o caminho usado pelas colunas
de vínculo/ciclo de vida de `objetos_detectados` e pelas tabelas `ocorrencia_estados` e `cameras`.

O JSON dos perfis de alvo (`perfis-alvo.json`) tem o equivalente em
`PerfilAlvoRepository.MigrarFotoUnica`: perfis gravados quando havia **uma** foto de referência
por alvo são convertidos, na leitura, para a lista de fotografias atual — sem reescrever o arquivo
até a próxima alteração, de modo que voltar à versão anterior do aplicativo continua funcionando.

### Próxima etapa: sincronização para a nuvem

O SQLite local é a fonte da verdade para operação em tempo real (nunca depende de rede). A etapa
seguinte adiciona uma tabela de outbox (`sync_outbox`) e um serviço em segundo plano que envia em
lote, de forma assíncrona, os registros de auditoria (`objetos_detectados`, `acoes_realizadas`,
`modo_atual_torre`) para um banco gerenciado na nuvem via API HTTPS — `usuarios` e
`preferencias_usuario` permanecem só locais.

## 2. Especificações técnicas do SGBD

* **SGBD**: SQLite (embarcado, arquivo único, sem servidor).
* **Driver/linguagem de acesso**: `Microsoft.Data.Sqlite` versão **9.0.0**
  (`src/RadarTorres.App/RadarTorres.App.csproj:50`), consumido em C#/.NET 9.
* **Versão do engine SQLite**: não pinada separadamente pelo projeto — vem embutida no pacote
  `Microsoft.Data.Sqlite` 9.0.0 via `SQLitePCLRaw`; não há `packages.lock.json` no repositório
  fixando o número exato da build nativa.
* **Codificação**: UTF-8 (padrão do SQLite para colunas `TEXT`; não há `PRAGMA encoding`
  customizado em `schema.sql`).
* **Collation**: `BINARY` (padrão do SQLite; nenhuma coluna declara `COLLATE NOCASE` ou outra em
  `schema.sql`).

### Índices (completo, de `schema.sql`)

| Índice | Tabela | Coluna(s) | Tipo |
|---|---|---|---|
| `idx_acoes_realizadas_torre` | `acoes_realizadas` | `ID_TORRE` | normal |
| `idx_objetos_detectados_datahora` | `objetos_detectados` | `DataHora` | normal |
| `idx_acoes_realizadas_datahora` | `acoes_realizadas` | `DH_ACAO` | normal |
| `idx_modo_atual_torre_usuario` | `modo_atual_torre` | `ID_USER` | normal |
| `idx_objetos_detectados_perfil` | `objetos_detectados` | `PerfilAlvoId` | normal |
| `idx_ocorrencia_estados_objeto` | `ocorrencia_estados` | `ObjetoDetectadoId` | normal |
| `idx_objetos_detectados_rastreamento` | `objetos_detectados` | `RastreamentoId, Quadrante` | único, parcial (`WHERE RastreamentoId IS NOT NULL`) |
| `idx_cameras_dispositivo` | `cameras` | `DispositivoId` | único |
| `idx_cameras_quadrante` | `cameras` | `Quadrante` | único |

Além desses, `Login` (`usuarios`) e `UsuarioId` (`preferencias_usuario`, PK) já têm índice
implícito por `UNIQUE`/`PRIMARY KEY`.

## 3. Diagrama de relacionamento (DER)

```mermaid
erDiagram
    OBJETOS_DETECTADOS ||--o{ ACOES_REALIZADAS : "originou (coordenadas)"
    OBJETOS_DETECTADOS ||--o{ OCORRENCIA_ESTADOS : "transita"
    CAMERAS ||--o{ OBJETOS_DETECTADOS : "detectou (quadrante)"
    USUARIOS ||--o{ MODO_ATUAL_TORRE : "confirma"
    USUARIOS ||--o| PREFERENCIAS_USUARIO : "tem"
    OBJETOS_DETECTADOS ||--o{ CLASSIFICACOES_ALVO : "classifica"

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

    MARCOS_FITNESS_TREINAMENTO {
        int ID PK
        int GERACAO_MARCO UK "a cada 500 geracoes"
        double MELHOR_FITNESS
        double FITNESS_MEDIO
        datetime DH_REGISTRO
    }

    INSTANTANEOS_COMPORTAMENTO_MELHOR_GENE {
        int ID PK
        int GERACAO_MARCO UK "a cada 1000 geracoes"
        double PONTUACAO
        int ALVOS_ABATIDOS
        int ALVOS_ESCAPADOS
        int TIROS_ERRADOS
        double TAXA_ACERTO
        double TAXA_COBERTURA
        string GENOMA_JSON "genoma inteiro serializado"
        datetime DH_REGISTRO
    }

    CLASSIFICACOES_ALVO {
        int ID PK
        int ObjetoDetectadoId FK "nullable - pode nao existir ainda no radar"
        string GrauPerigosidade "Baixo | Medio | Alto | Critico"
        double VelocidadeEstimada
        datetime DH_REGISTRO
    }
```

**Nota sobre chaves estrangeiras**: `ID_OBJETO` e `ID_USER` são referências por **Id**, mas sem
`FOREIGN KEY` declarada nas tabelas SQLite atuais — a consistência é garantida pelo código
(`IAuthService.CurrentUser`), não pelo armazenamento.

## 4. Detalhe de cada tabela

### `usuarios`
Contas de acesso ao aplicativo (login independente da conta do Windows).
- **Perfil**: `Administrador` | `Operador` | `Visualizador` (ver Requisito 7).
- **SenhaHash/SenhaSalt**: PBKDF2-HMACSHA256, 100.000 iterações, salt de 128 bits por usuário
  (`src/RadarTorres.App/Services/PasswordHasher.cs`). Nunca a senha em texto puro.
- Semeado automaticamente no primeiro uso com `admin` / `admin123`
  (`src/RadarTorres.App/Data/DataSeeder.cs`) — **troque a senha padrão em produção** via
  "Perfil > Alterar senha".

### `objetos_detectados`
Histórico de **ocorrências** de detecção (Requisito 4). Uma linha por ocorrência — nunca por
quadro de vídeo nem por alvo cadastrado:

- **Radar/Arduino**: uma linha na **primeira detecção** de um alvo (não a cada atualização de
  posição — ver `MainViewModel.OnTargetCreated`), para não inflar a tabela a cada ciclo de leitura
  (~150ms).
- **Câmera**: uma linha por **acompanhamento** (`RastreamentoId`) em cada quadrante. Enquanto o
  mesmo objeto continua sendo acompanhado pela mesma câmera, nada é inserido — só
  `DataHoraUltimaDeteccao` avança (`OcorrenciaDeteccaoService`). Um único alvo cadastrado pode,
  assim, originar até quatro ocorrências, uma por quadrante, sem duplicar dentro de um deles —
  garantido pelo índice único parcial `(RastreamentoId, Quadrante) WHERE RastreamentoId IS NOT
  NULL`: é a garantia de última instância contra duplicidade quando várias câmeras processam em
  paralelo (uma thread por câmera) — a inserção usa `ON CONFLICT … DO NOTHING` e devolve a linha
  vencedora. Parcial para não limitar o radar, que não tem rastreamento visual e pode repetir
  quadrante.

Colunas de vínculo e ciclo de vida:

- **PerfilAlvoId** — o alvo CADASTRADO (`perfis-alvo.json`) que originou a ocorrência; `null` nas
  detecções do radar/simulador, que não classificam o objeto. É este vínculo, e não o texto em
  `Tipo`, que mantém o histórico associado ao alvo quando o cadastro é editado.
- **CameraDispositivoId / CameraNome** — a câmera responsável. O nome é redundante de propósito: a
  listagem continua legível com a câmera desconectada.
- **RastreamentoId** — identidade da **ocorrência** (acompanhamento entre quadros), distinta da
  identidade do alvo cadastrado. `null` no radar e nos registros gravados antes deste mecanismo.
- **Estado** — `Detectado` | `AbatePendente` | `Abatido`. Sai de `Detectado` apenas por evento
  explícito: acionamento bem-sucedido (`FireControlService`) leva a `AbatePendente`, e só a
  confirmação de um operador na tela "Objetos Detectados" leva a `Abatido`. **Desaparecer da
  imagem nunca altera o estado** — é oclusão ou falha de detecção, não abate.
- **Z**: sempre `null` hoje — os sensores do Arduino são 2D (ângulo + distância). Campo mantido
  para sensores 3D futuros (decisão confirmada com o usuário).

### `ocorrencia_estados`
Histórico de transições de `objetos_detectados.Estado` — somente inserção. Existe para o estado
atual nunca ser a única informação disponível: a auditoria precisa de quando a ocorrência foi
aberta, quando o acionamento a deixou pendente e quem confirmou ou descartou o abate (`Usuario`).

### `cameras`
Qual câmera monitora qual quadrante (tela "Câmeras"). `DispositivoId` é o moniker DirectShow —
estável para o mesmo dispositivo na mesma máquina, que é o escopo do banco. Dois índices **únicos**
(`DispositivoId` e `Quadrante`) impõem a regra "uma câmera, um quadrante; um quadrante, uma
câmera"; atribuir um quadrante já ocupado transfere a responsabilidade na mesma transação, em vez
de falhar.

### `acoes_realizadas`
Auditoria de acionamentos (Requisito 5) — **somente inserção**: o repositório
(`IAcaoRealizadaRepository`) não expõe Update/Delete de propósito. Gravado em
`FireControlService.TryFireAsync` (`:105`), o único ponto do sistema por onde todo acionamento
passa, mas só quando o comando chega a ser efetivamente enviado com sucesso — tentativas
bloqueadas por segurança ou com erro de envio geram apenas log (`ILoggingService`), sem linha
aqui: o schema simplificado (alinhado com o orientador, ver `schema.sql` nesta mesma pasta) não
tem coluna de resultado para distingui-las de um acionamento real. `ID_TORRE` é o Id da torre
(`Tower.Id`, ver `appsettings.json > Towers`), não mais um texto combinando torre+ação.

### `modo_atual_torre`
Auditoria de troca de modo (Requisito 6) — também somente inserção, mas só da troca confirmada:
cancelamento na caixa de confirmação gera apenas log (`MainViewModel.CurrentMode` setter, em torno
da linha 291), sem coluna de resultado no schema simplificado para diferenciar de uma troca real.
`ID_USER` referencia `usuarios.Id` (antes era o `Login` em texto).

### `historico_treinamento_geracoes`
Comportamento do melhor indivíduo de cada geração treinada na tela "Treinamento do Controlador"
(neuroevolução do controlador híbrido heurística+rede neural —
`src/RadarTorres.App/NeuralEvolution/`). Diferente das demais tabelas desta lista, é um **buffer
circular limitado a 300 linhas**: ao inserir a 301ª, `SqliteHistoricoTreinamentoRepository.Registrar`
descarta o registro de `ID` mais antigo na mesma transação (`DELETE ... WHERE ID NOT IN (SELECT ID
... ORDER BY ID DESC LIMIT 300)`) — não é um TTL por data, é estritamente "os 300 mais recentes".
Uma linha por **geração** (não por indivíduo da população), com `ALVOS_ABATIDOS`/
`ALVOS_ESCAPADOS` do melhor genoma daquela geração. Índice normal em `GERACAO`
(`idx_historico_treinamento_geracao`).

### `marcos_fitness_treinamento`
Fitness agregado (médio e melhor) de cada bloco de 500 gerações de treinamento — complementar a
`historico_treinamento_geracoes`, mas SEM limite de linhas: um marco a cada 500 gerações nunca
acumula volume relevante, e é exatamente a tendência de longo prazo (sem o ruído geração a geração)
que o painel "Estatísticas de Treinamento" (exclusivo do Administrador) precisa. Só inserção;
`GeracaoMarco` é único (`idx_marcos_fitness_geracao`).

### `instantaneos_comportamento_melhor_gene`
Retrato completo do melhor genoma a cada 1.000 gerações — pontuação, decisões (abatidos/
escapados/tiros errados), taxas derivadas (acerto, cobertura) e o **genoma inteiro serializado** em
`GenomaJson`, para poder recarregar e reproduzir exatamente aquele comportamento depois. Só
inserção, sem limite de linhas e `GeracaoMarco` único (`idx_instantaneos_comportamento_geracao`) —
nunca sobrescrito por gerações seguintes, de propósito: é o histórico comparativo marco a marco
que permite estudar caso a caso se a evolução do treinamento é real ou só oscilação de ruído (ver
`TreinamentoControladorViewModel.RegistrarInstantaneoDeComportamentoSeCompletouBloco`).

### `classificacoes_alvo`
Auditoria de mudança de grau de perigosidade (`Baixo`/`Medio`/`Alto`/`Critico`) conforme a
velocidade estimada do alvo — `Helpers/ClassificadorPerigoAlvo.cs`. Só inserção, e só quando o
grau MUDA (`MainViewModel.RegistrarClassificacaoDePerigoSeMudou`), não a cada leitura do sensor —
senão seria uma linha por quadro. `ObjetoDetectadoId` referencia `objetos_detectados.Id`
(`INTEGER NULL REFERENCES objetos_detectados (Id)` — declarado no DDL mas não aplicado, já que o
app não ativa `PRAGMA foreign_keys`), podendo ser `NULL` nas primeiras leituras do radar, antes do
primeiro registro.

### `preferencias_usuario`
Uma linha por usuário (Requisito 8) — idioma, tema, sidebar recolhida/expandida. Colunas de
personalização mais granular (ordem dos cartões, colunas por tabela) ficam reservadas para a
Etapa 2.

## 5. Migração CSV → SQLite (histórico)

Esta seção documenta, em retrospecto, a migração que era descrita em versões anteriores como
"proposta futura" e que **já foi concluída**. O modelo de dados em si não mudou de estrutura entre
CSV e SQLite (mesmas tabelas/colunas, já mostradas em §4) — a mudança foi só no mecanismo de
armazenamento.

Passos efetivamente executados (refletidos no código atual):

1. Cada interface de repositório já existente (`IUsuarioRepository`, `IObjetoDetectadoRepository`,
   `IAcaoRealizadaRepository`, `IModoAtualTorreRepository`, `IPreferenciasUsuarioRepository`)
   ganhou uma implementação `Sqlite*Repository`
   (`src/RadarTorres.App/Repositories/Sqlite*Repository.cs`), mantendo a mesma assinatura de
   método que a versão CSV anterior — cada classe define o schema da sua tabela em um método
   `EnsureTable`.
2. O registro no contêiner de DI em `App.xaml.cs:141-146` foi trocado de `Csv*Repository` para
   `Sqlite*Repository` para as 5 tabelas.
3. Nenhum ViewModel, Service ou View precisou mudar — todos dependiam só da interface, como
   previsto no plano original.
4. Um mecanismo de resiliência foi adicionado além do que estava previsto originalmente: falhas de
   escrita são enfileiradas em `pending-writes.sql`
   (`src/RadarTorres.App/Data/PendingWriteQueue.cs`) e reaplicadas automaticamente na próxima
   inicialização, em vez de simplesmente propagar o erro.
5. O módulo `ChamadoAjuda` (model, repositório, tela, entrada de menu) foi removido do projeto
   antes da conclusão desta etapa — por isso não há tabela `chamados_ajuda` no schema atual.

Com o SGBD relacional, as referências que antes eram por texto (`Login`) passaram a ser chaves
estrangeiras reais por `Id` (`ID_OBJETO` → `objetos_detectados.Id`, `ID_USER` → `usuarios.Id`),
com integridade garantida pelo próprio banco (`PRAGMA foreign_keys = ON`) no schema acadêmico — ver
aviso de integridade referencial em §1.

O arquivo `schema.sql` (nesta mesma pasta) reflete esse schema acadêmico/idealizado e é usado para
visualização em ferramentas de modelagem (ex.: SQL Developer via driver JDBC SQLite); o arquivo de
dados real da aplicação é `radartorres.db`.

## Ver também

- [[DOCUMENTACAO_TECNICA]] — classes/repositórios que leem e gravam estas tabelas.
- [[Matriz_de_Rastreabilidade]] — requisito → tabela/coluna.
- [[UML_RadarTorres]] — diagrama de classes conceitual relacionado às entidades aqui descritas.
