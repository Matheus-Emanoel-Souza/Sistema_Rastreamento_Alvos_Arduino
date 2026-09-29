# Modelo de Banco de Dados

## 1. Situação real hoje: SQLite é a única persistência

O projeto usa **SQLite** como único mecanismo de persistência. A migração CSV → SQL descrita
anteriormente neste documento como plano futuro **já foi implementada**: o contêiner de DI em
`App.xaml.cs:141-146` registra `Sqlite*Repository` diretamente para as 5 tabelas (`Usuario`,
`ObjetoDetectado`, `AcaoRealizada`, `ModoAtualTorre`, `PreferenciasUsuario`). Não existem mais
classes `Csv*Repository`/`Resilient*Repository` de persistência no repositório.

Local do arquivo (por usuário do Windows, sem precisar de administrador):

```
%AppData%\RadarTorres\Data\
└── radartorres.db
```

Se uma gravação falhar (ex.: banco temporariamente indisponível), a operação é enfileirada em
`pending-writes.sql` (`src/RadarTorres.App/Data/PendingWriteQueue.cs`) e reaplicada
automaticamente na próxima inicialização — não há perda silenciosa de dados.

O único uso remanescente de CSV no projeto é o botão manual "Exportar CSV" da tela de Objetos
Detectados (exportação sob demanda para o usuário, não persistência da aplicação).

**Integridade referencial** agora é garantida pelo próprio SQLite: `ID_OBJETO` (em
`acoes_realizadas`) e `ID_USER` (em `modo_atual_torre`) são chaves estrangeiras reais para
`objetos_detectados.Id` e `usuarios.Id`, com `PRAGMA foreign_keys = ON` — ver
`Docs/Documentos_Entregaveis/Banco_de_Dados/schema.sql`.

## 2. Modelo identificado no código (SQLite atual)

### 2.1 Descrição textual das tabelas

* **`usuarios`** — contas de acesso ao aplicativo (login independente do Windows). Perfil:
  `Administrador` | `Operador` | `Visualizador`. Senha em PBKDF2-HMACSHA256 (100.000 iterações,
  salt de 128 bits por usuário, `PasswordHasher.cs`) — nunca texto puro. Semeado no primeiro uso
  com `admin`/`admin123` (`DataSeeder.cs`).
* **`objetos_detectados`** — histórico de detecções: uma linha por **primeira detecção** de um
  alvo (não a cada atualização de posição), para não inflar o arquivo a cada ciclo do radar
  (~150 ms). `Z` é sempre `null` hoje (sensores 2D); campo reservado para sensores 3D futuros.
* **`acoes_realizadas`** — auditoria de acionamentos, **somente inserção** (o repositório não
  expõe Update/Delete de propósito). Gravado em `FireControlService.cs:105`, apenas quando o
  acionamento é **efetivamente executado com sucesso**; tentativas bloqueadas por segurança ou
  com erro não geram linha nesta tabela, só uma mensagem de log. Colunas: `ID_ACAO` (PK),
  `ID_TORRE`, `ID_OBJETO` (FK opcional para `objetos_detectados`), `DH_ACAO`, `OBSERVACAO`.
* **`modo_atual_torre`** — auditoria de troca de `SystemMode`, também somente inserção; só a
  **confirmação** de uma troca de modo gera linha (`MainViewModel.cs`, em torno da linha 291) —
  um cancelamento na confirmação retorna antes de chegar ao repositório, só gerando log. Colunas:
  `ID_MODO` (PK), `MODOATUAL`, `MODOANTERIOR`, `ID_USER` (FK opcional para `usuarios`),
  `DHALTERACAO`.
* **`preferencias_usuario`** — uma linha por usuário (tema, idioma, sidebar recolhida);
  personalização mais granular (ordem de cartões, colunas por tabela) reservada para etapa
  futura.

### 2.2 DER — modelo atual (SQLite)

```mermaid
erDiagram
    USUARIOS ||--o{ MODO_ATUAL_TORRE : "solicita"
    USUARIOS ||--o| PREFERENCIAS_USUARIO : "tem"
    OBJETOS_DETECTADOS ||--o{ ACOES_REALIZADAS : "originou (quando houver)"

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
        int ID_TORRE
        int ID_OBJETO FK "nullable"
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
```

**Nota:** `OBJETOS_DETECTADOS` não tem relacionamento explícito com `USUARIOS` no código — o
campo `Dispositivo` identifica a fonte (ex.: "Arduino", "Simulador"), não um usuário.

## 3. Migração CSV → SQLite (histórico)

Esta seção documenta, em retrospecto, a migração que era descrita nas versões anteriores deste
documento como "proposta futura" e que **já foi concluída**. O modelo de dados em si não mudou
de estrutura entre CSV e SQLite (mesmas tabelas/colunas, já mostradas em §2) — a mudança foi só
no mecanismo de armazenamento.

Passos efetivamente executados (refletidos no código atual):

1. Cada interface de repositório já existente (`IUsuarioRepository`,
   `IObjetoDetectadoRepository`, `IAcaoRealizadaRepository`, `IModoAtualTorreRepository`,
   `IPreferenciasUsuarioRepository`) ganhou uma implementação `Sqlite*Repository`
   (`src/RadarTorres.App/Repositories/Sqlite*Repository.cs`), mantendo a mesma assinatura de
   método que a versão CSV anterior — cada classe define o schema da sua tabela em um método
   `EnsureTable`.
2. O registro no contêiner de DI em `App.xaml.cs:141-146` foi trocado de
   `Csv*Repository` para `Sqlite*Repository` para as 5 tabelas.
3. Nenhum ViewModel, Service ou View precisou mudar — todos dependiam só da interface, como
   previsto no plano original.
4. Um mecanismo de resiliência foi adicionado além do que estava previsto originalmente: falhas
   de escrita são enfileiradas em `pending-writes.sql`
   (`src/RadarTorres.App/Data/PendingWriteQueue.cs`) e reaplicadas automaticamente na próxima
   inicialização, em vez de simplesmente propagar o erro.
5. O módulo `ChamadoAjuda` (model, repositório, tela, entrada de menu) foi removido do projeto
   antes da conclusão desta etapa — por isso não há tabela `chamados_ajuda` no schema atual (ver
   §2).

Com o SGBD relacional, as referências que antes eram por texto (`Login`) passaram a ser chaves
estrangeiras reais por `Id` (`ID_OBJETO` → `objetos_detectados.Id`, `ID_USER` → `usuarios.Id`),
com integridade garantida pelo próprio banco (`PRAGMA foreign_keys = ON`) — exatamente a
estrutura já mostrada no DER de §2.2.

O arquivo `Docs/Documentos_Entregaveis/Banco_de_Dados/schema.sql` (nesta mesma pasta) reflete
esse schema e é usado para visualização em ferramentas de modelagem (ex.: SQL Developer via
driver JDBC SQLite — ver `README.md` desta pasta); o arquivo de dados real da aplicação é
`radartorres.db`.
