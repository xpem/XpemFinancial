using FsCheck;
using FsCheck.Xunit;
using FinancialService.Model.DTO;
using FinancialService.Model.Req;
using FinancialService.Repo;
using FinancialService.Service;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 2: Server Upsert Idempotence
/// Validates: Requirements 4.2, 4.3, 4.5
/// </summary>
[Trait("Feature", "transaction-guid-sync")]
[Trait("Property", "2")]
public class ServerUpsertIdempotencePropertyTests
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
    /// Creates a valid TransactionReq with the given TransactionId and a valid description.
    /// </summary>
    private static TransactionReq CreateReq(Guid transactionId, string description, decimal amount, DateTime date, bool inactive) => new()
    {
        TransactionId = transactionId,
        Description = description,
        Amount = amount,
        Date = date,
        Inactive = inactive,
        AccountId = 1,
        Type = TransactionType.Expense,
        Repetition = Repetition.None,
    };

    /// <summary>
    /// Generates a valid description string (1-250 chars, non-empty).
    /// </summary>
    private static string MakeValidDescription(NonEmptyString nes)
    {
        var raw = nes.Get;
        if (raw.Length > 250)
            return raw[..250];
        return raw;
    }

    /// <summary>
    /// Push same TransactionId+UserId N times → exactly one record, same Id returned.
    /// **Validates: Requirements 4.2, 4.3, 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> SameTransactionId_PushedNTimes_ExactlyOneRecord_SameIdReturned(
        Guid transactionId,
        PositiveInt userId,
        PositiveInt repeatCount,
        NonEmptyString description)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case

        int uid = userId.Get;
        int n = Math.Min(repeatCount.Get, 10); // cap at 10 to keep test fast
        string desc = MakeValidDescription(description);

        var dbName = $"ServerUpsert_Idempotence_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var repo = new TransactionRepo(factory);
        var service = new TransactionService(repo);

        var req = CreateReq(transactionId, desc, 100m, DateTime.UtcNow, false);

        int? firstId = null;
        for (int i = 0; i < n; i++)
        {
            var result = await service.AddAsync(req, uid);
            if (firstId == null)
                firstId = result.Id;
            else if (result.Id != firstId)
                return false; // Different Id returned — idempotence violated
        }

        // Verify exactly one record exists with this TransactionId+UserId
        using var ctx = factory.CreateDbContext();
        var count = ctx.Transaction.Count(t => t.TransactionId == transactionId && t.UserId == uid);

        return count == 1;
    }

    /// <summary>
    /// Mutable fields reflect the last request's values after multiple upserts.
    /// **Validates: Requirements 4.2, 4.3, 4.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> MutableFields_ReflectLastRequestValues(
        Guid transactionId,
        PositiveInt userId,
        NonEmptyString firstDesc,
        NonEmptyString lastDesc)
    {
        if (transactionId == Guid.Empty) return true; // skip trivial case

        int uid = userId.Get;
        string desc1 = MakeValidDescription(firstDesc);
        string desc2 = MakeValidDescription(lastDesc);

        var dbName = $"ServerUpsert_Mutable_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);
        var repo = new TransactionRepo(factory);
        var service = new TransactionService(repo);

        // First push with initial values
        var req1 = CreateReq(transactionId, desc1, 50m, new DateTime(2024, 1, 1), false);
        var result1 = await service.AddAsync(req1, uid);

        // Second push with different mutable values
        var req2 = CreateReq(transactionId, desc2, 200m, new DateTime(2024, 6, 15), true);
        var result2 = await service.AddAsync(req2, uid);

        // Same Id must be returned
        if (result1.Id != result2.Id) return false;

        // Verify mutable fields reflect the LAST request
        using var ctx = factory.CreateDbContext();
        var stored = ctx.Transaction.Single(t => t.TransactionId == transactionId && t.UserId == uid);

        return stored.Description == desc2
            && stored.Amount == 200m
            && stored.Date == new DateTime(2024, 6, 15)
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
