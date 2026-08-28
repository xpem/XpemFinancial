using ApiRepo;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Model.Req;
using Model.Resp.Api;
using NSubstitute;
using Repo;
using Service.Account;
using Service.Transaction;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 3: Push Round-Trip Preserves Identity
/// Validates: Requirements 3.1, 3.2, 6.3
/// </summary>
[Trait("Feature", "account-guid-sync")]
[Trait("Property", "3")]
public class AccountPushRoundTripPropertyTests
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
    /// when PushPendingAsync is called, the AccountReq sent to PostAccountAsync
    /// SHALL contain the local AccountId.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PushIncludesAccountIdInRequest(Guid accountId, NonEmptyString name)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case

        var dbName = $"AccPushRoundTrip_IncludesId_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed an account with a valid AccountId and null ExternalId (pending push)
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
                CurrentBalance = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // Setup mocks
        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var transactionService = Substitute.For<ITransactionService>();

        // Return DateTime.MinValue for cursor (all records are pending)
        syncCursorRepo.GetAsync(SyncCursorKeys.Account).Returns(DateTime.MinValue);

        // Capture the request sent to PostAccountAsync and return a valid response
        AccountReq? capturedReq = null;
        accountApiRepo.PostAccountAsync(Arg.Do<AccountReq>(r => capturedReq = r))
            .Returns(new AccountApiRes
            {
                Id = 42,
                Name = name.Get,
                Type = AccountType.Checking,
                AccountId = accountId,
            });

        var accountRepo = new AccountRepo(factory);
        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        await service.PushPendingAsync(1);

        // Verify: the captured request contains the correct AccountId
        return capturedReq is not null && capturedReq.AccountId == accountId;
    }

    /// <summary>
    /// For any account with a non-empty AccountId, when the server responds
    /// with a valid Id (> 0), the local record SHALL have ExternalId set to that value.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> SuccessfulPushPersistsExternalId(Guid accountId, PositiveInt serverId, NonEmptyString name)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case

        var serverIdValue = serverId.Get;
        var dbName = $"AccPushRoundTrip_ExternalId_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed an account with valid AccountId and null ExternalId
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
                CurrentBalance = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // Setup mocks
        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var transactionService = Substitute.For<ITransactionService>();

        // Return DateTime.MinValue for cursor
        syncCursorRepo.GetAsync(SyncCursorKeys.Account).Returns(DateTime.MinValue);

        // PostAccountAsync returns the serverId
        accountApiRepo.PostAccountAsync(Arg.Any<AccountReq>())
            .Returns(new AccountApiRes
            {
                Id = serverIdValue,
                Name = name.Get,
                Type = AccountType.Checking,
                AccountId = accountId,
            });

        var accountRepo = new AccountRepo(factory);
        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        await service.PushPendingAsync(1);

        // Verify: the local record now has ExternalId = serverId
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Account
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            return record is not null && record.ExternalId == serverIdValue;
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
