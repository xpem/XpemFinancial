using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Service.Recurring;
using Service.Transaction;

namespace XpemFinancial.VMs
{
    /// <summary>
    /// A single point on the line chart: day of month + cumulative value up to that day.
    /// </summary>
    public record ChartPoint(int Day, decimal Value);

    public partial class ChartVM(
        ITransactionService transactionService,
        IRecurringRuleService recurringRuleService) : VMBase
    {
        [ObservableProperty] private string monthYearDisplay = string.Empty;

        /// <summary>Cumulative income points, ordered by day.</summary>
        public List<ChartPoint> IncomePoints { get; private set; } = [];

        /// <summary>Cumulative expense points (positive values), ordered by day.</summary>
        public List<ChartPoint> ExpensePoints { get; private set; } = [];

        /// <summary>Total days in the selected month — used by the drawable for the X axis.</summary>
        public int DaysInMonth { get; private set; } = 30;

        /// <summary>
        /// Maximum cumulative value across both series — used to scale the Y axis.
        /// Always >= 1 to avoid divide-by-zero.
        /// </summary>
        public decimal MaxValue { get; private set; } = 1;

        /// <summary>Raised when chart data changes so the GraphicsView can invalidate itself.</summary>
        public event Action? DataChanged;

        private DateTime _selectedDate;

        public async Task InitializeAsync()
        {
            _selectedDate = DateTime.Now;
            await LoadChartAsync(_selectedDate);
        }

        private async Task LoadChartAsync(DateTime date)
        {
            MonthYearDisplay = date.ToString("MMMM/yyyy");
            IsBusy = true;

            try
            {
                var transactions = await transactionService.GetByMonthYear(date);

                DaysInMonth = DateTime.DaysInMonth(date.Year, date.Month);

                // Sum per day
                var incomeByDay = transactions
                    .Where(t => t.Type == TransactionType.Income)
                    .GroupBy(t => t.Date.Day)
                    .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

                var expenseByDay = transactions
                    .Where(t => t.Type == TransactionType.Expense)
                    .GroupBy(t => t.Date.Day)
                    .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

                // Build cumulative series — only emit a point when there is activity on that day
                var incomePoints = new List<ChartPoint>();
                var expensePoints = new List<ChartPoint>();

                decimal cumulativeIncome = 0;
                decimal cumulativeExpense = 0;

                for (int d = 1; d <= DaysInMonth; d++)
                {
                    bool hasIncome = incomeByDay.TryGetValue(d, out decimal dayIncome);
                    bool hasExpense = expenseByDay.TryGetValue(d, out decimal dayExpense);

                    if (hasIncome)
                    {
                        cumulativeIncome += dayIncome;
                        incomePoints.Add(new ChartPoint(d, cumulativeIncome));
                    }

                    if (hasExpense)
                    {
                        cumulativeExpense += dayExpense;
                        expensePoints.Add(new ChartPoint(d, cumulativeExpense));
                    }
                }

                IncomePoints = incomePoints;
                ExpensePoints = expensePoints;

                var allValues = incomePoints.Select(p => p.Value)
                    .Concat(expensePoints.Select(p => p.Value));

                MaxValue = allValues.Any() ? allValues.Max() : 1;
                if (MaxValue <= 0) MaxValue = 1;

                DataChanged?.Invoke();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task LoadPreviousMonth()
        {
            _selectedDate = _selectedDate.AddMonths(-1);
            await LoadChartAsync(_selectedDate);
        }

        [RelayCommand]
        private async Task LoadNextMonth()
        {
            _selectedDate = _selectedDate.AddMonths(1);

            if (_selectedDate > DateTime.Today.AddMonths(6))
                await recurringRuleService.RunSchedulerAsync(_selectedDate);

            await LoadChartAsync(_selectedDate);
        }
    }
}
