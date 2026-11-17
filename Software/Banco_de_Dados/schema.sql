-- Schema relacional do RadarTorres, espelhando o modelo de dados atual (SQLite) descrito em
-- Software/Banco_de_Dados/BANCO_DE_DADOS.md e as classes em src/RadarTorres.App/Models/.
-- Gerado para visualização em SQL Developer (driver JDBC SQLite) — ver README.md desta pasta.
--
-- Nomenclatura de tabelas: siglas de 6 letras (TABxxx). TABDIC documenta o nome completo de cada
-- sigla e TABOPC documenta cada opção válida das colunas que antes só tinham CHECK(... IN (...)).

PRAGMA foreign_keys = ON;

CREATE TABLE TABUSU (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    Nome           TEXT    NOT NULL,
    Login          TEXT    NOT NULL UNIQUE,
    SenhaHash      TEXT    NOT NULL,
    SenhaSalt      TEXT    NOT NULL,
    Perfil         TEXT    NOT NULL CHECK (Perfil IN ('Administrador', 'Operador', 'Visualizador')),
    Ativo          INTEGER NOT NULL DEFAULT 1,
    DataCriacao    TEXT    NOT NULL,
    UltimoAcesso   TEXT
);

-- Uma linha por OCORRÊNCIA de detecção (não por alvo cadastrado e não por quadro): enquanto o
-- mesmo rastreamento continua sendo acompanhado no mesmo quadrante, só DataHoraUltimaDeteccao
-- avança. O mesmo alvo pode originar até quatro ocorrências, uma por quadrante.
CREATE TABLE TABOBJ (
    Id                      INTEGER PRIMARY KEY AUTOINCREMENT,
    Tipo                    TEXT    NOT NULL,
    X                       REAL    NOT NULL,
    Y                       REAL    NOT NULL,
    Z                       REAL,
    Quadrante               TEXT    NOT NULL,
    DataHora                TEXT    NOT NULL,
    Dispositivo             TEXT    NOT NULL,
    NivelConfianca          REAL,
    Observacao              TEXT,
    ReferenciaImagem        TEXT,
    -- Alvo cadastrado que originou a ocorrência (NULL nas detecções do radar/simulador).
    PerfilAlvoId            INTEGER,
    -- Câmera responsável pela detecção (NULL quando a origem não é câmera).
    CameraDispositivoId     TEXT,
    CameraNome              TEXT,
    -- Identidade do acompanhamento que gerou a ocorrência — identidade da OCORRÊNCIA, não do
    -- alvo cadastrado. NULL no radar e nos registros anteriores a este mecanismo.
    RastreamentoId          TEXT,
    Estado                  TEXT    NOT NULL DEFAULT 'Detectado'
                                    CHECK (Estado IN ('Detectado', 'AbatePendente', 'Abatido')),
    DataHoraUltimaDeteccao  TEXT
);

-- Histórico de transições de cada ocorrência. Estado NÃO guarda mais o nome do ciclo de abate
-- (Detectado/AbatePendente/Abatido, ver TABOBJ.Estado para o estado ATUAL) — guarda
-- o ID_MODO de TABMOD vigente no instante da transição. NULL quando nenhum modo havia
-- sido registrado ainda.
CREATE TABLE TABOCE (
    Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    ObjetoDetectadoId  INTEGER NOT NULL REFERENCES TABOBJ (Id),
    Estado             INTEGER REFERENCES TABMOD (ID_MODO),
    DataHora           TEXT    NOT NULL,
    Usuario            TEXT,
    Observacao         TEXT,
    -- Sessão de quem desencadeou a transição (NULL para transições automáticas — ocorrência
    -- aberta pela câmera, sem operador por trás). Ver TABSES.
    SessaoId           TEXT    REFERENCES TABSES (ID_SESSAO)
);

-- Qual câmera monitora qual quadrante. Uma câmera responde por exatamente um quadrante e cada
-- quadrante tem no máximo uma câmera — garantido pelos dois índices únicos abaixo.
CREATE TABLE TABCAM (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    DispositivoId  TEXT    NOT NULL,
    Nome           TEXT    NOT NULL,
    Quadrante      TEXT    NOT NULL CHECK (Quadrante IN ('Q1', 'Q2', 'Q3', 'Q4')),
    AtualizadoEm   TEXT    NOT NULL
);

