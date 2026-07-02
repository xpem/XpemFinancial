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

        public DbSet<AccountDTO> Account => Set<AccountDTO>();

        public DbSet<RecurringRuleDTO> RecurringRule => Set<RecurringRuleDTO>();

        public DbSet<SyncCursorDTO> SyncCursor => Set<SyncCursorDTO>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TransactionDTO>(entity =>
            {
                // Filtered unique index: ensures no two transactions share the same
                // TransactionId, but excludes Guid.Empty so legacy rows don't conflict.
                entity.HasIndex(t => t.TransactionId)
                    .IsUnique()
                    .HasFilter("\"TransactionId\" != '00000000-0000-0000-0000-000000000000'");

                entity.Property(t => t.TransactionId)
                    .HasDefaultValue(Guid.Empty);
            });

            modelBuilder.Entity<CategoryDTO>(entity =>
            {
                entity.Property(c => c.CategoryId)
                    .HasDefaultValue(Guid.Empty);
            });

            modelBuilder.Entity<AccountDTO>(entity =>
            {
                entity.HasIndex(a => a.AccountId)
                    .IsUnique()
                    .HasFilter("\"AccountId\" != '00000000-0000-0000-0000-000000000000'");

                entity.Property(a => a.AccountId)
                    .HasDefaultValue(Guid.Empty);
            });
        }
    }
}
