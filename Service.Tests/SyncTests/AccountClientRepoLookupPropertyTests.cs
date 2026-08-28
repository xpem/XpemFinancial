using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Repo;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 9: Client Repository Lookup Correctness
/// Validates: Requirements 8.2, 8.3, 8.4
/// </summary>
[Trait("Feature", "account-guid-sync")]
[Trait("Property", "9")]
public class AccountClientRepoLookupPropertyTests
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
    /// Creates a valid AccountDTO with the given AccountId.
    /// </summary>
    private static AccountDTO CreateAccount(Guid accountId) => new()
    {
        AccountId = accountId,
        UserId = 1,
        Name = "Test account",
        Type = AccountType.Checking,
        IncludeInGeneralBalance = true,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// For any valid Guid (non-empty), when a matching record exists in the database,
    /// GetByAccountIdAsync returns that record.
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> ExistingGuid_ReturnsMatchingRecord(Guid accountId)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case

        var dbName = $"AccountClientLookup_Existing_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed the record
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Account.Add(CreateAccount(accountId));
            await ctx.SaveChangesAsync();
        }

        var repo = new AccountRepo(factory);
        var result = await repo.GetByAccountIdAsync(accountId);

        return result is not null && result.AccountId == accountId;
    }

    /// <summary>
    /// For any valid Guid (non-empty), when no matching record exists,
    /// GetByAccountIdAsync returns null.
    /// **Validates: Requirements 8.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> NonExistentGuid_ReturnsNull(Guid accountId)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case

        var dbName = $"AccountClientLookup_NonExist_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Empty database — no records seeded
        var repo = new AccountRepo(factory);
        var result = await repo.GetByAccountIdAsync(accountId);

        return result is null;
    }

    /// <summary>
    /// Guid.Empty always returns null — even when records exist in the database.
    /// This verifies the short-circuit guard without querying the DB.
    /// **Validates: Requirements 8.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> GuidEmpty_ReturnsNull_WithoutDbQuery(Guid seedId)
    {
        if (seedId == Guid.Empty) return true; // skip when seed is also empty

        var dbName = $"AccountClientLookup_Empty_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed a record so the database isn't empty
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Account.Add(CreateAccount(seedId));
            await ctx.SaveChangesAsync();
        }

        var repo = new AccountRepo(factory);
        var result = await repo.GetByAccountIdAsync(Guid.Empty);

        return result is null;
    }

    /// <summary>
    /// For any valid Guid (non-empty), inactive records are also returned.
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> InactiveRecord_IsStillReturned(Guid accountId)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case

        var dbName = $"AccountClientLookup_Inactive_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed an inactive record
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var account = CreateAccount(accountId);
            account.Inactive = true;
            ctx.Account.Add(account);
            await ctx.SaveChangesAsync();
        }

        var repo = new AccountRepo(factory);
        var result = await repo.GetByAccountIdAsync(accountId);

        return result is not null && result.AccountId == accountId && result.Inactive;
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
