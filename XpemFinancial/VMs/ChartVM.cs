using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Service;
using Service.Recurring;
using Service.Transaction;
using System.Collections.ObjectModel;
using XpemFinancial.Views;

namespace XpemFinancial.VMs
{
    /// <summary>
    /// A single point on the line chart: day of month + cumulative value up to that day.
    /// </summary>
    public record ChartPoint(int Day, decimal Value);

    public partial class ChartVM(
        ITransactionService transactionService,
        IRecurringRuleService recurringRuleService,
        IUserSessionService userSessionService,
        IUserService userService) : VMBase
    {
        [ObservableProperty] private string monthYearDisplay = string.Empty;
        [ObservableProperty] private ObservableCollection<TransactionDTO> transactions = [];
        [ObservableProperty] private TransactionDTO? selectedTransaction;
        [ObservableProperty] private bool includePreviousBalance;

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
        private int? _currentUserId;

        partial void OnIncludePreviousBalanceChanged(bool value)
        {
            if (_currentUserId.HasValue)
                _ = userService.UpdateIncludePreviousBalanceAsync(value, _currentUserId.Value);

            _ = LoadChartAsync(_selectedDate);
        }

        partial void OnSelectedTransactionChanged(TransactionDTO? oldValue, TransactionDTO? newValue)
        {
            if (newValue == null) return;
            GoToTransactionEditCommand.Execute(newValue.Id);
        }

        public async Task InitializeAsync()
        {
            var user = await userSessionService.GetCurrentUserAsync();
            if (user != null)
            {
                _currentUserId = user.Id;
                IncludePreviousBalance = user.IncludePreviousBalance;
            }

            _selectedDate = DateTime.Now;
            await LoadChartAsync(_selectedDate);
        }

        private async Task LoadChartAsync(DateTime date)
        {
            MonthYearDisplay = date.ToString("MMMM/yyyy");
            IsBusy = true;

            try
            {
                var allTransactions = (await transactionService.GetByMonthYear(date)).ToList();

                DaysInMonth = DateTime.DaysInMonth(date.Year, date.Month);

                // ── transaction list ──────────────────────────────────────────
                Transactions = new ObservableCollection<TransactionDTO>(allTransactions);

                // ── chart series ──────────────────────────────────────────────
                decimal previousBalance = IncludePreviousBalance
                    ? await transactionService.GetPreviousBalanceAsync(date)
                    : 0;

                var incomeByDay = allTransactions
                    .Where(t => t.Type == TransactionType.Income)
                    .GroupBy(t => t.Date.Day)
                    .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

                var expenseByDay = allTransactions
                    .Where(t => t.Type == TransactionType.Expense)
                    .GroupBy(t => t.Date.Day)
                    .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

                var incomePoints = new List<ChartPoint>();
                var expensePoints = new List<ChartPoint>();
                decimal cumulativeIncome = previousBalance > 0 ? previousBalance : 0;
                decimal cumulativeExpense = previousBalance < 0 ? Math.Abs(previousBalance) : 0;

                for (int d = 1; d <= DaysInMonth; d++)
                {
                    if (incomeByDay.TryGetValue(d, out decimal dayIncome))
                    {
                        cumulativeIncome += dayIncome;
                        incomePoints.Add(new ChartPoint(d, cumulativeIncome));
                    }

                    if (expenseByDay.TryGetValue(d, out decimal dayExpense))
                    {
                        cumulativeExpense += dayExpense;
                        expensePoints.Add(new ChartPoint(d, cumulativeExpense));
                    }
                }

                // Se incluímos saldo anterior, garantimos ponto inicial no dia 1
                if (IncludePreviousBalance && previousBalance != 0)
                {
                    if (previousBalance > 0 && (incomePoints.Count == 0 || incomePoints[0].Day != 1))
                        incomePoints.Insert(0, new ChartPoint(1, previousBalance > 0 ? previousBalance : 0));

                    if (previousBalance < 0 && (expensePoints.Count == 0 || expensePoints[0].Day != 1))
                        expensePoints.Insert(0, new ChartPoint(1, previousBalance < 0 ? Math.Abs(previousBalance) : 0));
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

        [RelayCommand]
        private void ToggleIncludePreviousBalance()
        {
            IncludePreviousBalance = !IncludePreviousBalance;
        }

        [RelayCommand]
        private async Task GoToTransactionEdit(int? transactionId = null)
        {
            var route = transactionId is not null
                ? $"{nameof(TransactionEditPage)}?TransactionId={transactionId}"
                : nameof(TransactionEditPage);

            await Shell.Current.GoToAsync(route);
        }
    }
}
