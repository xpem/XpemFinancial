using ApiRepo;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Model.Req;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Repo;
using Service.Account;
using Service.Transaction;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 4: Push Failure Leaves Record Unchanged
/// Validates: Requirements 3.3, 6.4
/// </summary>
[Trait("Feature", "account-guid-sync")]
[Trait("Property", "4")]
public class AccountPushFailurePropertyTests
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
    /// For any account with a non-empty AccountId and null ExternalId,
    /// when PostAccountAsync throws an exception, PushPendingAsync catches it
    /// and the local record SHALL retain ExternalId == null.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PushFailure_LeavesExternalIdUnchanged(Guid accountId, NonEmptyString name)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case

        var dbName = $"AccPushFailure_ExternalId_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed an account with valid AccountId and null ExternalId (pending push)
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Account.Add(new AccountDTO
            {
                AccountId = accountId,
                ExternalId = null,
                Name = name.Get,
                Type = AccountType.Checking,
                IncludeInGeneralBalance = true,
                IsActive = true,
                UserId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // Setup mocks
        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var transactionService = Substitute.For<ITransactionService>();

        // PostAccountAsync throws — simulating server unavailable / network error
        accountApiRepo.PostAccountAsync(Arg.Any<AccountReq>())
            .Throws(new Exception("Server unavailable"));

        // Return DateTime.MinValue so all records are considered pending
        syncCursorRepo.GetAsync(SyncCursorKeys.Account)
            .Returns(DateTime.MinValue);

        var accountRepo = new AccountRepo(factory);
        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        await service.PushPendingAsync(1);

        // Verify: ExternalId is still null after failed push
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Account
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            return record is not null && record.ExternalId is null;
        }
    }

    /// <summary>
    /// For any account with a non-empty AccountId, when PostAccountAsync throws,
    /// the local record SHALL retain its original AccountId unchanged.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PushFailure_LeavesAccountIdUnchanged(Guid accountId, NonEmptyString name)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case

        var dbName = $"AccPushFailure_AccountId_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed an account with valid AccountId and null ExternalId
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Account.Add(new AccountDTO
            {
                AccountId = accountId,
                ExternalId = null,
                Name = name.Get,
                Type = AccountType.Savings,
                IncludeInGeneralBalance = false,
                IsActive = true,
                UserId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // Setup mocks
        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var transactionService = Substitute.For<ITransactionService>();

        // PostAccountAsync throws — simulating network error
        accountApiRepo.PostAccountAsync(Arg.Any<AccountReq>())
            .Throws(new Exception("Network error"));

        syncCursorRepo.GetAsync(SyncCursorKeys.Account)
            .Returns(DateTime.MinValue);

        var accountRepo = new AccountRepo(factory);
        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        await service.PushPendingAsync(1);

        // Verify: AccountId is still the original value
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Account
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            return record is not null && record.AccountId == accountId;
        }
    }

    /// <summary>
    /// For any account pending push, when PostAccountAsync throws,
    /// the record SHALL continue to match the pending-push selection criteria
    /// (i.e., it should still appear in GetPendingPushAsync results).
    /// **Validates: Requirements 6.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PushFailure_RecordStillMatchesPendingCriteria(Guid accountId, NonEmptyString name)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case

        var dbName = $"AccPushFailure_Pending_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed an account with valid AccountId and null ExternalId (pending push)
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Account.Add(new AccountDTO
            {
                AccountId = accountId,
                ExternalId = null,
                Name = name.Get,
                Type = AccountType.Checking,
                IncludeInGeneralBalance = true,
                IsActive = true,
                UserId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // Setup mocks
        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var transactionService = Substitute.For<ITransactionService>();

        // PostAccountAsync throws — simulating server failure
        accountApiRepo.PostAccountAsync(Arg.Any<AccountReq>())
            .Throws(new Exception("Server unavailable"));

        syncCursorRepo.GetAsync(SyncCursorKeys.Account)
            .Returns(DateTime.MinValue);

        var accountRepo = new AccountRepo(factory);
        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        // Push fails for the record
        await service.PushPendingAsync(1);

        // Verify: the record still matches pending-push criteria
        var pendingAfterFailure = await accountRepo.GetPendingPushAsync(1, DateTime.MinValue);

        return pendingAfterFailure.Any(a => a.AccountId == accountId);
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
