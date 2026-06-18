# Design Técnico — Arquitetura Multi-Contas

## Overview

A feature multi-contas evolui o XpemFinancial de um modelo de conta única para suporte a N contas por usuário, impactando ambos os sistemas:

- **Front-end (MAUI)** — `d:\Emanuel\Projetos\XpemFinancial`: expande `AccountDTO`, repositório, serviço, sincronização e UI.
- **Back-end (API)** — `d:\Emanuel\Projetos\UniqueServer\UniqueServer\FinancialService`: expande `AccountDTO`, repositório, serviço e endpoints REST.

A sincronização segue o padrão offline-first existente: operações locais primeiro, push/pull na reconexão usando `SyncCursor` por entidade.

## Architecture

### Fluxo de Dados

```
[UI/ViewModel] → [AccountService] → [AccountRepo (SQLite)]
                                   → [AccountApiRepo] → [API /financial/account*]
                                                            ↓
                                   [AccountService (API)] → [AccountRepo (SQL Server)]
```

### Ordem de Sincronização (Atualizada)

**Pull (servidor → local):**
```
1. Categories
2. Accounts       ← NOVO
3. RecurringRules
4. Transactions
```

**Push (local → servidor):**
```
1. Categories
2. Accounts       ← NOVO
3. RecurringRules
4. Transactions
```

### Push de Contas

```
1. Buscar contas pendentes: ExternalId == null OU UpdatedAt > SyncCursor["Account"]
2. Para cada conta pendente:
   a. Se ExternalId == null → POST /financial/account (criação)
   b. Se ExternalId != null → PUT /financial/account/{ExternalId} (atualização)
3. Atualizar ExternalId local com Id retornado pela API
4. Se push individual falhar → manter ExternalId null, continuar com próxima conta
```

### Pull de Contas

```
1. GET /financial/accounts?updatedAt={SyncCursor["Account"]}
2. Para cada AccountRes recebida:
   a. Buscar local por ExternalId
   b. Se não existe → criar AccountDTO local com ExternalId = res.Id
   c. Se existe e res.UpdatedAt > local.UpdatedAt → atualizar local (servidor é fonte de verdade)
3. Avançar SyncCursor["Account"] para MAX(UpdatedAt) dos registros recebidos
```

### Mapeamento de AccountId na Transação (Push)

Ao fazer push de transações, o `AccountId` local é traduzido para o `ExternalId` da conta:

```csharp
transactionReq.AccountId = localAccount.ExternalId
    ?? throw new InvalidOperationException("Conta não sincronizada");
```

Se a conta não tem `ExternalId`, a transação é adiada para o próximo ciclo de sync.

## Components and Interfaces

### Front-end — `Repo/AccountRepo.cs`

```csharp
public interface IAccountRepo
{
    Task Add(AccountDTO account);
    Task Update(AccountDTO account);

    // Consultas multi-conta
    Task<List<AccountDTO>> GetAllAsync(int userId);
    Task<List<AccountDTO>> GetActiveAsync(int userId);
    Task<AccountDTO?> GetByIdAsync(int id);
    Task<AccountDTO?> GetByExternalIdAsync(int externalId);
    Task<AccountDTO?> GetDefaultAsync(int userId);
    Task<int> GetActiveCountAsync(int userId);

    // Sync helpers
    Task<int> GetLocalIdByExternalIdAsync(int externalId);
    Task<List<AccountDTO>> GetPendingPushAsync(int userId, DateTime lastSyncCursor);
    Task<DateTime> GetMaxUpdatedAtAsync();
}
```

### Front-end — `Services/Account/AccountService.cs`

```csharp
public interface IAccountService
{
    // CRUD
    Task<AccountDTO> CreateAsync(int userId, string name, AccountType type, bool includeInGeneralBalance);
    Task UpdateAsync(AccountDTO account);
    Task DeactivateAsync(int accountId);

    // Consultas
    Task<List<AccountDTO>> GetAllAsync(int userId);
    Task<List<AccountDTO>> GetActiveAsync(int userId);
    Task<AccountDTO?> GetByIdAsync(int id);
    Task<AccountDTO?> GetDefaultAsync(int userId);

    // Saldo
    Task RecalculateBalanceAsync(int accountId);
    Task<decimal> GetGeneralBalanceAsync(int userId);
    Task AdjustAccountBalanceAsync(int accountId, decimal newBalance, bool isOnline);

    // Sincronização
    Task PullAsync(int uid);
    Task PushAsync(int uid);

    // Migração
    Task EnsureDefaultAccountAsync(int userId);
}
```

