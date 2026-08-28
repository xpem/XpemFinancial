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
/// Property 6: Pull Inserts New Records
/// Validates: Requirements 5.5
/// </summary>
[Trait("Feature", "transaction-guid-sync")]
[Trait("Property", "6")]
public class PullInsertsNewRecordsPropertyTests
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
    /// When no local record exists matching the pulled TransactionId or ExternalId,
    /// ApplyFromApiAsync SHALL insert a new record with SyncStatus = Synced,
    /// preserving the pulled TransactionId and ExternalId.
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PullWithNoMatch_InsertsNewRecordAsSynced(Guid transactionId, int externalId)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case
        if (externalId <= 0) return true; // skip invalid ExternalId

        var dbName = $"PullInsert_NewRecord_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Empty database — no records seeded, so no match by TransactionId or ExternalId

        // Setup mocks for dependencies not under test
        var transactionApiRepo = Substitute.For<ITransactionApiRepo>();
        var categoryRepo = Substitute.For<ICategoryRepo>();
        var accountRepo = Substitute.For<IAccountRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        var transactionRepo = new TransactionRepo(factory);
        var service = new TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        // Build a pulled transaction with random TransactionId and ExternalId
        var pulledTransaction = new TransactionDTO
        {
            TransactionId = transactionId,
            ExternalId = externalId,
            UserId = 1,
            Description = "Pulled from server",
            Date = DateTime.UtcNow,
            Amount = 200m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await service.ApplyFromApiAsync(pulledTransaction);

        // Verify: the record was inserted with correct TransactionId, ExternalId, and SyncStatus=Synced
        using var ctx = await factory.CreateDbContextAsync();
        var inserted = await ctx.Transaction
            .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

        return inserted is not null
            && inserted.TransactionId == transactionId
            && inserted.ExternalId == externalId
            && inserted.SyncStatus == TransactionSyncStatus.Synced;
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
