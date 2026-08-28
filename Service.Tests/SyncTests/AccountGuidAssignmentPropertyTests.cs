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
/// Property 1: Guid Assignment on Creation
/// Validates: Requirements 1.2, 10.1, 10.2, 10.3, 10.4
/// </summary>
[Trait("Feature", "account-guid-sync")]
[Trait("Property", "1")]
public class AccountGuidAssignmentPropertyTests
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
    /// For any account created via CreateAsync with AccountId == Guid.Empty (the default),
    /// the persisted record SHALL have an AccountId != Guid.Empty.
    /// **Validates: Requirements 1.2, 10.1, 10.2, 10.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> CreateAsync_WithEmptyGuid_AssignsNonEmptyGuid(
        NonEmptyString name, PositiveInt userId)
    {
        var dbName = $"AccountGuidAssign_Create_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var accountRepo = new AccountRepo(factory);

        var transactionService = Substitute.For<ITransactionService>();
        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        // CreateAsync assigns Guid.NewGuid() internally; pass isOn: false to avoid triggering push
        await service.CreateAsync(
            userId: userId.Get,
            name: name.Get,
            type: AccountType.Checking,
            includeInGeneralBalance: true,
            isOn: false,
            initialBalance: 0);

        // Query the database to verify the stored record
        using var ctx = await factory.CreateDbContextAsync();
        var stored = await ctx.Account.FirstOrDefaultAsync();

        return stored is not null && stored.AccountId != Guid.Empty;
    }

    /// <summary>
    /// For any user with no accounts, EnsureDefaultAccountAsync SHALL create an account
    /// with AccountId != Guid.Empty.
    /// **Validates: Requirements 10.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> EnsureDefaultAccountAsync_AssignsNonEmptyGuid(PositiveInt userId)
    {
        var dbName = $"AccountGuidAssign_Default_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var accountRepo = new AccountRepo(factory);

        var transactionService = Substitute.For<ITransactionService>();
        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        await service.EnsureDefaultAccountAsync(userId.Get);

        // Query the database to verify the stored record
        using var ctx = await factory.CreateDbContextAsync();
        var stored = await ctx.Account.FirstOrDefaultAsync();

        return stored is not null && stored.AccountId != Guid.Empty;
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
