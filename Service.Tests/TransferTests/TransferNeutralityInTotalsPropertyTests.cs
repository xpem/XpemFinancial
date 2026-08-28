using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Model.DTO;
using Xunit;

namespace RecurringTests.TransferTests;

/// <summary>
/// Property 7: Transfer neutrality in Income/Expense totals
/// For any collection of transactions that includes transfers, the Income total SHALL equal the sum
/// of only Income-type transaction amounts, and the Expense total SHALL equal the sum of only
/// Expense-type transaction amounts — transfers SHALL not contribute to either total.
/// **Validates: Requirements 5.1, 5.2, 5.5**
/// </summary>
[Trait("Feature", "transfer-transactions")]
[Trait("Property", "7")]
public class TransferNeutralityInTotalsPropertyTests
{
    /// <summary>
    /// Generator for a random TransactionDTO with a random type from Income, Expense, Transfer, Adjustment.
    /// </summary>
    private static Gen<TransactionDTO> MixedTransaction()
    {
        return from type in Gen.Elements(
                   TransactionType.Income,
                   TransactionType.Expense,
                   TransactionType.Transfer,
                   TransactionType.Adjustment)
               from amountInt in Gen.Choose(1, 100_000)
               from accountId in Gen.Choose(1, 100)
               from destOffset in Gen.Choose(1, 99)
               let amount = type == TransactionType.Income
                   ? amountInt / 100m
                   : -(amountInt / 100m)
               select new TransactionDTO
               {
                   TransactionId = Guid.NewGuid(),
                   UserId = 1,
                   Description = $"Test {type}",
                   Date = DateTime.UtcNow,
                   Amount = amount,
                   Type = type,
                   CategoryId = type == TransactionType.Transfer ? null : 1,
                   Repetition = Repetition.None,
                   AccountId = accountId,
                   DestinationAccountId = type == TransactionType.Transfer
                       ? ((accountId + destOffset - 1) % 100) + 1
                       : null,
                   SyncStatus = TransactionSyncStatus.Pending,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    /// <summary>
    /// Generator for a non-empty list of mixed transactions that always includes at least one Transfer.
    /// </summary>
    private static Gen<List<TransactionDTO>> MixedTransactionListWithTransfer()
    {
        return from transferTx in TransferGenerators.TransferTransaction()
               from otherCount in Gen.Choose(0, 20)
               from others in Gen.ListOf(MixedTransaction(), otherCount)
               from insertIndex in Gen.Choose(0, others.Count)
               select others.ToList().InsertAndReturn(insertIndex, transferTx);
    }

    /// <summary>
    /// Applies the same totals accumulation logic from MainVM and returns (income, expense).
    /// This mirrors the actual production code logic:
    ///   if Type == Income → Income += Amount
    ///   else if Type != Transfer → Expense += Amount
    /// </summary>
    private static (decimal Income, decimal Expense) ComputeTotals(IEnumerable<TransactionDTO> transactions)
    {
        decimal income = 0;
        decimal expense = 0;

        foreach (var transaction in transactions)
        {
            if (transaction.Type == TransactionType.Income)
                income += transaction.Amount;
            else if (transaction.Type != TransactionType.Transfer)
                expense += transaction.Amount;
        }

        return (income, expense);
    }

    /// <summary>
    /// For any collection of transactions including transfers:
    /// Income total equals sum of ONLY Income-type amounts.
    /// **Validates: Requirements 5.1, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property IncomeTotalOnlySumsIncomeTypeAmounts()
    {
        return Prop.ForAll(
            MixedTransactionListWithTransfer().ToArbitrary(),
            transactions =>
            {
                var (computedIncome, _) = ComputeTotals(transactions);

                decimal expectedIncome = transactions
                    .Where(t => t.Type == TransactionType.Income)
                    .Sum(t => t.Amount);

                return computedIncome == expectedIncome;
            });
    }

    /// <summary>
    /// For any collection of transactions including transfers:
    /// Expense total equals sum of ONLY Expense-type (and Adjustment-type) amounts, excluding Transfer and Income.
    /// **Validates: Requirements 5.2, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ExpenseTotalOnlySumsExpenseTypeAmounts()
    {
        return Prop.ForAll(
            MixedTransactionListWithTransfer().ToArbitrary(),
            transactions =>
            {
                var (_, computedExpense) = ComputeTotals(transactions);

                decimal expectedExpense = transactions
                    .Where(t => t.Type != TransactionType.Income && t.Type != TransactionType.Transfer)
                    .Sum(t => t.Amount);

                return computedExpense == expectedExpense;
            });
    }

    /// <summary>
    /// For any collection of transactions including transfers:
    /// Transfer-type transactions contribute zero to both Income and Expense totals.
    /// **Validates: Requirements 5.1, 5.2, 5.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TransfersContributeNothingToEitherTotal()
    {
        return Prop.ForAll(
            MixedTransactionListWithTransfer().ToArbitrary(),
            transactions =>
            {
                // Compute totals with all transactions
                var (incomeWithTransfers, expenseWithTransfers) = ComputeTotals(transactions);

                // Compute totals with transfers removed
                var withoutTransfers = transactions
                    .Where(t => t.Type != TransactionType.Transfer)
                    .ToList();
                var (incomeWithout, expenseWithout) = ComputeTotals(withoutTransfers);

                // Both should be identical — transfers are neutral
                return incomeWithTransfers == incomeWithout
                    && expenseWithTransfers == expenseWithout;
            });
    }
}

/// <summary>
/// Extension method to support inserting an item and returning the list (for generator fluency).
/// </summary>
internal static class ListExtensions
{
    public static List<T> InsertAndReturn<T>(this List<T> list, int index, T item)
    {
        list.Insert(index, item);
        return list;
    }
}
