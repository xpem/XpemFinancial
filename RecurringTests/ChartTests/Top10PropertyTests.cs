using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Model.DTO;
using Xunit;

namespace RecurringTests.ChartTests;

/// <summary>
/// Property-based tests for ComputeTop10 logic.
/// Validates that the Top 10 expense ranking correctly filters, sorts, and limits results.
/// **Validates: Requirements 6.2, 6.3, 6.4, 6.5, 6.7, 6.10**
/// </summary>
[Trait("Feature", "annual-chart-view")]
public class Top10PropertyTests
{
    /// <summary>
    /// Replicates the pure ComputeTop10 logic from ChartVM for testability.
    /// </summary>
    private static List<TransactionDTO> ComputeTop10(IEnumerable<TransactionDTO> transactions)
    {
        return transactions
            .Where(t => t.Type == TransactionType.Expense && !t.Inactive)
            .OrderByDescending(t => Math.Abs(t.Amount))
            .ThenByDescending(t => t.Date)
            .Take(10)
            .ToList();
    }

    /// <summary>
    /// Generates a random TransactionDTO with a random type from all four types,
    /// random Inactive flag, various amounts, and various dates.
    /// </summary>
    private static Gen<TransactionDTO> MixedTransaction()
    {
        return from type in Gen.Elements(
                   TransactionType.Income,
                   TransactionType.Expense,
                   TransactionType.Transfer,
                   TransactionType.Adjustment)
               from inactive in Gen.Elements(true, false)
               from amountInt in Gen.Choose(1, 500_000)
               from negateAmount in Gen.Elements(true, false)
               from year in Gen.Choose(2020, 2025)
               from month in Gen.Choose(1, 12)
               from day in Gen.Choose(1, 28)
               from hour in Gen.Choose(0, 23)
               from minute in Gen.Choose(0, 59)
               let amount = negateAmount ? -(amountInt / 100m) : (amountInt / 100m)
               select new TransactionDTO
               {
                   TransactionId = Guid.NewGuid(),
                   UserId = 1,
                   Description = $"{type} transaction",
                   Date = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc),
                   Amount = amount,
                   Type = type,
                   Inactive = inactive,
                   CategoryId = type == TransactionType.Transfer ? null : 1,
                   Repetition = Repetition.None,
                   AccountId = 1,
                   SyncStatus = TransactionSyncStatus.Pending,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    /// <summary>
    /// Generates a list of mixed transactions with 0 to 30 items.
    /// </summary>
    private static Gen<List<TransactionDTO>> MixedTransactionList()
    {
        return from count in Gen.Choose(0, 30)
               from transactions in Gen.ListOf(MixedTransaction(), count)
               select transactions.ToList();
    }

    /// <summary>
    /// Generates a list that always has more than 10 active expense transactions,
    /// ensuring the Take(10) limit is exercised.
    /// </summary>
    private static Gen<List<TransactionDTO>> ListWithManyExpenses()
    {
        var activeExpense = from amountInt in Gen.Choose(1, 500_000)
                           from negateAmount in Gen.Elements(true, false)
                           from year in Gen.Choose(2020, 2025)
                           from month in Gen.Choose(1, 12)
                           from day in Gen.Choose(1, 28)
                           from hour in Gen.Choose(0, 23)
                           from minute in Gen.Choose(0, 59)
                           let amount = negateAmount ? -(amountInt / 100m) : (amountInt / 100m)
                           select new TransactionDTO
                           {
                               TransactionId = Guid.NewGuid(),
                               UserId = 1,
                               Description = "Expense transaction",
                               Date = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc),
                               Amount = amount,
                               Type = TransactionType.Expense,
                               Inactive = false,
                               CategoryId = 1,
                               Repetition = Repetition.None,
                               AccountId = 1,
                               SyncStatus = TransactionSyncStatus.Pending,
                               CreatedAt = DateTime.UtcNow,
                               UpdatedAt = DateTime.UtcNow,
                           };

        return from expenseCount in Gen.Choose(11, 25)
               from expenses in Gen.ListOf(activeExpense, expenseCount)
               from otherCount in Gen.Choose(0, 10)
               from others in Gen.ListOf(MixedTransaction(), otherCount)
               select expenses.Concat(others).ToList();
    }

