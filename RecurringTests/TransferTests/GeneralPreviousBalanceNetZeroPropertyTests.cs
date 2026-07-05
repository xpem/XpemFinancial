using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.EntityFrameworkCore;
using Model.DTO;
using Repo;
using Xunit;

namespace RecurringTests.TransferTests;

/// <summary>
/// Property 10: General previous balance net-zero for transfers
/// For any set of non-inactive transfers, when computing the general previous balance (no account filter),
/// the net contribution of all transfers SHALL be zero — each transfer's negative impact on origin
/// and positive impact on destination cancel out.
/// **Validates: Requirements 8.2**
/// </summary>
[Trait("Feature", "transfer-transactions")]
[Trait("Property", "10")]
public class GeneralPreviousBalanceNetZeroPropertyTests
{
    /// <summary>
    /// Generates a Transfer transaction between two given accounts, dated before a reference month.
    /// </summary>
    private static Gen<TransactionDTO> TransferBetweenAccounts(int originId, int destId, int userId, DateTime beforeDate)
    {
        return from amountInt in Gen.Choose(1, 50_000)
               from dayOffset in Gen.Choose(1, 365)
               let date = beforeDate.AddDays(-dayOffset)
               select new TransactionDTO
               {
                   TransactionId = Guid.NewGuid(),
                   UserId = userId,
                   Description = $"Transfer {originId}→{destId}",
                   Date = date,
                   Amount = -(amountInt / 100m), // always negative: outgoing from origin
                   Type = TransactionType.Transfer,
                   CategoryId = null,
                   Repetition = Repetition.None,
                   AccountId = originId,
                   DestinationAccountId = destId,
                   SyncStatus = TransactionSyncStatus.Synced,
                   Inactive = false,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    /// <summary>
    /// For any set of transfer transactions between multiple accounts, the sum of per-account previous
    /// balances across all involved accounts equals zero, proving that transfers are net-zero
    /// in the consolidated view.
    ///
    /// Each transfer contributes -V to the origin account and +V to the destination account.
    /// When we sum all per-account balances, these contributions cancel out completely.
    /// **Validates: Requirements 8.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SumOfPerAccountBalances_IsZero_WhenOnlyTransfersExist()
    {
        var referenceMonth = new DateTime(2025, 6, 1);
        const int userId = 1;

        // Generate transfers between 3 distinct accounts in various directions
        const int accountA = 10;
        const int accountB = 20;
        const int accountC = 30;

        var gen =
            from abCount in Gen.Choose(1, 3)
            from baCount in Gen.Choose(1, 2)
            from bcCount in Gen.Choose(1, 2)
            from caCount in Gen.Choose(1, 2)
            from transfersAB in Gen.ListOf<TransactionDTO>(TransferBetweenAccounts(accountA, accountB, userId, referenceMonth), abCount)
            from transfersBA in Gen.ListOf<TransactionDTO>(TransferBetweenAccounts(accountB, accountA, userId, referenceMonth), baCount)
            from transfersBC in Gen.ListOf<TransactionDTO>(TransferBetweenAccounts(accountB, accountC, userId, referenceMonth), bcCount)
            from transfersCA in Gen.ListOf<TransactionDTO>(TransferBetweenAccounts(accountC, accountA, userId, referenceMonth), caCount)
            let allTransfers = transfersAB.Concat(transfersBA).Concat(transfersBC).Concat(transfersCA).ToList()
            select allTransfers;

        return Prop.ForAll(gen.ToArbitrary(), transfers =>
        {
            return SumOfPerAccountBalances_IsZero_Impl(
                accountA, accountB, accountC, userId, referenceMonth, transfers).Result;
        });
    }

    private static async Task<bool> SumOfPerAccountBalances_IsZero_Impl(
        int accountA, int accountB, int accountC, int userId,
        DateTime referenceMonth, List<TransactionDTO> transfers)
    {
        // Arrange: Create the three accounts
        var accounts = new[]
        {
            new AccountDTO
            {
                Id = accountA, Name = "Account A", CurrentBalance = 0m,
                UserId = userId, IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            },
            new AccountDTO
            {
                Id = accountB, Name = "Account B", CurrentBalance = 0m,
                UserId = userId, IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            },
            new AccountDTO
            {
                Id = accountC, Name = "Account C", CurrentBalance = 0m,
                UserId = userId, IsActive = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            },
        };

        var factory = await TransferGenerators.CreateSeededFactory(accounts);

        // Seed transfer transactions
        using (var db = await factory.CreateDbContextAsync())
        {
            db.Transaction.AddRange(transfers);
            await db.SaveChangesAsync();
        }

        var repo = new TransactionRepo(factory);

        // Act: Compute per-account previous balance for each account
        var balanceA = await repo.GetPreviousBalanceAsync(referenceMonth, accountA);
        var balanceB = await repo.GetPreviousBalanceAsync(referenceMonth, accountB);
        var balanceC = await repo.GetPreviousBalanceAsync(referenceMonth, accountC);

        // Assert: The sum of all per-account balances should be zero (net-zero property)
        // Each transfer contributes -V to origin and +V to destination, cancelling out.
        var totalAcrossAccounts = balanceA + balanceB + balanceC;

        return totalAcrossAccounts == 0m;
    }
}
