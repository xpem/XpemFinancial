using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Repo;
using Service.Transaction;
using NSubstitute;
using ApiRepo;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 5: Pull Skips Records in Pushing State
/// Validates: Requirements 5.3
/// </summary>
[Trait("Feature", "transaction-guid-sync")]
[Trait("Property", "5")]
public class PullSkipsPushingPropertyTests
{
    /// <summary>
    /// Creates a DbContextFactory backed by a unique in-memory database.
    /// </summary>
    private static IDbContextFactory<DbCtx> CreateFactory(string dbName)
    {
        var options = new DbContextOptionsBuilder<DbCtx>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new TestDbContextFactory(options);
    }

    /// <summary>
    /// For any local transaction with SyncStatus == Pushing, when a pulled transaction
    /// arrives with the same TransactionId (even with different fields and newer UpdatedAt),
    /// the local record SHALL remain completely unchanged after ApplyFromApiAsync.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PushingRecord_IsNotOverwritten_ByPull(
        Guid transactionId,
        decimal localAmount,
        decimal pulledAmount)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case
        if (localAmount == pulledAmount) return true; // need different values to verify no overwrite

        var dbName = $"PullSkipsPushing_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var localDescription = "Local pushing description";
        var localDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var localUpdatedAt = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);
        var userId = 1;

        // Seed a local transaction with SyncStatus = Pushing
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Transaction.Add(new TransactionDTO
            {
                TransactionId = transactionId,
                UserId = userId,
                Description = localDescription,
                Date = localDate,
                Amount = localAmount,
                SyncStatus = TransactionSyncStatus.Pushing,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = localUpdatedAt,
            });

            await ctx.SaveChangesAsync();
        }

        // Build a pulled transaction with same TransactionId but different data and newer UpdatedAt
        var pulledTransaction = new TransactionDTO
        {
            TransactionId = transactionId,
            UserId = userId,
            Description = "Pulled different description",
            Date = new DateTime(2024, 2, 20, 12, 0, 0, DateTimeKind.Utc),
            Amount = pulledAmount,
            SyncStatus = TransactionSyncStatus.Synced,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = localUpdatedAt.AddDays(5), // newer than local
        };

        // Setup service with real TransactionRepo, mock other deps
        var transactionApiRepo = Substitute.For<ITransactionApiRepo>();
        var categoryRepo = Substitute.For<ICategoryRepo>();
        var accountRepo = Substitute.For<IAccountRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        var transactionRepo = new TransactionRepo(factory);
        var service = new TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        // Act: call ApplyFromApiAsync with the pulled transaction
        await service.ApplyFromApiAsync(pulledTransaction);

        // Assert: the local record should be completely unchanged
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Transaction
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

            return record is not null
                && record.Description == localDescription
                && record.Amount == localAmount
                && record.Date == localDate
                && record.SyncStatus == TransactionSyncStatus.Pushing
                && record.UpdatedAt == localUpdatedAt;
        }
    }

    /// <summary>
    /// For any local transaction with SyncStatus == Pushing, even when the pulled transaction
    /// has ExternalId set (fallback path), the record SHALL still not be overwritten.
    /// **Validates: Requirements 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PushingRecord_IsNotOverwritten_EvenWithExternalIdMatch(
        Guid transactionId,
        int externalId)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case
        if (externalId <= 0) return true; // need valid ExternalId

        var dbName = $"PullSkipsPushing_ExtId_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var localDescription = "Local pushing with external";
        var localAmount = 100m;
        var localUpdatedAt = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var userId = 1;

        // Seed local transaction: SyncStatus=Pushing with ExternalId
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Transaction.Add(new TransactionDTO
            {
                TransactionId = transactionId,
                UserId = userId,
                Description = localDescription,
                Date = DateTime.UtcNow,
                Amount = localAmount,
                ExternalId = externalId,
                SyncStatus = TransactionSyncStatus.Pushing,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = localUpdatedAt,
            });

            await ctx.SaveChangesAsync();
        }

        // Build pulled transaction with same TransactionId, different data, newer UpdatedAt
        var pulledTransaction = new TransactionDTO
        {
            TransactionId = transactionId,
            UserId = userId,
            Description = "Server version should be ignored",
            Date = DateTime.UtcNow,
            Amount = 999m,
            ExternalId = externalId,
            SyncStatus = TransactionSyncStatus.Synced,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = localUpdatedAt.AddDays(10), // much newer
        };

        // Setup service
        var transactionApiRepo = Substitute.For<ITransactionApiRepo>();
        var categoryRepo = Substitute.For<ICategoryRepo>();
        var accountRepo = Substitute.For<IAccountRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        var transactionRepo = new TransactionRepo(factory);
        var service = new TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        // Act
        await service.ApplyFromApiAsync(pulledTransaction);

        // Assert: local record unchanged
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Transaction
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

            return record is not null
                && record.Description == localDescription
                && record.Amount == localAmount
                && record.SyncStatus == TransactionSyncStatus.Pushing;
        }
    }

    /// <summary>
    /// Simple IDbContextFactory implementation for testing.
    /// </summary>
    private sealed class TestDbContextFactory(DbContextOptions<DbCtx> options) : IDbContextFactory<DbCtx>
    {
        public DbCtx CreateDbContext() => new(options);
        public Task<DbCtx> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new DbCtx(options));
    }
}
