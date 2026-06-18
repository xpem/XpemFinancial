using ApiRepo;
using Model.DTO;
using Model.Req;
using Model.Resp.Api;
using Repo;

namespace Service.Account
{
    public interface IAccountService
    {
        // CRUD
        Task<AccountDTO> CreateAsync(int userId, string name, AccountType type, bool includeInGeneralBalance, decimal initialBalance = 0);
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
        Task AdjustAccountBalanceAsync(int accountId, decimal newBalance, decimal oldbalance, bool isOnline);

        // Migração
        Task EnsureDefaultAccountAsync(int userId);

        // Sincronização multi-conta
        Task PullAsync(int uid);
        Task PushAsync(int uid);

        // ── Legacy (mantidos para compatibilidade com callers existentes até Tasks 9-10) ──
        Task<AccountDTO?> GetAsync();
        Task<DateTime> GetLastUpdatedAtAsync();
        Task PullAsync(int uid, DateTime lastUpdatedAt);
        Task AdjustAccountBalanceAsync(AccountDTO account, decimal oldbalance, decimal newbalance, bool isOnline);
        Task UpdateExternalIdsAsync(int localAccountId, int serverAccountId, int localTransactionId, int serverTransactionId);
    }

    public class AccountService(
        IAccountRepo accountRepo,
        ITransactionRepo transactionRepo,
        IAccountApiRepo accountApiRepo,
        ISyncCursorRepo syncCursorRepo) : IAccountService
    {
        private const int MaxActiveAccounts = 50;

        // ═══════════════════════════════════════════════════════════════════════
        // CRUD
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<AccountDTO> CreateAsync(int userId, string name, AccountType type, bool includeInGeneralBalance, decimal initialBalance = 0)
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
            };

            await accountRepo.Add(account);

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