### Front-end — `ApiRepo/AccountApiRepo.cs`

```csharp
public interface IAccountApiRepo
{
    Task<List<AccountApiRes>> GetAccountsAsync(DateTime updatedAt);
    Task<AccountApiRes> PostAccountAsync(AccountReq req);
    Task<AccountApiRes> PutAccountAsync(int externalId, AccountReq req);
    Task<AdjustAccountBalanceReq> PostAdjustAccountBalance(AdjustAccountBalanceReq req);
}
```

### Back-end — `FinancialService/Repo/AccountRepo.cs`

```csharp
public interface IAccountRepo
{
    Task Add(AccountDTO account);
    Task Update(AccountDTO account);
    Task<AccountDTO?> GetByIdAsync(int id, int uid);
    Task<List<AccountDTO>> GetAllAsync(int uid);
    Task<List<AccountDTO>> GetUpdatedAfterAsync(int uid, DateTime updatedAt);
}
```

### Back-end — `FinancialService/Service/AccountService.cs`

```csharp
public interface IAccountService
{
    // CRUD
    Task<AccountRes> CreateAsync(AccountReq req, int uid);
    Task<AccountRes> UpdateAsync(int id, AccountReq req, int uid);

    // Consultas
    Task<List<AccountRes>> GetUpdatedAfterAsync(int uid, DateTime updatedAt);

    // Saldo
    Task<AdjustAccountBalanceReq> AdjustAccountBalanceAsync(AdjustAccountBalanceReq req, int uid);
    Task RecalculateBalanceAsync(int accountId, int uid);
}
```

### API Contract — Endpoints

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/financial/accounts?updatedAt={timestamp}` | Retorna `List<AccountRes>` com contas atualizadas após o timestamp |
| POST | `/financial/account` | Cria conta. Body: `AccountReq`. Retorna `AccountRes` com Id gerado |
| PUT | `/financial/account/{id}` | Atualiza conta. Body: `AccountReq`. Retorna `AccountRes` |
| POST | `/financial/adjustAccountBalance` | Já existe — mantido sem alteração |

## Data Models

### Front-end — `Model/DTO/AccountDTO.cs` (estado proposto)

```csharp
[Table("Account")]
public class AccountDTO : BaseDTO  // BaseDTO: Id, ExternalId, CreatedAt, UpdatedAt, Inactive
{
    [StringLength(100)]
    public required string Name { get; set; }

    public AccountType Type { get; set; }

    public decimal CurrentBalance { get; set; }

    public bool IncludeInGeneralBalance { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public required int UserId { get; set; }

    public UserDTO? User { get; set; }
}

public enum AccountType
{
    Checking = 0,   // Conta Corrente
    Savings = 1,    // Poupança / Investimentos
    Benefits = 2,   // Benefícios (VA/VR/Combustível)
}
```

> **Nota:** `BaseDTO.Inactive` é para soft-delete na sync. `IsActive` é semântico para contas — permite desativar sem marcar como "deleted" no servidor.

### Front-end — `Model/DTO/TransactionDTO.cs`

`AccountId` permanece `int?` (nullable) para compatibilidade com migração de dados legados.

### Back-end — `FinancialService/Model/DTO/AccountDTO.cs` (estado proposto)

```csharp
[Table("Account")]
public class AccountDTO
{
    public int Id { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(100)]
    public required string Name { get; set; }

