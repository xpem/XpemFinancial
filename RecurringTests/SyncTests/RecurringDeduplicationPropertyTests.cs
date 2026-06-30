using ApiRepo;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using NSubstitute;
using Repo;
using Service.Recurring;
using Service.Transaction;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Property 8: Recurring Occurrence Deduplication
/// Validates: Requirements 6.3
/// </summary>
[Trait("Feature", "transaction-guid-sync")]
[Trait("Property", "8")]
public class RecurringDeduplicationPropertyTests
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
    /// For any valid RecurringRuleId and occurrence date, calling GeneratePendingAsync
    /// twice with the same rule and horizon results in exactly ONE record for the
    /// deterministic TransactionId — the second invocation is deduplicated.
    /// **Validates: Requirements 6.3**
    /// </summary>
    [Property(MaxTest = 50)]
    public async Task<bool> SameRuleAndDate_GeneratedTwice_ExactlyOneRecord(Guid ruleId, int userId)
    {
        // Guard against invalid inputs that would cause Guid.Empty TransactionId
        if (ruleId == Guid.Empty) return true;
        if (userId <= 0) return true;

        var dbName = $"RecurringDedup_{Guid.NewGuid()}";
        var factory = CreateFactory(dbName);

        // Setup mocked dependencies for TransactionService
        var transactionApiRepo = Substitute.For<ITransactionApiRepo>();
        var categoryRepo = Substitute.For<ICategoryRepo>();
        var accountRepo = Substitute.For<IAccountRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();

        var transactionRepo = new TransactionRepo(factory);
        var transactionService = new TransactionService(
            transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);

        var scheduler = new RecurringScheduler(transactionService, transactionRepo);

        // Build a rule with a fixed start date inside lookback window
        // Use a date that's within the lookback window (today - 1 month) and before horizon
        var startDate = DateTime.Today;
        var rule = new RecurringRuleDTO
        {
            RecurringRuleId = ruleId,
            StartDate = startDate,
            EndDate = null,
            Frequency = Frequency.Monthly,
            Description = "Dedup test",
            Amount = 100m,
            Type = TransactionType.Expense,
            CategoryId = 0,
            AccountId = 1,
            UserId = userId,
            Inactive = false,
        };

        // Use a horizon that captures exactly the start date occurrence
        var horizon = startDate.AddDays(1);

        // First invocation — should generate one occurrence
        await scheduler.GeneratePendingAsync([rule], horizon);

        // Second invocation — should be deduplicated
        await scheduler.GeneratePendingAsync([rule], horizon);

        // Verify: exactly one record exists with the deterministic TransactionId
        var expectedTransactionId = DeterministicGuid.FromRecurringRule(ruleId, startDate);

        using var ctx = await factory.CreateDbContextAsync();
        var count = await ctx.Transaction
            .CountAsync(t => t.TransactionId == expectedTransactionId);

        return count == 1;
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
