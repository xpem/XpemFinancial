using Model.DTO;
using Repo;
using System;
using System.Collections.Generic;
using System.Text;

namespace Service
{
    public interface ITransactionService
    {
        Task AddAsync(TransactionDTO transaction);
        Task<IEnumerable<TransactionDTO>> GetByMonthYear(DateTime monthYear);

        Task<decimal> GetPreviousBalanceAsync(DateTime monthYear);

        Task<decimal?> GetBalanceAsync(int accountId);

        Task<TransactionDTO> GetByIdAsync(int id);
    }

    public class TransactionService(ITransactionRepo transactionRepo) : ITransactionService
    {
        public async Task AddAsync(TransactionDTO transaction)
        {
            await transactionRepo.Add(transaction);
        }

        public async Task<TransactionDTO> GetByIdAsync(int id)
        {
            return await transactionRepo.GetByIdAsync(id);
        }

        public async Task<IEnumerable<TransactionDTO>> GetByMonthYear(DateTime monthYear)
        {
            // Implement the logic to get transactions by month and year
            return await transactionRepo.GetByMonthYear(monthYear);
        }

        //calculo do saldo anteior, que é o total das transações até o inicio do mes selecionado, considera todas as transações de ajuste, entrada e saída.
        public async Task<decimal> GetPreviousBalanceAsync(DateTime monthYear)
        {
            return await transactionRepo.GetPreviousBalanceAsync(monthYear);
        }

        public async Task<decimal?> GetBalanceAsync(int accountId)
        {
            return await transactionRepo.GetBalanceAsync(accountId);
        }
    }
}
