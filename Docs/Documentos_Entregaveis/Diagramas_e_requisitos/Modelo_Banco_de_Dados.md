# Modelo de Banco de Dados

## 1. Situação real hoje: não existe banco relacional

O projeto **não usa SQLite, SQL Server ou qualquer SGBD** no momento. A persistência é feita em
**arquivos CSV**, por decisão explícita registrada em `Docs/Tecnica/MODELO_DADOS.md`: *"no momento faça
só uma consulta em tabelas de CSV, mas deixe sinalizado que futuramente será convertido para
SQL"*.

Local dos arquivos (por usuário do Windows, sem precisar de administrador):

```
%AppData%\RadarTorres\Data\
├── usuarios.csv
├── objetos_detectados.csv
├── acoes_realizadas.csv
├── alteracoes_modo.csv
├── preferencias_usuario.csv
└── chamados_ajuda.csv
```

Mecanismo: `CsvTableStore<T>.EnsureFileWithHeader()` (`src/RadarTorres.App/Data/CsvTableStore.cs`)
cria cada arquivo com cabeçalho na primeira vez que o repositório correspondente é usado — não
há uma "migration" tradicional; o próprio código (modelo + `CsvColumn<T>`) é a definição do
schema. Cada tabela tem uma interface de repositório dedicada
(`src/RadarTorres.App/Repositories/I*Repository.cs`), o que permite trocar a implementação CSV
por uma implementação SQL sem alterar nenhum ViewModel/Service — cada arquivo tem um comentário
`TODO(SQL)` marcando esse ponto de troca.

**Não há integridade referencial imposta pelo armazenamento** hoje: campos como
`UsuarioResponsavel`, `UsuarioSolicitante` e `UsuarioId` são referências por **Login** (texto)
ou **Id**, validadas apenas pelo código (`IAuthService.CurrentUser`), não por chave estrangeira
real — isso é resolvido automaticamente ao migrar para SQL.

## 2. Modelo identificado no código (CSV atual)

### 2.1 Descrição textual das tabelas

* **`usuarios`** — contas de acesso ao aplicativo (login independente do Windows). Perfil:
  `Administrador` | `Operador` | `Visualizador`. Senha em PBKDF2-HMACSHA256 (100.000 iterações,
  salt de 128 bits por usuário, `PasswordHasher.cs`) — nunca texto puro. Semeado no primeiro uso
  com `admin`/`admin123` (`DataSeeder.cs`).
* **`objetos_detectados`** — histórico de detecções: uma linha por **primeira detecção** de um
  alvo (não a cada atualização de posição), para não inflar o arquivo a cada ciclo do radar
  (~150 ms). `Z` é sempre `null` hoje (sensores 2D); campo reservado para sensores 3D futuros.
* **`acoes_realizadas`** — auditoria de acionamentos, **somente inserção** (o repositório não
  expõe Update/Delete de propósito). Gravado em `FireControlService.TryFireAsync`, para toda
  tentativa (autorizada e executada, bloqueada por segurança, ou com erro).
* **`alteracoes_modo`** — auditoria de troca de `SystemMode`, também somente inserção; tanto
  confirmação quanto cancelamento de uma troca são registrados.
* **`preferencias_usuario`** — uma linha por usuário (tema, idioma, sidebar recolhida);
  personalização mais granular (ordem de cartões, colunas por tabela) reservada para etapa
  futura sem quebrar o CSV existente.
* **`chamados_ajuda`** — chamados de suporte; suporta `Update` porque um administrador altera
  `Status`/`RespostaAdmin` ao tratar o chamado.

### 2.2 DER — modelo atual (Mermaid)

```mermaid
erDiagram
    USUARIOS ||--o{ ACOES_REALIZADAS : "solicita (quando manual)"
    USUARIOS ||--o{ ALTERACOES_MODO : "solicita"
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
    }

    ACOES_REALIZADAS {
        int Id PK
        string Dispositivo
        string TipoAcao
        double X
        double Y
        double Z "nullable"
        datetime DataHora
        string UsuarioResponsavel FK "nullable, Login"
        string Origem "Manual | Automatica"
        string Resultado "Executada | Cancelada | Erro"
        string Observacao "nullable"
    }

    ALTERACOES_MODO {
        int Id PK
        string ModoAnterior
        string NovoModo
        datetime DataHoraSolicitacao
        string UsuarioSolicitante FK "Login"
        datetime DataHoraExecucao "nullable"
        string Resultado "Sucesso | Erro"
        string Observacao "nullable"
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

**Nota:** `OBJETOS_DETECTADOS` não tem relacionamento explícito com `USUARIOS` no código — o
campo `Dispositivo` identifica a fonte (ex.: "Arduino", "Simulador"), não um usuário.

## 3. Modelo proposto/inferido — migração futura para SQL

Esta seção é uma **proposta**, não uma implementação existente. É inferida a partir do plano
descrito em `Docs/Tecnica/MODELO_DADOS.md` ("Plano de migração futura para SQL") e do roadmap em
`Docs/Projeto/CONTEXTO_PROJETO.md" ("Migração de persistência: CSV → banco relacional (SQLite/EF Core)
quando fizer sentido").

Passos propostos (do próprio repositório, não inventados por esta análise):

1. Implementar uma versão `Sqlite*Repository` (ou `DbContext` do EF Core) para cada interface já
   existente (`IUsuarioRepository`, `IObjetoDetectadoRepository`, etc.), mantendo exatamente a
   mesma assinatura de método.
2. Trocar o registro no contêiner de DI em `App.xaml.cs`
   (`services.AddSingleton<IUsuarioRepository, CsvUsuarioRepository>()` →
   `..., SqliteUsuarioRepository>()`).
3. Nenhum ViewModel, Service ou View precisaria mudar — todos dependem só da interface.

Com um SGBD relacional, as referências textuais por `Login` poderiam virar chaves estrangeiras
reais por `Id`, e `PREFERENCIAS_USUARIO`/`USUARIOS` uma relação 1:1 com integridade garantida
pelo banco. O diagrama abaixo é a mesma estrutura de dados de hoje, mas com chaves estrangeiras
íntegras (`UsuarioId` em vez de `Login`) — mudança natural de uma migração para SQL, não algo
já implementado.

```mermaid
erDiagram
    USUARIOS ||--o{ ACOES_REALIZADAS : "solicita (quando manual)"
    USUARIOS ||--o{ ALTERACOES_MODO : "solicita"
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
    }
    ACOES_REALIZADAS {
        int Id PK
        string Dispositivo
        string TipoAcao
        double X
        double Y
        datetime DataHora
        int UsuarioResponsavelId FK "nullable — antes era Login (texto)"
        string Origem
        string Resultado
    }
    ALTERACOES_MODO {
        int Id PK
        string ModoAnterior
        string NovoModo
        datetime DataHoraSolicitacao
        int UsuarioSolicitanteId FK "antes era Login (texto)"
        string Resultado
    }
    PREFERENCIAS_USUARIO {
        int UsuarioId PK-FK
        string Idioma
        string Tema
        bool SidebarRecolhida
    }
    CHAMADOS_AJUDA {
        int Id PK
        int UsuarioId FK
        string Titulo
        string Status
    }
```

**Não implementado:** nenhuma dessas tabelas SQL, nenhuma migration EF Core, nenhum arquivo
`.db`/`.sqlite` existe no repositório hoje. Esta seção é só a extrapolação do plano já
documentado pelo próprio time do projeto.
