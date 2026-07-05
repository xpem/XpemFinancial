using ApiRepo;
using Model.DTO;
using Model.Req;
using Model.Resp.Api;
using Repo;
using Service.Transaction;

namespace Service.Account
{
    public interface IAccountService
    {
        // CRUD
        Task<AccountDTO> CreateAsync(int userId, string name, AccountType type, bool includeInGeneralBalance,bool isOnline, decimal initialBalance = 0);
        Task UpdateAsync(AccountDTO account, bool isOnline);
        Task DeactivateAsync(int accountId);

        // Consultas
        Task<List<AccountDTO>> GetAllAsync(int userId);
        Task<List<AccountDTO>> GetActiveAsync(int userId);
        Task<AccountDTO?> GetByIdAsync(int id);
        Task<AccountDTO?> GetDefaultAsync(int userId);

        // Saldo
        Task<decimal> GetGeneralBalanceAsync(int userId);
        Task AdjustAccountBalanceAsync(int accountId, decimal newBalance, decimal oldbalance, bool isOnline);

        // Migração
        Task EnsureDefaultAccountAsync(int userId);

        // Sincronização multi-conta
        Task PullAsync(int uid);
        Task PushPendingAsync(int uid);
    }

    public class AccountService(
        IAccountRepo accountRepo,
        ITransactionService transactionService,
        IAccountApiRepo accountApiRepo,
        ISyncCursorRepo syncCursorRepo) : IAccountService
    {
        private const int MaxActiveAccounts = 50;

        // ═══════════════════════════════════════════════════════════════════════
        // CRUD
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<AccountDTO> CreateAsync(int userId, string name, AccountType type, bool includeInGeneralBalance, bool isOn, decimal initialBalance = 0)
        {
            int activeCount = await accountRepo.GetActiveCountAsync(userId);
            if (activeCount >= MaxActiveAccounts)
                throw new InvalidOperationException("Limite de 50 contas ativas atingido.");

            var account = new AccountDTO
            {
                Name = name,
                Type = type,
                IncludeInGeneralBalance = includeInGeneralBalance,
                IsActive = true,
                CurrentBalance = initialBalance,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                AccountId = Guid.NewGuid(),
            };

            await accountRepo.Add(account);

            if (isOn)
                await Push(account);

            if (initialBalance != 0)
            {
                var initialTransaction = new TransactionDTO
                {
                    UserId = userId,
                    Amount = initialBalance,
                    Date = DateTime.UtcNow,
                    Description = "Saldo inicial",
                    AccountId = account.Id,
                    Type = TransactionType.Adjustment,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    Repetition = Repetition.None,
                    CategoryId = null,
                };

                await transactionService.AddAsync(initialTransaction, isOn);
            }

            return account;
        }

        public async Task UpdateAsync(AccountDTO account, bool isOnline)
        {
            // Defensive: never overwrite a non-empty AccountId with a different value
            var current = await accountRepo.GetByIdAsync(account.Id);
            if (current is not null && current.AccountId != Guid.Empty)
                account.AccountId = current.AccountId;

            account.UpdatedAt = DateTime.UtcNow;
            await accountRepo.Update(account);

            if (isOnline)
            {
                await Push(account);
            }
        }

        public async Task DeactivateAsync(int accountId)
        {
            var account = await accountRepo.GetByIdAsync(accountId)
                ?? throw new InvalidOperationException($"Conta com Id {accountId} não encontrada.");

            int activeCount = await accountRepo.GetActiveCountAsync(account.UserId);
            if (activeCount <= 1)
                throw new InvalidOperationException("Não é possível desativar a última conta ativa.");

            account.IsActive = false;
            account.UpdatedAt = DateTime.UtcNow;
            await accountRepo.Update(account);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Consultas
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<List<AccountDTO>> GetAllAsync(int userId)
            => await accountRepo.GetAllAsync(userId);

        public async Task<List<AccountDTO>> GetActiveAsync(int userId)
            => await accountRepo.GetActiveAsync(userId);

        public async Task<AccountDTO?> GetByIdAsync(int id)
            => await accountRepo.GetByIdAsync(id);

        public async Task<AccountDTO?> GetDefaultAsync(int userId)
            => await accountRepo.GetDefaultAsync(userId);

        // ═══════════════════════════════════════════════════════════════════════
        // Saldo
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<decimal> GetGeneralBalanceAsync(int userId)
        {
            var activeAccounts = await accountRepo.GetActiveAsync(userId);
            decimal total = 0;

            foreach (var account in activeAccounts.Where(a => a.IncludeInGeneralBalance))
            {
                total += await transactionService.GetBalanceAsync(account.Id) ?? 0;
            }

            return total;
        }

        public async Task AdjustAccountBalanceAsync(int accountId, decimal newBalance, decimal oldbalance, bool isOnline)
        {
            var account = await accountRepo.GetByIdAsync(accountId)
                ?? throw new InvalidOperationException($"Conta com Id {accountId} não encontrada.");

            if (!account.IsActive)
                throw new InvalidOperationException("Ajustes de saldo só são permitidos em contas ativas.");

            if (newBalance == oldbalance)
                return;

            decimal adjustmentAmount = newBalance - oldbalance;

            var adjustmentTransaction = new TransactionDTO
            {
                UserId = account.UserId,
                Amount = adjustmentAmount,
                Date = DateTime.MinValue,
                Description = "Ajuste de saldo",
                AccountId = accountId,
                Type = TransactionType.Adjustment,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Repetition = Repetition.None,
                CategoryId = null,
            };

            //somente insere local, transação de ajsute será cadastrada como AdjustAccountBalance
            await transactionService.AddAsync(adjustmentTransaction, !isOnline);

            account.CurrentBalance = newBalance;
            account.UpdatedAt = DateTime.UtcNow;
            await accountRepo.Update(account);

            if (!isOnline) return;

            await Push(account);

            AdjustAccountBalanceReq req = new()
            {
                UpdatedAt = DateTime.UtcNow,
                Inactive = account.Inactive,
                Transaction = new TransactionReq
                {
                    Description = "Ajuste de saldo",
                    Date = DateTime.MinValue,
                    Amount = adjustmentAmount,
                    Repetition = Repetition.None,
                    Type = TransactionType.Adjustment,
                }
            };

            var result = await accountApiRepo.PostAdjustAccountBalance(req);

            int serverAccountId = result.Id ?? throw new Exception("API não retornou o Id da conta.");
            int serverTransactionId = result.Transaction.Id ?? throw new Exception("API não retornou o Id da transação.");

            await UpdateExternalIdsAsync(account.Id, serverAccountId, adjustmentTransaction.Id, serverTransactionId);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Migração
        // ═══════════════════════════════════════════════════════════════════════

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
                CurrentBalance = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                AccountId = Guid.NewGuid(),
            };

            await accountRepo.Add(defaultAccount);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Sincronização multi-conta
        // ═══════════════════════════════════════════════════════════════════════

        public async Task PushPendingAsync(int uid)
        {
            DateTime cursor = await syncCursorRepo.GetAsync(SyncCursorKeys.Account);
            var pending = await accountRepo.GetPendingPushAsync(uid, cursor);

            foreach (var account in pending)
            {
                try
                {
                   await Push(account);
                }
                catch
                {
                    continue;
                }
            }
        }

        public async Task Push(AccountDTO accountDTO)
        {
            var req = new AccountReq
            {
                Id = accountDTO.ExternalId,
                Name = accountDTO.Name,
                Type = accountDTO.Type,
                IncludeInGeneralBalance = accountDTO.IncludeInGeneralBalance,
                Inactive = accountDTO.Inactive,
                UpdatedAt = accountDTO.UpdatedAt,
                AccountId = accountDTO.AccountId == Guid.Empty ? null : accountDTO.AccountId,
            };

            AccountApiRes res;
            if (accountDTO.ExternalId is not null)
            {
                // Already known by the server → PUT to update existing record
                res = await accountApiRepo.PutAccountAsync(accountDTO.ExternalId.Value, req);
            }
            else if (accountDTO.AccountId != Guid.Empty)
            {
                // AccountId-based upsert: POST — server handles dedup by AccountId
                res = await accountApiRepo.PostAccountAsync(req);
            }
            else
            {
                // Legacy: no AccountId, no ExternalId → POST (new record)
                res = await accountApiRepo.PostAccountAsync(req);
            }

            // Persist ExternalId from server response
            if (res.Id > 0)
                accountDTO.ExternalId = res.Id;

            // Persist AccountId from server response if non-empty
            if (res.AccountId is not null && res.AccountId.Value != Guid.Empty)
                accountDTO.AccountId = res.AccountId.Value;

            await accountRepo.Update(accountDTO);
        }

        public async Task PullAsync(int uid)
        {
            DateTime cursor = await syncCursorRepo.GetAsync(SyncCursorKeys.Account);
            var serverAccounts = await accountApiRepo.GetAccountsAsync(cursor);

            DateTime maxUpdatedAt = cursor;

            foreach (var res in serverAccounts)
            {
                AccountDTO? local = null;

                // 1. Try matching by AccountId first
                if (res.AccountId is not null && res.AccountId.Value != Guid.Empty)
                    local = await accountRepo.GetByAccountIdAsync(res.AccountId.Value);

                // 2. Fall back to ExternalId lookup
                if (local is null)
                    local = await accountRepo.GetByExternalIdAsync(res.Id);

                if (local is null)
                {
                    // No match — insert new record with both AccountId and ExternalId
                    var newAccount = new AccountDTO
                    {
                        Name = res.Name,
                        Type = res.Type,
                        CurrentBalance = res.CurrentBalance,
                        IncludeInGeneralBalance = res.IncludeInGeneralBalance,
                        IsActive = !res.Inactive,
                        Inactive = res.Inactive,
                        ExternalId = res.Id,
                        UserId = uid,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = res.UpdatedAt,
                        AccountId = res.AccountId is not null && res.AccountId.Value != Guid.Empty
                            ? res.AccountId.Value
                            : Guid.NewGuid(),
                    };

                    await accountRepo.Add(newAccount);
                }
                else if (res.UpdatedAt > local.UpdatedAt)
                {
                    // Last-writer-wins: update local record
                    local.Name = res.Name;
                    local.Type = res.Type;
                    local.CurrentBalance = res.CurrentBalance;
                    local.IncludeInGeneralBalance = res.IncludeInGeneralBalance;
                    local.IsActive = !res.Inactive;
                    local.Inactive = res.Inactive;
                    local.UpdatedAt = res.UpdatedAt;
                    local.ExternalId = res.Id;

                    // Persist AccountId from response if non-empty
                    if (res.AccountId is not null && res.AccountId.Value != Guid.Empty)
                        local.AccountId = res.AccountId.Value;

                    await accountRepo.Update(local);
                }
                else
                {
                    // UpdatedAt <= local — skip update but still persist AccountId if missing
                    if (res.AccountId is not null && res.AccountId.Value != Guid.Empty && local.AccountId == Guid.Empty)
                    {
                        local.AccountId = res.AccountId.Value;
                        await accountRepo.Update(local);
                    }
                }

                if (res.UpdatedAt > maxUpdatedAt)
                    maxUpdatedAt = res.UpdatedAt;
            }

            if (maxUpdatedAt > cursor)
                await syncCursorRepo.SaveAsync(SyncCursorKeys.Account, maxUpdatedAt);

            await EnsureDefaultAccountAsync(uid);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Legacy — mantidos para compatibilidade com callers existentes
        // (MainVM, AccountVM, TransactionEditVM, SyncService, FirstSyncProcessVM)
        // Serão removidos/refatorados nas Tasks 9-10.
        // ═══════════════════════════════════════════════════════════════════════

        private async Task UpdateExternalIdsAsync(int localAccountId, int serverAccountId, int localTransactionId, int serverTransactionId)
        {
            var account = await accountRepo.GetByIdAsync(localAccountId);
            if (account is not null)
            {
                account.ExternalId = serverAccountId;
                await accountRepo.Update(account);
            }

            var transaction = await transactionService.GetByIdAsync(localTransactionId);
            transaction.ExternalId = serverTransactionId;
            await transactionService.UpdateAsync(transaction, true);
        }

        // ── Private helpers ───────────────────────────────────────────────────
    }
}