CREATE TABLE TABACA (
    ID_ACAO               INTEGER PRIMARY KEY AUTOINCREMENT,
    ID_TORRE              INTEGER NOT NULL,
    ID_OBJETO             INTEGER REFERENCES TABOBJ (Id),
    DH_ACAO                TEXT   NOT NULL,
    OBSERVACAO             TEXT,
    -- Câmera que originou a ocorrência por trás da ação (resolvida via ID_OBJETO no momento do
    -- acionamento) — mesma convenção documentária de TABOBJ.CameraDispositivoId.
    CameraDispositivoId    TEXT    REFERENCES TABCAM (DispositivoId),
    -- Sessão de quem desencadeou o disparo. Ver TABSES.
    SessaoId                TEXT   REFERENCES TABSES (ID_SESSAO)
);

-- "AcoesRealizadas" (PascalCase) existiu como tabela órfã de uma versão anterior do schema em
-- bancos já instalados — nenhum código atual cria, lê ou escreve nela; TABACA acima é
-- a única em uso real. Repositório remove a órfã ao abrir o banco (ver
-- SqliteAcaoRealizadaRepository.CriarEsquema).

CREATE TABLE TABMOD (
    ID_MODO       INTEGER PRIMARY KEY AUTOINCREMENT,
    MODOATUAL     TEXT    NOT NULL,
    MODOANTERIOR  TEXT    NOT NULL,
    ID_USER       INTEGER REFERENCES TABUSU (Id),
    DHALTERACAO   TEXT    NOT NULL,
    -- Sessão de quem confirmou a troca. Ver TABSES.
    SessaoId      TEXT    REFERENCES TABSES (ID_SESSAO)
);

-- Uma sessão de uso do sistema: aberta no login (uma por aba — várias podem estar abertas ao
-- mesmo tempo, uma por usuário logado em cada aba) e encerrada no logout ou ao fechar a aba. É o
-- "código de sessão" referenciado pelas tabelas de auditoria acima: consultando o ID_SESSAO de
-- uma ação dá pra recuperar quem estava no sistema e em que janela de tempo exata (data, hora,
-- minuto, segundo) ela aconteceu.
CREATE TABLE TABSES (
    ID_SESSAO       TEXT    PRIMARY KEY,
    UsuarioId       INTEGER NOT NULL REFERENCES TABUSU (Id),
    DataHoraInicio  TEXT    NOT NULL,
    DataHoraFim     TEXT
);

-- Buffer circular com o comportamento do melhor indivíduo de cada geração treinada na tela
-- "Treinamento do Controlador" (neuroevolução do controlador híbrido heurística+rede neural —
-- ver src/RadarTorres.App/NeuralEvolution/). Limitado a 300 linhas: ao inserir a 301ª, o
-- registro de ID mais antigo é descartado (ver SqliteHistoricoTreinamentoRepository.Registrar).
CREATE TABLE TABHTG (
    ID              INTEGER PRIMARY KEY AUTOINCREMENT,
    GERACAO         INTEGER NOT NULL,
    MELHOR_FITNESS  REAL    NOT NULL,
    FITNESS_MEDIO   REAL    NOT NULL,
    ALVOS_ABATIDOS  INTEGER NOT NULL,
    ALVOS_ESCAPADOS INTEGER NOT NULL,
    DH_REGISTRO     TEXT    NOT NULL
);

-- Marcos agregados de fitness a cada 500 gerações de treinamento (neuroevolução) — nunca
-- descarta linhas (diferente de TABHTG acima).
CREATE TABLE TABMFT (
    Id                         INTEGER PRIMARY KEY AUTOINCREMENT,
    GeracaoMarco               INTEGER NOT NULL,
    FitnessMedioDoBloco        REAL    NOT NULL,
    MelhorFitnessDoBloco       REAL    NOT NULL,
    DataHora                   TEXT    NOT NULL
);

-- Retrato completo (decisões/acertos/erros/genoma) do melhor genoma a cada 1.000 gerações —
-- histórico comparativo marco a marco, também sem descarte de linhas, para estudar caso a caso
-- se a evolução do treinamento é real.
CREATE TABLE TABICG (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    GeracaoMarco    INTEGER NOT NULL,
    Fitness         REAL    NOT NULL,
    AlvosAbatidos   INTEGER NOT NULL,
    AlvosEscapados  INTEGER NOT NULL,
    TirosErrados    INTEGER NOT NULL,
    TaxaDeAcerto    REAL,
    TaxaDeCobertura REAL,
    GenomaJson      TEXT    NOT NULL,
    DataHora        TEXT    NOT NULL
);

