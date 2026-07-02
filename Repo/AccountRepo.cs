using Microsoft.EntityFrameworkCore;
using Model.DTO;

namespace Repo
{

    public interface IAccountRepo
    {
        Task Add(AccountDTO account);
        Task Update(AccountDTO account);

        // Consultas multi-conta
        Task<List<AccountDTO>> GetAllAsync(int userId);
        Task<List<AccountDTO>> GetActiveAsync(int userId);
        Task<AccountDTO?> GetByIdAsync(int id);
        Task<AccountDTO?> GetByExternalIdAsync(int externalId);
        Task<AccountDTO?> GetDefaultAsync(int userId);
        Task<int> GetActiveCountAsync(int userId);

        // Guid-based lookup
        Task<AccountDTO?> GetByAccountIdAsync(Guid accountId);

        // Sync helpers
        Task<int> GetLocalIdByExternalIdAsync(int externalId);
        Task<List<AccountDTO>> GetPendingPushAsync(int userId, DateTime lastSyncCursor);
        Task<DateTime> GetMaxUpdatedAtAsync();
    }

    public class AccountRepo(IDbContextFactory<DbCtx> DbCtx) : IAccountRepo
    {
        public async Task Add(AccountDTO account)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            db.Account.Add(account);
            await db.SaveChangesAsync();
        }

        public async Task Update(AccountDTO account)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            db.Account.Update(account);
            await db.SaveChangesAsync();
        }

        public async Task<List<AccountDTO>> GetAllAsync(int userId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Account.Where(a => a.UserId == userId).ToListAsync();
        }

        public async Task<List<AccountDTO>> GetActiveAsync(int userId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Account
                .Where(a => a.UserId == userId && a.IsActive)
                .ToListAsync();
        }

        public async Task<AccountDTO?> GetByIdAsync(int id)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Account.FirstOrDefaultAsync(a => a.Id == id);
        }

        public async Task<AccountDTO?> GetByExternalIdAsync(int externalId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Account.FirstOrDefaultAsync(a => a.ExternalId == externalId);
        }

        public async Task<AccountDTO?> GetDefaultAsync(int userId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Account
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<int> GetActiveCountAsync(int userId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Account.CountAsync(a => a.UserId == userId && a.IsActive);
        }

        public async Task<AccountDTO?> GetByAccountIdAsync(Guid accountId)
        {
            if (accountId == Guid.Empty) return null;

            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Account.FirstOrDefaultAsync(a => a.AccountId == accountId);
        }

        public async Task<int> GetLocalIdByExternalIdAsync(int externalId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            var account = await db.Account.FirstOrDefaultAsync(a => a.ExternalId == externalId);
            return account?.Id ?? 0;
        }

        public async Task<List<AccountDTO>> GetPendingPushAsync(int userId, DateTime lastSyncCursor)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Account
                .Where(a => a.UserId == userId && (a.ExternalId == null || a.UpdatedAt > lastSyncCursor))
                .ToListAsync();
        }

        public async Task<DateTime> GetMaxUpdatedAtAsync()
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Account.AnyAsync()
                ? await db.Account.MaxAsync(a => a.UpdatedAt)
                : DateTime.MinValue;
        }
    }
}
