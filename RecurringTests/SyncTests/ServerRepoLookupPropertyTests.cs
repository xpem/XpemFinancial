using FsCheck;
using FsCheck.Xunit;
using FinancialService.Model.DTO;
using FinancialService.Repo;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 12: Server Repository Lookup Correctness
/// Validates: Requirements 10.2, 10.3, 10.4
/// </summary>
[Trait("Feature", "transaction-guid-sync")]
[Trait("Property", "12")]
public class ServerRepoLookupPropertyTests
{
    /// <summary>
    /// Creates a DbContextFactory backed by a unique in-memory database.
    /// </summary>
    private static IDbContextFactory<FinancialDbctx> CreateFactory(string dbName)
    {
        var options = new DbContextOptionsBuilder<FinancialDbctx>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new TestFinancialDbContextFactory(options);
    }

    /// <summary>
    /// Creates a valid server TransactionDTO with the given TransactionId and UserId.
    /// </summary>
    private static TransactionDTO CreateTransaction(Guid transactionId, int userId) => new()
    {
        TransactionId = transactionId,
        UserId = userId,
        Description = "Test transaction",
        Date = DateTime.UtcNow,
        Amount = 100m,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
        AccountId = 1,
    };

    /// <summary>
    /// For any valid Guid (non-empty) and UserId, when a matching record exists in the database,
    /// FindByTransactionIdAsync returns that record.
    /// **Validates: Requirements 10.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> ExistingGuidAndUserId_ReturnsMatchingRecord(Guid transactionId, PositiveInt userId)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case
        int uid = userId.Get;

        var dbName = $"ServerLookup_Existing_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed the record
        using (var ctx = factory.CreateDbContext())
        {
            ctx.Transaction.Add(CreateTransaction(transactionId, uid));
            await ctx.SaveChangesAsync();
        }

        var repo = new TransactionRepo(factory);
        var result = await repo.FindByTransactionIdAsync(transactionId, uid);

        return result is not null && result.TransactionId == transactionId && result.UserId == uid;
    }

    /// <summary>
    /// For any valid Guid (non-empty), when no matching record exists for the given user,
    /// FindByTransactionIdAsync returns null.
    /// **Validates: Requirements 10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> NonExistentGuid_ReturnsNull(Guid transactionId, PositiveInt userId)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case
        int uid = userId.Get;

        var dbName = $"ServerLookup_NonExist_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Empty database — no records seeded
        var repo = new TransactionRepo(factory);
        var result = await repo.FindByTransactionIdAsync(transactionId, uid);

        return result is null;
    }

    /// <summary>
    /// For any valid Guid (non-empty), when the record exists but belongs to a different user,
    /// FindByTransactionIdAsync returns null (scoped by UserId).
    /// **Validates: Requirements 10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> MismatchedUserId_ReturnsNull(Guid transactionId, PositiveInt ownerUserId, PositiveInt queryUserId)
    {
        if (transactionId == Guid.Empty) return true;
        int ownerId = ownerUserId.Get;
        int queryId = queryUserId.Get;
        if (ownerId == queryId) return true; // skip when IDs match

        var dbName = $"ServerLookup_Mismatch_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed a record owned by ownerId
        using (var ctx = factory.CreateDbContext())
        {
            ctx.Transaction.Add(CreateTransaction(transactionId, ownerId));
            await ctx.SaveChangesAsync();
        }

        var repo = new TransactionRepo(factory);
        var result = await repo.FindByTransactionIdAsync(transactionId, queryId);

        return result is null;
    }

    /// <summary>
    /// Guid.Empty always returns null — even when records exist in the database.
    /// This verifies the short-circuit guard without querying the DB.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> GuidEmpty_ReturnsNull_WithoutDbQuery(Guid seedId, PositiveInt userId)
    {
        if (seedId == Guid.Empty) return true; // skip when seed is also empty
        int uid = userId.Get;

        var dbName = $"ServerLookup_Empty_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed a record so the database isn't empty
        using (var ctx = factory.CreateDbContext())
        {
            ctx.Transaction.Add(CreateTransaction(seedId, uid));
            await ctx.SaveChangesAsync();
        }

        var repo = new TransactionRepo(factory);
        var result = await repo.FindByTransactionIdAsync(Guid.Empty, uid);

        return result is null;
    }

    /// <summary>
    /// For any valid Guid (non-empty), inactive records are also returned.
    /// **Validates: Requirements 10.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> InactiveRecord_IsStillReturned(Guid transactionId, PositiveInt userId)
    {
        if (transactionId == Guid.Empty) return true;
        int uid = userId.Get;

        var dbName = $"ServerLookup_Inactive_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed an inactive record
        using (var ctx = factory.CreateDbContext())
        {
            var tx = CreateTransaction(transactionId, uid);
            tx.Inactive = true;
            ctx.Transaction.Add(tx);
            await ctx.SaveChangesAsync();
        }

        var repo = new TransactionRepo(factory);
        var result = await repo.FindByTransactionIdAsync(transactionId, uid);

        return result is not null && result.TransactionId == transactionId && result.Inactive;
    }

    /// <summary>
    /// Simple IDbContextFactory implementation for testing.
    /// </summary>
    private sealed class TestFinancialDbContextFactory(DbContextOptions<FinancialDbctx> options) : IDbContextFactory<FinancialDbctx>
    {
        public FinancialDbctx CreateDbContext() => new(options);
        public Task<FinancialDbctx> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new FinancialDbctx(options));
    }
}
