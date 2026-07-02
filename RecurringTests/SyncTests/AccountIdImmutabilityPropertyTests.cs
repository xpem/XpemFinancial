using ApiRepo;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using NSubstitute;
using Repo;
using Service.Account;
using Service.Transaction;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 8: AccountId Immutability After Assignment
/// Validates: Requirements 10.5
/// </summary>
[Trait("Feature", "account-guid-sync")]
[Trait("Property", "8")]
public class AccountIdImmutabilityPropertyTests
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
    /// For any account with a non-empty AccountId, calling UpdateAsync with a DIFFERENT
    /// AccountId SHALL NOT change the stored AccountId — it remains the original value.
    /// **Validates: Requirements 10.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> UpdateAsync_DoesNotChangeNonEmptyAccountId(
        NonEmptyString name, NonEmptyString newName, PositiveInt userId)
    {
        var dbName = $"AccountIdImmutability_Different_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var accountRepo = new AccountRepo(factory);

        var transactionService = Substitute.For<ITransactionService>();
        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        // Seed an account with a non-empty AccountId
        var originalAccountId = Guid.NewGuid();
        var account = new AccountDTO
        {
            Name = name.Get,
            Type = AccountType.Checking,
            IncludeInGeneralBalance = true,
            IsActive = true,
            UserId = userId.Get,
            AccountId = originalAccountId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await accountRepo.Add(account);

        // Create a modified copy with a DIFFERENT AccountId
        var differentAccountId = Guid.NewGuid();
        account.AccountId = differentAccountId;
        account.Name = newName.Get;

        // Call UpdateAsync with isOnline: false to avoid push
        await service.UpdateAsync(account, isOnline: false);

        // Verify stored AccountId equals the ORIGINAL value
        using var ctx = await factory.CreateDbContextAsync();
        var stored = await ctx.Account.FirstOrDefaultAsync(a => a.Id == account.Id);

        return stored is not null && stored.AccountId == originalAccountId;
    }

    /// <summary>
    /// For any account with a non-empty AccountId, calling UpdateAsync with the SAME
    /// AccountId SHALL preserve it unchanged.
    /// **Validates: Requirements 10.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> UpdateAsync_WithSameAccountId_Preserves(
        NonEmptyString name, NonEmptyString newName, PositiveInt userId)
    {
        var dbName = $"AccountIdImmutability_Same_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var accountRepo = new AccountRepo(factory);

        var transactionService = Substitute.For<ITransactionService>();
        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        // Seed an account with a non-empty AccountId
        var originalAccountId = Guid.NewGuid();
        var account = new AccountDTO
        {
            Name = name.Get,
            Type = AccountType.Checking,
            IncludeInGeneralBalance = true,
            IsActive = true,
            UserId = userId.Get,
            AccountId = originalAccountId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await accountRepo.Add(account);

        // Call UpdateAsync with the same AccountId (only changing other fields)
        account.Name = newName.Get;

        await service.UpdateAsync(account, isOnline: false);

        // Verify stored AccountId is preserved
        using var ctx = await factory.CreateDbContextAsync();
        var stored = await ctx.Account.FirstOrDefaultAsync(a => a.Id == account.Id);

        return stored is not null && stored.AccountId == originalAccountId;
    }

    /// <summary>
    /// For any account with a non-empty AccountId, calling UpdateAsync N times
    /// (each time attempting to set a different AccountId) SHALL never change the
    /// stored AccountId from the original value.
    /// **Validates: Requirements 10.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> MultipleUpdates_AccountIdNeverChanges(
        NonEmptyString name, PositiveInt userId, PositiveInt updateCount)
    {
        // Clamp update count to a reasonable range (1-10)
        var n = (updateCount.Get % 10) + 1;

        var dbName = $"AccountIdImmutability_Multi_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var accountRepo = new AccountRepo(factory);

        var transactionService = Substitute.For<ITransactionService>();
        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        // Seed an account with a non-empty AccountId
        var originalAccountId = Guid.NewGuid();
        var account = new AccountDTO
        {
            Name = name.Get,
            Type = AccountType.Checking,
            IncludeInGeneralBalance = true,
            IsActive = true,
            UserId = userId.Get,
            AccountId = originalAccountId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        await accountRepo.Add(account);

        // Perform N updates, each attempting to change the AccountId
        for (int i = 0; i < n; i++)
        {
            account.AccountId = Guid.NewGuid(); // attempt to overwrite
            account.Name = $"Updated_{i}";
            await service.UpdateAsync(account, isOnline: false);
        }

        // Verify stored AccountId equals the original
        using var ctx = await factory.CreateDbContextAsync();
        var stored = await ctx.Account.FirstOrDefaultAsync(a => a.Id == account.Id);

        return stored is not null && stored.AccountId == originalAccountId;
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