-- Auditoria de mudança de grau de perigosidade de um alvo (ver ClassificadorPerigoAlvo) —
-- só grava uma linha quando o grau MUDA, não a cada leitura do sensor.
CREATE TABLE TABCLA (
    Id                          INTEGER PRIMARY KEY AUTOINCREMENT,
    ObjetoDetectadoId           INTEGER REFERENCES TABOBJ (Id),
    VelocidadeMetrosPorSegundo  REAL    NOT NULL,
    Grau                        TEXT    NOT NULL CHECK (Grau IN ('Baixo', 'Medio', 'Alto', 'Critico')),
    DataHora                    TEXT    NOT NULL,
    -- Sessão de quem tinha o alvo na tela quando o grau mudou. Ver TABSES.
    SessaoId                    TEXT    REFERENCES TABSES (ID_SESSAO)
);

CREATE TABLE TABPRU (
    UsuarioId          INTEGER PRIMARY KEY REFERENCES TABUSU (Id) ON DELETE CASCADE,
    Idioma             TEXT    NOT NULL DEFAULT 'pt-BR',
    Tema               TEXT    NOT NULL DEFAULT 'Escuro' CHECK (Tema IN ('Claro', 'Escuro', 'Sistema')),
    SidebarRecolhida   INTEGER NOT NULL DEFAULT 0,
    TelaInicial        TEXT,
    RegistrosPorPagina INTEGER NOT NULL DEFAULT 25
);

-- Dicionário de tabelas: nome completo por trás de cada sigla de 6 letras usada no banco.
CREATE TABLE TABDIC (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    nometab TEXT NOT NULL UNIQUE,
    nometabcompleta TEXT NOT NULL
);

INSERT INTO TABDIC (nometab, nometabcompleta) VALUES
    ('TABUSU', 'Tabela de Usuários'),
    ('TABOBJ', 'Tabela de Objetos Detectados'),
    ('TABOCE', 'Tabela de Estados da Ocorrência'),
    ('TABCAM', 'Tabela de Câmeras'),
    ('TABACA', 'Tabela de Ações Realizadas'),
    ('TABMOD', 'Tabela de Modo Atual da Torre'),
    ('TABSES', 'Tabela de Sessões de Usuário'),
    ('TABHTG', 'Tabela de Histórico de Gerações de Treinamento'),
    ('TABMFT', 'Tabela de Marcos de Fitness do Treinamento'),
    ('TABICG', 'Tabela de Instantâneos de Comportamento do Melhor Gene'),
    ('TABCLA', 'Tabela de Classificações de Alvo'),
    ('TABPRU', 'Tabela de Preferências do Usuário'),
    ('TABDIC', 'Tabela de Dicionário de Tabelas'),
    ('TABOPC', 'Tabela de Opções de Campos');

-- Opções válidas de campos que antes só tinham CHECK(... IN (...)) — uma linha por opção.
CREATE TABLE TABOPC (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    nometab TEXT NOT NULL,
    nomecampo TEXT NOT NULL,
    opcao TEXT NOT NULL,
    descricao TEXT NOT NULL
);

CREATE UNIQUE INDEX idx_tabopc_campo_opcao ON TABOPC (nometab, nomecampo, opcao);

INSERT INTO TABOPC (nometab, nomecampo, opcao, descricao) VALUES
    ('TABUSU', 'Perfil', 'Administrador', 'Acesso total ao sistema, incluindo gestão de usuários.'),
    ('TABUSU', 'Perfil', 'Operador', 'Pode operar o sistema (monitorar, acionar) sem gerenciar usuários.'),
    ('TABUSU', 'Perfil', 'Visualizador', 'Acesso somente leitura às telas do sistema.'),
    ('TABCAM', 'Quadrante', 'Q1', 'Quadrante 1 monitorado pela câmera.'),
    ('TABCAM', 'Quadrante', 'Q2', 'Quadrante 2 monitorado pela câmera.'),
    ('TABCAM', 'Quadrante', 'Q3', 'Quadrante 3 monitorado pela câmera.'),
    ('TABCAM', 'Quadrante', 'Q4', 'Quadrante 4 monitorado pela câmera.'),
    ('TABOBJ', 'Estado', 'Detectado', 'Ocorrência detectada, aguardando decisão.'),
    ('TABOBJ', 'Estado', 'AbatePendente', 'Abate do alvo foi solicitado, aguardando confirmação.'),
    ('TABOBJ', 'Estado', 'Abatido', 'Alvo confirmado como abatido.'),
    ('TABCLA', 'Grau', 'Baixo', 'Perigo baixo — velocidade/comportamento pouco ameaçador.'),
    ('TABCLA', 'Grau', 'Medio', 'Perigo médio — requer atenção do operador.'),
    ('TABCLA', 'Grau', 'Alto', 'Perigo alto — requer ação rápida.'),
    ('TABCLA', 'Grau', 'Critico', 'Perigo crítico — ação imediata recomendada.'),
    ('TABPRU', 'Tema', 'Claro', 'Tema visual claro da interface.'),
    ('TABPRU', 'Tema', 'Escuro', 'Tema visual escuro da interface (padrão).'),
    ('TABPRU', 'Tema', 'Sistema', 'Segue o tema configurado no sistema operacional.');

