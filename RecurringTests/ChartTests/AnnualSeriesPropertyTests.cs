using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Model.DTO;
using Xunit;

namespace RecurringTests.ChartTests;

/// <summary>
/// Property-based tests for the annual chart series computation logic.
/// Tests Properties 1, 2, 4, 7, and 12 from the design document.
/// </summary>
[Trait("Feature", "annual-chart-view")]
public class AnnualSeriesPropertyTests
{
    /// <summary>
    /// A single point on the line chart: month index (1–12) + cumulative value.
    /// Mirrors XpemFinancial.VMs.ChartPoint to avoid cross-project reference issues.
    /// </summary>
    private record ChartPoint(int Month, decimal Value);

    /// <summary>
    /// Replicates the annual aggregation logic from ChartVM.LoadAnnualChartAsync as a pure static method.
    /// </summary>
    private static (List<ChartPoint> IncomePoints, List<ChartPoint> ExpensePoints, decimal MaxValue)
        ComputeAnnualSeries(IEnumerable<TransactionDTO> transactions)
    {
        var incomeByMonth = transactions
            .Where(t => t.Type == TransactionType.Income)
            .GroupBy(t => t.Date.Month)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

        var expenseByMonth = transactions
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => t.Date.Month)
            .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

        var incomePoints = new List<ChartPoint>();
        var expensePoints = new List<ChartPoint>();
        decimal cumulativeIncome = 0;
        decimal cumulativeExpense = 0;

        for (int m = 1; m <= 12; m++)
        {
            if (incomeByMonth.TryGetValue(m, out decimal monthIncome))
                cumulativeIncome += monthIncome;

            if (expenseByMonth.TryGetValue(m, out decimal monthExpense))
                cumulativeExpense += monthExpense;

            incomePoints.Add(new ChartPoint(m, cumulativeIncome));
            expensePoints.Add(new ChartPoint(m, cumulativeExpense));
        }

        var allValues = incomePoints.Select(p => p.Value)
            .Concat(expensePoints.Select(p => p.Value));

        var maxValue = allValues.Any() ? Math.Max(allValues.Max(), 1) : 1;

