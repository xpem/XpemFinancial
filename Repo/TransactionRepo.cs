using Microsoft.EntityFrameworkCore;
using Model.DTO;

namespace Repo
{
    public interface ITransactionRepo
    {
        Task Add(TransactionDTO transaction);
    }

    public class TransactionRepo(IDbContextFactory<DbCtx> DbCtx) : ITransactionRepo
    {
        public async Task Add(TransactionDTO transaction)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            db.Transaction.Add(transaction);
            await db.SaveChangesAsync();
        }
    }
}
