using ApiRepo;
using Model.DTO;
using Model.Req;
using Repo;

namespace Service
{
    public interface IAccountService
    {
        Task AdjustAccountBalanceAsync(AccountDTO account, decimal oldbalance, decimal newbalance, bool isON);
        Task<AccountDTO?> GetAsync();
    }

    public class AccountService(IAccountRepo accountRepo, ITransactionRepo transactionRepo, IAccountApiRepo accountApiRepo) : IAccountService
    {
        public async Task<AccountDTO?> GetAsync() =>
            await accountRepo.GetAsync();

        public async Task AdjustAccountBalanceAsync(AccountDTO account, decimal oldbalance, decimal newbalance, bool isON)
        {
            var existingAccount = await accountRepo.GetAsync();

            if (existingAccount == null)
            {
                await accountRepo.Add(account);
                existingAccount = account;
            }

            TransactionDTO adjustmentTransaction = BuildAdjustmentTransaction(account.UserId, oldbalance, newbalance, existingAccount.Id);

            await transactionRepo.Add(adjustmentTransaction);
            await accountRepo.Update(account);
        }

        public async Task PostAdjustAccountBalanceAsync(AccountDTO accountDTO, TransactionDTO adjustmentTransaction)
        {
            AdjustAccountBalanceReq adjustAccountBalanceReq = new AdjustAccountBalanceReq
            {
                UpdatedAt = DateTime.UtcNow,
                Inactive = accountDTO.Inactive,
                Transaction = new TransactionReq
                {
                    Description = adjustmentTransaction.Description,
                    Date = adjustmentTransaction.Date,
                    Amount = adjustmentTransaction.Amount,
                    Repetition = adjustmentTransaction.Repetition,
                    TotalInstallments = adjustmentTransaction.TotalInstallments,
                    InstallmentId = adjustmentTransaction.InstallmentId,
                    Installment = adjustmentTransaction.Installment,
                    CategoryId = adjustmentTransaction.CategoryId,
                    Type = adjustmentTransaction.Type,
                    Note = adjustmentTransaction.Note,
                }
            };

            await accountApiRepo.PostAdjustAccountBalance(adjustAccountBalanceReq);
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
