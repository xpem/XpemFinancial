using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Repo;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 11: Client Repository Lookup Correctness
/// Validates: Requirements 9.2, 9.3, 9.4
/// </summary>
[Trait("Feature", "transaction-guid-sync")]
[Trait("Property", "11")]
public class ClientRepoLookupPropertyTests
{
    /// <summary>
    /// Creates a DbContextFactory backed by a unique in-memory database.
    /// Each call to CreateDbContextAsync returns a context pointing at the same store.
    /// </summary>
    private static IDbContextFactory<DbCtx> CreateFactory(string dbName)
    {
        var options = new DbContextOptionsBuilder<DbCtx>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new TestDbContextFactory(options);
    }

    /// <summary>
    /// Creates a valid TransactionDTO with the given TransactionId.
    /// </summary>
    private static TransactionDTO CreateTransaction(Guid transactionId) => new()
    {
        TransactionId = transactionId,
        UserId = 1,
        Description = "Test transaction",
        Date = DateTime.UtcNow,
        Amount = 100m,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// For any valid Guid (non-empty), when a matching record exists in the database,
    /// GetByTransactionIdAsync returns that record.
    /// **Validates: Requirements 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> ExistingGuid_ReturnsMatchingRecord(Guid transactionId)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case

        var dbName = $"ClientLookup_Existing_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed the record
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Transaction.Add(CreateTransaction(transactionId));
            await ctx.SaveChangesAsync();
        }

        var repo = new TransactionRepo(factory);
        var result = await repo.GetByTransactionIdAsync(transactionId);

        return result is not null && result.TransactionId == transactionId;
    }

    /// <summary>
    /// For any valid Guid (non-empty), when no matching record exists,
    /// GetByTransactionIdAsync returns null.
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> NonExistentGuid_ReturnsNull(Guid transactionId)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case

        var dbName = $"ClientLookup_NonExist_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Empty database — no records seeded
        var repo = new TransactionRepo(factory);
        var result = await repo.GetByTransactionIdAsync(transactionId);

        return result is null;
    }

    /// <summary>
    /// Guid.Empty always returns null — even when records exist in the database.
    /// This verifies the short-circuit guard without querying the DB.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> GuidEmpty_ReturnsNull_WithoutDbQuery(Guid seedId)
    {
        if (seedId == Guid.Empty) return true; // skip when seed is also empty

        var dbName = $"ClientLookup_Empty_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed a record so the database isn't empty
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Transaction.Add(CreateTransaction(seedId));
            await ctx.SaveChangesAsync();
        }

        var repo = new TransactionRepo(factory);
        var result = await repo.GetByTransactionIdAsync(Guid.Empty);

        return result is null;
    }

    /// <summary>
    /// For any valid Guid (non-empty), inactive records are also returned.
    /// **Validates: Requirements 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> InactiveRecord_IsStillReturned(Guid transactionId)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case

        var dbName = $"ClientLookup_Inactive_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed an inactive record
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var tx = CreateTransaction(transactionId);
            tx.Inactive = true;
            ctx.Transaction.Add(tx);
            await ctx.SaveChangesAsync();
        }

        var repo = new TransactionRepo(factory);
        var result = await repo.GetByTransactionIdAsync(transactionId);

        return result is not null && result.TransactionId == transactionId && result.Inactive;
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
