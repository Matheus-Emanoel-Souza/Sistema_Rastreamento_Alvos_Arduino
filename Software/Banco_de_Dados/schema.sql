-- Schema relacional do RadarTorres, espelhando o modelo de dados atual (SQLite) descrito em
-- Software/Banco_de_Dados/BANCO_DE_DADOS.md e as classes em src/RadarTorres.App/Models/.
-- Gerado para visualização em SQL Developer (driver JDBC SQLite) — ver README.md desta pasta.

PRAGMA foreign_keys = ON;

CREATE TABLE usuarios (
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
CREATE TABLE objetos_detectados (
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
-- (Detectado/AbatePendente/Abatido, ver objetos_detectados.Estado para o estado ATUAL) — guarda
-- o ID_MODO de modo_atual_torre vigente no instante da transição. NULL quando nenhum modo havia
-- sido registrado ainda.
CREATE TABLE ocorrencia_estados (
    Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    ObjetoDetectadoId  INTEGER NOT NULL REFERENCES objetos_detectados (Id),
    Estado             INTEGER REFERENCES modo_atual_torre (ID_MODO),
    DataHora           TEXT    NOT NULL,
    Usuario            TEXT,
    Observacao         TEXT,
    -- Sessão de quem desencadeou a transição (NULL para transições automáticas — ocorrência
    -- aberta pela câmera, sem operador por trás). Ver sessoes_usuario.
    SessaoId           TEXT    REFERENCES sessoes_usuario (ID_SESSAO)
);

-- Qual câmera monitora qual quadrante. Uma câmera responde por exatamente um quadrante e cada
-- quadrante tem no máximo uma câmera — garantido pelos dois índices únicos abaixo.
CREATE TABLE cameras (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    DispositivoId  TEXT    NOT NULL,
    Nome           TEXT    NOT NULL,
    Quadrante      TEXT    NOT NULL CHECK (Quadrante IN ('Q1', 'Q2', 'Q3', 'Q4')),
    AtualizadoEm   TEXT    NOT NULL
);

CREATE TABLE acoes_realizadas (
    ID_ACAO               INTEGER PRIMARY KEY AUTOINCREMENT,
    ID_TORRE              INTEGER NOT NULL,
    ID_OBJETO             INTEGER REFERENCES objetos_detectados (Id),
    DH_ACAO                TEXT   NOT NULL,
    OBSERVACAO             TEXT,
    -- Câmera que originou a ocorrência por trás da ação (resolvida via ID_OBJETO no momento do
    -- acionamento) — mesma convenção documentária de objetos_detectados.CameraDispositivoId.
    CameraDispositivoId    TEXT    REFERENCES cameras (DispositivoId),
    -- Sessão de quem desencadeou o disparo. Ver sessoes_usuario.
    SessaoId                TEXT   REFERENCES sessoes_usuario (ID_SESSAO)
);

-- "AcoesRealizadas" (PascalCase) existiu como tabela órfã de uma versão anterior do schema em
-- bancos já instalados — nenhum código atual cria, lê ou escreve nela; acoes_realizadas acima é
-- a única em uso real. Repositório remove a órfã ao abrir o banco (ver
-- SqliteAcaoRealizadaRepository.CriarEsquema).

CREATE TABLE modo_atual_torre (
    ID_MODO       INTEGER PRIMARY KEY AUTOINCREMENT,
    MODOATUAL     TEXT    NOT NULL,
    MODOANTERIOR  TEXT    NOT NULL,
    ID_USER       INTEGER REFERENCES usuarios (Id),
    DHALTERACAO   TEXT    NOT NULL,
    -- Sessão de quem confirmou a troca. Ver sessoes_usuario.
    SessaoId      TEXT    REFERENCES sessoes_usuario (ID_SESSAO)
);

-- Uma sessão de uso do sistema: aberta no login (uma por aba — várias podem estar abertas ao
-- mesmo tempo, uma por usuário logado em cada aba) e encerrada no logout ou ao fechar a aba. É o
-- "código de sessão" referenciado pelas tabelas de auditoria acima: consultando o ID_SESSAO de
-- uma ação dá pra recuperar quem estava no sistema e em que janela de tempo exata (data, hora,
-- minuto, segundo) ela aconteceu.
CREATE TABLE sessoes_usuario (
    ID_SESSAO       TEXT    PRIMARY KEY,
    UsuarioId       INTEGER NOT NULL REFERENCES usuarios (Id),
    DataHoraInicio  TEXT    NOT NULL,
    DataHoraFim     TEXT
);

-- Buffer circular com o comportamento do melhor indivíduo de cada geração treinada na tela
-- "Treinamento do Controlador" (neuroevolução do controlador híbrido heurística+rede neural —
-- ver src/RadarTorres.App/NeuralEvolution/). Limitado a 300 linhas: ao inserir a 301ª, o
-- registro de ID mais antigo é descartado (ver SqliteHistoricoTreinamentoRepository.Registrar).
CREATE TABLE historico_treinamento_geracoes (
    ID              INTEGER PRIMARY KEY AUTOINCREMENT,
    GERACAO         INTEGER NOT NULL,
    MELHOR_FITNESS  REAL    NOT NULL,
    FITNESS_MEDIO   REAL    NOT NULL,
    ALVOS_ABATIDOS  INTEGER NOT NULL,
    ALVOS_ESCAPADOS INTEGER NOT NULL,
    DH_REGISTRO     TEXT    NOT NULL
);

-- Marcos agregados de fitness a cada 500 gerações de treinamento (neuroevolução) — nunca
-- descarta linhas (diferente de historico_treinamento_geracoes acima).
CREATE TABLE marcos_fitness_treinamento (
    Id                         INTEGER PRIMARY KEY AUTOINCREMENT,
    GeracaoMarco               INTEGER NOT NULL,
    FitnessMedioDoBloco        REAL    NOT NULL,
    MelhorFitnessDoBloco       REAL    NOT NULL,
    DataHora                   TEXT    NOT NULL
);

-- Retrato completo (decisões/acertos/erros/genoma) do melhor genoma a cada 1.000 gerações —
-- histórico comparativo marco a marco, também sem descarte de linhas, para estudar caso a caso
-- se a evolução do treinamento é real.
CREATE TABLE instantaneos_comportamento_melhor_gene (
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
CREATE TABLE classificacoes_alvo (
    Id                          INTEGER PRIMARY KEY AUTOINCREMENT,
    ObjetoDetectadoId           INTEGER REFERENCES objetos_detectados (Id),
    VelocidadeMetrosPorSegundo  REAL    NOT NULL,
    Grau                        TEXT    NOT NULL CHECK (Grau IN ('Baixo', 'Medio', 'Alto', 'Critico')),
    DataHora                    TEXT    NOT NULL,
    -- Sessão de quem tinha o alvo na tela quando o grau mudou. Ver sessoes_usuario.
    SessaoId                    TEXT    REFERENCES sessoes_usuario (ID_SESSAO)
);

CREATE TABLE preferencias_usuario (
    UsuarioId          INTEGER PRIMARY KEY REFERENCES usuarios (Id) ON DELETE CASCADE,
    Idioma             TEXT    NOT NULL DEFAULT 'pt-BR',
    Tema               TEXT    NOT NULL DEFAULT 'Escuro' CHECK (Tema IN ('Claro', 'Escuro', 'Sistema')),
    SidebarRecolhida   INTEGER NOT NULL DEFAULT 0,
    TelaInicial        TEXT,
    RegistrosPorPagina INTEGER NOT NULL DEFAULT 25
);

CREATE INDEX idx_acoes_realizadas_torre ON acoes_realizadas (ID_TORRE);
CREATE INDEX idx_objetos_detectados_datahora ON objetos_detectados (DataHora);
CREATE INDEX idx_acoes_realizadas_datahora ON acoes_realizadas (DH_ACAO);
CREATE INDEX idx_modo_atual_torre_usuario ON modo_atual_torre (ID_USER);
CREATE INDEX idx_objetos_detectados_perfil ON objetos_detectados (PerfilAlvoId);
CREATE INDEX idx_ocorrencia_estados_objeto ON ocorrencia_estados (ObjetoDetectadoId);
CREATE INDEX idx_historico_treinamento_geracao ON historico_treinamento_geracoes (GERACAO);
CREATE INDEX idx_classificacoes_alvo_objeto ON classificacoes_alvo (ObjetoDetectadoId);
CREATE UNIQUE INDEX idx_marcos_fitness_geracao ON marcos_fitness_treinamento (GeracaoMarco);
CREATE UNIQUE INDEX idx_instantaneos_comportamento_geracao ON instantaneos_comportamento_melhor_gene (GeracaoMarco);
CREATE INDEX idx_sessoes_usuario_usuario ON sessoes_usuario (UsuarioId);

-- É esta restrição que torna impossível duplicar a ocorrência do mesmo acompanhamento num
-- mesmo quadrante, inclusive quando várias câmeras processam em paralelo. Parcial (WHERE NOT
-- NULL) para não limitar o radar, que não tem rastreamento visual e pode repetir quadrante.
CREATE UNIQUE INDEX idx_objetos_detectados_rastreamento
    ON objetos_detectados (RastreamentoId, Quadrante) WHERE RastreamentoId IS NOT NULL;

CREATE UNIQUE INDEX idx_cameras_dispositivo ON cameras (DispositivoId);
CREATE UNIQUE INDEX idx_cameras_quadrante   ON cameras (Quadrante);

-- Usuário admin semeado da mesma forma que DataSeeder.cs (login "admin"), só como referência
-- visual das relações — sem senha real (hash/salt fictícios, não usar para login).
INSERT INTO usuarios (Nome, Login, SenhaHash, SenhaSalt, Perfil, Ativo, DataCriacao)
VALUES ('Administrador', 'admin', 'N/A', 'N/A', 'Administrador', 1, datetime('now'));

INSERT INTO preferencias_usuario (UsuarioId, Idioma, Tema, SidebarRecolhida, RegistrosPorPagina)
VALUES (1, 'pt-BR', 'Escuro', 0, 25);

-- ============================================================================
-- Views de leitura/relatório — nenhuma delas é consumida pelo app (que faz os
-- joins em memória no C#); existem só para facilitar consulta e auditoria via
-- SQL Developer/DBeaver, como descrito no topo deste arquivo.
-- ============================================================================

-- Ocorrência atual de cada detecção, com a última transição registrada em
-- ocorrencia_estados e o modo da torre vigente nesse instante (Estado ali
-- referencia modo_atual_torre.ID_MODO, não o enum Detectado/Abatido — ver
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
FROM objetos_detectados od
LEFT JOIN ocorrencia_estados oe
    ON oe.Id = (SELECT MAX(oe2.Id) FROM ocorrencia_estados oe2
                WHERE oe2.ObjetoDetectadoId = od.Id)
LEFT JOIN modo_atual_torre mat ON mat.ID_MODO = oe.Estado;

-- Cada acionamento com o alvo, a câmera e o usuário/sessão por trás, numa
-- linha só (hoje isso exige três consultas separadas em acoes_realizadas,
-- objetos_detectados, cameras e sessoes_usuario).
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
FROM acoes_realizadas ar
LEFT JOIN objetos_detectados od ON od.Id = ar.ID_OBJETO
LEFT JOIN cameras c             ON c.DispositivoId = ar.CameraDispositivoId
LEFT JOIN sessoes_usuario su    ON su.ID_SESSAO = ar.SessaoId
LEFT JOIN usuarios u            ON u.Id = su.UsuarioId;

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
FROM modo_atual_torre mat
LEFT JOIN usuarios u         ON u.Id = mat.ID_USER
LEFT JOIN sessoes_usuario su ON su.ID_SESSAO = mat.SessaoId;

-- Mudanças de grau de perigosidade com o alvo e o usuário/sessão que tinha o
-- alvo em tela no momento da mudança (classificacoes_alvo só grava quando o
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
FROM classificacoes_alvo ca
LEFT JOIN objetos_detectados od ON od.Id = ca.ObjetoDetectadoId
LEFT JOIN sessoes_usuario su    ON su.ID_SESSAO = ca.SessaoId
LEFT JOIN usuarios u            ON u.Id = su.UsuarioId;

-- Linha do tempo única do treinamento (neuroevolução), unindo o buffer
-- recente (historico_treinamento_geracoes, limitado a 300 linhas) com os
-- marcos agregados (marcos_fitness_treinamento, nunca descartados) — Origem
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
FROM historico_treinamento_geracoes
UNION ALL
SELECT
    'Marco'                 AS Origem,
    GeracaoMarco            AS Geracao,
    MelhorFitnessDoBloco    AS MelhorFitness,
    FitnessMedioDoBloco     AS FitnessMedio,
    NULL                    AS AlvosAbatidos,
    NULL                    AS AlvosEscapados,
    DataHora
FROM marcos_fitness_treinamento;
