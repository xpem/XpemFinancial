using Microsoft.EntityFrameworkCore;
using Model.DTO;

namespace Repo
{

    public interface IAccountRepo
    {
        Task Add(AccountDTO account);
        Task<AccountDTO?> GetAsync();
        Task Update(AccountDTO account);
        Task<int> GetLocalIdByExternalIdAsync(int externalId);
        Task<DateTime> GetMaxUpdatedAtAsync();
    }

    public class AccountRepo(IDbContextFactory<DbCtx> DbCtx) : IAccountRepo
    {
        public async Task Add(Model.DTO.AccountDTO account)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            db.Account.Add(account);
            await db.SaveChangesAsync();
        }

        //por enquanto só teremos uma conta e um usuário
        public async Task<Model.DTO.AccountDTO?> GetAsync()
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.Account.FirstOrDefaultAsync();
        }

        public async Task Update(Model.DTO.AccountDTO account)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            db.Account.Update(account);
            await db.SaveChangesAsync();
        }

        public async Task<int> GetLocalIdByExternalIdAsync(int externalId)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return (await db.Account.FirstOrDefaultAsync(a => a.ExternalId == externalId)).Id;
        }

        public async Task<DateTime> GetMaxUpdatedAtAsync()
        {
            using var db = await DbCtx.CreateDbContextAsync();
            var account = await db.Account.FirstOrDefaultAsync();
            return account?.UpdatedAt ?? DateTime.MinValue;
        }
    }
}
