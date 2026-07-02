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
/// Property 11: Server Generates Guid When None Provided
/// Validates: Requirements 4.4, 2.6
/// </summary>
[Trait("Feature", "account-guid-sync")]
[Trait("Property", "11")]
public class AccountServerGuidGenerationPropertyTests
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
    /// Creates an AccountService with a real AccountRepo and a mocked ITransactionRepo.
    /// </summary>
    private static (AccountService service, IDbContextFactory<FinancialDbctx> factory) CreateService(string dbName)
    {
        var factory = CreateFactory(dbName);
        var accountRepo = new AccountRepo(factory);
        var transactionRepo = Substitute.For<ITransactionRepo>();
        var service = new AccountService(accountRepo, transactionRepo);
        return (service, factory);
    }

    /// <summary>
    /// Generates a valid Name string (1-100 chars, non-whitespace).
    /// Falls back to "Account" if the generated string is whitespace-only.
    /// </summary>
    private static string MakeValidName(NonEmptyString nes)
    {
        var raw = nes.Get.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return "Account";
        if (raw.Length > 100)
            return raw[..100];
        return raw;
    }

    /// <summary>
    /// Creates a valid AccountReq with the given AccountId.
    /// </summary>
    private static AccountReq CreateReq(Guid? accountId, string name, AccountType type, bool includeInBalance, bool inactive) => new()
    {
        AccountId = accountId,
        Name = name,
        Type = type,
        IncludeInGeneralBalance = includeInBalance,
        Inactive = inactive,
        UpdatedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// When AccountId is null, the server generates a new non-empty Guid and returns it.
    /// **Validates: Requirements 4.4, 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> NullAccountId_GeneratesNewGuid(
        PositiveInt userId,
        NonEmptyString name)
    {
        int uid = userId.Get;
        string validName = MakeValidName(name);

        var dbName = $"AccGuidGen_Null_{Guid.NewGuid()}";
        var (service, _) = CreateService(dbName);

        var req = CreateReq(null, validName, AccountType.Checking, true, false);

        var result = await service.CreateAsync(req, uid);

        // Response must contain a non-null, non-empty AccountId
        return result.AccountId is not null && result.AccountId.Value != Guid.Empty;
    }

    /// <summary>
    /// When AccountId is Guid.Empty, the server treats it as null and generates a new Guid.
    /// **Validates: Requirements 4.4, 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> GuidEmptyAccountId_GeneratesNewGuid(
        PositiveInt userId,
        NonEmptyString name)
    {
        int uid = userId.Get;
        string validName = MakeValidName(name);

        var dbName = $"AccGuidGen_Empty_{Guid.NewGuid()}";
        var (service, _) = CreateService(dbName);

        var req = CreateReq(Guid.Empty, validName, AccountType.Checking, true, false);

        var result = await service.CreateAsync(req, uid);

        // Response must contain a non-null, non-empty AccountId
        return result.AccountId is not null && result.AccountId.Value != Guid.Empty;
    }

    /// <summary>
    /// Two separate requests with null AccountId produce different (unique) Guids.
    /// **Validates: Requirements 4.4, 2.6**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> TwoRequestsWithNullAccountId_ProduceDifferentGuids(
        PositiveInt userId,
        NonEmptyString name1,
        NonEmptyString name2)
    {
        int uid = userId.Get;
        string validName1 = MakeValidName(name1);
        string validName2 = MakeValidName(name2);

        var dbName = $"AccGuidGen_Unique_{Guid.NewGuid()}";
        var (service, _) = CreateService(dbName);

        var req1 = CreateReq(null, validName1, AccountType.Checking, true, false);
        var req2 = CreateReq(null, validName2, AccountType.Savings, true, false);

        var result1 = await service.CreateAsync(req1, uid);
        var result2 = await service.CreateAsync(req2, uid);

        // Both must have non-empty AccountIds
        if (result1.AccountId is null || result1.AccountId.Value == Guid.Empty)
            return false;
        if (result2.AccountId is null || result2.AccountId.Value == Guid.Empty)
            return false;

        // The two generated Guids must be different
        return result1.AccountId.Value != result2.AccountId.Value;
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
