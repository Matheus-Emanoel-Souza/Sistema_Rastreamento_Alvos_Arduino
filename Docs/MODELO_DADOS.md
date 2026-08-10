# Modelo de Dados

Este documento descreve as "tabelas" criadas para a Etapa 1 (fundação multiusuário):
estrutura, tipos, relacionamentos e o plano de migração para um banco relacional.

## 1. Tecnologia atual: CSV (decisão explícita do usuário)

Diferente do que normalmente seria recomendado (SQLite/SQL Server), esta etapa usa
**arquivos CSV** como armazenamento, por decisão explícita do solicitante ("no momento faça
só uma consulta em tabelas de CSV, mas deixe sinalizado que futuramente será convertido para
SQL"). Local dos arquivos:

```
%AppData%\RadarTorres\Data\
├── usuarios.csv
├── objetos_detectados.csv
├── acoes_realizadas.csv
├── alteracoes_modo.csv
├── preferencias_usuario.csv
└── chamados_ajuda.csv
```

`%AppData%` (por usuário do Windows, sem precisar de administrador) foi escolhido em vez da
pasta de instalação (`C:\Program Files\...`, somente leitura para usuários comuns) — ver
`src/RadarTorres.App/Data/AppDataPaths.cs`.

### Como funciona a "migration" hoje

Não existe uma migration tradicional porque não há schema de banco: cada arquivo CSV é
criado automaticamente (com cabeçalho) na primeira vez que o repositório correspondente é
usado — ver `CsvTableStore<T>.EnsureFileWithHeader()` em
`src/RadarTorres.App/Data/CsvTableStore.cs`. Isso significa que **o próprio código é a
"migration"**: adicionar uma coluna = adicionar uma propriedade no modelo + uma
`CsvColumn<T>` no repositório correspondente.

### Plano de migração futura para SQL

Cada tabela tem hoje uma interface de repositório dedicada (`IUsuarioRepository`,
`IObjetoDetectadoRepository`, etc., em `src/RadarTorres.App/Repositories/`). Quando o
projeto migrar para um banco relacional:

1. Implementar uma versão `Sqlite*Repository` (ou EF Core `DbContext`) para cada interface,
   mantendo exatamente a mesma assinatura.
2. Trocar o registro no contêiner de DI em `App.xaml.cs` (`ConfigureServices`), de
   `services.AddSingleton<IUsuarioRepository, CsvUsuarioRepository>()` para
   `..., SqliteUsuarioRepository>()`.
3. Nenhum ViewModel, Service ou View precisa mudar — todos dependem só da interface.

Todos os arquivos de repositório/armazenamento têm um comentário `TODO(SQL)` marcando esse
ponto de troca (ver `src/RadarTorres.App/Data/AppDataPaths.cs` e
`src/RadarTorres.App/Data/CsvTableStore.cs`).

## 2. Diagrama de relacionamento

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

**Nota sobre chaves estrangeiras**: como não há banco relacional ainda, os relacionamentos
acima (`UsuarioResponsavel`, `UsuarioSolicitante`, `UsuarioId`) são referências por
**Login** (texto) ou **Id**, sem integridade referencial imposta pelo armazenamento — a
consistência é garantida pelo código (`IAuthService.CurrentUser`), não pelo arquivo. Isso é
resolvido automaticamente ao migrar para SQL (chaves estrangeiras reais).

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
Histórico de detecções (Requisito 4). Uma linha por **primeira detecção** de um alvo (não a
cada atualização de posição — ver `MainViewModel.OnTargetCreated`), para não inflar o
arquivo a cada ciclo de leitura do radar (~150ms).
- **Z**: sempre `null` hoje — os sensores do Arduino são 2D (ângulo + distância). Campo
  mantido para sensores 3D futuros (decisão confirmada com o usuário).

### `acoes_realizadas`
Auditoria de acionamentos (Requisito 5) — **somente inserção**: o repositório
(`IAcaoRealizadaRepository`) não expõe Update/Delete de propósito. Gravado em
`FireControlService.TryFireAsync`, o único ponto do sistema por onde todo acionamento passa,
para cada tentativa (autorizada e executada, bloqueada por segurança, ou com erro de envio).

### `alteracoes_modo`
Auditoria de troca de modo (Requisito 6) — também somente inserção. Toda troca de
`SystemMode` (agora incluindo `Manutenção` e `Emergência`, adicionados nesta etapa) passa por
uma confirmação do usuário antes de ser aplicada; tanto a confirmação quanto o cancelamento
são registrados (`MainViewModel.CurrentMode` setter).

### `preferencias_usuario`
Uma linha por usuário (Requisito 8) — idioma, tema, sidebar recolhida/expandida. Colunas de
personalização mais granular (ordem dos cartões, colunas por tabela) ficam reservadas para a
Etapa 2, sem quebrar o CSV já existente (só seriam novas colunas ao final).

### `chamados_ajuda`
Chamados de suporte (Requisito 9) — suporta atualização (`Update`) porque um administrador
pode alterar `Status`/`RespostaAdmin` ao tratar o chamado; usuário e data de envio são
preenchidos automaticamente pelo sistema, nunca digitados.