                await transactionRepo.Add(initialTransaction);
            }

            return account;
        }

        public async Task UpdateAsync(AccountDTO account)
        {
            account.UpdatedAt = DateTime.UtcNow;
            await accountRepo.Update(account);
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

        public async Task RecalculateBalanceAsync(int accountId)
        {
            var account = await accountRepo.GetByIdAsync(accountId)
                ?? throw new InvalidOperationException($"Conta com Id {accountId} não encontrada.");

            decimal sum = await transactionRepo.GetSumByAccountIdAsync(accountId);
            account.CurrentBalance = sum;
            account.UpdatedAt = DateTime.UtcNow;
            await accountRepo.Update(account);
        }

        public async Task<decimal> GetGeneralBalanceAsync(int userId)
        {
            var accounts = await accountRepo.GetActiveAsync(userId);
            return accounts
                .Where(a => a.IncludeInGeneralBalance)
                .Sum(a => a.CurrentBalance);
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
                Date = DateTime.UtcNow,
                Description = "Ajuste de saldo",
                AccountId = accountId,
                Type = TransactionType.Adjustment,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Repetition = Repetition.None,
                CategoryId = null,
            };

            await transactionRepo.Add(adjustmentTransaction);

            account.CurrentBalance = newBalance;
            account.UpdatedAt = DateTime.UtcNow;
            await accountRepo.Update(account);

            if (!isOnline) return;

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
            };

            await accountRepo.Add(defaultAccount);

            // Assign orphan transactions (AccountId == null) to the newly created default account
            await transactionRepo.AssignAccountToOrphansAsync(defaultAccount.Id);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Sincronização multi-conta
        // ═══════════════════════════════════════════════════════════════════════

        public async Task PushAsync(int uid)
        {
            DateTime cursor = await syncCursorRepo.GetAsync(SyncCursorKeys.Account);
            var pending = await accountRepo.GetPendingPushAsync(uid, cursor);

            foreach (var account in pending)
            {
                try
                {
                    var req = new AccountReq
                    {
                        Id = account.ExternalId,
                        Name = account.Name,
                        Type = account.Type,
                        IncludeInGeneralBalance = account.IncludeInGeneralBalance,
                        Inactive = account.Inactive,
                        UpdatedAt = account.UpdatedAt,
                    };

                    AccountApiRes res;

                    if (account.ExternalId is null)
                    {
                        res = await accountApiRepo.PostAccountAsync(req);
                    }
                    else
                    {
                        res = await accountApiRepo.PutAccountAsync(account.ExternalId.Value, req);
                    }

                    account.ExternalId = res.Id;
                    await accountRepo.Update(account);
                }
                catch
                {
                    // Push individual falhou — manter ExternalId atual, tentar no próximo ciclo.
                    // Não interrompe push das demais contas (Req 10.7).
                    continue;
                }
            }
        }

        public async Task PullAsync(int uid)
        {
            DateTime cursor = await syncCursorRepo.GetAsync(SyncCursorKeys.Account);
            var serverAccounts = await accountApiRepo.GetAccountsAsync(cursor);

            DateTime maxUpdatedAt = cursor;

            foreach (var res in serverAccounts)
            {
                var local = await accountRepo.GetByExternalIdAsync(res.Id);

                if (local is null)
                {
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
                    };
                    await accountRepo.Add(newAccount);
                }
                else if (res.UpdatedAt > local.UpdatedAt)
                {
                    local.Name = res.Name;
                    local.Type = res.Type;
                    local.CurrentBalance = res.CurrentBalance;
                    local.IncludeInGeneralBalance = res.IncludeInGeneralBalance;
                    local.IsActive = !res.Inactive;
                    local.Inactive = res.Inactive;
                    local.UpdatedAt = res.UpdatedAt;
                    await accountRepo.Update(local);
                }

                if (res.UpdatedAt > maxUpdatedAt)
                    maxUpdatedAt = res.UpdatedAt;
            }

            if (maxUpdatedAt > cursor)
                await syncCursorRepo.SaveAsync(SyncCursorKeys.Account, maxUpdatedAt);

            // Garantir que pelo menos uma conta padrão exista após o pull
            await EnsureDefaultAccountAsync(uid);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Legacy — mantidos para compatibilidade com callers existentes
        // (MainVM, AccountVM, TransactionEditVM, SyncService, FirstSyncProcessVM)
        // Serão removidos/refatorados nas Tasks 9-10.
        // ═══════════════════════════════════════════════════════════════════════

        public async Task<AccountDTO?> GetAsync()
        {
            // Legacy: returns the first account found (single-account behavior)
            var all = await accountRepo.GetAllAsync(0);
            if (all.Count > 0) return all[0];

            // Fallback: try to find any account regardless of userId (legacy had no userId filter)
            return await accountRepo.GetByIdAsync(1);
        }

        public async Task<DateTime> GetLastUpdatedAtAsync()
            => await syncCursorRepo.GetAsync(SyncCursorKeys.Account);

        public async Task PullAsync(int uid, DateTime lastUpdatedAt)
        {
            var apiAccount = await accountApiRepo.GetAccountAsync(lastUpdatedAt);

            if (apiAccount is null) return;

            await UpsertFromApiAsync(uid, apiAccount.Id, apiAccount.UpdatedAt, apiAccount.Inactive);

            DateTime current = await syncCursorRepo.GetAsync(SyncCursorKeys.Account);
            if (apiAccount.UpdatedAt > current)
                await syncCursorRepo.SaveAsync(SyncCursorKeys.Account, apiAccount.UpdatedAt);
        }

        public async Task AdjustAccountBalanceAsync(AccountDTO account, decimal oldbalance, decimal newbalance, bool isOnline)
        {
            (int localAccountId, int localTransactionId) = await AdjustBalanceLocalLegacyAsync(account, oldbalance, newbalance);

            if (!isOnline) return;

            AdjustAccountBalanceReq req = new()
            {
                UpdatedAt = DateTime.UtcNow,
                Inactive = account.Inactive,
                Transaction = new TransactionReq
                {
                    Description = "Ajuste de saldo",
                    Date = DateTime.MinValue,
                    Amount = newbalance - oldbalance,
                    Repetition = Repetition.None,
                    Type = TransactionType.Adjustment,
                }
            };

            var result = await accountApiRepo.PostAdjustAccountBalance(req);

            int serverAccountId = result.Id ?? throw new Exception("API não retornou o Id da conta.");
            int serverTransactionId = result.Transaction.Id ?? throw new Exception("API não retornou o Id da transação.");

            await UpdateExternalIdsAsync(localAccountId, serverAccountId, localTransactionId, serverTransactionId);
        }

        public async Task UpdateExternalIdsAsync(int localAccountId, int serverAccountId, int localTransactionId, int serverTransactionId)
        {
            var account = await accountRepo.GetByIdAsync(localAccountId);
            if (account is not null)
            {
                account.ExternalId = serverAccountId;
                await accountRepo.Update(account);
            }

            var transaction = await transactionRepo.GetByIdAsync(localTransactionId);
            transaction.ExternalId = serverTransactionId;
            await transactionRepo.Update(transaction);
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private async Task UpsertFromApiAsync(int uid, int externalId, DateTime updatedAt, bool inactive)
        {
            var localAccount = await accountRepo.GetByExternalIdAsync(externalId);

            if (localAccount is null)
            {
                // Check if any account exists for this user — if so, update the first one
                var allAccounts = await accountRepo.GetAllAsync(uid);
                if (allAccounts.Count > 0)
                {
                    var first = allAccounts[0];
                    if (first.ExternalId is null)
                    {
                        first.ExternalId = externalId;
                        first.UpdatedAt = updatedAt;
                        first.Inactive = inactive;
                        await accountRepo.Update(first);
                        return;
                    }
                }

                await accountRepo.Add(new AccountDTO
                {
                    Name = "Conta Principal",
                    UserId = uid,
                    ExternalId = externalId,
                    Inactive = inactive,
                    UpdatedAt = updatedAt,
                    CreatedAt = DateTime.UtcNow,
                });
            }
            else if (updatedAt > localAccount.UpdatedAt)
            {
                localAccount.UpdatedAt = updatedAt;
                localAccount.Inactive = inactive;
                await accountRepo.Update(localAccount);
            }
        }

        private async Task<(int localAccountId, int localTransactionId)> AdjustBalanceLocalLegacyAsync(AccountDTO account, decimal oldbalance, decimal newbalance)
        {
            var existingAccount = await accountRepo.GetByIdAsync(account.Id);

            if (existingAccount == null)
            {
                account.CreatedAt = DateTime.UtcNow;
                account.UpdatedAt = DateTime.UtcNow;
                await accountRepo.Add(account);
                existingAccount = account;
            }

            account.Id = existingAccount.Id;

            TransactionDTO adjustmentTransaction = new()
            {
                UserId = account.UserId,
                Amount = newbalance - oldbalance,
                Date = DateTime.MinValue,
                Description = "Ajuste de saldo",
                AccountId = existingAccount.Id,
                Type = TransactionType.Adjustment,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Repetition = Repetition.None,
                CategoryId = null,
            };

            await transactionRepo.Add(adjustmentTransaction);
            await accountRepo.Update(account);

            return (account.Id, adjustmentTransaction.Id);
        }
    }
}
