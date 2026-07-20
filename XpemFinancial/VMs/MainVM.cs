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
        [ObservableProperty] private ObservableCollection<TransactionDTO> transactions;
        [ObservableProperty] private TransactionDTO selectedTransaction;
        [ObservableProperty] private bool includePreviousBalance;
        [ObservableProperty] private decimal previousBalance;
        [ObservableProperty] private decimal income;
        [ObservableProperty] private decimal expense;
        [ObservableProperty] private decimal total;
        //[ObservableProperty] private decimal generalBalance;
        [ObservableProperty] private bool isNullAccount = false;
        [ObservableProperty] private bool isNotNullAccount = false;
        [ObservableProperty] private string monthYearDisplay;
        [ObservableProperty] private ObservableCollection<MonthOption> monthOptions = [];
        [ObservableProperty] private MonthOption? selectedMonthOption;
        [ObservableProperty] private bool isRequired;

        // ── Account filter (Task 12) ──
        [ObservableProperty] private ObservableCollection<AccountFilterItem> accountFilterOptions = [];
        [ObservableProperty] private AccountFilterItem? selectedAccountFilter;
        [ObservableProperty] private bool hasMultipleAccounts;

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

        partial void OnSelectedTransactionChanged(TransactionDTO? oldValue, TransactionDTO? newValue)
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
                route = accountParam.HasValue
                    ? $"{nameof(Views.TransactionEditPage)}?DashboardAccountId={accountParam}"
                    : nameof(Views.TransactionEditPage);
            }

            await Shell.Current.GoToAsync(route);
        }
    }
}