CREATE INDEX idx_acoes_realizadas_torre ON TABACA (ID_TORRE);
CREATE INDEX idx_objetos_detectados_datahora ON TABOBJ (DataHora);
CREATE INDEX idx_acoes_realizadas_datahora ON TABACA (DH_ACAO);
CREATE INDEX idx_modo_atual_torre_usuario ON TABMOD (ID_USER);
CREATE INDEX idx_objetos_detectados_perfil ON TABOBJ (PerfilAlvoId);
CREATE INDEX idx_ocorrencia_estados_objeto ON TABOCE (ObjetoDetectadoId);
CREATE INDEX idx_historico_treinamento_geracao ON TABHTG (GERACAO);
CREATE INDEX idx_classificacoes_alvo_objeto ON TABCLA (ObjetoDetectadoId);
CREATE UNIQUE INDEX idx_marcos_fitness_geracao ON TABMFT (GeracaoMarco);
CREATE UNIQUE INDEX idx_instantaneos_comportamento_geracao ON TABICG (GeracaoMarco);
CREATE INDEX idx_sessoes_usuario_usuario ON TABSES (UsuarioId);

-- É esta restrição que torna impossível duplicar a ocorrência do mesmo acompanhamento num
-- mesmo quadrante, inclusive quando várias câmeras processam em paralelo. Parcial (WHERE NOT
-- NULL) para não limitar o radar, que não tem rastreamento visual e pode repetir quadrante.
CREATE UNIQUE INDEX idx_objetos_detectados_rastreamento
    ON TABOBJ (RastreamentoId, Quadrante) WHERE RastreamentoId IS NOT NULL;

CREATE UNIQUE INDEX idx_cameras_dispositivo ON TABCAM (DispositivoId);
CREATE UNIQUE INDEX idx_cameras_quadrante   ON TABCAM (Quadrante);

-- Usuário admin semeado da mesma forma que DataSeeder.cs (login "admin"), só como referência
-- visual das relações — sem senha real (hash/salt fictícios, não usar para login).
INSERT INTO TABUSU (Nome, Login, SenhaHash, SenhaSalt, Perfil, Ativo, DataCriacao)
VALUES ('Administrador', 'admin', 'N/A', 'N/A', 'Administrador', 1, datetime('now'));

INSERT INTO TABPRU (UsuarioId, Idioma, Tema, SidebarRecolhida, RegistrosPorPagina)
VALUES (1, 'pt-BR', 'Escuro', 0, 25);

