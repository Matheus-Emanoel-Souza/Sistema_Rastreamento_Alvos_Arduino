-- Schema relacional do RadarTorres, espelhando o modelo de dados atual (SQLite) descrito em
-- Docs/Tecnica/MODELO_DADOS.md e as classes em src/RadarTorres.App/Models/.
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

-- Histórico de transições de estado de cada ocorrência (auditoria do ciclo de abate:
-- Detectado → AbatePendente pelo acionamento → Abatido por confirmação do operador).
CREATE TABLE ocorrencia_estados (
    Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    ObjetoDetectadoId  INTEGER NOT NULL REFERENCES objetos_detectados (Id),
    Estado             TEXT    NOT NULL
                               CHECK (Estado IN ('Detectado', 'AbatePendente', 'Abatido')),
    DataHora           TEXT    NOT NULL,
    Usuario            TEXT,
    Observacao         TEXT
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
    ID_ACAO     INTEGER PRIMARY KEY AUTOINCREMENT,
    ID_TORRE    INTEGER NOT NULL,
    ID_OBJETO   INTEGER REFERENCES objetos_detectados (Id),
    DH_ACAO     TEXT    NOT NULL,
    OBSERVACAO  TEXT
);

CREATE TABLE modo_atual_torre (
    ID_MODO       INTEGER PRIMARY KEY AUTOINCREMENT,
    MODOATUAL     TEXT    NOT NULL,
    MODOANTERIOR  TEXT    NOT NULL,
    ID_USER       INTEGER REFERENCES usuarios (Id),
    DHALTERACAO   TEXT    NOT NULL
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
