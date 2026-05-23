using ApiRepo;
using Model.DTO;
using Model.Req;
using Repo;

namespace Service.Account
{
    public interface IAccountService
    {
        Task AdjustAccountBalanceAsync(AccountDTO account, decimal oldbalance, decimal newbalance, bool isON);
        Task<AccountDTO?> GetAsync();
    }

    public class AccountService(
        IAccountRepo accountRepo,
        ITransactionRepo transactionRepo,
        IAccountSyncService accountSyncService) : IAccountService
    {
        public async Task<AccountDTO?> GetAsync() => await accountRepo.GetAsync();

        public async Task AdjustAccountBalanceAsync(AccountDTO account, decimal oldbalance, decimal newbalance, bool isON)
        {
            var existingAccount = await accountRepo.GetAsync();

            if (existingAccount == null)
            {
                await accountRepo.Add(account);
                existingAccount = account;
            }

            // Garante que account tem o Id correto antes do update
            account.Id = existingAccount.Id;

            TransactionDTO adjustmentTransaction = BuildAdjustmentTransaction(account.UserId, oldbalance, newbalance, existingAccount.Id);

            await transactionRepo.Add(adjustmentTransaction);

            if (isON)
            {
                // Sincroniza com o servidor e atualiza os IDs gerados remotamente
                (int serverAccountId, int serverTransactionId) = await accountSyncService.PushAdjustAccountBalanceAsync(account, adjustmentTransaction);

                account.ExternalId = serverAccountId;
                adjustmentTransaction.ExternalId = serverTransactionId;

                await accountRepo.Update(account);
                await transactionRepo.Update(adjustmentTransaction);
            }
        }

        private static TransactionDTO BuildAdjustmentTransaction(int userId, decimal oldbalance, decimal newbalance, int accountId) =>
            new()
            {
                UserId = userId,
                Amount = newbalance - oldbalance,
                Date = DateTime.UtcNow,
                Description = "Ajuste de saldo",
                AccountId = accountId,
                Type = TransactionType.Adjustment,
                CreatedAt = DateTime.Now,
                Repetition = Repetition.None,
                CategoryId = 1,
            };
    }
}