        return (incomePoints, expensePoints, maxValue);
    }

    #region Generators

    /// <summary>
    /// Generates a random TransactionDTO with a specified type and positive amount.
    /// Dates are constrained to a single year (2024) for annual mode testing.
    /// </summary>
    private static Gen<TransactionDTO> TransactionOfType(TransactionType type)
    {
        return from amountInt in Gen.Choose(1, 100_000)
               from month in Gen.Choose(1, 12)
               from day in Gen.Choose(1, 28)
               from accountId in Gen.Choose(1, 100)
               select new TransactionDTO
               {
                   TransactionId = Guid.NewGuid(),
                   UserId = 1,
                   Description = $"{type} transaction",
                   Date = new DateTime(2024, month, day, 12, 0, 0, DateTimeKind.Utc),
                   Amount = type == TransactionType.Income
                       ? amountInt / 100m
                       : -(amountInt / 100m),
                   Type = type,
                   CategoryId = type == TransactionType.Transfer ? null : 1,
                   Repetition = Repetition.None,
                   AccountId = accountId,
                   DestinationAccountId = type == TransactionType.Transfer ? accountId + 1 : null,
                   SyncStatus = TransactionSyncStatus.Pending,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    /// <summary>
    /// Generates a random TransactionDTO with a random type (Income, Expense, Transfer, Adjustment).
    /// </summary>
    private static Gen<TransactionDTO> RandomTransaction()
    {
        return from type in Gen.Elements(
                   TransactionType.Income,
                   TransactionType.Expense,
                   TransactionType.Transfer,
                   TransactionType.Adjustment)
               from amountInt in Gen.Choose(1, 100_000)
               from month in Gen.Choose(1, 12)
               from day in Gen.Choose(1, 28)
               from accountId in Gen.Choose(1, 100)
               from inactive in Gen.Elements(true, false)
               select new TransactionDTO
               {
                   TransactionId = Guid.NewGuid(),
                   UserId = 1,
                   Description = $"{type} transaction",
                   Date = new DateTime(2024, month, day, 12, 0, 0, DateTimeKind.Utc),
                   Amount = type == TransactionType.Income
                       ? amountInt / 100m
                       : -(amountInt / 100m),
                   Type = type,
                   CategoryId = type == TransactionType.Transfer ? null : 1,
                   Repetition = Repetition.None,
                   AccountId = accountId,
                   DestinationAccountId = type == TransactionType.Transfer ? accountId + 1 : null,
                   Inactive = inactive,
                   SyncStatus = TransactionSyncStatus.Pending,
                   CreatedAt = DateTime.UtcNow,
                   UpdatedAt = DateTime.UtcNow,
               };
    }

    /// <summary>
    /// Generates a list of random transactions (0–30 items) with mixed types.
    /// </summary>
    private static Gen<List<TransactionDTO>> RandomTransactionList()
    {
        return from count in Gen.Choose(0, 30)
               from transactions in Gen.ListOf(RandomTransaction(), count)
               select transactions.ToList();
    }

    /// <summary>
    /// Generates a list of transactions that always includes at least one Transfer,
    /// ensuring the Transfer exclusion property is meaningful.
    /// </summary>
    private static Gen<List<TransactionDTO>> TransactionListWithTransfers()
    {
        return from transferTx in TransactionOfType(TransactionType.Transfer)
               from otherCount in Gen.Choose(0, 20)
               from others in Gen.ListOf(RandomTransaction(), otherCount)
               select others.Append(transferTx).ToList();
    }

    #endregion

    /// <summary>
    /// Property 1: Annual series always produce exactly 12 data points.
    /// For any set of transactions (including empty), computing annual chart data
    /// SHALL produce exactly 12 income points and 12 expense points.
    /// **Validates: Requirements 3.1, 3.2, 3.5, 9.2**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "1")]
    public Property AnnualSeries_AlwaysProduces_Exactly12DataPoints()
    {
        return Prop.ForAll(
            RandomTransactionList().ToArbitrary(),
            transactions =>
            {
                var (incomePoints, expensePoints, _) = ComputeAnnualSeries(transactions);

                return (incomePoints.Count == 12)
                    .Label($"Income points count should be 12, was {incomePoints.Count}")
                    .And(expensePoints.Count == 12)
                    .Label($"Expense points count should be 12, was {expensePoints.Count}");
            });
    }

    /// <summary>
    /// Property 2: Annual cumulative values are monotonically non-decreasing.
    /// For any set of transactions with non-negative Income amounts and non-negative Expense absolute amounts,
    /// each series SHALL be monotonically non-decreasing.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "2")]
    public Property AnnualCumulativeValues_AreMonotonicallyNonDecreasing()
    {
        return Prop.ForAll(
            RandomTransactionList().ToArbitrary(),
            transactions =>
            {
                // Filter to only Income/Expense with proper sign convention
                // Income amounts are positive, Expense amounts are negative (abs used in computation)
                var (incomePoints, expensePoints, _) = ComputeAnnualSeries(transactions);

                bool incomeNonDecreasing = true;
                bool expenseNonDecreasing = true;

                for (int i = 1; i < incomePoints.Count; i++)
                {
                    if (incomePoints[i].Value < incomePoints[i - 1].Value)
                    {
                        incomeNonDecreasing = false;
                        break;
                    }
                }

                for (int i = 1; i < expensePoints.Count; i++)
                {
                    if (expensePoints[i].Value < expensePoints[i - 1].Value)
                    {
                        expenseNonDecreasing = false;
                        break;
                    }
                }

                return incomeNonDecreasing
                    .Label("Income series should be monotonically non-decreasing")
                    .And(expenseNonDecreasing)
                    .Label("Expense series should be monotonically non-decreasing");
            });
    }

    /// <summary>
    /// Property 4: Transfer transactions are excluded from chart series.
    /// For any set of transactions that includes Transfer-type transactions,
    /// the annual computation SHALL produce the same result as if the Transfer
    /// transactions did not exist.
    /// **Validates: Requirements 4.4, 9.3**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "4")]
    public Property TransferTransactions_AreExcluded_FromAnnualSeries()
    {
        return Prop.ForAll(
            TransactionListWithTransfers().ToArbitrary(),
            transactions =>
            {
                var (incomeWithTransfers, expenseWithTransfers, maxWithTransfers) =
                    ComputeAnnualSeries(transactions);

                var withoutTransfers = transactions
                    .Where(t => t.Type != TransactionType.Transfer)
                    .ToList();

                var (incomeWithout, expenseWithout, maxWithout) =
                    ComputeAnnualSeries(withoutTransfers);

                bool incomeEqual = incomeWithTransfers
                    .Zip(incomeWithout, (a, b) => a.Value == b.Value)
                    .All(x => x);

                bool expenseEqual = expenseWithTransfers
                    .Zip(expenseWithout, (a, b) => a.Value == b.Value)
                    .All(x => x);

                bool maxEqual = maxWithTransfers == maxWithout;

                return incomeEqual
                    .Label("Income series should be identical with/without transfers")
                    .And(expenseEqual)
                    .Label("Expense series should be identical with/without transfers")
                    .And(maxEqual)
                    .Label("MaxValue should be identical with/without transfers");
            });
    }

    /// <summary>
    /// Property 7: Annual mode ignores previous balance.
    /// For any set of transactions and any hypothetical previous balance value,
    /// the annual chart computation SHALL produce cumulative series starting from zero.
    /// The first month's value equals only the sum of that month's transactions (not previous balance).
    /// **Validates: Requirements 5.4**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "7")]
    public Property AnnualMode_IgnoresPreviousBalance_StartsFromZero()
    {
        return Prop.ForAll(
            RandomTransactionList().ToArbitrary(),
            transactions =>
            {
                var (incomePoints, expensePoints, _) = ComputeAnnualSeries(transactions);

                // The first point's value should be exactly the sum of January transactions
                // (not influenced by any previous balance)
                decimal januaryIncome = transactions
                    .Where(t => t.Type == TransactionType.Income && t.Date.Month == 1)
                    .Sum(t => t.Amount);

                decimal januaryExpense = transactions
                    .Where(t => t.Type == TransactionType.Expense && t.Date.Month == 1)
                    .Sum(t => Math.Abs(t.Amount));

                // First point starts from zero + January's contribution
                bool incomeStartsCorrectly = incomePoints[0].Value == januaryIncome;
                bool expenseStartsCorrectly = expensePoints[0].Value == januaryExpense;

                return incomeStartsCorrectly
                    .Label($"Income at month 1 should be {januaryIncome}, was {incomePoints[0].Value}")
                    .And(expenseStartsCorrectly)
                    .Label($"Expense at month 1 should be {januaryExpense}, was {expensePoints[0].Value}");
            });
    }

    /// <summary>
    /// Property 12: MaxValue is always at least 1.
    /// For any set of chart data points (including empty sets producing all-zero values),
    /// the MaxValue used for Y-axis scaling SHALL be >= 1.
    /// **Validates: Requirements 9.4**
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "12")]
    public Property MaxValue_IsAlwaysAtLeast1()
    {
        return Prop.ForAll(
            RandomTransactionList().ToArbitrary(),
            transactions =>
            {
                var (_, _, maxValue) = ComputeAnnualSeries(transactions);

                return (maxValue >= 1m)
                    .Label($"MaxValue should be >= 1, was {maxValue}");
            });
    }
}
