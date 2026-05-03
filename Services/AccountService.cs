using Model.DTO;
using Repo;

namespace Service
{
    public interface IAccountService
    {
        Task AdjustAccountBalanceAsync(AccountDTO account);

        Task<AccountDTO?> GetAsync();
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
                Type = Model.TransactionType.Transfer,
                CreatedAt = DateTime.Now,
                Repetition = Model.Repetition.None,
            };

            await transactionRepo.Add(transaction);

            await accountRepo.Update(account);
        }
    }
}
