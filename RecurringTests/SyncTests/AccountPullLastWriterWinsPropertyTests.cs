using ApiRepo;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Model.Resp.Api;
using NSubstitute;
using Repo;
using Service.Account;
using Service.Transaction;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 5: Pull AccountId Matching with Last-Writer-Wins
/// Validates: Requirements 5.1, 5.2, 5.3, 5.4, 1.3, 7.3
/// </summary>
[Trait("Feature", "account-guid-sync")]
[Trait("Property", "5")]
public class AccountPullLastWriterWinsPropertyTests
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
    /// For any local account with a non-empty AccountId,
    /// when a pulled account with the same AccountId has UpdatedAt strictly greater
    /// than the local UpdatedAt, the local record SHALL be updated with the pulled data.
    /// **Validates: Requirements 5.1, 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PulledNewerUpdatedAt_UpdatesLocalRecord(Guid accountId, PositiveInt ticksOffset)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case

        var dbName = $"AccPullLWW_Newer_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var localUpdatedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        // Pulled UpdatedAt is strictly greater (add positive offset in ticks)
        var pulledUpdatedAt = localUpdatedAt.AddTicks(ticksOffset.Get);

        var userId = 1;
        var externalId = 42;

        // Seed local account
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Account.Add(new AccountDTO
            {
                AccountId = accountId,
                ExternalId = externalId,
                Name = "Original local",
                Type = AccountType.Checking,
                CurrentBalance = 100m,
                IncludeInGeneralBalance = true,
                IsActive = true,
                Inactive = false,
                UserId = userId,
                UpdatedAt = localUpdatedAt,
                CreatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // Build pulled account response with same AccountId but newer UpdatedAt
        var pulledAccounts = new List<AccountApiRes>
        {
            new()
            {
                Id = externalId,
                AccountId = accountId,
                Name = "Updated from server",
                Type = AccountType.Savings,
                CurrentBalance = 250m,
                IncludeInGeneralBalance = false,
                Inactive = true,
                UpdatedAt = pulledUpdatedAt,
            }
        };

        // Setup service with real repo, mocked API dependencies
        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var transactionService = Substitute.For<ITransactionService>();

        accountApiRepo.GetAccountsAsync(Arg.Any<DateTime>())
            .Returns(pulledAccounts);

        syncCursorRepo.GetAsync(SyncCursorKeys.Account).Returns(DateTime.MinValue);

        var accountRepo = new AccountRepo(factory);
        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        await service.PullAsync(userId);

        // Verify: local record was updated
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Account
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            return record is not null
                && record.Name == "Updated from server"
                && record.Type == AccountType.Savings
                && record.CurrentBalance == 250m
                && record.IncludeInGeneralBalance == false
                && record.Inactive == true
                && record.UpdatedAt == pulledUpdatedAt;
        }
    }

    /// <summary>
    /// For any local account with a non-empty AccountId,
    /// when a pulled account with the same AccountId has UpdatedAt less than or equal
    /// to the local UpdatedAt, the local record SHALL NOT be updated.
    /// **Validates: Requirements 5.3, 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PulledOlderOrEqualUpdatedAt_DoesNotUpdateLocalRecord(Guid accountId, NonNegativeInt ticksOffset)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case

        var dbName = $"AccPullLWW_OlderEq_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var localUpdatedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        // Pulled UpdatedAt is less than or equal (subtract offset ticks from local)
        var pulledUpdatedAt = localUpdatedAt.AddTicks(-ticksOffset.Get);

        var userId = 1;
        var externalId = 42;

        // Seed local account
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Account.Add(new AccountDTO
            {
                AccountId = accountId,
                ExternalId = externalId,
                Name = "Original local",
                Type = AccountType.Checking,
                CurrentBalance = 100m,
                IncludeInGeneralBalance = true,
                IsActive = true,
                Inactive = false,
                UserId = userId,
                UpdatedAt = localUpdatedAt,
                CreatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // Build pulled account response with same AccountId but older/equal UpdatedAt
        var pulledAccounts = new List<AccountApiRes>
        {
            new()
            {
                Id = externalId,
                AccountId = accountId,
                Name = "Should not overwrite",
                Type = AccountType.Savings,
                CurrentBalance = 999m,
                IncludeInGeneralBalance = false,
                Inactive = true,
                UpdatedAt = pulledUpdatedAt,
            }
        };

        // Setup service with real repo, mocked API dependencies
        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var transactionService = Substitute.For<ITransactionService>();

        accountApiRepo.GetAccountsAsync(Arg.Any<DateTime>())
            .Returns(pulledAccounts);

        syncCursorRepo.GetAsync(SyncCursorKeys.Account).Returns(DateTime.MinValue);

        var accountRepo = new AccountRepo(factory);
        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        await service.PullAsync(userId);

        // Verify: local record was NOT updated
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Account
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            return record is not null
                && record.Name == "Original local"
                && record.Type == AccountType.Checking
                && record.CurrentBalance == 100m
                && record.IncludeInGeneralBalance == true
                && record.Inactive == false
                && record.UpdatedAt == localUpdatedAt;
        }
    }

    /// <summary>
    /// For any pulled account with a non-empty AccountId that matches a local record,
    /// after update the local record's AccountId SHALL equal the pulled value.
    /// **Validates: Requirements 1.3, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> PulledAccountId_IsPreservedOnLocalRecord(Guid accountId, PositiveInt ticksOffset)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case

        var dbName = $"AccPullLWW_Preserved_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var localUpdatedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var pulledUpdatedAt = localUpdatedAt.AddTicks(ticksOffset.Get);

        var userId = 1;
        var externalId = 42;

        // Seed local account with the same AccountId
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Account.Add(new AccountDTO
            {
                AccountId = accountId,
                ExternalId = externalId,
                Name = "Local record",
                Type = AccountType.Checking,
                CurrentBalance = 50m,
                IncludeInGeneralBalance = true,
                IsActive = true,
                Inactive = false,
                UserId = userId,
                UpdatedAt = localUpdatedAt,
                CreatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // Build pulled account with same AccountId and newer UpdatedAt
        var pulledAccounts = new List<AccountApiRes>
        {
            new()
            {
                Id = externalId,
                AccountId = accountId,
                Name = "From server",
                Type = AccountType.Savings,
                CurrentBalance = 200m,
                IncludeInGeneralBalance = false,
                Inactive = false,
                UpdatedAt = pulledUpdatedAt,
            }
        };

        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var transactionService = Substitute.For<ITransactionService>();

        accountApiRepo.GetAccountsAsync(Arg.Any<DateTime>())
            .Returns(pulledAccounts);

        syncCursorRepo.GetAsync(SyncCursorKeys.Account).Returns(DateTime.MinValue);

        var accountRepo = new AccountRepo(factory);
        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        await service.PullAsync(userId);

        // Verify: AccountId is preserved on local record
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Account
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            return record is not null && record.AccountId == accountId;
        }
    }

    /// <summary>
    /// For any local account matched by ExternalId (with empty AccountId),
    /// when the pulled response contains a non-empty AccountId, the local record
    /// SHALL have that AccountId persisted after the pull.
    /// **Validates: Requirements 5.4, 1.3, 7.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> ExternalIdFallback_PersistsAccountId(Guid pulledAccountId, PositiveInt ticksOffset, PositiveInt externalIdVal)
    {
        if (pulledAccountId == Guid.Empty) return true; // skip trivial case

        var dbName = $"AccPullLWW_ExtFallback_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var localUpdatedAt = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var pulledUpdatedAt = localUpdatedAt.AddTicks(ticksOffset.Get);

        var userId = 1;
        var externalId = externalIdVal.Get;

        // Seed local account with ExternalId but empty AccountId
        using (var ctx = await factory.CreateDbContextAsync())
        {
            ctx.Account.Add(new AccountDTO
            {
                AccountId = Guid.Empty,
                ExternalId = externalId,
                Name = "Legacy account",
                Type = AccountType.Checking,
                CurrentBalance = 75m,
                IncludeInGeneralBalance = true,
                IsActive = true,
                Inactive = false,
                UserId = userId,
                UpdatedAt = localUpdatedAt,
                CreatedAt = DateTime.UtcNow,
            });
            await ctx.SaveChangesAsync();
        }

        // Build pulled account with same ExternalId + non-empty AccountId and newer UpdatedAt
        var pulledAccounts = new List<AccountApiRes>
        {
            new()
            {
                Id = externalId,
                AccountId = pulledAccountId,
                Name = "Server updated",
                Type = AccountType.Savings,
                CurrentBalance = 300m,
                IncludeInGeneralBalance = false,
                Inactive = false,
                UpdatedAt = pulledUpdatedAt,
            }
        };

        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var transactionService = Substitute.For<ITransactionService>();

        accountApiRepo.GetAccountsAsync(Arg.Any<DateTime>())
            .Returns(pulledAccounts);

        syncCursorRepo.GetAsync(SyncCursorKeys.Account).Returns(DateTime.MinValue);

        var accountRepo = new AccountRepo(factory);
        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        await service.PullAsync(userId);

        // Verify: local record now has the AccountId from the pulled response
        using (var ctx = await factory.CreateDbContextAsync())
        {
            var record = await ctx.Account
                .FirstOrDefaultAsync(a => a.ExternalId == externalId);

            return record is not null
                && record.AccountId == pulledAccountId;
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
