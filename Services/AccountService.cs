using Microsoft.EntityFrameworkCore.Storage;
using Model.DTO;
using Repo;

namespace Service
{
    public interface IAccountService
    {
        Task AdjustAccountBalanceAsync(AccountDTO account, decimal oldbalance, decimal newbalance);
        Task<AccountDTO?> GetAsync();
        Task MockAccount(int userId);
    }

    public class AccountService(IAccountRepo accountRepo, ITransactionRepo transactionRepo) : IAccountService
    {
        public async Task<AccountDTO?> GetAsync() =>
            await accountRepo.GetAsync();

        public async Task AdjustAccountBalanceAsync(AccountDTO account, decimal oldbalance, decimal newbalance)
        {
            var existingAccount = await accountRepo.GetAsync();

            if (existingAccount == null)
            {
                await accountRepo.Add(account);
                existingAccount = account;
            }

            var adjustmentTransaction = BuildAdjustmentTransaction(account.UserId, oldbalance, newbalance, existingAccount.Id);

            await transactionRepo.Add(adjustmentTransaction);
            await accountRepo.Update(account);
        }

        public async Task MockAccount(int userId)
        {
            if (await accountRepo.GetAsync() != null)
                return;

            var mockAccount = new AccountDTO
            {
                CreatedAt = DateTime.UtcNow,
                UserId = userId,
            };

            await AdjustAccountBalanceAsync(mockAccount, 0, 1000);
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
                CategoryId = 1,
            };
    }
}
