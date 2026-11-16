using ApiRepo;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Model.Req;
using Model.Resp.Api;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Repo;
using RecurringTests.TransferTests;
using Service.Category;
using Service.Recurring;
using Xunit;

namespace RecurringTests.SyncTests;

/// <summary>
/// Regression tests for the sync defects found when the same account is used on two devices:
/// writes that never reached the server being marked as synced, recurring rules and edited
/// categories never being pushed, and cancelled occurrences not being propagated.
/// </summary>
public class OfflineSyncRegressionTests
{
    // ── helpers ───────────────────────────────────────────────────────────

    private static AccountDTO MakeAccount(int id, int? externalId) => new()
    {
        Id = id,
        ExternalId = externalId,
        Name = $"Account {id}",
        UserId = 1,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static TransactionDTO MakeTransaction(int accountId, int? externalId = null,
        TransactionSyncStatus status = TransactionSyncStatus.Pending) => new()
    {
        TransactionId = Guid.NewGuid(),
        UserId = 1,
        Description = "Test",
        Date = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc),
        Amount = -10m,
        Type = TransactionType.Expense,
        Repetition = Repetition.None,
        AccountId = accountId,
        ExternalId = externalId,
        SyncStatus = status,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static async Task<(TransferTestContext ctx, AccountDTO acc)> Ctx()
    {
        var origin = MakeAccount(1, externalId: 100);
        var other = MakeAccount(2, externalId: 200);
        return (await TransferGenerators.CreateServiceContext(origin, other), origin);
    }

    private static async Task<TransactionDTO> Reload(TransferTestContext ctx, int id)
    {
        using var db = await ctx.Factory.CreateDbContextAsync();
        return await db.Transaction.AsNoTracking().FirstAsync(t => t.Id == id);
    }

    /// <summary>In-memory ISyncCursorRepo so outbox markers can be asserted.</summary>
    private sealed class FakeCursorRepo : ISyncCursorRepo
    {
        public Dictionary<string, DateTime> Rows { get; } = [];
        public Task<DateTime> GetAsync(string e) => Task.FromResult(Rows.GetValueOrDefault(e, DateTime.MinValue));
        public Task SaveAsync(string e, DateTime t) { Rows[e] = t; return Task.CompletedTask; }
        public Task DeleteAsync(string e) { Rows.Remove(e); return Task.CompletedTask; }
        public Task<List<string>> ListNamesAsync(string prefix)
            => Task.FromResult(Rows.Keys.Where(k => k.StartsWith(prefix)).ToList());
    }

    // ── 1. server unreachable must never look "synced" ────────────────────

    [Fact]
    public async Task Push_ApiReturnsZero_KeepsPendingAndNoExternalId()
    {
        var (ctx, acc) = await Ctx();
        var tx = MakeTransaction(acc.Id);
        await ctx.TransactionRepo.Add(tx);

        ctx.TransactionApiRepo.PostAsync(Arg.Any<TransactionReq>()).Returns(0); // server unreachable

        await ctx.Service.PushPendingAsync(1);

        var saved = await Reload(ctx, tx.Id);
        Assert.Null(saved.ExternalId);
        Assert.Equal(TransactionSyncStatus.Pending, saved.SyncStatus);
    }

    [Fact]
    public async Task Update_ServerUnavailable_StaysPendingAndDoesNotThrow()
    {
        var (ctx, acc) = await Ctx();
        var tx = MakeTransaction(acc.Id, externalId: 55, status: TransactionSyncStatus.Synced);
        await ctx.TransactionRepo.Add(tx);

        ctx.TransactionApiRepo.PutAsync(55, Arg.Any<TransactionReq>())
            .ThrowsAsync(new ServerUnavailableException("down"));

        await ctx.Service.UpdateAsync(tx, isOnline: true);

        Assert.Equal(TransactionSyncStatus.Pending, (await Reload(ctx, tx.Id)).SyncStatus);
    }

    [Fact]
    public async Task Update_ServerConfirms_BecomesSynced()
    {
        var (ctx, acc) = await Ctx();
        var tx = MakeTransaction(acc.Id, externalId: 55, status: TransactionSyncStatus.Synced);
        await ctx.TransactionRepo.Add(tx);

        await ctx.Service.UpdateAsync(tx, isOnline: true);

        await ctx.TransactionApiRepo.Received(1).PutAsync(55, Arg.Any<TransactionReq>());
        Assert.Equal(TransactionSyncStatus.Synced, (await Reload(ctx, tx.Id)).SyncStatus);
    }

    // ── 3. pending edits of records that already exist on the server ──────

    [Fact]
    public async Task PushPending_RecordWithExternalId_SendsPut()
    {
        var (ctx, acc) = await Ctx();
        var tx = MakeTransaction(acc.Id, externalId: 77, status: TransactionSyncStatus.Pending);
        await ctx.TransactionRepo.Add(tx);

        await ctx.Service.PushPendingAsync(1);

        await ctx.TransactionApiRepo.Received(1).PutAsync(77, Arg.Any<TransactionReq>());
        await ctx.TransactionApiRepo.DidNotReceive().PostAsync(Arg.Any<TransactionReq>());
        Assert.Equal(TransactionSyncStatus.Synced, (await Reload(ctx, tx.Id)).SyncStatus);
    }

    [Fact]
    public async Task DeleteFutureOccurrences_MarksThemPendingSoTheyAreDeliveredToOtherDevices()
    {
        var (ctx, acc) = await Ctx();
        var ruleId = Guid.NewGuid();
        var tx = MakeTransaction(acc.Id, externalId: 9, status: TransactionSyncStatus.Synced);
        tx.RecurringRuleId = ruleId;
        tx.Date = new DateTime(2030, 1, 10);
        await ctx.TransactionRepo.Add(tx);

        await ctx.Service.DeleteFutureOccurrencesAsync(ruleId, new DateTime(2030, 1, 1));

        var saved = await Reload(ctx, tx.Id);
        Assert.True(saved.Inactive);
        Assert.Equal(TransactionSyncStatus.Pending, saved.SyncStatus);

        await ctx.Service.PushPendingAsync(1);
        await ctx.TransactionApiRepo.Received(1)
            .PutAsync(9, Arg.Is<TransactionReq>(r => r.Inactive));
    }

    [Fact]
    public async Task ResetStuck_HealsLegacyZeroExternalId()
    {
        var (ctx, acc) = await Ctx();
        var tx = MakeTransaction(acc.Id, externalId: 0, status: TransactionSyncStatus.Synced);
        await ctx.TransactionRepo.Add(tx);

        await ctx.Service.ResetStuckPushingAsync();

        var saved = await Reload(ctx, tx.Id);
        Assert.Null(saved.ExternalId);
        Assert.Equal(TransactionSyncStatus.Pending, saved.SyncStatus);
    }

    // ── 2. a local projection must not hide the server's version ──────────

    [Fact]
    public async Task Pull_LocalProjectionNeverPushed_ServerVersionWins()
    {
        var (ctx, acc) = await Ctx();
        var ruleId = Guid.NewGuid();

        // Occurrence generated locally by the scheduler: UpdatedAt = "now", Pending, no ExternalId.
        var local = MakeTransaction(acc.Id, status: TransactionSyncStatus.Pending);
        local.RecurringRuleId = ruleId;
        local.UpdatedAt = DateTime.Now;
        await ctx.TransactionRepo.Add(local);

        // Server copy was deleted on another device a while ago (older UpdatedAt).
        var fromServer = MakeTransaction(acc.Id, externalId: 321, status: TransactionSyncStatus.Synced);
        fromServer.TransactionId = local.TransactionId;
        fromServer.RecurringRuleId = ruleId;
        fromServer.Inactive = true;
        fromServer.UpdatedAt = DateTime.Now.AddDays(-2);

        await ctx.Service.ApplyFromApiAsync(fromServer);

        var saved = await Reload(ctx, local.Id);
        Assert.True(saved.Inactive);
        Assert.Equal(321, saved.ExternalId);
        Assert.Equal(TransactionSyncStatus.Synced, saved.SyncStatus);
    }

    // ── 4. recurring rules ────────────────────────────────────────────────

    private static (RecurringRuleService svc, IRecurringRuleRepo repo, IRecurringRuleApiRepo api,
        FakeCursorRepo cursors, IAccountRepo accounts) RuleSut(AccountDTO? account)
    {
        var repo = Substitute.For<IRecurringRuleRepo>();
        var api = Substitute.For<IRecurringRuleApiRepo>();
        var accounts = Substitute.For<IAccountRepo>();
        var cursors = new FakeCursorRepo();
        if (account is not null)
            accounts.GetByIdAsync(account.Id).Returns(Task.FromResult<AccountDTO?>(account));

        var svc = new RecurringRuleService(repo, Substitute.For<IRecurringScheduler>(), api,
            Substitute.For<Service.Transaction.ITransactionService>(),
            Substitute.For<ICategoryRepo>(), accounts, cursors);
        return (svc, repo, api, cursors, accounts);
    }

    private static RecurringRuleDTO MakeRule(int? localAccountId, int? externalId) => new()
    {
        RecurringRuleId = Guid.NewGuid(),
        UserId = 1,
        Description = "Rent",
        Amount = -100m,
        Frequency = Frequency.Monthly,
        StartDate = new DateTime(2024, 1, 1),
        AccountId = localAccountId,
        ExternalId = externalId,
        UpdatedAt = DateTime.Now,
    };

    [Fact]
    public async Task RulePushPending_NeverSyncedRule_SendsServerAccountId_NotLocalId()
    {
        var account = MakeAccount(id: 3, externalId: 900);
        var (svc, repo, api, _, _) = RuleSut(account);
        var rule = MakeRule(localAccountId: 3, externalId: null);
        repo.GetPendingPushAsync(1).Returns(Task.FromResult(new List<RecurringRuleDTO> { rule }));
        api.PostAsync(Arg.Any<RecurringRuleReq>()).Returns(42);

        await svc.PushPendingAsync(1);

        await api.Received(1).PostAsync(Arg.Is<RecurringRuleReq>(r => r.AccountId == 900));
        await repo.Received().UpdateAsync(Arg.Is<RecurringRuleDTO>(r => r.ExternalId == 42));
    }

    [Fact]
    public async Task RulePushPending_EditedRuleMarkedInOutbox_IsPushedAndMarkerCleared()
    {
        var account = MakeAccount(id: 3, externalId: 900);
        var (svc, repo, api, cursors, _) = RuleSut(account);
        var rule = MakeRule(localAccountId: 3, externalId: 10); // already on the server
        repo.GetPendingPushAsync(1).Returns(Task.FromResult(new List<RecurringRuleDTO>()));
        repo.GetByIdAsync(rule.RecurringRuleId).Returns(Task.FromResult<RecurringRuleDTO?>(rule));
        api.PostAsync(Arg.Any<RecurringRuleReq>()).Returns(10);
        cursors.Rows[SyncCursorKeys.RuleOutbox(rule.RecurringRuleId)] = rule.UpdatedAt;

        await svc.PushPendingAsync(1);

        await api.Received(1).PostAsync(Arg.Any<RecurringRuleReq>());
        Assert.Empty(cursors.Rows);
    }

    [Fact]
    public async Task RulePushPending_ServerUnavailable_KeepsMarkerAndExternalId()
    {
        var account = MakeAccount(id: 3, externalId: 900);
        var (svc, repo, api, cursors, _) = RuleSut(account);
        var rule = MakeRule(localAccountId: 3, externalId: 10);
        repo.GetPendingPushAsync(1).Returns(Task.FromResult(new List<RecurringRuleDTO>()));
        repo.GetByIdAsync(rule.RecurringRuleId).Returns(Task.FromResult<RecurringRuleDTO?>(rule));
        api.PostAsync(Arg.Any<RecurringRuleReq>()).Returns(0); // unreachable
        cursors.Rows[SyncCursorKeys.RuleOutbox(rule.RecurringRuleId)] = rule.UpdatedAt;

        await svc.PushPendingAsync(1);

        Assert.Single(cursors.Rows);
        Assert.Equal(10, rule.ExternalId);
    }

    [Fact]
    public async Task RulePushPending_AccountNotSyncedYet_DefersPush()
    {
        var account = MakeAccount(id: 3, externalId: null);
        var (svc, repo, api, _, _) = RuleSut(account);
        var rule = MakeRule(localAccountId: 3, externalId: null);
        repo.GetPendingPushAsync(1).Returns(Task.FromResult(new List<RecurringRuleDTO> { rule }));

        await svc.PushPendingAsync(1);

        await api.DidNotReceive().PostAsync(Arg.Any<RecurringRuleReq>());
    }

    // ── 5. edited categories ──────────────────────────────────────────────

    [Fact]
    public async Task CategoryEdit_PushFails_IsRetriedByPushPending_AndMarkerCleared()
    {
        var repo = Substitute.For<ICategoryRepo>();
        var api = Substitute.For<ICategoryApiRepo>();
        var cursors = new FakeCursorRepo();
        var svc = new CategoryService(repo, api, cursors);

        var category = new CategoryDTO
        {
            Id = 1,
            CategoryId = Guid.NewGuid(),
            ExternalId = 12, // already known by the server
            Name = "Food",
            IsMainCategory = true,
            UserId = 1,
        };
        repo.GetByCategoryIdAsync(category.CategoryId).Returns(Task.FromResult<CategoryDTO?>(category));
        repo.GetPendingPushAsync().Returns(Task.FromResult(new List<CategoryDTO>()));

        // Edit while the server is down: the push fails and is swallowed...
        api.PostCategoryAsync(Arg.Any<CategoryReq>()).ThrowsAsync(new Exception("down"));
        category.Name = "Groceries";
        await svc.UpdateLocalAsync(category);
        await svc.PushAsync(category);
        Assert.Single(cursors.Rows); // ...but the edit is remembered.

        // Next sync cycle, server is back.
        api.PostCategoryAsync(Arg.Any<CategoryReq>()).Returns(new CategoryPushRes { Id = 12 });
        await svc.PushPendingAsync(1);

        await api.Received().PostCategoryAsync(Arg.Is<CategoryReq>(r => r.Name == "Groceries"));
        Assert.Empty(cursors.Rows);
    }
}
