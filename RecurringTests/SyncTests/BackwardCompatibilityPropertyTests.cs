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
/// Property 10: Backward Compatibility — Guid.Empty Falls Back to ExternalId
/// Validates: Requirements 8.1, 8.6
/// </summary>
[Trait("Feature", "transaction-guid-sync")]
[Trait("Property", "10")]
public class BackwardCompatibilityPropertyTests
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
    /// For any transaction with TransactionId == Guid.Empty and a valid ExternalId,
    /// when a pulled transaction arrives with TransactionId == Guid.Empty and the same ExternalId
    /// with a newer UpdatedAt, the local record SHALL be updated (matched by ExternalId).
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PullFallsBackToExternalIdWhenTransactionIdIsEmpty(PositiveInt externalId, PositiveInt ticksOffset)
    {
        var dbName = $"BackCompat_PullFallback_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var userId = 1;
        var extId = externalId.Get;
        var localUpdatedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var pulledUpdatedAt = localUpdatedAt.AddTicks(ticksOffset.Get);

        // Seed a local transaction with TransactionId=Guid.Empty, ExternalId set, SyncStatus=Synced
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Transaction.Add(new TransactionDTO
            {
                TransactionId = Guid.Empty,
                UserId = userId,
                Description = "Original local",
                Date = DateTime.UtcNow,
                Amount = 100m,
                ExternalId = extId,
                SyncStatus = TransactionSyncStatus.Synced,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = localUpdatedAt,
            });
            await ctx.SaveChangesAsync();
        }

        // Build pulled transaction with TransactionId=Guid.Empty, same ExternalId, newer UpdatedAt
        var pulledTransaction = new TransactionDTO
        {
            TransactionId = Guid.Empty,
            UserId = userId,
            Description = "Updated from server via ExternalId",
            Date = DateTime.UtcNow,
            Amount = 250m,
            ExternalId = extId,
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

        // Verify: the local record was updated (matched by ExternalId fallback)
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Transaction
                .FirstOrDefaultAsync(t => t.ExternalId == extId);

            return record is not null
                && record.Description == "Updated from server via ExternalId"
                && record.Amount == 250m
                && record.UpdatedAt == pulledUpdatedAt;
        }
    }

    /// <summary>
    /// For any transaction with TransactionId == Guid.Empty, a valid ExternalId,
    /// and SyncStatus == Synced, GetPendingPushAsync SHALL NOT return the record.
    /// Empty TransactionId alone must not trigger a spurious push.
    /// **Validates: Requirements 8.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> NoSpuriousPushForGuidEmptyWithValidExternalId(PositiveInt externalId, int userId)
    {
        if (userId <= 0) return true; // skip invalid userId

        var dbName = $"BackCompat_NoSpuriousPush_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var extId = externalId.Get;

        // Seed a local transaction with TransactionId=Guid.Empty, valid ExternalId, SyncStatus=Synced
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Transaction.Add(new TransactionDTO
            {
                TransactionId = Guid.Empty,
                UserId = userId,
                Description = "Legacy synced transaction",
                Date = DateTime.UtcNow,
                Amount = 75m,
                ExternalId = extId,
                SyncStatus = TransactionSyncStatus.Synced,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        var transactionRepo = new TransactionRepo(factory);

        // Verify: GetPendingPushAsync does NOT return this record
        var pending = await transactionRepo.GetPendingPushAsync(userId);

        return !pending.Any(t => t.ExternalId == extId);
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
