using Microsoft.EntityFrameworkCore;
using Model.DTO;

namespace Repo
{
    public interface IUserRepo
    {
        Task AddAsync(UserDTO user);
        Task<UserDTO?> GetAsync();
    }

    public class UserRepo(IDbContextFactory<DbCtx> DbCtx) : IUserRepo
    {
        //somente um user por dispositivo.
        public async Task<UserDTO?> GetAsync()
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.User.FirstOrDefaultAsync();
        }

        public async Task AddAsync(UserDTO user)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            db.User.Add(user);
            await db.SaveChangesAsync();
        }
    }
}