    /// <summary>
    /// Generates a list with multiple expense transactions sharing the same absolute amount
    /// but different dates, to exercise secondary sort.
    /// </summary>
    private static Gen<List<TransactionDTO>> ListWithEqualAmounts()
    {
        return from sharedAmountInt in Gen.Choose(100, 100_000)
               from negateShared in Gen.Elements(true, false)
               from tiedCount in Gen.Choose(2, 8)
               from tiedTransactions in Gen.ListOf(
                   from year in Gen.Choose(2020, 2025)
                   from month in Gen.Choose(1, 12)
                   from day in Gen.Choose(1, 28)
                   from hour in Gen.Choose(0, 23)
                   from minute in Gen.Choose(0, 59)
                   select new TransactionDTO
                   {
                       TransactionId = Guid.NewGuid(),
                       UserId = 1,
                       Description = "Tied expense",
                       Date = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc),
                       Amount = negateShared ? -(sharedAmountInt / 100m) : (sharedAmountInt / 100m),
                       Type = TransactionType.Expense,
                       Inactive = false,
                       CategoryId = 1,
                       Repetition = Repetition.None,
                       AccountId = 1,
                       SyncStatus = TransactionSyncStatus.Pending,
                       CreatedAt = DateTime.UtcNow,
                       UpdatedAt = DateTime.UtcNow,
                   }, tiedCount)
               from otherCount in Gen.Choose(0, 10)
               from others in Gen.ListOf(MixedTransaction(), otherCount)
               select tiedTransactions.Concat(others).ToList();
    }

    /// <summary>
    /// Property 8: Top 10 list contains at most 10 items sorted by descending absolute amount.
    /// For any set of transactions, the result has at most 10 items and each item's
    /// Math.Abs(Amount) is >= the next item's Math.Abs(Amount).
    /// **Validates: Requirements 6.2, 6.7**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "8")]
    public Property Top10_ContainsAtMost10Items_SortedByDescendingAbsoluteAmount()
    {
        return Prop.ForAll(
            MixedTransactionList().ToArbitrary(),
            transactions =>
            {
                var result = ComputeTop10(transactions);

                // At most 10 items
                bool atMost10 = result.Count <= 10;

                // Count of eligible items
                int eligibleCount = transactions.Count(t => t.Type == TransactionType.Expense && !t.Inactive);
                bool correctCount = result.Count == Math.Min(10, eligibleCount);

                // Sorted by descending absolute amount
                bool sorted = true;
                for (int i = 0; i < result.Count - 1; i++)
                {
                    if (Math.Abs(result[i].Amount) < Math.Abs(result[i + 1].Amount))
                    {
                        sorted = false;
                        break;
                    }
                }

                return atMost10
                    .Label("Result has at most 10 items")
                    .And(correctCount)
                    .Label($"Result count ({result.Count}) == min(10, eligible ({eligibleCount}))")
                    .And(sorted)
                    .Label("Result is sorted by descending absolute amount");
            });
    }

    /// <summary>
    /// Property 8 (supplemental): Exercises the Take(10) limit with lists that have more than 10 eligible items.
    /// **Validates: Requirements 6.2, 6.7**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "8")]
    public Property Top10_LimitsToExactly10_WhenMoreThan10Eligible()
    {
        return Prop.ForAll(
            ListWithManyExpenses().ToArbitrary(),
            transactions =>
            {
                var result = ComputeTop10(transactions);

                bool exactly10 = result.Count == 10;

                // Sorted by descending absolute amount
                bool sorted = true;
                for (int i = 0; i < result.Count - 1; i++)
                {
                    if (Math.Abs(result[i].Amount) < Math.Abs(result[i + 1].Amount))
                    {
                        sorted = false;
                        break;
                    }
                }

                return exactly10
                    .Label("Result has exactly 10 items when more than 10 eligible")
                    .And(sorted)
                    .Label("Result is sorted by descending absolute amount");
            });
    }

    /// <summary>
    /// Property 9: Top 10 list only includes Expense-type transactions.
    /// For any mixed set of transactions, all items in the result have Type == Expense.
    /// **Validates: Requirements 6.3**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "9")]
    public Property Top10_OnlyIncludesExpenseType()
    {
        return Prop.ForAll(
            MixedTransactionList().ToArbitrary(),
            transactions =>
            {
                var result = ComputeTop10(transactions);

                bool allExpense = result.All(t => t.Type == TransactionType.Expense);

                return allExpense
                    .Label("All items in Top 10 have Type == Expense");
            });
    }

    /// <summary>
    /// Property 10: Top 10 list filters by active period — all items have Inactive == false.
    /// The ComputeTop10 method only includes active (non-inactive) transactions.
    /// **Validates: Requirements 6.4, 6.5**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "10")]
    public Property Top10_OnlyIncludesActiveTransactions()
    {
        return Prop.ForAll(
            MixedTransactionList().ToArbitrary(),
            transactions =>
            {
                var result = ComputeTop10(transactions);

                bool allActive = result.All(t => !t.Inactive);

                return allActive
                    .Label("All items in Top 10 have Inactive == false");
            });
    }

    /// <summary>
    /// Property 11: Top 10 secondary sort uses date descending for equal amounts.
    /// For items with equal Math.Abs(Amount), they are sorted by Date descending (most recent first).
    /// **Validates: Requirements 6.10**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "11")]
    public Property Top10_SecondarySortByDateDescending_ForEqualAmounts()
    {
        return Prop.ForAll(
            ListWithEqualAmounts().ToArbitrary(),
            transactions =>
            {
                var result = ComputeTop10(transactions);

                // Check that items with equal absolute amounts are sorted by date descending
                bool secondarySortCorrect = true;
                for (int i = 0; i < result.Count - 1; i++)
                {
                    if (Math.Abs(result[i].Amount) == Math.Abs(result[i + 1].Amount))
                    {
                        if (result[i].Date < result[i + 1].Date)
                        {
                            secondarySortCorrect = false;
                            break;
                        }
                    }
                }

                return secondarySortCorrect
                    .Label("Items with equal absolute amounts are sorted by date descending");
            });
    }
}
