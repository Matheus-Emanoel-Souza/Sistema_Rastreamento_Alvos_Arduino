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
Histórico de detecções (Requisito 4). Uma linha por **primeira detecção** de um alvo (não a
cada atualização de posição — ver `MainViewModel.OnTargetCreated`), para não inflar o
arquivo a cada ciclo de leitura do radar (~150ms).
- **Z**: sempre `null` hoje — os sensores do Arduino são 2D (ângulo + distância). Campo
  mantido para sensores 3D futuros (decisão confirmada com o usuário).

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

### `preferencias_usuario`
Uma linha por usuário (Requisito 8) — idioma, tema, sidebar recolhida/expandida. Colunas de
personalização mais granular (ordem dos cartões, colunas por tabela) ficam reservadas para a
Etapa 2.

### `chamados_ajuda`
Chamados de suporte (Requisito 9) — suporta atualização (`Update`) porque um administrador
pode alterar `Status`/`RespostaAdmin` ao tratar o chamado; usuário e data de envio são
preenchidos automaticamente pelo sistema, nunca digitados.
