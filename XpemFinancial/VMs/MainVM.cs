using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Model.DTO;
using Service;
using Service.Account;
using Service.Recurring;
using Service.Transaction;
using System.Collections.ObjectModel;
using XpemFinancial.Utils;
using XpemFinancial.Views;
using XpemFinancial.Views.Account;

namespace XpemFinancial.VMs
{
    public partial class MainVM(IAccountService accountService,
        ITransactionService transactionService,
        IUserSessionService userSessionService,
        IRecurringRuleService recurringRuleService,
        IUserService userService) : VMBase
    {
        [ObservableProperty] public partial ObservableCollection<TransactionDTO> Transactions { get; set; }
        [ObservableProperty] public partial TransactionDTO SelectedTransaction { get; set; }
        [ObservableProperty] public partial bool IncludePreviousBalance { get; set; }
        [ObservableProperty] public partial decimal PreviousBalance { get; set; }
        [ObservableProperty] public partial decimal Income { get; set; }
        [ObservableProperty] public partial decimal Expense { get; set; }
        [ObservableProperty] public partial decimal Total { get; set; }
        //[ObservableProperty] private decimal generalBalance;
        [ObservableProperty] public partial bool IsNullAccount { get; set; } = false;
        [ObservableProperty] public partial bool IsNotNullAccount { get; set; } = false;
        [ObservableProperty] public partial string MonthYearDisplay { get; set; }
        [ObservableProperty] public partial ObservableCollection<MonthOption> MonthOptions { get; set; } = [];
        [ObservableProperty] public partial MonthOption? SelectedMonthOption { get; set; }
        [ObservableProperty] public partial bool IsRequired { get; set; }

        // ── Account filter (Task 12) ──
        [ObservableProperty] public partial ObservableCollection<AccountFilterItem> AccountFilterOptions { get; set; } = [];
        [ObservableProperty] public partial AccountFilterItem? SelectedAccountFilter { get; set; }
        [ObservableProperty] public partial bool HasMultipleAccounts { get; set; }

        /// <summary>
        /// Preserves the selected account filter across VM re-creations within the same app session.
        /// Reset on cold start (static field initializes to null).
        /// </summary>
        private static int? _sessionSelectedAccountId;

        private DateTime SelectedDate { get; set; } = DateTime.Today;
        private int? _currentUserId;
        private bool _isInitializing;

        partial void OnIncludePreviousBalanceChanged(bool value)
        {
            Total = value
                ? (PreviousBalance + Income) + Expense
                : Income + Expense;

            // persiste a preferência de forma assíncrona (fire-and-forget intencional)
            if (_currentUserId.HasValue)
                _ = userService.UpdateIncludePreviousBalanceAsync(value, _currentUserId.Value);
        }

        partial void OnIsNullAccountChanged(bool value) => IsNotNullAccount = !value;

        partial void OnSelectedTransactionChanged(TransactionDTO oldValue, TransactionDTO newValue)
        {
            if (newValue == null)
                return;

            GoToTransactionEditCommand.Execute(newValue.Id);
        }

        partial void OnSelectedAccountFilterChanged(AccountFilterItem? value)
        {
            // Persist selection in session memory
            _sessionSelectedAccountId = value?.AccountId;

            // Skip reload during initialization (InitializeAsync handles the first load)
            if (_isInitializing) return;

            // Reload transactions with the new filter
            _ = LoadTransactionsForMonthAsync(SelectedDate);
        }

        partial void OnSelectedMonthOptionChanged(MonthOption? value)
        {
            if (value == null || _isInitializing) return;

            // Avoid re-triggering if already on that month
            if (SelectedDate.Year == value.Date.Year && SelectedDate.Month == value.Date.Month)
                return;

            SelectedDate = value.Date;
            NotifyNavigationCanExecuteChanged();
            _ = LoadTransactionsForMonthAsync(SelectedDate);
        }

        private void BuildMonthOptions()
        {
            var today = DateTime.Today;
            var options = new ObservableCollection<MonthOption>();

            for (int i = -6; i <= 6; i++)
            {
                var date = new DateTime(today.Year, today.Month, 1).AddMonths(i);
                options.Add(new MonthOption(date));
            }

            MonthOptions = options;
        }

        private void SyncSelectedMonthOption()
        {
            var match = MonthOptions.FirstOrDefault(m =>
                m.Date.Year == SelectedDate.Year && m.Date.Month == SelectedDate.Month);

            if (match != null && match != SelectedMonthOption)
                SelectedMonthOption = match;
        }

        public async Task InitializeAsync()
        {
            _isInitializing = true;

            var user = await userSessionService.GetCurrentUserAsync();

            if (user == null)
            {
                _ = Shell.Current.GoToAsync($"{nameof(SignInPage)}");
                return;
            }

            SelectedDate = DateTime.Now;
            _currentUserId = user.Id;
            IncludePreviousBalance = user.IncludePreviousBalance;

            BuildMonthOptions();
            SyncSelectedMonthOption();

            // Load active accounts for filter
            await LoadAccountFilterOptionsAsync();

            var existingAccounts = await accountService.GetActiveAsync(_currentUserId.Value);
            IsNullAccount = existingAccounts.Count == 0;
            IsNotNullAccount = !IsNullAccount;

            _isInitializing = false;

            await LoadTransactionsForMonthAsync(SelectedDate);

            //GeneralBalance = await accountService.GetGeneralBalanceAsync(_currentUserId.Value);
        }

        /// <summary>
        /// Loads active accounts and builds the filter picker options.
        /// Restores session selection or defaults to "Todas as Contas".
        /// Reverts to consolidated if previously selected account was deactivated.
        /// </summary>
        private async Task LoadAccountFilterOptionsAsync()
        {
            if (!_currentUserId.HasValue) return;

            var activeAccounts = await accountService.GetActiveAsync(_currentUserId.Value);

            var options = new ObservableCollection<AccountFilterItem>
            {
                new AccountFilterItem
                {
                    AccountId = null,
                    DisplayName = "Todas as Contas (Consolidado)"
                }
            };

            // Sort active accounts alphabetically by Name (Req 6.1)
            foreach (var account in activeAccounts.OrderBy(a => a.Name))
            {
                options.Add(new AccountFilterItem
                {
                    AccountId = account.Id,
                    DisplayName = account.Name
                });
            }

            AccountFilterOptions = options;

            // More than one real account (excluding the "Todas" entry) → show account names
            HasMultipleAccounts = options.Count > 2;

            // Restore session selection or default to "Todas as Contas" (Req 6.5, 6.6, 6.7)
            if (_sessionSelectedAccountId.HasValue)
            {
                var restoredItem = options.FirstOrDefault(o => o.AccountId == _sessionSelectedAccountId.Value);
                if (restoredItem != null)
                {
                    // Account still active — restore selection
                    SelectedAccountFilter = restoredItem;
                }
                else
                {
                    // Account was deactivated — revert to consolidated (Req 6.7)
                    _sessionSelectedAccountId = null;
                    SelectedAccountFilter = options[0];
                }
            }
            else
            {
                // Cold start or no previous selection — default to "Todas as Contas" (Req 6.6)
                SelectedAccountFilter = options[0];
            }
        }

        /// <summary>
        /// Silently refreshes the transaction list for the currently displayed month.
        /// Called by the view after a background sync cycle completes (Fix #4).
        /// Does nothing if a load is already in progress.
        /// </summary>
        public async Task RefreshTransactionsAsync()
        {
            if (IsBusy) return;

            // Reload filter options in case accounts changed during sync
            await LoadAccountFilterOptionsAsync();

            await LoadTransactionsForMonthAsync(SelectedDate);

            //if (_currentUserId.HasValue)
            //    GeneralBalance = await accountService.GetGeneralBalanceAsync(_currentUserId.Value);
        }

        private async Task LoadTransactionsForMonthAsync(DateTime date)
        {
            var accountId = SelectedAccountFilter?.AccountId;

            MonthYearDisplay = date.ToString("MMMM/yyyy");
            PreviousBalance = await transactionService.GetPreviousBalanceAsync(date, accountId);
            Transactions = [];
            Expense = Income = 0;

            var transactionsFromService = await transactionService.GetByMonthYear(date, accountId);

            foreach (var transaction in transactionsFromService)
            {
                if (transaction.Type == TransactionType.Income)
                    Income += transaction.Amount;
                else if (transaction.Type != TransactionType.Transfer)
                    Expense += transaction.Amount;

                Transactions.Add(transaction);
            }

            Total = IncludePreviousBalance
                ? (PreviousBalance + Income) + Expense
                : Income + Expense;
        }

        private bool CanLoadPreviousMonth()
            => SelectedDate > DateTime.Today.AddMonths(-6);

        [RelayCommand(CanExecute = nameof(CanLoadPreviousMonth))]
        private async Task LoadPreviousMonth()
        {
            SelectedDate = SelectedDate.AddMonths(-1);
            NotifyNavigationCanExecuteChanged();
            SyncSelectedMonthOption();
            await LoadTransactionsForMonthAsync(SelectedDate);
        }

        private bool CanLoadNextMonth()
            => SelectedDate < DateTime.Today.AddMonths(6);

        [RelayCommand(CanExecute = nameof(CanLoadNextMonth))]
        private async Task LoadNextMonth()
        {
            SelectedDate = SelectedDate.AddMonths(1);
            NotifyNavigationCanExecuteChanged();
            SyncSelectedMonthOption();

            if (SelectedDate > DateTime.Today.AddMonths(6))
                await recurringRuleService.RunSchedulerAsync(SelectedDate);

            await LoadTransactionsForMonthAsync(SelectedDate);
        }

        private void NotifyNavigationCanExecuteChanged()
        {
            LoadPreviousMonthCommand.NotifyCanExecuteChanged();
            LoadNextMonthCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private async Task GoToAccountPage() => await Shell.Current.GoToAsync($"{nameof(AccountsPage)}");

        [RelayCommand]
        private void ToggleIncludePreviousBalance()
        {
            IncludePreviousBalance = !IncludePreviousBalance;
        }

        [RelayCommand]
        private async Task GoToTransactionEdit(int? transactionId = null)
        {
            await NavigateToTransactionEdit(transactionId, transactionType: null);
        }

        [RelayCommand]
        private async Task GoToNewTransaction(TransactionType transactionType)
        {
            await NavigateToTransactionEdit(transactionId: null, transactionType);
        }

        private async Task NavigateToTransactionEdit(int? transactionId, TransactionType? transactionType)
        {
            var accountParam = SelectedAccountFilter?.AccountId;

            string route;
            if (transactionId is not null)
            {
                route = accountParam.HasValue
                    ? $"{nameof(Views.TransactionEditPage)}?TransactionId={transactionId}&DashboardAccountId={accountParam}"
                    : $"{nameof(Views.TransactionEditPage)}?TransactionId={transactionId}";
            }
            else
            {
                var typeParam = transactionType.HasValue ? $"TransactionType={transactionType.Value}" : null;
                var dashParam = accountParam.HasValue ? $"DashboardAccountId={accountParam}" : null;

                var queryParams = new[] { typeParam, dashParam }
                    .Where(p => p is not null);

                route = queryParams.Any()
                    ? $"{nameof(Views.TransactionEditPage)}?{string.Join("&", queryParams)}"
                    : nameof(Views.TransactionEditPage);
            }

            await Shell.Current.GoToAsync(route);
        }
    }
}
