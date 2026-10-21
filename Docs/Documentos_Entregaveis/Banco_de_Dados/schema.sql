-- Schema relacional do RadarTorres, espelhando o modelo de dados atual (CSV) descrito em
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

CREATE TABLE objetos_detectados (
    Id                INTEGER PRIMARY KEY AUTOINCREMENT,
    Tipo              TEXT    NOT NULL,
    X                 REAL    NOT NULL,
    Y                 REAL    NOT NULL,
    Z                 REAL,
    Quadrante         TEXT    NOT NULL,
    DataHora          TEXT    NOT NULL,
    Dispositivo       TEXT    NOT NULL,
    NivelConfianca    REAL,
    Observacao        TEXT,
    ReferenciaImagem  TEXT
);

CREATE TABLE acoes_realizadas (
    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    Dispositivo         TEXT    NOT NULL,
    TipoAcao            TEXT    NOT NULL,
    X                   REAL    NOT NULL,
    Y                   REAL    NOT NULL,
    Z                   REAL,
    DataHora            TEXT    NOT NULL,
    UsuarioResponsavel  TEXT REFERENCES usuarios (Login),
    Origem              TEXT    NOT NULL CHECK (Origem IN ('Manual', 'Automatica')),
    Resultado           TEXT    NOT NULL CHECK (Resultado IN ('Executada', 'Cancelada', 'Erro')),
    Observacao          TEXT
);

CREATE TABLE modo_atual_torre (
    Id                    INTEGER PRIMARY KEY AUTOINCREMENT,
    ModoAnterior          TEXT    NOT NULL,
    NovoModo              TEXT    NOT NULL,
    DataHoraSolicitacao   TEXT    NOT NULL,
    DataHoraExecucao      TEXT,
    Resultado             TEXT    NOT NULL CHECK (Resultado IN ('Sucesso', 'Erro')),
    Observacao            TEXT
);

CREATE TABLE preferencias_usuario (
    UsuarioId          INTEGER PRIMARY KEY REFERENCES usuarios (Id) ON DELETE CASCADE,
    Idioma             TEXT    NOT NULL DEFAULT 'pt-BR',
    Tema               TEXT    NOT NULL DEFAULT 'Escuro' CHECK (Tema IN ('Claro', 'Escuro', 'Sistema')),
    SidebarRecolhida   INTEGER NOT NULL DEFAULT 0,
    TelaInicial        TEXT,
    RegistrosPorPagina INTEGER NOT NULL DEFAULT 25
);

CREATE INDEX idx_acoes_realizadas_usuario ON acoes_realizadas (UsuarioResponsavel);
CREATE INDEX idx_objetos_detectados_datahora ON objetos_detectados (DataHora);
CREATE INDEX idx_acoes_realizadas_datahora ON acoes_realizadas (DataHora);

-- Usuário admin semeado da mesma forma que DataSeeder.cs (login "admin"), só como referência
-- visual das relações — sem senha real (hash/salt fictícios, não usar para login).
INSERT INTO usuarios (Nome, Login, SenhaHash, SenhaSalt, Perfil, Ativo, DataCriacao)
VALUES ('Administrador', 'admin', 'N/A', 'N/A', 'Administrador', 1, datetime('now'));

INSERT INTO preferencias_usuario (UsuarioId, Idioma, Tema, SidebarRecolhida, RegistrosPorPagina)
VALUES (1, 'pt-BR', 'Escuro', 0, 25);