-- ============================================================================
-- Views de leitura/relatório — nenhuma delas é consumida pelo app (que faz os
-- joins em memória no C#); existem só para facilitar consulta e auditoria via
-- SQL Developer/DBeaver, como descrito no topo deste arquivo.
-- ============================================================================

-- Ocorrência atual de cada detecção, com a última transição registrada em
-- TABOCE e o modo da torre vigente nesse instante (Estado ali
-- referencia TABMOD.ID_MODO, não o enum Detectado/Abatido — ver
-- comentário da tabela).
CREATE VIEW vw_ocorrencias_atuais AS
SELECT
    od.Id,
    od.Tipo,
    od.X, od.Y, od.Z,
    od.Quadrante,
    od.DataHora,
    od.Dispositivo,
    od.NivelConfianca,
    od.CameraDispositivoId,
    od.CameraNome,
    od.RastreamentoId,
    od.Estado                  AS EstadoAtual,
    od.DataHoraUltimaDeteccao,
    oe.DataHora                AS DataHoraUltimaTransicao,
    oe.Usuario                 AS UsuarioUltimaTransicao,
    mat.MODOATUAL               AS ModoVigenteNaTransicao,
    oe.SessaoId                AS SessaoUltimaTransicao
FROM TABOBJ od
LEFT JOIN TABOCE oe
    ON oe.Id = (SELECT MAX(oe2.Id) FROM TABOCE oe2
                WHERE oe2.ObjetoDetectadoId = od.Id)
LEFT JOIN TABMOD mat ON mat.ID_MODO = oe.Estado;

-- Cada acionamento com o alvo, a câmera e o usuário/sessão por trás, numa
-- linha só (hoje isso exige três consultas separadas em TABACA,
-- TABOBJ, TABCAM e TABSES).
CREATE VIEW vw_acoes_com_contexto AS
SELECT
    ar.ID_ACAO,
    ar.ID_TORRE,
    ar.ID_OBJETO,
    od.Tipo                AS TipoAlvo,
    od.Quadrante,
    ar.DH_ACAO,
    ar.OBSERVACAO,
    ar.CameraDispositivoId,
    c.Nome                 AS CameraNome,
    ar.SessaoId,
    u.Nome                 AS UsuarioNome,
    u.Login                AS UsuarioLogin
FROM TABACA ar
LEFT JOIN TABOBJ od ON od.Id = ar.ID_OBJETO
LEFT JOIN TABCAM c             ON c.DispositivoId = ar.CameraDispositivoId
LEFT JOIN TABSES su    ON su.ID_SESSAO = ar.SessaoId
LEFT JOIN TABUSU u            ON u.Id = su.UsuarioId;

-- Histórico de troca de modo da torre com nome/login do usuário em vez de só
-- ID_USER, e a janela da sessão que confirmou a troca.
CREATE VIEW vw_auditoria_modo_torre AS
SELECT
    mat.ID_MODO,
    mat.MODOATUAL,
    mat.MODOANTERIOR,
    mat.ID_USER,
    u.Nome              AS UsuarioNome,
    u.Login             AS UsuarioLogin,
    mat.DHALTERACAO,
    mat.SessaoId,
    su.DataHoraInicio   AS SessaoInicio,
    su.DataHoraFim      AS SessaoFim
FROM TABMOD mat
LEFT JOIN TABUSU u         ON u.Id = mat.ID_USER
LEFT JOIN TABSES su ON su.ID_SESSAO = mat.SessaoId;

-- Mudanças de grau de perigosidade com o alvo e o usuário/sessão que tinha o
-- alvo em tela no momento da mudança (TABCLA só grava quando o
-- grau muda, não a cada leitura — ver comentário da tabela).
CREATE VIEW vw_classificacoes_com_contexto AS
SELECT
    ca.Id,
    ca.ObjetoDetectadoId,
    od.Tipo                         AS TipoAlvo,
    od.Quadrante,
    ca.VelocidadeMetrosPorSegundo,
    ca.Grau,
    ca.DataHora,
    ca.SessaoId,
    u.Nome                          AS UsuarioNome,
    u.Login                         AS UsuarioLogin
FROM TABCLA ca
LEFT JOIN TABOBJ od ON od.Id = ca.ObjetoDetectadoId
LEFT JOIN TABSES su    ON su.ID_SESSAO = ca.SessaoId
LEFT JOIN TABUSU u            ON u.Id = su.UsuarioId;

-- Linha do tempo única do treinamento (neuroevolução), unindo o buffer
-- recente (TABHTG, limitado a 300 linhas) com os
-- marcos agregados (TABMFT, nunca descartados) — Origem
-- distingue a granularidade de cada linha.
CREATE VIEW vw_treinamento_resumo AS
SELECT
    'Geracao'               AS Origem,
    GERACAO                 AS Geracao,
    MELHOR_FITNESS          AS MelhorFitness,
    FITNESS_MEDIO           AS FitnessMedio,
    ALVOS_ABATIDOS          AS AlvosAbatidos,
    ALVOS_ESCAPADOS         AS AlvosEscapados,
    DH_REGISTRO             AS DataHora
FROM TABHTG
UNION ALL
SELECT
    'Marco'                 AS Origem,
    GeracaoMarco            AS Geracao,
    MelhorFitnessDoBloco    AS MelhorFitness,
    FitnessMedioDoBloco     AS FitnessMedio,
    NULL                    AS AlvosAbatidos,
    NULL                    AS AlvosEscapados,
    DataHora
FROM TABMFT;
