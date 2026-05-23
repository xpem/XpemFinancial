using Microsoft.EntityFrameworkCore;
using Model.DTO;

namespace Repo
{
    public interface ITransactionRepo
    {
        Task Add(TransactionDTO transaction);
        Task Update(TransactionDTO transaction);
        Task<IEnumerable<TransactionDTO>> GetByMonthYear(DateTime monthYear);
        Task<decimal> GetPreviousBalanceAsync(DateTime monthYear);
        Task<decimal?> GetBalanceAsync(int accountId);
        Task<TransactionDTO> GetByIdAsync(int transactionId);
    }

    public class TransactionRepo(IDbContextFactory<DbCtx> DbCtx) : ITransactionRepo
    {
        public async Task Add(TransactionDTO transaction)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            db.Transaction.Add(transaction);
            await db.SaveChangesAsync();
        }

        public async Task Update(TransactionDTO transaction)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            db.Transaction.Update(transaction);
            await db.SaveChangesAsync();
        }

        public async Task<IEnumerable<TransactionDTO>> GetByMonthYear(DateTime monthYear)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Transaction
                .Where(t => t.Date.Month == monthYear.Month && t.Date.Year == monthYear.Year).OrderByDescending(t => t.Date)
                .ToListAsync();
        }

        //calculo do saldo anteior, que é o total das transações até o inicio do mes selecionado, considera todas as transações de ajuste, entrada e saída.
        public async Task<decimal> GetPreviousBalanceAsync(DateTime monthYear)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            var previousTransactions = await db.Transaction.Where(t => t.Date < new DateTime(monthYear.Year, monthYear.Month, 1)).ToListAsync();
            return previousTransactions.Sum(t => t.Amount);
        }

        public async Task<decimal?> GetBalanceAsync(int accountId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            var transactions = await db.Transaction.Where(t => t.AccountId == accountId).ToListAsync();
            return transactions.Sum(t => t.Amount);
        }

        public async Task<TransactionDTO> GetByIdAsync(int transactionId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Transaction.Include(x => x.Category).FirstAsync(t => t.Id == transactionId);
        }
    }
}
