using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Repo;
using Xunit;

namespace RecurringTests.TransferTests;

/// <summary>
/// Property 6: Balance reversal on transfer deletion
/// For any transfer of value V that is deleted, the origin account balance SHALL increase by V
/// and the destination account balance SHALL decrease by V.
/// **Validates: Requirements 3.3**
/// </summary>
[Trait("Feature", "transfer-transactions")]
[Trait("Property", "6")]
public class BalanceReversalOnDeletionPropertyTests
{
    /// <summary>
    /// For any transfer of value V (positive input, stored as -V), after creation the origin
    /// balance = -V and destination balance = +V. After deletion (soft-delete), the origin
    /// balance increases by V (back to 0) and destination balance decreases by V (back to 0).
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public async Task<bool> DeletingTransferReversesBalanceImpact(int seed)
    {
        if (seed <= 0) return true;

        var transfer = Gen.Sample(TransferGenerators.TransferTransaction(), seed % 1000, 1).First();
        var (origin, destination) = Gen.Sample(TransferGenerators.AccountPair(), (seed * 7) % 1000, 1).First();

        // Use zero initial balances to isolate the transfer's impact
        origin.CurrentBalance = 0m;
        destination.CurrentBalance = 0m;

        // Align the transfer's account IDs with the generated accounts
        transfer.AccountId = origin.Id;
        transfer.DestinationAccountId = destination.Id;
        transfer.UserId = origin.UserId;

        var ctx = await TransferGenerators.CreateServiceContext(origin, destination);

        // Step 1: Create the transfer (Amount will be stored as negative)
        await ctx.Service.AddAsync(transfer, isOnline: false);

        // Step 2: Record balances after creation
        decimal originBalanceAfterCreate;
        decimal destBalanceAfterCreate;
        using (var db = await ctx.Factory.CreateDbContextAsync())
        {
            var originAcct = await db.Account.FirstAsync(a => a.Id == origin.Id);
            var destAcct = await db.Account.FirstAsync(a => a.Id == destination.Id);
            originBalanceAfterCreate = originAcct.CurrentBalance;
            destBalanceAfterCreate = destAcct.CurrentBalance;
        }

        // Get the persisted transaction ID for deletion
        int transactionId;
        using (var db = await ctx.Factory.CreateDbContextAsync())
        {
            var persisted = await db.Transaction.FirstAsync();
            transactionId = persisted.Id;
        }

        // Step 3: Delete (soft-delete) the transfer
        await ctx.Service.DeleteAsync(transactionId, isOnline: false);

        // Step 4: Record balances after deletion
        decimal originBalanceAfterDelete;
        decimal destBalanceAfterDelete;
        using (var db = await ctx.Factory.CreateDbContextAsync())
        {
            var originAcct = await db.Account.FirstAsync(a => a.Id == origin.Id);
            var destAcct = await db.Account.FirstAsync(a => a.Id == destination.Id);
            originBalanceAfterDelete = originAcct.CurrentBalance;
            destBalanceAfterDelete = destAcct.CurrentBalance;
        }

        // The transfer amount V = |Amount| (after AddAsync negates positive input)
        decimal V = Math.Abs(originBalanceAfterCreate); // V = |originBalanceAfterCreate| since origin started at 0

        // Verify: origin balance increased by V compared to post-creation state
        bool originIncreasedByV = originBalanceAfterDelete - originBalanceAfterCreate == V;

        // Verify: destination balance decreased by V compared to post-creation state
        bool destDecreasedByV = destBalanceAfterCreate - destBalanceAfterDelete == V;

        // Alternative check: balances returned to initial state (0) since the transaction
        // is now inactive and excluded from sums
        bool originBackToZero = originBalanceAfterDelete == 0m;
        bool destBackToZero = destBalanceAfterDelete == 0m;

        return originIncreasedByV && destDecreasedByV && originBackToZero && destBackToZero;
    }
}
