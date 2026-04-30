using Microsoft.EntityFrameworkCore;
using Model.DTO;

namespace Repo
{
    public class DbCtx(DbContextOptions<DbCtx> options) : DbContext(options)
    {
        public DbSet<VersionDbTablesDTO> VersionDbTables => Set<VersionDbTablesDTO>();

        public DbSet<UserDTO> User => Set<UserDTO>();

        public DbSet<CategoryDTO> Category => Set<CategoryDTO>();

        public DbSet<TransactionDTO> Transaction => Set<TransactionDTO>();
    }
}
