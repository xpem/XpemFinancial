using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Repo;
using Xunit;

namespace RecurringTests.TransferTests;

/// <summary>
/// Property 9: Per-account previous balance includes transfers as origin and destination
/// For any account and any reference month, the previous balance calculation SHALL include
/// the Amount of all non-inactive transfers where the account is the origin (contributing the negative Amount)
/// AND the inverted Amount of all non-inactive transfers where the account is the destination
/// (contributing the positive value).
/// **Validates: Requirements 8.1**
/// </summary>
[Trait("Feature", "transfer-transactions")]
[Trait("Property", "9")]
public class PerAccountPreviousBalancePropertyTests
{
    /// <summary>
    /// Generates a non-Transfer transaction (Income or Expense) for a given account dated before a reference month.
    /// </summary>
    private static Gen<TransactionDTO> NonTransferTransaction(int accountId, int userId, DateTime beforeDate)
    {
        return from type in Gen.Elements(TransactionType.Income, TransactionType.Expense)
               from amountInt in Gen.Choose(1, 50_000)
               from dayOffset in Gen.Choose(1, 365)
               let date = beforeDate.AddDays(-dayOffset)
               let amount = type == TransactionType.Income ? amountInt / 100m : -(amountInt / 100m)
               select new TransactionDTO
               {
                   TransactionId = Guid.NewGuid(),
                   UserId = userId,
                   Description = $"Txn {type}",
                   Date = date,
                   Amount = amount,
                   Type = type,
                   CategoryId = 1,
                   Repetition = Repetition.None,
                   AccountId = accountId,
                   DestinationAccountId = null,
                   SyncStatus = TransactionSyncStatus.Synced,
                   Inactive = false,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    /// <summary>
    /// Generates a Transfer transaction originating from a given account, dated before a reference month.
    /// </summary>
    private static Gen<TransactionDTO> TransferFromAccount(int originAccountId, int destAccountId, int userId, DateTime beforeDate)
    {
        return from amountInt in Gen.Choose(1, 50_000)
               from dayOffset in Gen.Choose(1, 365)
               let date = beforeDate.AddDays(-dayOffset)
               select new TransactionDTO
               {
                   TransactionId = Guid.NewGuid(),
                   UserId = userId,
                   Description = "Transfer out",
                   Date = date,
                   Amount = -(amountInt / 100m), // negative: outgoing from origin
                   Type = TransactionType.Transfer,
                   CategoryId = null,
                   Repetition = Repetition.None,
                   AccountId = originAccountId,
                   DestinationAccountId = destAccountId,
                   SyncStatus = TransactionSyncStatus.Synced,
                   Inactive = false,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    /// <summary>
    /// Generates a Transfer transaction where a given account is the destination, dated before a reference month.
    /// </summary>
    private static Gen<TransactionDTO> TransferToAccount(int destAccountId, int originAccountId, int userId, DateTime beforeDate)
    {
        return from amountInt in Gen.Choose(1, 50_000)
               from dayOffset in Gen.Choose(1, 365)
               let date = beforeDate.AddDays(-dayOffset)
               select new TransactionDTO
               {
                   TransactionId = Guid.NewGuid(),
                   UserId = userId,
                   Description = "Transfer in",
                   Date = date,
                   Amount = -(amountInt / 100m), // negative: stored as outgoing from origin
                   Type = TransactionType.Transfer,
                   CategoryId = null,
                   Repetition = Repetition.None,
                   AccountId = originAccountId,
                   DestinationAccountId = destAccountId,
                   SyncStatus = TransactionSyncStatus.Synced,
                   Inactive = false,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    /// <summary>
    /// For any account with a mix of transfers (as origin and destination) plus non-transfer transactions,
    /// the per-account previous balance equals:
    ///   sum(Amount where AccountId==account) + sum(-Amount where DestinationAccountId==account and Type==Transfer)
    /// **Validates: Requirements 8.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PreviousBalance_IncludesTransfersAsOriginAndDestination()
    {
        var referenceMonth = new DateTime(2025, 6, 1); // reference month: June 2025
        const int targetAccountId = 10;
        const int otherAccountId = 20;
        const int userId = 1;

        var gen =
            from nonTransferCount in Gen.Choose(1, 5)
            from transferOutCount in Gen.Choose(1, 3)
            from transferInCount in Gen.Choose(1, 3)
            from nonTransfers in Gen.ListOf<TransactionDTO>(NonTransferTransaction(targetAccountId, userId, referenceMonth), nonTransferCount)
            from transfersOut in Gen.ListOf<TransactionDTO>(TransferFromAccount(targetAccountId, otherAccountId, userId, referenceMonth), transferOutCount)
            from transfersIn in Gen.ListOf<TransactionDTO>(TransferToAccount(targetAccountId, otherAccountId, userId, referenceMonth), transferInCount)
            select (NonTransfers: nonTransfers.ToList(), TransfersOut: transfersOut.ToList(), TransfersIn: transfersIn.ToList());

        return Prop.ForAll(gen.ToArbitrary(), data =>
        {
            return PreviousBalance_IncludesTransfers_Impl(
                targetAccountId, otherAccountId, userId, referenceMonth,
                data.NonTransfers, data.TransfersOut, data.TransfersIn).Result;
        });
    }

    private static async Task<bool> PreviousBalance_IncludesTransfers_Impl(
        int targetAccountId, int otherAccountId, int userId, DateTime referenceMonth,
        List<TransactionDTO> nonTransfers, List<TransactionDTO> transfersOut, List<TransactionDTO> transfersIn)
    {
        // Arrange: Create accounts and seed the in-memory DB
        var targetAccount = new AccountDTO
        {
            Id = targetAccountId,
            Name = "Target Account",
            CurrentBalance = 0m,
            UserId = userId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var otherAccount = new AccountDTO
        {
            Id = otherAccountId,
            Name = "Other Account",
            CurrentBalance = 0m,
            UserId = userId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var factory = await TransferGenerators.CreateSeededFactory(targetAccount, otherAccount);

        // Seed transactions directly in the DB
        var allTransactions = new List<TransactionDTO>();
        allTransactions.AddRange(nonTransfers);
        allTransactions.AddRange(transfersOut);
        allTransactions.AddRange(transfersIn);

        using (var db = await factory.CreateDbContextAsync())
        {
            db.Transaction.AddRange(allTransactions);
            await db.SaveChangesAsync();
        }

        // Create TransactionRepo directly
        var repo = new TransactionRepo(factory);

        // Act: Call GetPreviousBalanceAsync with the target account
        var actualBalance = await repo.GetPreviousBalanceAsync(referenceMonth, targetAccountId);

        // Expected: sum(Amount where AccountId == targetAccountId)
        //         + sum(-Amount where DestinationAccountId == targetAccountId and Type == Transfer)
        var expectedOriginSum = allTransactions
            .Where(t => t.AccountId == targetAccountId && !t.Inactive)
            .Sum(t => t.Amount);

        var expectedDestinationSum = allTransactions
            .Where(t => t.DestinationAccountId == targetAccountId && !t.Inactive && t.Type == TransactionType.Transfer)
            .Sum(t => -t.Amount);

        var expectedBalance = expectedOriginSum + expectedDestinationSum;

        return actualBalance == expectedBalance;
    }
}
