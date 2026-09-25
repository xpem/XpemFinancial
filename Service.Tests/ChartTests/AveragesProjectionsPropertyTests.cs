using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Model.DTO;
using Xunit;

namespace RecurringTests.ChartTests;

/// <summary>
/// Property-based tests for the "Médias e Projeções" screen's pure computation logic.
/// Mirrors XpemFinancial.VMs.AveragesProjectionsVM.LoadAsync's aggregation/average/projection
/// logic as a pure static method (same approach as AnnualSeriesPropertyTests, to avoid
/// cross-project reference issues between Service.Tests and the XpemFinancial MAUI project).
/// </summary>
[Trait("Feature", "averages-projections")]
public class AveragesProjectionsPropertyTests
{
    private record ChartPoint(int Month, decimal Value);

    private const int HistoryMonths = 6;
    private const int ProjectionMonths = 3;

    // Fixed reference "today" so the 6-month window (Jan..Jun/2024) is deterministic across test runs.
    private static readonly DateTime Today = new(2024, 6, 15);

    /// <summary>
    /// Replicates AveragesProjectionsVM.LoadAsync's pure aggregation/average/projection logic.
    /// </summary>
    private static (List<ChartPoint> IncomePoints, List<ChartPoint> ExpensePoints,
        decimal AverageIncome, decimal AverageExpense, decimal AverageBalance, decimal MaxValue)
        ComputeAveragesProjections(IEnumerable<TransactionDTO> transactions)
    {
        var from = new DateTime(Today.Year, Today.Month, 1).AddMonths(-(HistoryMonths - 1));

        var months = Enumerable.Range(0, HistoryMonths)
            .Select(i => from.AddMonths(i))
            .ToList();

        var incomeByMonth = transactions
            .Where(t => t.Type == TransactionType.Income)
            .GroupBy(t => (t.Date.Year, t.Date.Month))
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

        var expenseByMonth = transactions
            .Where(t => t.Type == TransactionType.Expense)
            .GroupBy(t => (t.Date.Year, t.Date.Month))
            .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

        var incomeTotals = months
            .Select(m => incomeByMonth.TryGetValue((m.Year, m.Month), out var v) ? v : 0)
            .ToList();

        var expenseTotals = months
            .Select(m => expenseByMonth.TryGetValue((m.Year, m.Month), out var v) ? v : 0)
            .ToList();

        decimal averageIncome = incomeTotals.Average();
        decimal averageExpense = expenseTotals.Average();
        decimal averageBalance = averageIncome - averageExpense;

        var incomePoints = new List<ChartPoint>();
        var expensePoints = new List<ChartPoint>();

        for (int i = 0; i < HistoryMonths; i++)
        {
            incomePoints.Add(new ChartPoint(i + 1, incomeTotals[i]));
            expensePoints.Add(new ChartPoint(i + 1, expenseTotals[i]));
        }

        for (int i = 0; i < ProjectionMonths; i++)
        {
            incomePoints.Add(new ChartPoint(HistoryMonths + i + 1, averageIncome));
            expensePoints.Add(new ChartPoint(HistoryMonths + i + 1, averageExpense));
        }

        var allValues = incomePoints.Select(p => p.Value).Concat(expensePoints.Select(p => p.Value));
        decimal maxValue = allValues.Any() ? Math.Max(allValues.Max(), 1) : 1;

        return (incomePoints, expensePoints, averageIncome, averageExpense, averageBalance, maxValue);
    }

    #region Generators

    /// <summary>
    /// Generates a random TransactionDTO with a specified type and positive amount,
    /// dated within the fixed 6-month window (Jan..Jun/2024).
    /// </summary>
    private static Gen<TransactionDTO> TransactionOfType(TransactionType type)
    {
        return from amountInt in Gen.Choose(1, 100_000)
               from month in Gen.Choose(1, 6)
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
    /// Generates a random TransactionDTO with a random type (Income, Expense, Transfer, Adjustment)
    /// and a random Inactive flag, dated within the fixed 6-month window.
    /// </summary>
    private static Gen<TransactionDTO> RandomTransaction()
    {
        return from type in Gen.Elements(
                   TransactionType.Income,
                   TransactionType.Expense,
                   TransactionType.Transfer,
                   TransactionType.Adjustment)
               from amountInt in Gen.Choose(1, 100_000)
               from month in Gen.Choose(1, 6)
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
    /// Generates a list of random transactions (0–30 items) with mixed types and Inactive flags.
    /// </summary>
    private static Gen<List<TransactionDTO>> RandomTransactionList()
    {
        return from count in Gen.Choose(0, 30)
               from transactions in Gen.ListOf(RandomTransaction(), count)
               select transactions.ToList();
    }

    /// <summary>
    /// Generates a list of transactions that always includes at least one Transfer and one
    /// Adjustment, so the exclusion property is meaningful. Note: Inactive filtering is a
    /// repo-layer concern (see TransactionRepo.GetByDateRangeAsync's `!t.Inactive` clause,
    /// mirrored from GetByMonthYear/GetByYear) — by the time transactions reach this pure
    /// aggregation logic they are already assumed active, exactly like ChartVM's monthly/annual
    /// aggregation, so Inactive is not re-checked here.
    /// </summary>
    private static Gen<List<TransactionDTO>> TransactionListWithExclusions()
    {
        return from transferTx in TransactionOfType(TransactionType.Transfer)
               from adjustmentTx in TransactionOfType(TransactionType.Adjustment)
               from otherCount in Gen.Choose(0, 15)
               from others in Gen.ListOf(RandomTransaction(), otherCount)
               select others
                   .Append(transferTx)
                   .Append(adjustmentTx)
                   .ToList();
    }

    #endregion

    /// <summary>
    /// Property 1: Always produces exactly 6 historical points + 3 projected points (9 total)
    /// per series, for any set of transactions (including empty).
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "1")]
    public Property AlwaysProduces_Exactly6RealPlus3ProjectedPoints()
    {
        return Prop.ForAll(
            RandomTransactionList().ToArbitrary(),
            transactions =>
            {
                var result = ComputeAveragesProjections(transactions);

                return (result.IncomePoints.Count == HistoryMonths + ProjectionMonths)
                    .Label($"Income points count should be 9, was {result.IncomePoints.Count}")
                    .And(result.ExpensePoints.Count == HistoryMonths + ProjectionMonths)
                    .Label($"Expense points count should be 9, was {result.ExpensePoints.Count}");
            });
    }

