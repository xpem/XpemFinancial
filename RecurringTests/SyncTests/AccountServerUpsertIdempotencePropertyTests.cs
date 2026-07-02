using FsCheck;
using FsCheck.Xunit;
using FinancialService.Model.DTO;
using FinancialService.Model.Req;
using FinancialService.Repo;
using FinancialService.Service;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 2: Server Upsert Idempotence
/// Validates: Requirements 4.1, 4.2, 4.3, 4.5, 2.5
/// </summary>
[Trait("Feature", "account-guid-sync")]
[Trait("Property", "2")]
public class AccountServerUpsertIdempotencePropertyTests
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
    /// Creates a valid AccountReq with the given AccountId and fields.
    /// </summary>
    private static AccountReq CreateReq(Guid accountId, string name, AccountType type, bool includeInGeneralBalance, bool inactive) => new()
    {
        AccountId = accountId,
        Name = name,
        Type = type,
        IncludeInGeneralBalance = includeInGeneralBalance,
        Inactive = inactive,
        UpdatedAt = DateTime.UtcNow
    };

    /// <summary>
    /// Generates a valid Name string (1-100 chars, non-whitespace).
    /// The [Required] attribute rejects whitespace-only strings, so we
    /// prefix with "A" to guarantee non-whitespace content.
    /// </summary>
    private static string MakeValidName(NonEmptyString nes)
    {
        var raw = "A" + nes.Get.Trim();
        if (raw.Length > 100)
            return raw[..100];
        return raw;
    }

    /// <summary>
    /// Push same AccountId+UserId N times → exactly one record, same Id returned.
    /// **Validates: Requirements 4.1, 4.2, 4.3, 4.5, 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> SameAccountId_PushedNTimes_ExactlyOneRecord_SameIdReturned(
        Guid accountId,
        PositiveInt userId,
        PositiveInt repeatCount,
        NonEmptyString name)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case

        int uid = userId.Get;
        int n = Math.Min(repeatCount.Get, 10); // cap at 10 to keep test fast
        string validName = MakeValidName(name);

        var dbName = $"AccServerUpsert_Idempotence_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var accountRepo = new AccountRepo(factory);
        var transactionRepo = Substitute.For<ITransactionRepo>();
        var service = new AccountService(accountRepo, transactionRepo);

        var req = CreateReq(accountId, validName, AccountType.Checking, true, false);

        int? firstId = null;
        for (int i = 0; i < n; i++)
        {
            var result = await service.CreateAsync(req, uid);
            if (firstId == null)
                firstId = result.Id;
            else if (result.Id != firstId)
                return false; // Different Id returned — idempotence violated
        }

        // Verify exactly one record exists with this AccountId+UserId
        using var ctx = factory.CreateDbContext();
        var count = ctx.Account.Count(a => a.AccountId == accountId && a.UserId == uid);

        return count == 1;
    }

    /// <summary>
    /// Mutable fields reflect the last request's values after multiple upserts.
    /// **Validates: Requirements 4.1, 4.2, 4.3, 4.5, 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> MutableFields_ReflectLastRequestValues(
        Guid accountId,
        PositiveInt userId,
        NonEmptyString firstName,
        NonEmptyString lastName)
    {
        if (accountId == Guid.Empty) return true; // skip trivial case

        int uid = userId.Get;
        string name1 = MakeValidName(firstName);
        string name2 = MakeValidName(lastName);

        var dbName = $"AccServerUpsert_Mutable_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var accountRepo = new AccountRepo(factory);
        var transactionRepo = Substitute.For<ITransactionRepo>();
        var service = new AccountService(accountRepo, transactionRepo);

        // First push with initial values
        var req1 = CreateReq(accountId, name1, AccountType.Checking, true, false);
        var result1 = await service.CreateAsync(req1, uid);

        // Second push with different mutable values
        var req2 = CreateReq(accountId, name2, AccountType.Savings, false, true);
        var result2 = await service.CreateAsync(req2, uid);

        // Same Id must be returned
        if (result1.Id != result2.Id) return false;

        // Verify mutable fields reflect the LAST request
        using var ctx = factory.CreateDbContext();
        var stored = ctx.Account.Single(a => a.AccountId == accountId && a.UserId == uid);

        return stored.Name == name2
            && stored.Type == AccountType.Savings
            && stored.IncludeInGeneralBalance == false
            && stored.Inactive == true;
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
