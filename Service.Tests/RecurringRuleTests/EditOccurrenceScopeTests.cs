using ApiRepo;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Model.Req;
using NSubstitute;
using Repo;
using Service.Recurring;
using Service.Transaction;
using Xunit;

namespace RecurringTests.RecurringRuleTests;

/// <summary>
/// Happy-path unit tests for RecurringRuleService.EditOccurrenceAsync, covering the
/// three edit scopes: ThisOnly, ThisAndFuture and All.
/// </summary>
[Trait("Feature", "recurring-rule-edit-scopes")]
public class EditOccurrenceScopeTests
{
    private const int UserId = 1;

    private static IDbContextFactory<DbCtx> CreateFactory(string dbName)
    {
        var options = new DbContextOptionsBuilder<DbCtx>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new TestDbContextFactory(options);
    }

    private static (RecurringRuleService Sut, TransactionRepo TransactionRepo, RecurringRuleRepo RecurringRuleRepo, ITransactionService TransactionService)
        CreateSut(string dbName)
    {
        var factory = CreateFactory(dbName);

        var transactionApiRepo = Substitute.For<ITransactionApiRepo>();
        var categoryRepo = Substitute.For<ICategoryRepo>();
        var accountRepo = Substitute.For<IAccountRepo>();
        var syncCursorRepo = Substitute.For<ISyncCursorRepo>();
        var recurringRuleApiRepo = Substitute.For<IRecurringRuleApiRepo>();

        var transactionRepo = new TransactionRepo(factory);
        var recurringRuleRepo = new RecurringRuleRepo(factory);

        var transactionService = new TransactionService(transactionRepo, transactionApiRepo, categoryRepo, accountRepo, syncCursorRepo);
        var scheduler = new RecurringScheduler(transactionService, transactionRepo);

        var sut = new RecurringRuleService(
            recurringRuleRepo, scheduler, recurringRuleApiRepo, transactionService, categoryRepo, accountRepo, syncCursorRepo);

        return (sut, transactionRepo, recurringRuleRepo, transactionService);
    }

    private static RecurringRuleDTO BuildRule(Guid ruleId, DateTime startDate) => new()
    {
        RecurringRuleId = ruleId,
        Description = "Netflix",
        Amount = 50m,
        Type = TransactionType.Expense,
        CategoryId = 1,
        AccountId = 10,
        Frequency = Frequency.Monthly,
        StartDate = startDate,
        EndDate = null,
        UserId = UserId,
        CreatedAt = DateTime.Now,
        UpdatedAt = DateTime.Now,
    };

    private static async Task<TransactionDTO> SeedOccurrenceAsync(
        ITransactionService transactionService, Guid ruleId, DateTime date, string description, decimal amount, int categoryId, int accountId)
    {
        var occurrence = new TransactionDTO
        {
            Description = description,
            Amount = amount,
            Type = TransactionType.Expense,
            CategoryId = categoryId,
            AccountId = accountId,
            RecurringRuleId = ruleId,
            Repetition = Repetition.Recurring,
            Date = date,
            UserId = UserId,
            TransactionId = DeterministicGuid.FromRecurringRule(ruleId, date),
        };

        await transactionService.AddOccurrenceAsync(occurrence);
        return occurrence;
    }

    [Fact]
    public async Task ThisOnly_UpdatesSingleOccurrence_AndLeavesRuleUntouched()
    {
        var (sut, transactionRepo, recurringRuleRepo, transactionService) = CreateSut($"EditScope_ThisOnly_{Guid.NewGuid()}");

        var ruleId = Guid.NewGuid();
        var occurrenceDate = DateTime.Today;

        await recurringRuleRepo.AddAsync(BuildRule(ruleId, occurrenceDate));
        var occurrence = await SeedOccurrenceAsync(transactionService, ruleId, occurrenceDate, "Netflix", 50m, categoryId: 1, accountId: 10);

        var req = new EditOccurrenceReq
        {
            TransactionId = occurrence.Id,
            RecurringRuleId = ruleId,
            Scope = EditScope.ThisOnly,
            UpdatedRule = new RecurringRuleDTO
            {
                RecurringRuleId = ruleId,
                Description = "Netflix Premium",
                Amount = 75m,
                Type = TransactionType.Expense,
                CategoryId = 2,
                UserId = UserId,
            },
        };

        var resp = await sut.EditOccurrenceAsync(req, isOnline: false);

        Assert.True(resp.Success);

        var reloadedOccurrence = await transactionRepo.GetByIdAsync(occurrence.Id);
        Assert.Equal("Netflix Premium", reloadedOccurrence.Description);
        Assert.Equal(75m, reloadedOccurrence.Amount);
        Assert.Equal(2, reloadedOccurrence.CategoryId);
        Assert.True(reloadedOccurrence.IsCustomized);

        // The rule itself must remain unchanged — this was a single-occurrence edit.
        var reloadedRule = await recurringRuleRepo.GetByIdAsync(ruleId);
        Assert.Equal("Netflix", reloadedRule!.Description);
        Assert.Equal(50m, reloadedRule.Amount);
    }

