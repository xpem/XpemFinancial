using ApiRepo;
using Model.DTO;
using Model.Req;
using Repo;

namespace Service.Account
{
    public interface IAccountService
    {
        Task<AccountDTO?> GetAsync();
        Task<DateTime> GetLastUpdatedAtAsync();
        Task PullAsync(int uid);
        Task UpsertFromApiAsync(int uid, int externalId, DateTime updatedAt, bool inactive);
        Task AdjustAccountBalanceAsync(AccountDTO account, decimal oldbalance, decimal newbalance, bool isOnline);
        Task UpdateExternalIdsAsync(int localAccountId, int serverAccountId, int localTransactionId, int serverTransactionId);
    }

    public class AccountService(
        IAccountRepo accountRepo,
        ITransactionRepo transactionRepo,
        IAccountApiRepo accountApiRepo) : IAccountService
    {
        public async Task<AccountDTO?> GetAsync() => await accountRepo.GetAsync();

        public async Task<DateTime> GetLastUpdatedAtAsync()
        {
            var account = await accountRepo.GetAsync();
            return account?.UpdatedAt ?? DateTime.MinValue;
        }

        public async Task PullAsync(int uid)
        {
            DateTime lastUpdatedAt = await GetLastUpdatedAtAsync();
            var apiAccount = await accountApiRepo.GetAccountAsync(lastUpdatedAt);

            if (apiAccount is null) return;

            await UpsertFromApiAsync(uid, apiAccount.Id, apiAccount.UpdatedAt, apiAccount.Inactive);
        }

        public async Task UpsertFromApiAsync(int uid, int externalId, DateTime updatedAt, bool inactive)
        {
            var localAccount = await accountRepo.GetAsync();

            if (localAccount is null)
            {
                await accountRepo.Add(new AccountDTO
                {
                    UserId = uid,
                    ExternalId = externalId,
                    Inactive = inactive,
                    UpdatedAt = updatedAt,
                    CreatedAt = DateTime.Now,
                });
            }
            else if (localAccount.ExternalId != externalId)
            {
                localAccount.ExternalId = externalId;
                localAccount.UpdatedAt = updatedAt;
                await accountRepo.Update(localAccount);
            }
        }

        public async Task AdjustAccountBalanceAsync(AccountDTO account, decimal oldbalance, decimal newbalance, bool isOnline)
        {
            (int localAccountId, int localTransactionId) = await AdjustBalanceLocalAsync(account, oldbalance, newbalance);

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

        private async Task<(int localAccountId, int localTransactionId)> AdjustBalanceLocalAsync(AccountDTO account, decimal oldbalance, decimal newbalance)
        {
            var existingAccount = await accountRepo.GetAsync();

            if (existingAccount == null)
            {
                await accountRepo.Add(account);
                existingAccount = account;
            }

            account.Id = existingAccount.Id;

            TransactionDTO adjustmentTransaction = BuildAdjustmentTransaction(account.UserId, oldbalance, newbalance, existingAccount.Id);

            await transactionRepo.Add(adjustmentTransaction);
            await accountRepo.Update(account);

            return (account.Id, adjustmentTransaction.Id);
        }

        public async Task UpdateExternalIdsAsync(int localAccountId, int serverAccountId, int localTransactionId, int serverTransactionId)
        {
            var account = await accountRepo.GetAsync();
            if (account is not null && account.Id == localAccountId)
            {
                account.ExternalId = serverAccountId;
                await accountRepo.Update(account);
            }

            var transaction = await transactionRepo.GetByIdAsync(localTransactionId);
            transaction.ExternalId = serverTransactionId;
            await transactionRepo.Update(transaction);
        }

        private static TransactionDTO BuildAdjustmentTransaction(int userId, decimal oldbalance, decimal newbalance, int accountId) =>
            new()
            {
                UserId = userId,
                Amount = newbalance - oldbalance,
                Date = DateTime.MinValue,
                Description = "Ajuste de saldo",
                AccountId = accountId,
                Type = TransactionType.Adjustment,
                CreatedAt = DateTime.Now,
                Repetition = Repetition.None,
            };
    }
}
