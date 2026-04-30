using Microsoft.EntityFrameworkCore;
using Model.DTO;

namespace Repo
{
    public interface IUserRepo
    {
        Task Add(UserDTO user);
        Task<UserDTO?> Get();
    }

    public class UserRepo(IDbContextFactory<DbCtx> DbCtx) : IUserRepo
    {
        //somente um user por dispositivo.
        public async Task<UserDTO?> Get()
        {
            using var db = await DbCtx.CreateDbContextAsync();
            return await db.User.FirstOrDefaultAsync();
        }

        public async Task Add(UserDTO user)
        {
            using var db = await DbCtx.CreateDbContextAsync();
            db.User.Add(user);
            await db.SaveChangesAsync();
        }
    }
}
