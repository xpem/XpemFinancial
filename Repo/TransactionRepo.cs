using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Model.Res;

namespace Repo
{
    public interface ITransactionRepo
    {
        Task Add(TransactionDTO transaction);
        Task Update(TransactionDTO transaction);
        Task<TransactionDTO?> GetByExternalIdAsync(int externalId);
        Task<DateTime> GetMaxUpdatedAtAsync();
        Task<IEnumerable<TransactionDTO>> GetByMonthYear(DateTime monthYear, int? accountId = null);
        Task<decimal> GetPreviousBalanceAsync(DateTime monthYear, int? accountId = null);
        Task<decimal?> GetBalanceAsync(int accountId, DateTime dateLimit);
        Task<TransactionDTO> GetByIdAsync(int transactionId);
        Task<IEnumerable<TransactionDTO>> GetByRecurringRuleIdAsync(Guid recurringRuleId);
        Task<decimal> GetSumByAccountIdAsync(int accountId);
        Task AssignAccountToOrphansAsync(int accountId);

        Task<List<TransactionDescriptionRes>> GetTransactionDescription(string description);
        Task<List<TransactionDTO>> GetPendingPushAsync(int userId);
        Task ResetStuckPushingAsync();
        Task SetSyncStatusAsync(int transactionId, TransactionSyncStatus status);
    }

    public class TransactionRepo(IDbContextFactory<DbCtx> DbCtx) : ITransactionRepo
    {
        public async Task Add(TransactionDTO transaction)
        {
            try
            {
                using var db = await DbCtx.CreateDbContextAsync();
                db.Transaction.Add(transaction);
                await db.SaveChangesAsync();
            }catch(Exception) { throw; }
        }

        public async Task Update(TransactionDTO transaction)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            db.Transaction.Update(transaction);
            await db.SaveChangesAsync();
        }

        public async Task<IEnumerable<TransactionDTO>> GetByMonthYear(DateTime monthYear, int? accountId = null)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            var query = db.Transaction
                .Where(t => t.Date.Month == monthYear.Month
                         && t.Date.Year == monthYear.Year
                         && t.Type != TransactionType.Adjustment
                         && !t.Inactive);

            if (accountId.HasValue)
                query = query.Where(t => t.AccountId == accountId.Value);

            return await query
                .OrderByDescending(t => t.Date)
                .ToListAsync();
        }

        //calculo do saldo anteior, que é o total das transações até o inicio do mes selecionado, considera todas as transações de ajuste, entrada e saída.
        public async Task<decimal> GetPreviousBalanceAsync(DateTime monthYear, int? accountId = null)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            var query = db.Transaction
                .Where(t => t.Date < new DateTime(monthYear.Year, monthYear.Month, 1) && !t.Inactive);

            if (accountId.HasValue)
                query = query.Where(t => t.AccountId == accountId.Value);

            var previousTransactions = await query.ToListAsync();
            return previousTransactions.Sum(t => t.Amount);
        }

        public async Task<decimal?> GetBalanceAsync(int accountId, DateTime dateLimit)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            var transactions = await db.Transaction
                .Where(t => t.AccountId == accountId && t.Date < dateLimit && !t.Inactive)
                .ToListAsync();
            return transactions.Sum(t => t.Amount);
        }

        public async Task<TransactionDTO?> GetByExternalIdAsync(int externalId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Transaction.FirstOrDefaultAsync(t => t.ExternalId == externalId);
        }

        public async Task<DateTime> GetMaxUpdatedAtAsync()
        {
            using var db = await DbCtx.CreateDbContextAsync();
            if (!await db.Transaction.AnyAsync()) return DateTime.MinValue;
            return await db.Transaction.MaxAsync(t => t.UpdatedAt);
        }

        public async Task<TransactionDTO> GetByIdAsync(int transactionId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Transaction.Include(x => x.Category).FirstAsync(t => t.Id == transactionId);
        }

        public async Task<IEnumerable<TransactionDTO>> GetByRecurringRuleIdAsync(Guid recurringRuleId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Transaction
                .Where(t => t.RecurringRuleId == recurringRuleId)
                .ToListAsync();
        }

        public async Task<decimal> GetSumByAccountIdAsync(int accountId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Transaction
                .Where(t => t.AccountId == accountId && !t.Inactive)
                .SumAsync(t => t.Amount);
        }

        public async Task AssignAccountToOrphansAsync(int accountId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            var orphans = await db.Transaction
                .Where(t => t.AccountId == null)
                .ToListAsync();

            foreach (var t in orphans)
            {
                t.AccountId = accountId;
            }

            await db.SaveChangesAsync();
        }

        /// <summary>
        /// para construir a lista de recomendações de auto preenchimento da transação
        /// </summary>
        /// <param name="description"></param>
        /// <returns></returns>
        public async Task<List<TransactionDescriptionRes>> GetTransactionDescription(string description)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            var list = await db.Transaction
                .Where(t => EF.Functions.Like(t.Description, $"%{description}%"))
                .Include(t => t.Category)
                .Select(t => new TransactionDescriptionRes
                {
                    TransactionID = t.Id,
                    Description = t.Description,
                    CategoryId = t.CategoryId,
                    CategoryName = t.Category.Name,
                    AccountId = t.AccountId
                })
                .ToListAsync();
            return list;
        }

        public async Task<List<TransactionDTO>> GetPendingPushAsync(int userId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Transaction
                .Where(t => t.UserId == userId && t.SyncStatus == TransactionSyncStatus.Pending)
                .ToListAsync();
        }

        public async Task ResetStuckPushingAsync()
        {
            using var db = await DbCtx.CreateDbContextAsync();
            var stuck = await db.Transaction
                .Where(t => t.SyncStatus == TransactionSyncStatus.Pushing)
                .ToListAsync();

            foreach (var t in stuck)
                t.SyncStatus = TransactionSyncStatus.Pending;

            await db.SaveChangesAsync();
        }

        public async Task SetSyncStatusAsync(int transactionId, TransactionSyncStatus status)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            var transaction = await db.Transaction.FirstOrDefaultAsync(t => t.Id == transactionId);
            if (transaction is null) return;

            transaction.SyncStatus = status;
            await db.SaveChangesAsync();
        }
    }
}
