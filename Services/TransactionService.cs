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
            if (transaction.Repetition == Repetition.Monthly)
            {
                if (transaction.TotalInstallments == null || transaction.TotalInstallments <= 0)
                {
                    throw new ArgumentException("TotalInstallments must be greater than 0 for monthly transactions.");
                }

                if(transaction.Installment != null && transaction.Installment > transaction.TotalInstallments)
                {
                    throw new ArgumentException("Installment number cannot be greater than TotalInstallments.");
                }

                if (transaction.InstallmentId == null)
                {
                    transaction.InstallmentId = Guid.NewGuid();
                }

                for (int i = transaction.Installment!.Value; i <= transaction.TotalInstallments; i++)
                {
                    var installmentTransaction = new TransactionDTO
                    {
                        Description = transaction.Description,
                        Date = transaction.Date.AddMonths(i-1),
                        Amount = transaction.Amount,
                        Repetition = transaction.Repetition,
                        TotalInstallments = transaction.TotalInstallments,
                        InstallmentId = transaction.InstallmentId,
                        Installment = i,
                        CategoryId = transaction.CategoryId,
                        Type = transaction.Type,
                        Note = transaction.Note,
                        AccountId = transaction.AccountId,
                        UserId = transaction.UserId
                    };

                    await transactionRepo.Add(installmentTransaction);
                }
            }
            else
            {
                await transactionRepo.Add(transaction);
            }
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