    [Fact]
    public async Task ThisAndFuture_UpdatesRuleAndFutureOccurrences_ButPreservesThePast()
    {
        var (sut, transactionRepo, recurringRuleRepo, transactionService) = CreateSut($"EditScope_ThisAndFuture_{Guid.NewGuid()}");

        var ruleId = Guid.NewGuid();
        var pastDate = DateTime.Today.AddMonths(-2);
        var targetDate = DateTime.Today.AddMonths(-1);
        var futureDate = DateTime.Today;

        await recurringRuleRepo.AddAsync(BuildRule(ruleId, pastDate));

        var pastOccurrence = await SeedOccurrenceAsync(transactionService, ruleId, pastDate, "Netflix", 50m, categoryId: 1, accountId: 10);
        var targetOccurrence = await SeedOccurrenceAsync(transactionService, ruleId, targetDate, "Netflix", 50m, categoryId: 1, accountId: 10);
        var futureOccurrence = await SeedOccurrenceAsync(transactionService, ruleId, futureDate, "Netflix", 50m, categoryId: 1, accountId: 10);

        var req = new EditOccurrenceReq
        {
            TransactionId = targetOccurrence.Id,
            RecurringRuleId = ruleId,
            Scope = EditScope.ThisAndFuture,
            UpdatedRule = new RecurringRuleDTO
            {
                RecurringRuleId = ruleId,
                Description = "Netflix Premium",
                Amount = 75m,
                Type = TransactionType.Expense,
                CategoryId = 2,
                AccountId = 10,
                Frequency = Frequency.Monthly,
                EndDate = null,
                UserId = UserId,
            },
        };

        var resp = await sut.EditOccurrenceAsync(req, isOnline: false);

        Assert.True(resp.Success);

        // The rule now reflects the new values and its StartDate moved to the edited occurrence.
        var reloadedRule = await recurringRuleRepo.GetByIdAsync(ruleId);
        Assert.Equal("Netflix Premium", reloadedRule!.Description);
        Assert.Equal(75m, reloadedRule.Amount);
        Assert.Equal(2, reloadedRule.CategoryId);
        Assert.Equal(targetDate.Date, reloadedRule.StartDate.Date);

        // The past occurrence is preserved as-is, just detached from the rule.
        var reloadedPast = await transactionRepo.GetByIdAsync(pastOccurrence.Id);
        Assert.Equal("Netflix", reloadedPast.Description);
        Assert.Equal(50m, reloadedPast.Amount);
        Assert.Null(reloadedPast.RecurringRuleId);
        Assert.Equal(Repetition.None, reloadedPast.Repetition);
        Assert.False(reloadedPast.Inactive);

        // The edited occurrence and the future one are regenerated with the new values.
        var reloadedTarget = await transactionRepo.GetByIdAsync(targetOccurrence.Id);
        Assert.Equal("Netflix Premium", reloadedTarget.Description);
        Assert.Equal(75m, reloadedTarget.Amount);
        Assert.False(reloadedTarget.Inactive);
        Assert.Equal(ruleId, reloadedTarget.RecurringRuleId);

        var reloadedFuture = await transactionRepo.GetByIdAsync(futureOccurrence.Id);
        Assert.Equal("Netflix Premium", reloadedFuture.Description);
        Assert.Equal(75m, reloadedFuture.Amount);
        Assert.False(reloadedFuture.Inactive);
    }

    [Fact]
    public async Task All_UpdatesRuleAndEveryActiveOccurrence()
    {
        var (sut, transactionRepo, recurringRuleRepo, transactionService) = CreateSut($"EditScope_All_{Guid.NewGuid()}");

        var ruleId = Guid.NewGuid();
        var pastDate = DateTime.Today.AddMonths(-1);
        var futureDate = DateTime.Today.AddMonths(1);

        await recurringRuleRepo.AddAsync(BuildRule(ruleId, pastDate));

        var pastOccurrence = await SeedOccurrenceAsync(transactionService, ruleId, pastDate, "Netflix", 50m, categoryId: 1, accountId: 10);
        var futureOccurrence = await SeedOccurrenceAsync(transactionService, ruleId, futureDate, "Netflix", 50m, categoryId: 1, accountId: 10);

        var req = new EditOccurrenceReq
        {
            TransactionId = futureOccurrence.Id,
            RecurringRuleId = ruleId,
            Scope = EditScope.All,
            UpdatedRule = new RecurringRuleDTO
            {
                RecurringRuleId = ruleId,
                Description = "Netflix Premium",
                Amount = 75m,
                Type = TransactionType.Expense,
                CategoryId = 2,
                AccountId = 10,
                Frequency = Frequency.Monthly,
                EndDate = null,
                UserId = UserId,
            },
        };

        var resp = await sut.EditOccurrenceAsync(req, isOnline: false);

        Assert.True(resp.Success);

        // The rule is updated, but its StartDate is left alone — unlike ThisAndFuture.
        var reloadedRule = await recurringRuleRepo.GetByIdAsync(ruleId);
        Assert.Equal("Netflix Premium", reloadedRule!.Description);
        Assert.Equal(75m, reloadedRule.Amount);
        Assert.Equal(2, reloadedRule.CategoryId);
        Assert.Equal(pastDate.Date, reloadedRule.StartDate.Date);

        // Every active occurrence — past and future alike — picks up the new values in place.
        var reloadedPast = await transactionRepo.GetByIdAsync(pastOccurrence.Id);
        Assert.Equal("Netflix Premium", reloadedPast.Description);
        Assert.Equal(75m, reloadedPast.Amount);
        Assert.Equal(2, reloadedPast.CategoryId);
        Assert.Equal(ruleId, reloadedPast.RecurringRuleId);

        var reloadedFuture = await transactionRepo.GetByIdAsync(futureOccurrence.Id);
        Assert.Equal("Netflix Premium", reloadedFuture.Description);
        Assert.Equal(75m, reloadedFuture.Amount);
        Assert.Equal(2, reloadedFuture.CategoryId);
        Assert.Equal(ruleId, reloadedFuture.RecurringRuleId);
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
