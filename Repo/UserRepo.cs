using Microsoft.EntityFrameworkCore;
using Model.DTO;

namespace Repo
{
    public interface IUserRepo
    {
        Task AddAsync(UserDTO user);

        Task<UserDTO?> GetAsync();

        Task UpdateAsync(UserDTO user);

        /// <summary>
        /// Writes only the token columns. A null <paramref name="refreshToken"/> keeps the stored one.
        /// </summary>
        Task UpdateTokensAsync(int uid, string token, string? refreshToken);

        /// <summary>
        /// Same-user sign-in: refreshes name, e-mail and tokens WITHOUT touching local preferences
        /// (IncludePreviousBalance, LastUpdate, Balance...), which a full <see cref="UpdateAsync"/> would reset.
        /// </summary>
        Task UpdateSessionAsync(UserDTO user);

        Task UpdateLastUpdateAsync(DateTime lastUpdate, int uid);

        Task UpdateIncludePreviousBalanceAsync(bool value, int uid);
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

        public async Task UpdateTokensAsync(int uid, string token, string? refreshToken)
        {
            using var context = dbCtx.CreateDbContext();

            if (refreshToken is null)
            {
                await context.User.Where(x => x.Id == uid)
                    .ExecuteUpdateAsync(y => y.SetProperty(z => z.Token, token));
                return;
            }

            await context.User.Where(x => x.Id == uid)
                .ExecuteUpdateAsync(y => y
                    .SetProperty(z => z.Token, token)
                    .SetProperty(z => z.RefreshToken, refreshToken));
        }

        public async Task UpdateSessionAsync(UserDTO user)
        {
            using var context = dbCtx.CreateDbContext();
            await context.User.Where(x => x.Id == user.Id)
                .ExecuteUpdateAsync(y => y
                    .SetProperty(z => z.Name, user.Name)
                    .SetProperty(z => z.Email, user.Email)
                    .SetProperty(z => z.Token, user.Token)
                    .SetProperty(z => z.RefreshToken, user.RefreshToken));
        }

        public async Task UpdateLastUpdateAsync(DateTime lastUpdate, int uid)
        {
            using var context = dbCtx.CreateDbContext();
            await context.User.Where(x => x.Id == uid)
                .ExecuteUpdateAsync(y => y.SetProperty(z => z.LastUpdate, lastUpdate));
        }

        public async Task UpdateIncludePreviousBalanceAsync(bool value, int uid)
        {
            using var context = dbCtx.CreateDbContext();
            await context.User.Where(x => x.Id == uid)
                .ExecuteUpdateAsync(y => y.SetProperty(z => z.IncludePreviousBalance, value));
        }
    }
}
