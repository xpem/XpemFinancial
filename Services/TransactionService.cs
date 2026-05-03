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
    }

    public class TransactionService(ITransactionRepo transactionRepo) : ITransactionService
    {
        public async Task AddAsync(TransactionDTO transaction)
        {
            await transactionRepo.Add(transaction);
        }
    }
}
