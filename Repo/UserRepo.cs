using Microsoft.EntityFrameworkCore;
using Model.DTO;

namespace Repo
{
    public interface IUserRepo
    {
        Task AddAsync(UserDTO user);

        Task<UserDTO?> GetAsync();

        Task UpdateAsync(UserDTO user);

        Task UpdateLastUpdateAsync(DateTime lastUpdate, int uid);
    }

    public class UserRepo(IDbContextFactory<DbCtx> dbCtx) : IUserRepo
    {
        //somente um user por dispositivo.
        public async Task<UserDTO?> GetAsync()
        {
            using var db = await dbCtx.CreateDbContextAsync();
            return await db.User.FirstOrDefaultAsync();
        }

        public async Task AddAsync(UserDTO user)
        {
            using var db = await dbCtx.CreateDbContextAsync();
            db.User.Add(user);
            await db.SaveChangesAsync();
        }

        public async Task UpdateAsync(UserDTO user)
        {
            using var context = dbCtx.CreateDbContext();
            context.User.Update(user);
            await context.SaveChangesAsync();
        }

        public async Task UpdateLastUpdateAsync(DateTime lastUpdate, int uid)
        {
            using var context = dbCtx.CreateDbContext();
            await context.User.Where(x => x.Id == uid)
                .ExecuteUpdateAsync(y => y.SetProperty(z => z.LastUpdate, lastUpdate));
        }
    }
}
