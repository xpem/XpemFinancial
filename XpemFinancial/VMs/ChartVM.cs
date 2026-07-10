using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Service;
using Service.Account;
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
        IUserService userService,
        IAccountService accountService) : VMBase
    {
        [ObservableProperty] private string monthYearDisplay = string.Empty;
        [ObservableProperty] private ObservableCollection<TransactionDTO> transactions = [];
        [ObservableProperty] private TransactionDTO? selectedTransaction;
        [ObservableProperty] private bool includePreviousBalance;
        //[ObservableProperty] private decimal generalBalance;

        [ObservableProperty] private bool isAnnualMode;
        [ObservableProperty] private bool isAnnualCumulative = true;
        [ObservableProperty] private ObservableCollection<TransactionDTO> topExpenses = [];
        [ObservableProperty] private bool hasMultipleAccounts;

        /// <summary>Cumulative income points, ordered by day.</summary>
        public List<ChartPoint> IncomePoints { get; private set; } = [];

        /// <summary>Cumulative expense points (positive values), ordered by day.</summary>
        public List<ChartPoint> ExpensePoints { get; private set; } = [];

        /// <summary>Total days in the selected month — used by the drawable for the X axis.</summary>
        public int DaysInMonth { get; private set; } = 30;

        /// <summary>Number of X-axis data points: 12 for annual mode, DaysInMonth for monthly.</summary>
        public int XAxisPointCount { get; private set; } = 30;

        /// <summary>Portuguese month abbreviations for annual mode X-axis; null for monthly mode.</summary>
        public string[]? XAxisLabels { get; private set; }

        /// <summary>
        /// Maximum cumulative value across both series — used to scale the Y axis.
        /// Always >= 1 to avoid divide-by-zero.
        /// </summary>
        public decimal MaxValue { get; private set; } = 1;

        /// <summary>Raised when chart data changes so the GraphicsView can invalidate itself.</summary>
        public event Action? DataChanged;

        private DateTime _selectedDate;
        private int? _currentUserId;
        private CancellationTokenSource? _loadCts;

        partial void OnIsAnnualCumulativeChanged(bool value)
        {
            if (IsAnnualMode)
                _ = LoadAnnualChartAsync(_selectedDate);
        }

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

                var activeAccounts = await accountService.GetActiveAsync(user.Id);
                HasMultipleAccounts = activeAccounts.Count > 1;
            }

            _selectedDate = DateTime.Now;
            await LoadChartAsync(_selectedDate);
        }

        [RelayCommand]
        private async Task SetScope(bool annual)
        {
            IsAnnualMode = annual;
            if (annual)
            {
                XAxisPointCount = 12;
                XAxisLabels = ["Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez"];
                await LoadAnnualChartAsync(_selectedDate);
            }
            else
            {
                XAxisPointCount = DateTime.DaysInMonth(_selectedDate.Year, _selectedDate.Month);
                XAxisLabels = null;
                await LoadChartAsync(_selectedDate);
            }
        }

        private async Task LoadChartAsync(DateTime date)
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            MonthYearDisplay = date.ToString("MMMM/yyyy");
            IsBusy = true;

            try
            {
                var allTransactions = (await transactionService.GetByMonthYear(date)).ToList();
                if (token.IsCancellationRequested) return;

                DaysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
                XAxisPointCount = DaysInMonth;
                XAxisLabels = null;

                // ── transaction list ──────────────────────────────────────────
                Transactions = new ObservableCollection<TransactionDTO>(
                    allTransactions.Where(t => t.Type != TransactionType.Transfer));

                // ── chart series ──────────────────────────────────────────────
                decimal previousBalance = IncludePreviousBalance
                    ? await transactionService.GetPreviousBalanceAsync(date)
                    : 0;

                if (token.IsCancellationRequested) return;

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

                if (token.IsCancellationRequested) return;

                IncomePoints = incomePoints;
                ExpensePoints = expensePoints;

                var allValues = incomePoints.Select(p => p.Value)
                    .Concat(expensePoints.Select(p => p.Value));

                MaxValue = allValues.Any() ? allValues.Max() : 1;
                if (MaxValue <= 0) MaxValue = 1;

                ComputeTop10(allTransactions);

                DataChanged?.Invoke();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadAnnualChartAsync(DateTime date)
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            MonthYearDisplay = date.Year.ToString();
            IsBusy = true;

            try
            {
                var allTransactions = (await transactionService.GetByYear(date.Year)).ToList();
                if (token.IsCancellationRequested) return;

                // Cumulative monthly series — 12 points each
                var incomeByMonth = allTransactions
                    .Where(t => t.Type == TransactionType.Income)
                    .GroupBy(t => t.Date.Month)
                    .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

                var expenseByMonth = allTransactions
                    .Where(t => t.Type == TransactionType.Expense)
                    .GroupBy(t => t.Date.Month)
                    .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

                var incomePoints = new List<ChartPoint>();
                var expensePoints = new List<ChartPoint>();

                if (IsAnnualCumulative)
                {
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
                }
                else
                {
                    for (int m = 1; m <= 12; m++)
                    {
                        incomeByMonth.TryGetValue(m, out decimal monthIncome);
                        expenseByMonth.TryGetValue(m, out decimal monthExpense);

                        incomePoints.Add(new ChartPoint(m, monthIncome));
                        expensePoints.Add(new ChartPoint(m, monthExpense));
                    }
                }

                if (token.IsCancellationRequested) return;

                IncomePoints = incomePoints;
                ExpensePoints = expensePoints;
                XAxisPointCount = 12;
                XAxisLabels = ["Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez"];

                var allValues = incomePoints.Select(p => p.Value)
                    .Concat(expensePoints.Select(p => p.Value));

                MaxValue = allValues.Any() ? Math.Max(allValues.Max(), 1) : 1;

                ComputeTop10(allTransactions);
                DataChanged?.Invoke();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ComputeTop10(IEnumerable<TransactionDTO> transactions)
        {
            var top = transactions
                .Where(t => t.Type == TransactionType.Expense && !t.Inactive)
                .OrderByDescending(t => Math.Abs(t.Amount))
                .ThenByDescending(t => t.Date)
                .Take(10)
                .ToList();

            TopExpenses = new ObservableCollection<TransactionDTO>(top);
        }

        [RelayCommand]
        private async Task LoadPreviousPeriod()
        {
            if (IsAnnualMode)
            {
                _selectedDate = _selectedDate.AddYears(-1);
                await LoadAnnualChartAsync(_selectedDate);
            }
            else
            {
                _selectedDate = _selectedDate.AddMonths(-1);
                await LoadChartAsync(_selectedDate);
            }
        }

        [RelayCommand]
        private async Task LoadNextPeriod()
        {
            if (IsAnnualMode)
            {
                _selectedDate = _selectedDate.AddYears(1);
                await LoadAnnualChartAsync(_selectedDate);
            }
            else
            {
                _selectedDate = _selectedDate.AddMonths(1);

                if (_selectedDate > DateTime.Today.AddMonths(6))
                    await recurringRuleService.RunSchedulerAsync(_selectedDate);

                await LoadChartAsync(_selectedDate);
            }
        }

        [RelayCommand]
        private void ToggleIncludePreviousBalance()
        {
            IncludePreviousBalance = !IncludePreviousBalance;
        }

        [RelayCommand]
        private void ToggleAnnualCumulative()
        {
            IsAnnualCumulative = !IsAnnualCumulative;
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
