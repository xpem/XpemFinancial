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
/// Property 7: Backward Compatibility — Guid.Empty Falls Back to ExternalId
/// Validates: Requirements 7.1, 7.5, 7.6, 5.6
/// </summary>
[Trait("Feature", "account-guid-sync")]
[Trait("Property", "7")]
public class AccountBackwardCompatibilityPropertyTests
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
    /// For any pulled account with a null/empty AccountId, the client SHALL match
    /// by ExternalId only, preserving current behavior.
    /// Seed a local account with AccountId=Guid.Empty and ExternalId=N,
    /// pull a response with Id=N and null AccountId → local record is matched and updated.
    /// **Validates: Requirements 7.1, 5.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PullWithNullAccountId_UsesExternalIdMatching(PositiveInt externalId, PositiveInt ticksOffset)
    {
        var externalIdValue = externalId.Get;
        var dbName = $"AccBackCompat_PullExternalId_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var localUpdatedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var pulledUpdatedAt = localUpdatedAt.AddTicks(ticksOffset.Get);

        // Seed local account with Guid.Empty (legacy record) and a valid ExternalId
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Account.Add(new AccountDTO
            {
                AccountId = Guid.Empty,
                ExternalId = externalIdValue,
                Name = "Original local",
                Type = AccountType.Checking,
                IncludeInGeneralBalance = true,
                IsActive = true,
                UserId = 1,
                CurrentBalance = 100,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = localUpdatedAt,
            });
            await ctx.SaveChangesAsync();
        }

        // Build pulled account with null AccountId (backward-compatible) and newer UpdatedAt
        var pulledAccounts = new List<AccountApiRes>
        {
            new()
            {
                Id = externalIdValue,
                AccountId = null, // null AccountId → should fall back to ExternalId matching
                Name = "Updated from server",
                Type = AccountType.Savings,
                IncludeInGeneralBalance = false,
                Inactive = false,
                CurrentBalance = 200,
                UpdatedAt = pulledUpdatedAt,
            }
        };

        // Setup mocks
        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var transactionService = Substitute.For<ITransactionService>();

        accountApiRepo.GetAccountsAsync(Arg.Any<DateTime>())
            .Returns(pulledAccounts);

        syncCursorRepo.GetAsync(SyncCursorKeys.Account).Returns(DateTime.MinValue);

        var accountRepo = new AccountRepo(factory);
        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        await service.PullAsync(1);

        // Verify: local record was matched by ExternalId and updated
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Account
                .FirstOrDefaultAsync(a => a.ExternalId == externalIdValue);

            return record is not null
                && record.Name == "Updated from server"
                && record.Type == AccountType.Savings
                && record.IncludeInGeneralBalance == false
                && record.UpdatedAt == pulledUpdatedAt;
        }
    }

    /// <summary>
    /// For any account with AccountId=Guid.Empty and a non-null ExternalId,
    /// PushPendingAsync SHALL call PutAccountAsync (not PostAccountAsync),
    /// following the existing POST/PUT decision logic.
    /// **Validates: Requirements 7.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PushWithGuidEmpty_AndExternalId_UsesPut(PositiveInt externalId, NonEmptyString name)
    {
        var externalIdValue = externalId.Get;
        var dbName = $"AccBackCompat_PushPut_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed account with AccountId=Guid.Empty and a non-null ExternalId
        // UpdatedAt must be recent enough to be selected as pending
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Account.Add(new AccountDTO
            {
                AccountId = Guid.Empty,
                ExternalId = externalIdValue,
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

        // Return a cursor in the past so the record is selected as pending
        syncCursorRepo.GetAsync(SyncCursorKeys.Account).Returns(DateTime.MinValue);

        // PutAccountAsync should be called for Guid.Empty + ExternalId != null
        accountApiRepo.PutAccountAsync(externalIdValue, Arg.Any<AccountReq>())
            .Returns(new AccountApiRes
            {
                Id = externalIdValue,
                Name = name.Get,
                Type = AccountType.Checking,
                AccountId = null,
            });

        var accountRepo = new AccountRepo(factory);
        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        await service.PushPendingAsync(1);

        // Verify: PutAccountAsync was called, PostAccountAsync was NOT called
        await accountApiRepo.Received(1).PutAccountAsync(externalIdValue, Arg.Any<AccountReq>());
        await accountApiRepo.DidNotReceive().PostAccountAsync(Arg.Any<AccountReq>());

        return true;
    }

    /// <summary>
    /// For any account with AccountId=Guid.Empty and null ExternalId,
    /// when PostAccountAsync returns a response with a non-empty AccountId and a valid Id,
    /// the local record SHALL persist both ExternalId and AccountId.
    /// **Validates: Requirements 7.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PushWithGuidEmpty_AndNullExternalId_PersistsBothIdsOnSuccess(
        Guid returnedAccountId, PositiveInt returnedExternalId, NonEmptyString name)
    {
        if (returnedAccountId == Guid.Empty) return true; // skip trivial case

        var returnedExternalIdValue = returnedExternalId.Get;
        var dbName = $"AccBackCompat_PushPost_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Seed account with AccountId=Guid.Empty and ExternalId=null (never pushed before)
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Account.Add(new AccountDTO
            {
                AccountId = Guid.Empty,
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

        syncCursorRepo.GetAsync(SyncCursorKeys.Account).Returns(DateTime.MinValue);

        // PostAccountAsync returns a response with both a valid Id and a non-empty AccountId
        accountApiRepo.PostAccountAsync(Arg.Any<AccountReq>())
            .Returns(new AccountApiRes
            {
                Id = returnedExternalIdValue,
                Name = name.Get,
                Type = AccountType.Checking,
                AccountId = returnedAccountId,
            });

        var accountRepo = new AccountRepo(factory);
        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        await service.PushPendingAsync(1);

        // Verify: local record now has both ExternalId and AccountId persisted
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Account.FirstOrDefaultAsync(a => a.UserId == 1);

            return record is not null
                && record.ExternalId == returnedExternalIdValue
                && record.AccountId == returnedAccountId;
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
