using Model.DTO;
using Repo;

namespace Service
{
    public interface IAccountService
    {
        Task AdjustAccountBalanceAsync(AccountDTO account);

        Task<AccountDTO?> GetAsync();
        Task MockAccount(int userId);
    }

    public class AccountService(IAccountRepo accountRepo, ITransactionRepo transactionRepo) : IAccountService
    {
        public async Task<AccountDTO?> GetAsync()
        {
            return await accountRepo.GetAsync();
        }

        public async Task AdjustAccountBalanceAsync(Model.DTO.AccountDTO account)
        {
            var existingAccount = await accountRepo.GetAsync();

            if (existingAccount == null)
            {
                await accountRepo.Add(account);
                return;
            }

            //atualização de conta. deve lançar uma transação de transferencia ajustando o valor da conta sem impactar nos gráficos.
            var transaction = new Model.DTO.TransactionDTO
            {
                Amount = account.Balance - existingAccount.Balance,
                Date = DateTime.Now,
                Description = "Ajuste de saldo",
                AccountId = existingAccount.Id,
                Type = TransactionType.Transfer,
                CreatedAt = DateTime.Now,
                Repetition = Repetition.None,
            };

            await transactionRepo.Add(transaction);

            await accountRepo.Update(account);
        }

        public async Task MockAccount(int userId)
        {
            if (await accountRepo.GetAsync() != null)
                return;

            var mockAccount = new Model.DTO.AccountDTO
            {
                Balance = 1000,
                CreatedAt = DateTime.UtcNow,
                UserId = userId,
            };

            await accountRepo.Add(mockAccount);
        }
    }
}
