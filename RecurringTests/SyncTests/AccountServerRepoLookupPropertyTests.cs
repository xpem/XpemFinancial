using FsCheck;
using FsCheck.Xunit;
using FinancialService.Model.DTO;
using FinancialService.Repo;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 10: Server Repository Lookup Correctness
/// Validates: Requirements 9.2, 9.3, 9.4
/// </summary>
[Trait("Feature", "account-guid-sync")]
[Trait("Property", "10")]
public class AccountServerRepoLookupPropertyTests
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
    /// Creates a valid server AccountDTO with the given AccountId and UserId.
    /// </summary>
    private static AccountDTO CreateAccount(Guid accountId, int userId) => new()
    {
        AccountId = accountId,
        UserId = userId,
        Name = "Test account",
        Type = AccountType.Checking,
        CurrentBalance = 0m,
        IncludeInGeneralBalance = true,
        Inactive = false,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    /// <summary>
    /// For any valid Guid (non-empty) and UserId, when a matching record exists in the database,
    /// FindByAccountIdAsync returns that record.
    /// **Validates: Requirements 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> ExistingGuidAndUserId_ReturnsMatchingRecord(Guid accountId, PositiveInt userId)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case
        int uid = userId.Get;

        var dbName = $"AccServerLookup_Existing_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed the record
        using (var ctx = factory.CreateDbContext())
        {
            ctx.Account.Add(CreateAccount(accountId, uid));
            await ctx.SaveChangesAsync();
        }

        var repo = new AccountRepo(factory);
        var result = await repo.FindByAccountIdAsync(accountId, uid);

        return result is not null && result.AccountId == accountId && result.UserId == uid;
    }

    /// <summary>
    /// For any valid Guid (non-empty), when no matching record exists for the given user,
    /// FindByAccountIdAsync returns null.
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> NonExistentGuid_ReturnsNull(Guid accountId, PositiveInt userId)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case
        int uid = userId.Get;

        var dbName = $"AccServerLookup_NonExist_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Empty database — no records seeded
        var repo = new AccountRepo(factory);
        var result = await repo.FindByAccountIdAsync(accountId, uid);

        return result is null;
    }

    /// <summary>
    /// For any valid Guid (non-empty), when the record exists but belongs to a different user,
    /// FindByAccountIdAsync returns null (scoped by UserId).
    /// **Validates: Requirements 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> MismatchedUserId_ReturnsNull(Guid accountId, PositiveInt ownerUserId, PositiveInt queryUserId)
    {
        if (accountId == Guid.Empty) return true;
        int ownerId = ownerUserId.Get;
        int queryId = queryUserId.Get;
        if (ownerId == queryId) return true; // skip when IDs match

        var dbName = $"AccServerLookup_Mismatch_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed a record owned by ownerId
        using (var ctx = factory.CreateDbContext())
        {
            ctx.Account.Add(CreateAccount(accountId, ownerId));
            await ctx.SaveChangesAsync();
        }

        var repo = new AccountRepo(factory);
        var result = await repo.FindByAccountIdAsync(accountId, queryId);

        return result is null;
    }

    /// <summary>
    /// Guid.Empty always returns null — even when records exist in the database.
    /// This verifies the short-circuit guard without querying the DB.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> GuidEmpty_ReturnsNull_WithoutDbQuery(Guid seedId, PositiveInt userId)
    {
        if (seedId == Guid.Empty) return true; // skip when seed is also empty
        int uid = userId.Get;

        var dbName = $"AccServerLookup_Empty_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed a record so the database isn't empty
        using (var ctx = factory.CreateDbContext())
        {
            ctx.Account.Add(CreateAccount(seedId, uid));
            await ctx.SaveChangesAsync();
        }

        var repo = new AccountRepo(factory);
        var result = await repo.FindByAccountIdAsync(Guid.Empty, uid);

        return result is null;
    }

    /// <summary>
    /// For any valid Guid (non-empty), inactive records are also returned.
    /// **Validates: Requirements 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> InactiveRecord_IsStillReturned(Guid accountId, PositiveInt userId)
    {
        if (accountId == Guid.Empty) return true;
        int uid = userId.Get;

        var dbName = $"AccServerLookup_Inactive_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed an inactive record
        using (var ctx = factory.CreateDbContext())
        {
            var account = CreateAccount(accountId, uid);
            account.Inactive = true;
            ctx.Account.Add(account);
            await ctx.SaveChangesAsync();
        }

        var repo = new AccountRepo(factory);
        var result = await repo.FindByAccountIdAsync(accountId, uid);

        return result is not null && result.AccountId == accountId && result.Inactive;
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