    /// <summary>
    /// Property 2: The 3 projected points are always exactly equal to AverageIncome/AverageExpense
    /// (flat projection).
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "2")]
    public Property ProjectedPoints_AreAlwaysExactlyTheAverage()
    {
        return Prop.ForAll(
            RandomTransactionList().ToArbitrary(),
            transactions =>
            {
                var result = ComputeAveragesProjections(transactions);

                var projectedIncome = result.IncomePoints.Skip(HistoryMonths);
                var projectedExpense = result.ExpensePoints.Skip(HistoryMonths);

                bool incomeFlat = projectedIncome.All(p => p.Value == result.AverageIncome);
                bool expenseFlat = projectedExpense.All(p => p.Value == result.AverageExpense);

                return incomeFlat
                    .Label("All projected income points should equal AverageIncome")
                    .And(expenseFlat)
                    .Label("All projected expense points should equal AverageExpense");
            });
    }

    /// <summary>
    /// Property 3: AverageIncome/AverageExpense are always the arithmetic mean of the 6 real
    /// monthly totals.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "3")]
    public Property Averages_AreAlwaysTheMeanOfThe6RealMonthlyTotals()
    {
        return Prop.ForAll(
            RandomTransactionList().ToArbitrary(),
            transactions =>
            {
                var result = ComputeAveragesProjections(transactions);

                decimal expectedAvgIncome = result.IncomePoints.Take(HistoryMonths).Average(p => p.Value);
                decimal expectedAvgExpense = result.ExpensePoints.Take(HistoryMonths).Average(p => p.Value);

                return (result.AverageIncome == expectedAvgIncome)
                    .Label($"AverageIncome should be {expectedAvgIncome}, was {result.AverageIncome}")
                    .And(result.AverageExpense == expectedAvgExpense)
                    .Label($"AverageExpense should be {expectedAvgExpense}, was {result.AverageExpense}");
            });
    }

    /// <summary>
    /// Property 4: AverageBalance always equals AverageIncome - AverageExpense.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "4")]
    public Property AverageBalance_AlwaysEqualsIncomeMinusExpense()
    {
        return Prop.ForAll(
            RandomTransactionList().ToArbitrary(),
            transactions =>
            {
                var result = ComputeAveragesProjections(transactions);

                return (result.AverageBalance == result.AverageIncome - result.AverageExpense)
                    .Label($"AverageBalance should be {result.AverageIncome - result.AverageExpense}, was {result.AverageBalance}");
            });
    }

    /// <summary>
    /// Property 5: Transfer and Adjustment transactions never enter the totals — the
    /// computation SHALL produce the same result as if those transactions did not exist.
    /// (Inactive filtering is a repo-layer concern, not asserted here — see
    /// TransactionListWithExclusions' remarks.)
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "5")]
    public Property TransferAndAdjustment_AreExcluded_FromTotals()
    {
        return Prop.ForAll(
            TransactionListWithExclusions().ToArbitrary(),
            transactions =>
            {
                var withExclusions = ComputeAveragesProjections(transactions);

                var withoutExcluded = transactions
                    .Where(t => t.Type != TransactionType.Transfer
                             && t.Type != TransactionType.Adjustment)
                    .ToList();

                var withoutThem = ComputeAveragesProjections(withoutExcluded);

                bool incomeEqual = withExclusions.IncomePoints
                    .Zip(withoutThem.IncomePoints, (a, b) => a.Value == b.Value)
                    .All(x => x);

                bool expenseEqual = withExclusions.ExpensePoints
                    .Zip(withoutThem.ExpensePoints, (a, b) => a.Value == b.Value)
                    .All(x => x);

                return incomeEqual
                    .Label("Income series should be identical with/without excluded transactions")
                    .And(expenseEqual)
                    .Label("Expense series should be identical with/without excluded transactions");
            });
    }

    /// <summary>
    /// Property 6: MaxValue is always at least 1 (same divide-by-zero guard as ChartVM).
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "6")]
    public Property MaxValue_IsAlwaysAtLeast1()
    {
        return Prop.ForAll(
            RandomTransactionList().ToArbitrary(),
            transactions =>
            {
                var result = ComputeAveragesProjections(transactions);

                return (result.MaxValue >= 1m)
                    .Label($"MaxValue should be >= 1, was {result.MaxValue}");
            });
    }
}
