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
/// Property 6: Pull Inserts New Records
/// Validates: Requirements 5.5
/// </summary>
[Trait("Feature", "account-guid-sync")]
[Trait("Property", "6")]
public class AccountPullInsertsNewRecordsPropertyTests
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
    /// When PullAsync receives an account whose AccountId does not match any local record
    /// and whose ExternalId does not match any local record, a new record is inserted.
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> NoLocalMatch_InsertsNewRecord(Guid accountId, PositiveInt externalId)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case

        var dbName = $"AccPullInsert_New_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var userId = 1;

        // DB starts empty — no seeded records

        var serverAccounts = new List<AccountApiRes>
        {
            new()
            {
                Id = externalId.Get,
                AccountId = accountId,
                Name = "Server Account",
                Type = AccountType.Checking,
                CurrentBalance = 100m,
                IncludeInGeneralBalance = true,
                Inactive = false,
                UpdatedAt = DateTime.UtcNow,
            }
        };

        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var transactionService = Substitute.For<ITransactionService>();

        accountApiRepo.GetAccountsAsync(Arg.Any<DateTime>(), Arg.Any<int>())
            .Returns(serverAccounts);

        syncCursorRepo.GetAsync(SyncCursorKeys.Account).Returns(DateTime.MinValue);

        var accountRepo = new AccountRepo(factory);
        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        await service.PullAsync(userId);

        // Verify: a new record exists in the DB
        using var ctx = await factory.CreateDbContextAsync();
        var records = await ctx.Account.Where(a => a.UserId == userId).ToListAsync();

        // At least one record should exist (the pulled one + possibly default from EnsureDefaultAccountAsync)
        return records.Any(r => r.AccountId == accountId);
    }

    /// <summary>
    /// After inserting a new record from pull, the local record has both
    /// AccountId == res.AccountId and ExternalId == res.Id.
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> InsertedRecord_HasBothAccountIdAndExternalId(Guid accountId, PositiveInt externalId)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case

        var dbName = $"AccPullInsert_BothIds_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var userId = 1;

        var serverAccounts = new List<AccountApiRes>
        {
            new()
            {
                Id = externalId.Get,
                AccountId = accountId,
                Name = "Server Account",
                Type = AccountType.Savings,
                CurrentBalance = 250m,
                IncludeInGeneralBalance = false,
                Inactive = false,
                UpdatedAt = DateTime.UtcNow,
            }
        };

        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var transactionService = Substitute.For<ITransactionService>();

        accountApiRepo.GetAccountsAsync(Arg.Any<DateTime>(), Arg.Any<int>())
            .Returns(serverAccounts);

        syncCursorRepo.GetAsync(SyncCursorKeys.Account).Returns(DateTime.MinValue);

        var accountRepo = new AccountRepo(factory);
        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        await service.PullAsync(userId);

        // Verify: the inserted record has both AccountId and ExternalId from response
        using var ctx = await factory.CreateDbContextAsync();
        var record = await ctx.Account.FirstOrDefaultAsync(a => a.AccountId == accountId);

        return record is not null
            && record.AccountId == accountId
            && record.ExternalId == externalId.Get;
    }

    /// <summary>
    /// After inserting a new record from pull, the mutable fields (Name, Type,
    /// IncludeInGeneralBalance, Inactive) match the response values.
    /// **Validates: Requirements 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> InsertedRecord_HasCorrectMutableFields(
        Guid accountId,
        PositiveInt externalId,
        NonEmptyString name,
        bool includeInBalance,
        bool inactive)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case

        // Trim name to fit the 100 char limit on AccountDTO.Name
        var accountName = name.Get.Length > 100 ? name.Get[..100] : name.Get;

        var dbName = $"AccPullInsert_Fields_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        var userId = 1;
        var accountType = AccountType.Checking;

        var serverAccounts = new List<AccountApiRes>
        {
            new()
            {
                Id = externalId.Get,
                AccountId = accountId,
                Name = accountName,
                Type = accountType,
                CurrentBalance = 500m,
                IncludeInGeneralBalance = includeInBalance,
                Inactive = inactive,
                UpdatedAt = DateTime.UtcNow,
            }
        };

        var accountApiRepo = Substitute.For<IAccountApiRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var transactionService = Substitute.For<ITransactionService>();

        accountApiRepo.GetAccountsAsync(Arg.Any<DateTime>(), Arg.Any<int>())
            .Returns(serverAccounts);

        syncCursorRepo.GetAsync(SyncCursorKeys.Account).Returns(DateTime.MinValue);

        var accountRepo = new AccountRepo(factory);
        var service = new AccountService(accountRepo, transactionService, accountApiRepo, syncCursorRepo);

        await service.PullAsync(userId);

        // Verify: mutable fields match response
        using var ctx = await factory.CreateDbContextAsync();
        var record = await ctx.Account.FirstOrDefaultAsync(a => a.AccountId == accountId);

        return record is not null
            && record.Name == accountName
            && record.Type == accountType
            && record.IncludeInGeneralBalance == includeInBalance
            && record.Inactive == inactive;
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