    public AccountType Type { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IncludeInGeneralBalance { get; set; } = true;
    public bool Inactive { get; set; }
    public required int UserId { get; set; }  // muda de int? para int
}

public enum AccountType
{
    Checking = 0,
    Savings = 1,
    Benefits = 2,
}
```

### Back-end — `FinancialService/Model/Res/AccountRes.cs` (expandido)

```csharp
public record AccountRes
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public AccountType Type { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IncludeInGeneralBalance { get; set; }
    public bool Inactive { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

### Back-end — `FinancialService/Model/Req/AccountReq.cs` (novo)

```csharp
public record AccountReq : BaseReq
{
    public int? Id { get; set; }
    public DateTime UpdatedAt { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public required string Name { get; set; }

    public AccountType Type { get; set; }
    public bool IncludeInGeneralBalance { get; set; } = true;
    public bool Inactive { get; set; }
}
```

### Front-end — `Model/Res/Api/AccountApiRes.cs` (expandido)

```csharp
public class AccountApiRes
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public AccountType Type { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool IncludeInGeneralBalance { get; set; }
    public bool Inactive { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

## Error Handling

### Falhas de Sincronização

- **Push de Conta falha (servidor indisponível):** manter `ExternalId = null` localmente, retry no próximo ciclo. Não interrompe push de outras contas.
- **Push de Conta falha (erro 400/403):** registrar erro, não retry (problema de dados). Exibir notificação ao usuário.
- **Push de Transação com conta sem ExternalId:** adiar a transação (não enviar). Será resolvida no próximo ciclo após conta ser sincronizada.
- **Pull retorna conta com ExternalId desconhecido localmente:** criar nova conta local.
- **Pull de Transação referencia AccountExternalId inexistente localmente:** registrar erro, pular transação, retry no próximo pull.

### Falhas de Operação Local

- **Criação de conta excede limite de 50:** retornar erro de validação, exibir mensagem ao usuário.
- **Desativação da última conta ativa:** bloquear operação, exibir mensagem ao usuário.
- **Migração (EnsureDefaultAccountAsync) interrompida:** idempotente — se conta já existe, não duplica. Transações sem AccountId são resolvidas no próximo ciclo.

### Cadeia de Interrupção na Sync

```
Categories falha → PARA tudo (Accounts, RecurringRules, Transactions)
Accounts falha   → PARA RecurringRules e Transactions
RecurringRules falha → PARA Transactions
Transactions falha   → registra erro, fim do ciclo
```

## Correctness Properties

### Property 1: Integridade Referencial de Transações
Toda transação ativa no sistema deve possuir um `AccountId` válido referenciando uma conta existente (ativa ou inativa).

**Validates:** Requirements 3.1

### Property 2: Existência Mínima de Conta
Pelo menos uma conta ativa deve existir por usuário em qualquer estado do sistema.

**Validates:** Requirements 8.7

### Property 3: Consistência de Saldo por Conta
`CurrentBalance` de uma conta = `SUM(Amount)` de todas as transações vinculadas onde `Inactive = false`.

**Validates:** Requirements 5.1

### Property 4: Consistência do Saldo Geral
`SaldoGeral` = `SUM(CurrentBalance)` de contas onde `IncludeInGeneralBalance = true AND IsActive = true`.

**Validates:** Requirements 5.3

### Property 5: Ordem de Sync Garante FKs Resolvidas
A ordem de sync (Categories → Accounts → RecurringRules → Transactions) garante que todas as chaves estrangeiras estão resolvidas antes de inserir entidades dependentes.

**Validates:** Requirements 9.1, 9.2

### Property 6: Limite de Contas Ativas
Máximo 50 contas ativas por usuário, validado antes de qualquer criação.

**Validates:** Requirements 1.6

## Testing Strategy

### Back-end (UniqueServer)

- **Unitários:** testar `AccountService.CreateAsync`, `UpdateAsync`, `GetUpdatedAfterAsync` com mocks do repositório.
- **Integração:** testar endpoints REST (POST, PUT, GET) com banco em memória, validando autenticação/autorização (403 para conta alheia).
- **Migração:** verificar que migration EF Core aplica sem erro e que contas existentes recebem valores default.

### Front-end (XpemFinancial)

- **Unitários:** testar `AccountService` (CRUD, recalculo de saldo, migração idempotente, limite de 50).
- **Sync:** testar fluxo push/pull com mocks do `AccountApiRepo`, verificando mapeamento ExternalId e avanço de cursor.
- **Integração:** testar `EnsureDefaultAccountAsync` com banco SQLite real, verificando idempotência.
- **UI (manual):** validar filtro do dashboard, seletor na criação de transação, tela de gerenciamento.

## Migration Strategy

### Front-end

1. Incrementar `CurrentDbVersion` para 14 em `BuildDbService.cs`.
2. `BuildDbService.InitAsync()` faz `EnsureDeleted + EnsureCreated` quando versão muda — banco recriado com novo schema.
3. Pós-login: servidor envia contas no pull. Se nenhuma conta existir após sync, `EnsureDefaultAccountAsync` cria "Conta Principal".

```csharp
public async Task EnsureDefaultAccountAsync(int userId)
{
    var accounts = await accountRepo.GetAllAsync(userId);
    if (accounts.Count > 0) return; // idempotente

    var defaultAccount = new AccountDTO
    {
        Name = "Conta Principal",
        Type = AccountType.Checking,
        IncludeInGeneralBalance = true,
        IsActive = true,
        UserId = userId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
    await accountRepo.Add(defaultAccount);
    await transactionRepo.AssignAccountToOrphansAsync(defaultAccount.Id);
}
```

### Back-end

EF Core Migration para adicionar colunas:

```sql
ALTER TABLE [Account] ADD [Name] NVARCHAR(100) NOT NULL DEFAULT N'Conta Principal';
ALTER TABLE [Account] ADD [Type] INT NOT NULL DEFAULT 0;
ALTER TABLE [Account] ADD [CurrentBalance] DECIMAL(18,2) NOT NULL DEFAULT 0;
ALTER TABLE [Account] ADD [IncludeInGeneralBalance] BIT NOT NULL DEFAULT 1;
ALTER TABLE [Account] ADD [Inactive] BIT NOT NULL DEFAULT 0;
UPDATE [Account] SET [UserId] = 0 WHERE [UserId] IS NULL;
ALTER TABLE [Account] ALTER COLUMN [UserId] INT NOT NULL;
```

## Implementation Order (Dependency Graph)

```
┌─────────────────────────────────────────────────────────────────┐
│ BACK-END                                                         │
├──────────────────────────────────────────────────────────────────┤
│ 1. AccountDTO expansion + AccountType enum + EF Migration        │
│ 2. AccountReq (novo) + AccountRes expansion                      │
│ 3. AccountRepo — queries multi-conta                             │
│ 4. AccountService CRUD + novos endpoints                         │
├──────────────────────────────────────────────────────────────────┤
│ FRONT-END                                                        │
├──────────────────────────────────────────────────────────────────┤
│ 5. AccountDTO expansion + AccountType enum                       │
│ 6. AccountRepo — queries multi-conta                             │
│ 7. AccountService — CRUD + migração + recalculo de saldo         │
│ 8. AccountApiRepo — novos endpoints (GET plural, POST, PUT)      │
│ 9. Sync flow — adicionar Account no push/pull order              │
│ 10. TransactionService — filtro por AccountId + recalculo saldo  │
│ 11. BuildDbService — incrementar CurrentDbVersion para 14        │
│ 12. UI — Tela de Gerenciamento de Contas (CRUD)                  │
│ 13. UI — Dashboard filtro por conta                              │
│ 14. UI — Seletor de conta na criação de transação                │
└──────────────────────────────────────────────────────────────────┘
```

### Dependências:

```
1 → 2 → 3 → 4        (back-end sequencial)
5 → 6 → 7             (front-end model → repo → service)
4 + 7 → 8             (API pronta + service local → api repo)
8 → 9                 (api repo → sync flow)
7 + 9 → 10            (service + sync → transaction aware)
5 → 11                (model pronto → bump DB version)
7 → 12                (service pronto → UI gerenciamento)
10 + 12 → 13          (transaction filtrada + contas → dashboard)
13 → 14               (dashboard filter → pre-seleção na criação)
```

## Design Decisions

| Decisão | Justificativa |
|---------|---------------|
| `IsActive` separado de `Inactive` (BaseDTO) no front-end | `Inactive` do BaseDTO é para sync (soft-delete geral). `IsActive` é semântico para contas — desativar sem marcar como "deleted" no servidor. |
| `TransactionDTO.AccountId` permanece nullable no front-end | Permite migração gradual. Transações antigas sem conta resolvidas pela `EnsureDefaultAccountAsync`. |
| Limite de 50 contas ativas | Previne abuso sem restringir uso razoável. Validado localmente antes do push. |
| Servidor como fonte de verdade no pull | Conflitos resolvidos por `UpdatedAt` — servidor sempre ganha no pull. |
| `CurrentBalance` calculado localmente mas persistido | Evita recalcular N transações a cada visualização. Atualizado em cada operação de transação. |
| Rota plural `/financial/accounts` para lista | Diferencia do endpoint singular existente. Permite depreciar o antigo gradualmente. |
