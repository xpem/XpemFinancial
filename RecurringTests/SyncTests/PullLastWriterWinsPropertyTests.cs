using ApiRepo;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using NSubstitute;
using Repo;
using Service.Transaction;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 4: Pull TransactionId Matching with Last-Writer-Wins
/// Validates: Requirements 5.2, 1.3, 8.3
/// </summary>
[Trait("Feature", "transaction-guid-sync")]
[Trait("Property", "4")]
public class PullLastWriterWinsPropertyTests
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
    /// For any local transaction with SyncStatus != Pushing and a non-empty TransactionId,
    /// when a pulled transaction with the same TransactionId has UpdatedAt strictly greater
    /// than the local UpdatedAt, the local record SHALL be updated with the pulled data.
    /// **Validates: Requirements 5.2, 1.3, 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PulledNewerUpdatedAt_UpdatesLocalRecord(Guid transactionId, PositiveInt ticksOffset)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case

        var dbName = $"PullLWW_Newer_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var localUpdatedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        // Pulled UpdatedAt is strictly greater (add positive offset in ticks)
        var pulledUpdatedAt = localUpdatedAt.AddTicks(ticksOffset.Get);

        var userId = 1;

        // Seed local transaction
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Transaction.Add(new TransactionDTO
            {
                TransactionId = transactionId,
                UserId = userId,
                Description = "Original local",
                Date = DateTime.UtcNow,
                Amount = 100m,
                SyncStatus = TransactionSyncStatus.Synced,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = localUpdatedAt,
            });
            await ctx.SaveChangesAsync();
        }

        // Build pulled transaction DTO with same TransactionId but newer UpdatedAt
        var pulledTransaction = new TransactionDTO
        {
            TransactionId = transactionId,
            UserId = userId,
            Description = "Updated from server",
            Date = DateTime.UtcNow,
            Amount = 200m,
            SyncStatus = TransactionSyncStatus.Synced,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = pulledUpdatedAt,
        };

        // Setup service with real repo, mocked dependencies
        var transactionApiRepo = Substitute.For<ITransactionApiRepo>();
        var categoryRepo = Substitute.For<ICategoryRepo>();
        var accountRepo = Substitute.For<IAccountRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        var transactionRepo = new TransactionRepo(factory);
        var service = new TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        await service.ApplyFromApiAsync(pulledTransaction);

        // Verify: local record was updated
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Transaction
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

            return record is not null
                && record.Description == "Updated from server"
                && record.Amount == 200m
                && record.UpdatedAt == pulledUpdatedAt;
        }
    }

    /// <summary>
    /// For any local transaction with SyncStatus != Pushing and a non-empty TransactionId,
    /// when a pulled transaction with the same TransactionId has UpdatedAt less than or equal
    /// to the local UpdatedAt, the local record SHALL NOT be updated.
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PulledOlderOrEqualUpdatedAt_DoesNotUpdateLocalRecord(Guid transactionId, NonNegativeInt ticksOffset)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case

        var dbName = $"PullLWW_OlderEq_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var localUpdatedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        // Pulled UpdatedAt is less than or equal (subtract offset ticks from local)
        var pulledUpdatedAt = localUpdatedAt.AddTicks(-ticksOffset.Get);

        var userId = 1;

        // Seed local transaction
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Transaction.Add(new TransactionDTO
            {
                TransactionId = transactionId,
                UserId = userId,
                Description = "Original local",
                Date = DateTime.UtcNow,
                Amount = 100m,
                SyncStatus = TransactionSyncStatus.Synced,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = localUpdatedAt,
            });
            await ctx.SaveChangesAsync();
        }

        // Build pulled transaction DTO with same TransactionId but older/equal UpdatedAt
        var pulledTransaction = new TransactionDTO
        {
            TransactionId = transactionId,
            UserId = userId,
            Description = "Should not overwrite",
            Date = DateTime.UtcNow,
            Amount = 999m,
            SyncStatus = TransactionSyncStatus.Synced,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = pulledUpdatedAt,
        };

        // Setup service with real repo, mocked dependencies
        var transactionApiRepo = Substitute.For<ITransactionApiRepo>();
        var categoryRepo = Substitute.For<ICategoryRepo>();
        var accountRepo = Substitute.For<IAccountRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        var transactionRepo = new TransactionRepo(factory);
        var service = new TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        await service.ApplyFromApiAsync(pulledTransaction);

        // Verify: local record was NOT updated
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Transaction
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

            return record is not null
                && record.Description == "Original local"
                && record.Amount == 100m
                && record.UpdatedAt == localUpdatedAt;
        }
    }

    /// <summary>
    /// For any pulled transaction with a non-empty TransactionId that matches a local record,
    /// the local record's TransactionId SHALL equal the pulled value after the operation.
    /// **Validates: Requirements 1.3, 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PulledTransactionId_IsPreservedOnLocalRecord(Guid transactionId, PositiveInt ticksOffset)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case

        var dbName = $"PullLWW_Preserved_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var localUpdatedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var pulledUpdatedAt = localUpdatedAt.AddTicks(ticksOffset.Get);

        var userId = 1;

        // Seed local transaction
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Transaction.Add(new TransactionDTO
            {
                TransactionId = transactionId,
                UserId = userId,
                Description = "Local record",
                Date = DateTime.UtcNow,
                Amount = 50m,
                SyncStatus = TransactionSyncStatus.Synced,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = localUpdatedAt,
            });
            await ctx.SaveChangesAsync();
        }

        // Build pulled transaction with same TransactionId
        var pulledTransaction = new TransactionDTO
        {
            TransactionId = transactionId,
            UserId = userId,
            Description = "From server",
            Date = DateTime.UtcNow,
            Amount = 150m,
            SyncStatus = TransactionSyncStatus.Synced,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = pulledUpdatedAt,
        };

        var transactionApiRepo = Substitute.For<ITransactionApiRepo>();
        var categoryRepo = Substitute.For<ICategoryRepo>();
        var accountRepo = Substitute.For<IAccountRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        var transactionRepo = new TransactionRepo(factory);
        var service = new TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        await service.ApplyFromApiAsync(pulledTransaction);

        // Verify: TransactionId is preserved on local record
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Transaction
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

            return record is not null && record.TransactionId == transactionId;
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
